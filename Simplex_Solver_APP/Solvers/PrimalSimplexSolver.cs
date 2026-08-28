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
    /// Solves a Linear Programming model using the Primal (Tableau) Simplex
    /// Algorithm together with the Big-M method, so that &lt;=, &gt;= and =
    /// constraints can all be handled in a single, uniform pass.
    ///
    /// The solver:
    ///   1) rewrites the model in standard/canonical form (substituting
    ///      negative and unrestricted-in-sign (urs) variables, and adding
    ///      slack / surplus / artificial variables),
    ///   2) prints the canonical form,
    ///   3) prints every tableau iteration until optimality, infeasibility
    ///      or unboundedness is detected, and
    ///   4) prints the final solution translated back to the ORIGINAL
    ///      decision variables (x1, x2, ...).
    /// </summary>
    public class PrimalSimplexSolver
    {
        private const double BigM = 1000000.0;   // large penalty for artificial variables
        private const double Epsilon = 1e-9;     // numeric tolerance

        // Describes how one original decision variable (x_j) maps onto the
        // column(s) that are actually used inside the standard-form tableau.
        private class VarMap
        {
            public int OriginalIndex;
            public int PositiveColumn = -1;   // column of x_j (or the "+" half of a urs split), or y_j if negated
            public int NegativeColumn = -1;   // column of the "-" half of a urs split (urs only)
            public bool Negated;              // true when x_j was substituted as x_j = -y_j
        }

        // A slack / surplus / artificial column that still needs to be added to a row.
        private class ExtraColumn
        {
            public int Row;
            public string Kind; // "slack", "surplus" or "artificial"
        }

        public void Solve(Formulation model, File_writer writer)
        {
            writer.WriteLine("========================================================");
            writer.WriteLine("           PRIMAL SIMPLEX ALGORITHM (Big-M method)        ");
            writer.WriteLine("========================================================");
            writer.WriteLine();

            bool maximize = model.Objective == Formulation_type.Max;
            // Internally we always MAXIMIZE (dirSign * f(x)); the true objective
            // value is recomputed from the original coefficients at the end, so
            // no re-negation of the reported result is required.
            double dirSign = maximize ? 1.0 : -1.0;

            // ------------------------------------------------------------------
            // 1) Build the variable map: +, -, urs, int and bin restrictions.
            //    (int / bin are relaxed to ordinary continuous, non-negative
            //     variables for the Primal Simplex; binary variables also get
            //     an implicit x_j <= 1 bound so the relaxation stays bounded.)
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
                    // x_j <= 0  ->  x_j = -y_j , y_j >= 0
                    map.Negated = true;
                    map.PositiveColumn = columnNames.Count;
                    columnNames.Add("y" + (j + 1));
                    objCoeffs.Add(-c);
                }
                else if (restriction == Sign_Restriction.urs)
                {
                    // x_j unrestricted -> x_j = p_j - n_j , p_j, n_j >= 0
                    map.PositiveColumn = columnNames.Count;
                    columnNames.Add("x" + (j + 1) + "+");
                    objCoeffs.Add(c);

                    map.NegativeColumn = columnNames.Count;
                    columnNames.Add("x" + (j + 1) + "-");
                    objCoeffs.Add(-c);
                }
                else
                {
                    // Positive, Int or Bin -> ordinary non-negative variable
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

            // Binary variables need an explicit upper bound for the LP relaxation.
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

            // Make sure every RHS is >= 0 (required so slack/artificial vars can start basic).
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
                    // Equal stays Equal
                }
            }

            // ------------------------------------------------------------------
            // 3) Attach slack / surplus / artificial columns.
            // ------------------------------------------------------------------
            var extraColumns = new List<ExtraColumn>();
            var extraNames = new List<string>();
            int slackCounter = 1, surplusCounter = 1, artCounter = 1;

            for (int i = 0; i < m; i++)
            {
                switch (relations[i])
                {
                    case Equality_Sign.LessThanOrEqual:
                        extraNames.Add("s" + slackCounter++);
                        extraColumns.Add(new ExtraColumn { Row = i, Kind = "slack" });
                        break;
                    case Equality_Sign.GreaterThanOrEqual:
                        extraNames.Add("e" + surplusCounter++);
                        extraColumns.Add(new ExtraColumn { Row = i, Kind = "surplus" });
                        extraNames.Add("a" + artCounter++);
                        extraColumns.Add(new ExtraColumn { Row = i, Kind = "artificial" });
                        break;
                    case Equality_Sign.Equal:
                        extraNames.Add("a" + artCounter++);
                        extraColumns.Add(new ExtraColumn { Row = i, Kind = "artificial" });
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
            foreach (var extra in extraColumns)
            {
                if (extra.Kind == "slack")
                {
                    A[extra.Row, col] = 1;
                    objCoeffs.Add(0);
                    basis[extra.Row] = col;
                }
                else if (extra.Kind == "surplus")
                {
                    A[extra.Row, col] = -1;
                    objCoeffs.Add(0);
                }
                else // artificial
                {
                    A[extra.Row, col] = 1;
                    objCoeffs.Add(-BigM);
                    artificialColumns.Add(col);
                    if (basis[extra.Row] == -1) basis[extra.Row] = col;
                }
                col++;
            }

            double[] b = rhsList.ToArray();

            // ------------------------------------------------------------------
            // 4) Print the canonical form.
            // ------------------------------------------------------------------
            writer.WriteLine();
            writer.WriteLine("--- Canonical Form ---");
            var objLine = new StringBuilder();
            objLine.Append(maximize ? "Maximize Z = " : "Minimize Z = ");
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
            // 5) Simplex iterations.
            // ------------------------------------------------------------------
            int iteration = 0;
            while (true)
            {
                PrintTableau(writer, iteration, columnNames, A, b, basis, m, totalColumns);

                // z_j - c_j for every column.
                double[] zMinusC = new double[totalColumns];
                for (int j = 0; j < totalColumns; j++)
                {
                    double zj = 0;
                    for (int i = 0; i < m; i++)
                        zj += objCoeffs[basis[i]] * A[i, j];
                    zMinusC[j] = zj - objCoeffs[j];
                }

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
                    break;
                }

                // Ratio test (minimum ratio; Bland's rule on ties to avoid cycling).
                int leave = -1;
                double bestRatio = double.PositiveInfinity;
                for (int i = 0; i < m; i++)
                {
                    if (A[i, enter] > Epsilon)
                    {
                        double ratio = b[i] / A[i, enter];
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

                writer.WriteLine("Entering variable: " + columnNames[enter] +
                                  "   Leaving variable: " + columnNames[basis[leave]] +
                                  "   (pivot row c" + (leave + 1) + ", ratio = " + Round(bestRatio) + ")");
                writer.WriteLine();

                // Pivot.
                double pivot = A[leave, enter];
                for (int j = 0; j < totalColumns; j++) A[leave, j] /= pivot;
                b[leave] /= pivot;

                for (int i = 0; i < m; i++)
                {
                    if (i == leave) continue;
                    double factor = A[i, enter];
                    if (Math.Abs(factor) < Epsilon) continue;
                    for (int j = 0; j < totalColumns; j++)
                        A[i, j] -= factor * A[leave, j];
                    b[i] -= factor * b[leave];
                }

                basis[leave] = enter;
                iteration++;

                if (iteration > 500)
                {
                    writer.WriteLine("Stopped after 500 iterations (possible cycling/degeneracy). Please check the model.");
                    return;
                }
            }

            // ------------------------------------------------------------------
            // 6) Feasibility check: an artificial variable left positive in the
            //    basis at optimality means the original model was infeasible.
            // ------------------------------------------------------------------
            for (int i = 0; i < m; i++)
            {
                if (artificialColumns.Contains(basis[i]) && b[i] > Epsilon)
                {
                    writer.WriteLine("Problem is INFEASIBLE: artificial variable " + columnNames[basis[i]] +
                                      " remains positive (" + Round(b[i]) + ") in the optimal basis.");
                    return;
                }
            }

            // ------------------------------------------------------------------
            // 7) Extract & report the solution (translated back to x1, x2, ...).
            // ------------------------------------------------------------------
            double[] values = new double[totalColumns];
            for (int i = 0; i < m; i++) values[basis[i]] = b[i];

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

        private static void PrintTableau(File_writer writer, int iteration, List<string> columnNames,
            double[,] A, double[] b, int[] basis, int m, int totalColumns)
        {
            writer.WriteLine(iteration == 0 ? "--- Iteration 0 (Initial Tableau) ---" : "--- Iteration " + iteration + " ---");

            var header = new StringBuilder("Basis".PadRight(8));
            foreach (var name in columnNames) header.Append(name.PadLeft(10));
            header.Append("RHS".PadLeft(10));
            writer.WriteLine(header.ToString());

            for (int i = 0; i < m; i++)
            {
                var line = new StringBuilder(columnNames[basis[i]].PadRight(8));
                for (int j = 0; j < totalColumns; j++)
                    line.Append(Round(A[i, j]).ToString("0.###").PadLeft(10));
                line.Append(Round(b[i]).ToString("0.###").PadLeft(10));
                writer.WriteLine(line.ToString());
            }
            writer.WriteLine();
        }

        private static double Round(double v) => File_writer.Round3(v);
    }
}
