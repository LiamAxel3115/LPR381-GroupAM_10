using System;
using System.Collections.Generic;
using System.Text;

//import modules
using Simplex_Solver_APP.Model;
using Simplex_Solver_APP.File_handler;

namespace Simplex_Solver_APP.Solvers
{
    // Cutting Plane algorithm. The LP relaxation is solved with the primal simplex,
    // then a Gomory fractional cut is added for whichever integer variable came out
    // fractional and the dual simplex puts the tableau right again. That repeats
    // until every int/bin variable is whole.
    //
    // Handles max and min models, <= >= and = constraints, negative right hand
    // sides, and +, -, urs, int and bin sign restrictions. Big M is used so the
    // >= and = rows have something to start basic with.
    //
    // Everything runs on one double[,] tableau: row 0 is the Z row, the last
    // column is the RHS, and the tableau is optimal once nothing in the Z row is
    // negative anymore.
    public class CuttingPlaneSolver
    {
        private const double bigM = 1000000;        // penalty put on artificial variables
        private const double tolerance = 0.000001;  // anything smaller than this counts as zero
        private const int maxCuts = 50;             // safety net so we cant keep cutting forever

        private File_writer writer;
        private List<string> columnNames = new List<string>();
        private List<double> objCoefficients = new List<double>(); // internal MAX coefficient per column
        private List<int> artificialColumns = new List<int>();
        private List<int> integerColumns = new List<int>();        // columns the cuts must make whole
        private List<int> positiveColumn = new List<int>();        // column holding x_j (or y_j)
        private List<int> negativeColumn = new List<int>();        // second column when x_j is urs
        private List<bool> negatedVariable = new List<bool>();     // true when x_j was swapped for -y_j
        private int cutNumber = 1;

        public void Solve(Formulation model, File_writer output)
        {
            writer = output;

            writer.WriteLine("========================================================");
            writer.WriteLine("                 CUTTING PLANE ALGORITHM                 ");
            writer.WriteLine("========================================================");
            writer.WriteLine();

            double[,] matrix = buildCanonical(model);
            PrintMatrix("--- Canonical Form ---", matrix);

            double[,] answer = cuttingPlane(matrix, 0);
            if (answer == null)
            {
                return; // infeasible or unbounded, the reason was already written out
            }
            reportSolution(model, answer);
        }

