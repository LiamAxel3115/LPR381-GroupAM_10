using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

//import modules
using Simplex_Solver_APP.Model;
using Simplex_Solver_APP.File_handler;

namespace Simplex_Solver_APP.Solvers
{
    /// <summary>
    /// Solves a Linear Programming model using the Revised Primal Simplex
    /// Algorithm with the Product Form of the Inverse (PFI) and Big-M, so
    /// that &lt;=, &gt;= and = constraints, plus +, -, urs, int and bin
    /// variables, are all handled in a single, uniform pass (same standard
    /// form scope as PrimalSimplexSolver, just solved via the basis inverse
    /// instead of a full tableau).
    ///
    /// At every iteration this prints:
    ///   - the Price Out step: y = c_B^T B^-1, and z_j - c_j for every
    ///     column (used to pick the entering variable), and
    ///   - the Product Form step: the current B^-1, the resulting basic
    ///     solution x_B = B^-1 b, the updated entering column
    ///     d = B^-1 A_enter, and the ratio test used to pick the leaving
    ///     variable.
    /// </summary>
    public class RevisedPrimalSimplexSolver
    {
        private const double BigM = 1000000.0;
        private const double Epsilon = 1e-9;

        private class VarMap
        {
            public int OriginalIndex;
            public int PositiveColumn = -1;
            public int NegativeColumn = -1;
            public bool Negated;
        }