        // Turns the model into the starting tableau. min becomes a max of the
        // negated objective, the real objective value gets worked out again from
        // the original coefficients at the end so nothing has to be flipped back.
        private double[,] buildCanonical(Formulation model)
        {
            double direction = 1;
            if (model.Objective == Formulation_type.Min)
            {
                direction = -1;
            }

            // ---- one or two columns per decision variable, depending on its sign ----
            for (int j = 0; j < model.VarCount; j++)
            {
                Sign_Restriction restriction = model.Sign_Restrictions[j];
                double c = direction * model.Obj_Func_coefficients[j];

                if (restriction == Sign_Restriction.Negative)
                {
                    // x_j <= 0, so swap it for x_j = -y_j with y_j >= 0
                    negatedVariable.Add(true);
                    positiveColumn.Add(columnNames.Count);
                    negativeColumn.Add(-1);
                    columnNames.Add("y" + (j + 1));
                    objCoefficients.Add(-c);
                }
                else if (restriction == Sign_Restriction.urs)
                {
                    // x_j unrestricted, so split it into x_j = x+ - x-
                    negatedVariable.Add(false);
                    positiveColumn.Add(columnNames.Count);
                    columnNames.Add("x" + (j + 1) + "+");
                    objCoefficients.Add(c);
                    negativeColumn.Add(columnNames.Count);
                    columnNames.Add("x" + (j + 1) + "-");
                    objCoefficients.Add(-c);
                }
                else
                {
                    // +, int and bin all become an ordinary variable that is >= 0
                    negatedVariable.Add(false);
                    positiveColumn.Add(columnNames.Count);
                    negativeColumn.Add(-1);
                    columnNames.Add("x" + (j + 1));
                    objCoefficients.Add(c);
                }

                // int and bin are the ones the cuts have to make whole again
                if (restriction == Sign_Restriction.Int || restriction == Sign_Restriction.Bin)
                {
                    integerColumns.Add(positiveColumn[j]);
                }
            }

            int decisionColumns = columnNames.Count;

            // ---- rebuild every constraint on top of those columns ----
            List<double[]> rows = new List<double[]>();
            List<Equality_Sign> relations = new List<Equality_Sign>();
            List<double> rhs = new List<double>();

            for (int i = 0; i < model.ConstraintCount; i++)
            {
                Conditions constraint = model.Constraint[i];
                double[] row = new double[decisionColumns];
                for (int j = 0; j < model.VarCount; j++)
                {
                    double coefficient = constraint.Constraint_Coefficients[j];
                    if (negatedVariable[j])
                    {
                        row[positiveColumn[j]] = -coefficient;
                    }
                    else if (negativeColumn[j] != -1)
                    {
                        row[positiveColumn[j]] = coefficient;
                        row[negativeColumn[j]] = -coefficient;
                    }
                    else
                    {
                        row[positiveColumn[j]] = coefficient;
                    }
                }
                rows.Add(row);
                relations.Add(constraint.Inequality);
                rhs.Add(constraint.RHS_Value);
            }

            // a binary variable still needs x_j <= 1, the >= 0 half is already there
            for (int j = 0; j < model.VarCount; j++)
            {
                if (model.Sign_Restrictions[j] == Sign_Restriction.Bin)
                {
                    double[] row = new double[decisionColumns];
                    row[positiveColumn[j]] = 1;
                    rows.Add(row);
                    relations.Add(Equality_Sign.LessThanOrEqual);
                    rhs.Add(1);
                    writer.WriteLine("x" + (j + 1) + " is binary so the bound x" + (j + 1) + " <= 1 was added.");
                }
            }

            // every RHS has to be positive before the slacks go in, flipping a row
            // also flips the relation that goes with it
            for (int i = 0; i < rows.Count; i++)
            {
                if (rhs[i] < 0)
                {
                    for (int j = 0; j < decisionColumns; j++)
                    {
                        rows[i][j] = -rows[i][j];
                    }
                    rhs[i] = -rhs[i];
                    if (relations[i] == Equality_Sign.LessThanOrEqual)
                    {
                        relations[i] = Equality_Sign.GreaterThanOrEqual;
                    }
                    else if (relations[i] == Equality_Sign.GreaterThanOrEqual)
                    {
                        relations[i] = Equality_Sign.LessThanOrEqual;
                    }
                }
            }

            // ---- work out which extra columns each relation needs ----
            List<int> extraRow = new List<int>();
            List<string> extraKind = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                if (relations[i] == Equality_Sign.LessThanOrEqual)
                {
                    extraRow.Add(i);
                    extraKind.Add("slack");
                }
                else if (relations[i] == Equality_Sign.GreaterThanOrEqual)
                {
                    extraRow.Add(i);
                    extraKind.Add("surplus");
                    extraRow.Add(i);
                    extraKind.Add("artificial");
                }
                else
                {
                    extraRow.Add(i);
                    extraKind.Add("artificial");
                }
            }

            int constraintRows = rows.Count;
            int totalColumns = decisionColumns + extraKind.Count + 1; // the +1 is the RHS column
            double[,] matrix = new double[constraintRows + 1, totalColumns];
            int[] basis = new int[constraintRows];
            for (int i = 0; i < constraintRows; i++)
            {
                basis[i] = -1;
            }

            for (int i = 0; i < constraintRows; i++)
            {
                for (int j = 0; j < decisionColumns; j++)
                {
                    matrix[i + 1, j] = rows[i][j];
                }
                matrix[i + 1, totalColumns - 1] = rhs[i];
            }

            int slackNumber = 1;
            int surplusNumber = 1;
            int artificialNumber = 1;
            int column = decisionColumns;
            for (int k = 0; k < extraKind.Count; k++)
            {
                int row = extraRow[k] + 1; // +1 because row 0 is the Z row
                if (extraKind[k] == "slack")
                {
                    matrix[row, column] = 1;
                    columnNames.Add("s" + slackNumber);
                    objCoefficients.Add(0);
                    basis[extraRow[k]] = column;
                    slackNumber++;
                }
                else if (extraKind[k] == "surplus")
                {
                    matrix[row, column] = -1;
                    columnNames.Add("e" + surplusNumber);
                    objCoefficients.Add(0);
                    surplusNumber++;
                }
                else
                {
                    matrix[row, column] = 1;
                    columnNames.Add("a" + artificialNumber);
                    objCoefficients.Add(-bigM);
                    artificialColumns.Add(column);
                    if (basis[extraRow[k]] == -1)
                    {
                        basis[extraRow[k]] = column;
                    }
                    artificialNumber++;
                }
                column++;
            }