        public void Solve(Formulation model, File_writer writer)
        {
            writer.WriteLine("========================================================");
            writer.WriteLine("   REVISED PRIMAL SIMPLEX ALGORITHM (Product Form / Price Out, Big-M)");
            writer.WriteLine("========================================================");
            writer.WriteLine();

            bool maximize = model.Objective == Formulation_type.Max;
            double dirSign = maximize ? 1.0 : -1.0;

            // ------------------------------------------------------------------
            // 1) Build the variable map: +, -, urs, int and bin restrictions.
            // ------------------------------------------------------------------
            var maps = new List<VarMap>();
            var columnNames = new List<string>();
            var objCoeffs = new List<double>();

            for (int j = 0; j < model.VarCount; j++)
            {
                var restriction = model.Sign_Restrictions[j];
                var map = new VarMap { OriginalIndex = j };
                double c = dirSign * model.Obj_Func_coefficients[j];

                if (restriction == Sign_Restriction.Negative)
                {
                    map.Negated = true;
                    map.PositiveColumn = columnNames.Count;
                    columnNames.Add("y" + (j + 1));
                    objCoeffs.Add(-c);
                }
                else if (restriction == Sign_Restriction.urs)
                {
                    map.PositiveColumn = columnNames.Count;
                    columnNames.Add("x" + (j + 1) + "+");
                    objCoeffs.Add(c);

                    map.NegativeColumn = columnNames.Count;
                    columnNames.Add("x" + (j + 1) + "-");
                    objCoeffs.Add(-c);
                }
                else
                {
                    map.PositiveColumn = columnNames.Count;
                    columnNames.Add("x" + (j + 1));
                    objCoeffs.Add(c);
                }

                maps.Add(map);
            }

            int decisionColumns = columnNames.Count;

            // ------------------------------------------------------------------
            // 2) Rebuild every constraint row using the mapped columns.
            // ------------------------------------------------------------------
            var rows = new List<double[]>();
            var relations = new List<Equality_Sign>();
            var rhsList = new List<double>();

            foreach (var constraint in model.Constraint)
            {
                double[] row = new double[decisionColumns];
                for (int j = 0; j < model.VarCount; j++)
                {
                    var map = maps[j];
                    double coeff = constraint.Constraint_Coefficients[j];

                    if (map.Negated)
                        row[map.PositiveColumn] = -coeff;
                    else if (map.NegativeColumn != -1)
                    {
                        row[map.PositiveColumn] = coeff;
                        row[map.NegativeColumn] = -coeff;
                    }
                    else
                        row[map.PositiveColumn] = coeff;
                }
                rows.Add(row);
                relations.Add(constraint.Inequality);
                rhsList.Add(constraint.RHS_Value);
            }

            for (int j = 0; j < model.VarCount; j++)
            {
                if (model.Sign_Restrictions[j] == Sign_Restriction.Bin)
                {
                    double[] row = new double[decisionColumns];
                    row[maps[j].PositiveColumn] = 1;
                    rows.Add(row);
                    relations.Add(Equality_Sign.LessThanOrEqual);
                    rhsList.Add(1);
                    writer.WriteLine("Note: x" + (j + 1) + " is binary -> added implicit bound x" + (j + 1) + " <= 1 for the LP relaxation.");
                }
            }

            int m = rows.Count;

            for (int i = 0; i < m; i++)
            {
                if (rhsList[i] < -Epsilon)
                {
                    for (int j = 0; j < decisionColumns; j++)
                        rows[i][j] = -rows[i][j];
                    rhsList[i] = -rhsList[i];

                    if (relations[i] == Equality_Sign.LessThanOrEqual)
                        relations[i] = Equality_Sign.GreaterThanOrEqual;
                    else if (relations[i] == Equality_Sign.GreaterThanOrEqual)
                        relations[i] = Equality_Sign.LessThanOrEqual;
                }
            }

            // ------------------------------------------------------------------
            // 3) Attach slack / surplus / artificial columns.
            // ------------------------------------------------------------------
            var extraNames = new List<string>();
            var extraKinds = new List<string>();
            var extraRows = new List<int>();
            int slackCounter = 1, surplusCounter = 1, artCounter = 1;

            for (int i = 0; i < m; i++)
            {
                switch (relations[i])
                {
                    case Equality_Sign.LessThanOrEqual:
                        extraNames.Add("s" + slackCounter++); extraKinds.Add("slack"); extraRows.Add(i);
                        break;
                    case Equality_Sign.GreaterThanOrEqual:
                        extraNames.Add("e" + surplusCounter++); extraKinds.Add("surplus"); extraRows.Add(i);
                        extraNames.Add("a" + artCounter++); extraKinds.Add("artificial"); extraRows.Add(i);
                        break;
                    case Equality_Sign.Equal:
                        extraNames.Add("a" + artCounter++); extraKinds.Add("artificial"); extraRows.Add(i);
                        break;
                }
            }

            columnNames.AddRange(extraNames);
            int totalColumns = decisionColumns + extraNames.Count;

            double[,] A = new double[m, totalColumns];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < decisionColumns; j++)
                    A[i, j] = rows[i][j];

            var artificialColumns = new List<int>();
            int[] basis = new int[m];
            for (int i = 0; i < m; i++) basis[i] = -1;

            int col = decisionColumns;
            for (int k = 0; k < extraKinds.Count; k++)
            {
                int row = extraRows[k];
                if (extraKinds[k] == "slack")
                {
                    A[row, col] = 1;
                    objCoeffs.Add(0);
                    basis[row] = col;
                }
                else if (extraKinds[k] == "surplus")
                {
                    A[row, col] = -1;
                    objCoeffs.Add(0);
                }
                else // artificial
                {
                    A[row, col] = 1;
                    objCoeffs.Add(-BigM);
                    artificialColumns.Add(col);
                    if (basis[row] == -1) basis[row] = col;
                }
                col++;
            }

            double[] b = rhsList.ToArray();

            // ------------------------------------------------------------------
            // 4) Print the canonical form.
            // ------------------------------------------------------------------
            writer.WriteLine();
            writer.WriteLine("--- Canonical Form ---");
            var objLine = new StringBuilder(maximize ? "Maximize Z = " : "Minimize Z = ");
            for (int j = 0; j < decisionColumns; j++)
                objLine.Append((objCoeffs[j] >= 0 ? " +" : " ") + Round(objCoeffs[j]) + columnNames[j]);
            writer.WriteLine(objLine.ToString());
            if (!maximize)
                writer.WriteLine("(solved internally as Maximize -Z; the reported objective value below is the true minimum)");