            // Z row starts as -c_j, so the tableau is optimal once nothing is negative
            for (int j = 0; j < totalColumns - 1; j++)
            {
                matrix[0, j] = -objCoefficients[j];
            }
            // any artificial that starts in the basis has to be priced out of the Z row
            for (int i = 0; i < constraintRows; i++)
            {
                if (isArtificial(basis[i]))
                {
                    for (int j = 0; j < totalColumns; j++)
                    {
                        matrix[0, j] = matrix[0, j] - bigM * matrix[i + 1, j];
                    }
                }
            }

            return matrix;
        }

        // Solve the relaxation, cut, re-solve, and keep going until it comes out whole.
        private double[,] cuttingPlane(double[,] matrix, int cutsSoFar)
        {
            matrix = primalSimplex(matrix);
            if (matrix == null)
            {
                return null; // unbounded, already reported
            }

            if (artificialLeftInBasis(matrix))
            {
                writer.WriteLine("INFEASIBLE: an artificial variable is still in the basis with a value above zero.");
                writer.WriteLine();
                return null;
            }

            int pRow = findFractionalRow(matrix);
            if (pRow == -1)
            {
                writer.WriteLine("Every integer variable is whole, no more cuts are needed.");
                writer.WriteLine();
                return matrix;
            }

            if (cutsSoFar >= maxCuts)
            {
                writer.WriteLine("Stopped after " + maxCuts + " cuts and the model is still not integer, please check it.");
                writer.WriteLine();
                return matrix;
            }

            matrix = addCut(matrix, pRow);
            matrix = dualSimplex(matrix);
            if (matrix == null)
            {
                return null; // the cut made it infeasible, already reported
            }

            return cuttingPlane(matrix, cutsSoFar + 1);
        }

        // Builds the Gomory fractional cut  -frac(a) x + s = -frac(b)  off the given
        // row. The tableau grows by one row and one column, the new column being the
        // cut's own slack so the new row has something to be basic on.
        private double[,] addCut(double[,] matrix, int pRow)
        {
            int rows = matrix.GetLength(0);
            int columns = matrix.GetLength(1);
            int lastColumn = columns - 1;

            double[,] newMatrix = new double[rows + 1, columns + 1];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < lastColumn; j++)
                {
                    newMatrix[i, j] = matrix[i, j];
                }
                newMatrix[i, columns] = matrix[i, lastColumn]; // the RHS shifts one place right
            }

            for (int j = 0; j < lastColumn; j++)
            {
                newMatrix[rows, j] = -fraction(matrix[pRow, j]);
            }
            newMatrix[rows, lastColumn] = 1; // the cut's own slack
            newMatrix[rows, columns] = -fraction(matrix[pRow, lastColumn]);

            columnNames.Add("sc" + cutNumber);

            string cutFrom = columnNames[findBasicColumn(matrix, pRow)];
            PrintMatrix("--- Cut " + cutNumber + " added, taken off the " + cutFrom + " row ---", newMatrix);
            cutNumber++;

            return newMatrix;
        }

        private double[,] primalSimplex(double[,] matrix)
        {
            int pColoum = findPColumnPrimal(matrix);
            if (pColoum == -1)
            {
                return matrix; // nothing negative left in the Z row
            }
            int pRow = findPRowPrimal(matrix, pColoum);
            if (pRow == -1)
            {
                writer.WriteLine("UNBOUNDED: " + columnNames[pColoum] + " can grow without a limit.");
                writer.WriteLine();
                return null;
            }
            double[,] newMatrix = PivotMatrix(matrix, pRow, pColoum);
            PrintMatrix("--- Primal Simplex, " + columnNames[pColoum] + " enters on row " + pRow + " ---", newMatrix);
            return primalSimplex(newMatrix);
        }

        private double[,] dualSimplex(double[,] matrix)
        {
            int pRow = findPRowDual(matrix);
            if (pRow == -1)
            {
                return matrix; // every RHS is positive again
            }
            int pColoum = findPColumnDual(matrix, pRow);
            if (pColoum == -1)
            {
                // a negative row with nothing negative to pivot on means there is no
                // answer left that satisfies the cut
                writer.WriteLine("INFEASIBLE: row " + pRow + " is negative but has no negative coefficient to pivot on.");
                writer.WriteLine();
                return null;
            }
            double[,] newMatrix = PivotMatrix(matrix, pRow, pColoum);
            PrintMatrix("--- Dual Simplex, " + columnNames[pColoum] + " enters on row " + pRow + " ---", newMatrix);
            return dualSimplex(newMatrix);
        }

        private double[,] PivotMatrix(double[,] matrix, int pRow, int pColoumn)
        {
            double[,] newMatrix = new double[matrix.GetLength(0), matrix.GetLength(1)];
            for (int i = 0; i < matrix.GetLength(1); i++)
            {
                newMatrix[pRow, i] = matrix[pRow, i] / matrix[pRow, pColoumn];
            }
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (i != pRow)
                    {
                        newMatrix[i, j] = matrix[i, j] - (matrix[i, pColoumn] * newMatrix[pRow, j]);
                    }
                }
            }
            return newMatrix;
        }

        private int findPColumnPrimal(double[,] matrix)
        {
            int pColoum = 0;
            double min = 999999999;
            for (int i = 0; i < matrix.GetLength(1) - 1; i++)
            {
                if (matrix[0, i] < min)
                {
                    min = matrix[0, i];
                    pColoum = i;
                }
            }
            if (min >= -tolerance)
            {
                return -1;
            }
            return pColoum;
        }

        private int findPRowPrimal(double[,] matrix, int pColoum)
        {
            int lastColumn = matrix.GetLength(1) - 1;
            int pRow = 0;
            double min = 999999999;
            for (int i = 1; i < matrix.GetLength(0); i++)
            {
                if (matrix[i, pColoum] > tolerance)
                {
                    double ratio = matrix[i, lastColumn] / matrix[i, pColoum];
                    if (ratio < min)
                    {
                        min = ratio;
                        pRow = i;
                    }
                }
            }
            if (min == 999999999)
            {
                return -1;
            }
            return pRow;
        }

        private int findPColumnDual(double[,] matrix, int pRow)
        {
            int pColoum = 0;
            double min = 999999999;
            for (int i = 0; i < matrix.GetLength(1) - 1; i++)
            {
                if (matrix[pRow, i] < -tolerance)
                {
                    double ratio = Math.Abs(matrix[0, i] / matrix[pRow, i]);
                    if (ratio < min)
                    {
                        min = ratio;
                        pColoum = i;
                    }
                }
            }
            if (min == 999999999)
            {
                return -1;
            }
            return pColoum;
        }

        private int findPRowDual(double[,] matrix)
        {
            int lastColumn = matrix.GetLength(1) - 1;
            int pRow = 0;
            double min = 999999999;
            for (int i = 1; i < matrix.GetLength(0); i++)
            {
                if (matrix[i, lastColumn] < -tolerance && matrix[i, lastColumn] < min)
                {
                    min = matrix[i, lastColumn];
                    pRow = i;
                }
            }
            if (min == 999999999)
            {
                return -1;
            }
            return pRow;
        }

        // The row to cut on is a row whose basic variable has to be an integer but
        // came out fractional. The one closest to a half usually gives the best cut.
        private int findFractionalRow(double[,] matrix)
        {
            int lastColumn = matrix.GetLength(1) - 1;
            int pRow = -1;
            double best = 999999999;
            for (int i = 1; i < matrix.GetLength(0); i++)
            {
                int basicColumn = findBasicColumn(matrix, i);
                if (basicColumn == -1 || !isInteger(basicColumn))
                {
                    continue;
                }
                double frac = fraction(matrix[i, lastColumn]);
                if (frac > tolerance)
                {
                    double distance = Math.Abs(frac - 0.5);
                    if (distance < best)
                    {
                        best = distance;
                        pRow = i;
                    }
                }
            }
            return pRow;
        }

        // A column is basic on a row when it is a 1 on that row and a 0 everywhere
        // else, the Z row included.
        private int findBasicColumn(double[,] matrix, int row)
        {
            for (int j = 0; j < matrix.GetLength(1) - 1; j++)
            {
                if (Math.Abs(matrix[row, j] - 1) > tolerance)
                {
                    continue;
                }
                bool basic = true;
                for (int i = 0; i < matrix.GetLength(0); i++)
                {
                    if (i != row && Math.Abs(matrix[i, j]) > tolerance)
                    {
                        basic = false;
                        break;
                    }
                }
                if (basic)
                {
                    return j;
                }
            }
            return -1;
        }

        private bool artificialLeftInBasis(double[,] matrix)
        {
            int lastColumn = matrix.GetLength(1) - 1;
            for (int i = 1; i < matrix.GetLength(0); i++)
            {
                int basicColumn = findBasicColumn(matrix, i);
                if (isArtificial(basicColumn) && matrix[i, lastColumn] > tolerance)
                {
                    return true;
                }
            }
            return false;
        }

        private bool isArtificial(int column)
        {
            for (int i = 0; i < artificialColumns.Count; i++)
            {
                if (artificialColumns[i] == column)
                {
                    return true;
                }
            }
            return false;
        }

        private bool isInteger(int column)
        {
            for (int i = 0; i < integerColumns.Count; i++)
            {
                if (integerColumns[i] == column)
                {
                    return true;
                }
            }
            return false;
        }

        // How far a number is past the whole number below it, with anything sitting
        // on a whole number treated as having no fraction at all.
        private double fraction(double value)
        {
            if (Math.Abs(value - Math.Round(value)) < tolerance)
            {
                return 0;
            }
            return value - Math.Floor(value);
        }

        private void reportSolution(Formulation model, double[,] matrix)
        {
            int lastColumn = matrix.GetLength(1) - 1;
            double[] values = new double[lastColumn];
            for (int i = 1; i < matrix.GetLength(0); i++)
            {
                int basicColumn = findBasicColumn(matrix, i);
                if (basicColumn != -1)
                {
                    values[basicColumn] = matrix[i, lastColumn];
                }
            }

            writer.WriteLine("--- Optimal Solution ---");
            double objective = 0;
            for (int j = 0; j < model.VarCount; j++)
            {
                double x = values[positiveColumn[j]];
                if (negatedVariable[j])
                {
                    x = -x;
                }
                else if (negativeColumn[j] != -1)
                {
                    x = values[positiveColumn[j]] - values[negativeColumn[j]];
                }
                writer.WriteLine("x" + (j + 1) + " = " + File_writer.Round3(x));
                objective = objective + model.Obj_Func_coefficients[j] * x;
            }

            if (model.Objective == Formulation_type.Max)
            {
                writer.WriteLine("Optimal objective value (max) = " + File_writer.Round3(objective));
            }
            else
            {
                writer.WriteLine("Optimal objective value (min) = " + File_writer.Round3(objective));
            }
        }

        private void PrintMatrix(string title, double[,] matrix)
        {
            writer.WriteLine(title);

            StringBuilder heading = new StringBuilder("Basis".PadRight(8));
            for (int j = 0; j < columnNames.Count; j++)
            {
                heading.Append(columnNames[j].PadLeft(10));
            }
            heading.Append("RHS".PadLeft(10));
            writer.WriteLine(heading.ToString());

            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                StringBuilder line = new StringBuilder();
                if (i == 0)
                {
                    line.Append("Z".PadRight(8));
                }
                else
                {
                    int basicColumn = findBasicColumn(matrix, i);
                    if (basicColumn == -1)
                    {
                        line.Append(("c" + i).PadRight(8));
                    }
                    else
                    {
                        line.Append(columnNames[basicColumn].PadRight(8));
                    }
                }
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    line.Append(File_writer.Round3(matrix[i, j]).ToString("0.###").PadLeft(10));
                }
                writer.WriteLine(line.ToString());
            }
            writer.WriteLine();
        }
    }
}