            for (int i = 0; i < m; i++)
            {
                var sb = new StringBuilder("  c" + (i + 1) + ": ");
                for (int j = 0; j < totalColumns; j++)
                {
                    if (Math.Abs(A[i, j]) < Epsilon) continue;
                    sb.Append((A[i, j] >= 0 ? "+" : "") + Round(A[i, j]) + columnNames[j] + " ");
                }
                sb.Append("= " + Round(b[i]));
                writer.WriteLine(sb.ToString());
            }
            writer.WriteLine();

            // ------------------------------------------------------------------
            // 5) Revised simplex: B^-1 starts as the identity (the initial
            //    basis columns - slacks/artificials - form an identity
            //    sub-matrix by construction).
            // ------------------------------------------------------------------
            double[,] Binv = Identity(m);
            int iteration = 0;

            while (true)
            {
                double[] cB = new double[m];
                for (int i = 0; i < m; i++) cB[i] = objCoeffs[basis[i]];

                // y = c_B^T * B^-1
                double[] y = new double[m];
                for (int k = 0; k < m; k++)
                {
                    double sum = 0;
                    for (int i = 0; i < m; i++) sum += cB[i] * Binv[i, k];
                    y[k] = sum;
                }

                // x_B = B^-1 * b
                double[] xB = new double[m];
                for (int i = 0; i < m; i++)
                {
                    double sum = 0;
                    for (int k = 0; k < m; k++) sum += Binv[i, k] * b[k];
                    xB[i] = sum;
                }

                // Price out: z_j - c_j for every column.
                double[] zMinusC = new double[totalColumns];
                for (int j = 0; j < totalColumns; j++)
                {
                    double zj = 0;
                    for (int k = 0; k < m; k++) zj += y[k] * A[k, j];
                    zMinusC[j] = zj - objCoeffs[j];
                }

                writer.WriteLine(iteration == 0 ? "--- Iteration 0 (Initial Basis) ---" : "--- Iteration " + iteration + " ---");
                writer.WriteLine("Basis: " + string.Join(", ", basis.Select(bi => columnNames[bi])));
                PrintMatrixBlock(writer, "Product Form - B^-1:", Binv, m, m);
                writer.WriteLine("x_B = B^-1 * b = [" + string.Join(", ", xB.Select(Round)) + "]");
                writer.WriteLine();
                writer.WriteLine("Price Out - y = c_B^T * B^-1 = [" + string.Join(", ", y.Select(Round)) + "]");
                var priceSb = new StringBuilder("  z_j - c_j: ");
                for (int j = 0; j < totalColumns; j++)
                    priceSb.Append(columnNames[j] + "=" + Round(zMinusC[j]) + "  ");
                writer.WriteLine(priceSb.ToString());
                writer.WriteLine();

                // Entering column = most negative (z_j - c_j).
                int enter = -1;
                double best = -Epsilon;
                for (int j = 0; j < totalColumns; j++)
                {
                    if (zMinusC[j] < best)
                    {
                        best = zMinusC[j];
                        enter = j;
                    }
                }

                if (enter == -1)
                {
                    writer.WriteLine("Optimality reached: all (z_j - c_j) >= 0.");
                    writer.WriteLine();

                    for (int i = 0; i < m; i++)
                    {
                        if (artificialColumns.Contains(basis[i]) && xB[i] > Epsilon)
                        {
                            writer.WriteLine("Problem is INFEASIBLE: artificial variable " + columnNames[basis[i]] +
                                              " remains positive (" + Round(xB[i]) + ") in the optimal basis.");
                            return;
                        }
                    }

                    ReportSolution(writer, model, maps, basis, xB, totalColumns, maximize);
                    return;
                }

                // d = B^-1 * A_enter (the entering column, updated through the current basis).
                double[] d = new double[m];
                for (int i = 0; i < m; i++)
                {
                    double sum = 0;
                    for (int k = 0; k < m; k++) sum += Binv[i, k] * A[k, enter];
                    d[i] = sum;
                }
                writer.WriteLine("Entering variable: " + columnNames[enter] + "   d = B^-1 * A_" + columnNames[enter] +
                                  " = [" + string.Join(", ", d.Select(Round)) + "]");

                // Ratio test (minimum ratio; Bland's rule on ties to avoid cycling).
                int leave = -1;
                double bestRatio = double.PositiveInfinity;
                for (int i = 0; i < m; i++)
                {
                    if (d[i] > Epsilon)
                    {
                        double ratio = xB[i] / d[i];
                        if (leave == -1 || ratio < bestRatio - Epsilon ||
                            (Math.Abs(ratio - bestRatio) < Epsilon && basis[i] < basis[leave]))
                        {
                            bestRatio = ratio;
                            leave = i;
                        }
                    }
                }

                if (leave == -1)
                {
                    writer.WriteLine("Problem is UNBOUNDED: column " + columnNames[enter] + " can increase without limit.");
                    return;
                }

                writer.WriteLine("Leaving variable: " + columnNames[basis[leave]] +
                                  "   (pivot row " + (leave + 1) + ", ratio = " + Round(bestRatio) + ")");
                writer.WriteLine();

                // Product-form update: B^-1_new = E * B^-1, where E is the
                // identity with column 'leave' replaced by the eta column
                // built from d (this is the "product form of the inverse").
                double[,] eta = Identity(m);
                for (int i = 0; i < m; i++)
                {
                    eta[i, leave] = (i == leave) ? 1.0 / d[leave] : -d[i] / d[leave];
                }

                double[,] newBinv = new double[m, m];
                for (int i = 0; i < m; i++)
                    for (int k = 0; k < m; k++)
                    {
                        double sum = 0;
                        for (int l = 0; l < m; l++) sum += eta[i, l] * Binv[l, k];
                        newBinv[i, k] = sum;
                    }
                Binv = newBinv;

                basis[leave] = enter;
                iteration++;

                if (iteration > 500)
                {
                    writer.WriteLine("Stopped after 500 iterations (possible cycling/degeneracy). Please check the model.");
                    return;
                }
            }
        }

        private static void ReportSolution(File_writer writer, Formulation model, List<VarMap> maps,
            int[] basis, double[] xB, int totalColumns, bool maximize)
        {
            double[] values = new double[totalColumns];
            for (int i = 0; i < basis.Length; i++) values[basis[i]] = xB[i];

            writer.WriteLine("--- Optimal Solution ---");
            double objectiveValue = 0;
            for (int j = 0; j < model.VarCount; j++)
            {
                var map = maps[j];
                double xj;
                if (map.Negated)
                    xj = -values[map.PositiveColumn];
                else if (map.NegativeColumn != -1)
                    xj = values[map.PositiveColumn] - values[map.NegativeColumn];
                else
                    xj = values[map.PositiveColumn];

                writer.WriteLine("x" + (j + 1) + " = " + Round(xj));
                objectiveValue += model.Obj_Func_coefficients[j] * xj;
            }

            writer.WriteLine("Optimal objective value (" + (maximize ? "max" : "min") + ") = " + Round(objectiveValue));
        }

        private static double[,] Identity(int size)
        {
            double[,] result = new double[size, size];
            for (int i = 0; i < size; i++) result[i, i] = 1.0;
            return result;
        }

        private static void PrintMatrixBlock(File_writer writer, string title, double[,] matrix, int rows, int cols)
        {
            writer.WriteLine(title);
            for (int i = 0; i < rows; i++)
            {
                var sb = new StringBuilder("  ");
                for (int j = 0; j < cols; j++)
                    sb.Append(Round(matrix[i, j]).ToString("0.###").PadLeft(10));
                writer.WriteLine(sb.ToString());
            }
        }

        private static double Round(double v) => File_writer.Round3(v);
    }
}
