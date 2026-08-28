using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplex_Solver_APP.Analysis
{
    public class DualSimplex
    {
        private readonly Optimal result;

        public DualSimplex(Optimal optimal)
        {
            result = optimal;
        }

        public string Solve()
        {
            StringBuilder output = new StringBuilder();

            output.AppendLine("================================================");
            output.AppendLine("           DUAL SIMPLEX RE-OPTIMIZATION");
            output.AppendLine("================================================");
            output.AppendLine();

            int iteration = 0;

            while (true)
            {
                int leaving = FindLeavingRow();

                if (leaving == -1)
                {
                    output.AppendLine("Optimality reached.");
                    break;
                }

                int entering = FindEnteringColumn(leaving);

                if (entering == -1)
                {
                    output.AppendLine("Problem became infeasible.");
                    result.IsInfeasible = true;
                    break;
                }

                output.AppendLine(
                    $"Iteration {iteration}: " +
                    $"Leaving {result.ColumnNames[result.Basis[leaving]]}, " +
                    $"Entering {result.ColumnNames[entering]}");

                Pivot(leaving, entering);

                result.Basis[leaving] = entering;

                iteration++;
            }

            UpdateSolution();

            output.AppendLine();
            output.AppendLine("New Objective Value: " +
                result.ObjectiveValue.ToString("0.###"));

            return output.ToString();
        }

        // ---------------- Leaving Row ----------------

        private int FindLeavingRow()
        {
            int rhs = result.Tableau.GetLength(1) - 1;

            int row = -1;
            double mostNegative = -1e-9;

            for (int i = 0; i < result.ConstraintCount; i++)
            {
                if (result.Tableau[i, rhs] < mostNegative)
                {
                    mostNegative = result.Tableau[i, rhs];
                    row = i;
                }
            }

            return row;
        }

        // ---------------- Entering Column ----------------

        private int FindEnteringColumn(int row)
        {
            int rhs = result.Tableau.GetLength(1) - 1;
            int objective = result.Tableau.GetLength(0) - 1;

            int entering = -1;
            double bestRatio = double.PositiveInfinity;

            for (int j = 0; j < rhs; j++)
            {
                double a = result.Tableau[row, j];

                if (a < -1e-9)
                {
                    double ratio =
                        result.Tableau[objective, j] / -a;

                    if (ratio < bestRatio)
                    {
                        bestRatio = ratio;
                        entering = j;
                    }
                }
            }

            return entering;
        }

        // ---------------- Pivot ----------------

        private void Pivot(int row, int column)
        {
            int rows = result.Tableau.GetLength(0);
            int cols = result.Tableau.GetLength(1);

            double pivot = result.Tableau[row, column];

            for (int j = 0; j < cols; j++)
                result.Tableau[row, j] /= pivot;

            for (int i = 0; i < rows; i++)
            {
                if (i == row)
                    continue;

                double factor = result.Tableau[i, column];

                for (int j = 0; j < cols; j++)
                    result.Tableau[i, j] -=
                        factor * result.Tableau[row, j];
            }
        }

        // ---------------- Update Solution ----------------

        private void UpdateSolution()
        {
            int rhs = result.Tableau.GetLength(1) - 1;

            result.VariableValues.Clear();

            foreach (string name in result.ColumnNames)
                result.VariableValues[name] = 0;

            for (int i = 0; i < result.ConstraintCount; i++)
            {
                string name = result.ColumnNames[result.Basis[i]];
                result.VariableValues[name] =
                    result.Tableau[i, rhs];
            }

            result.ObjectiveValue =
                result.Tableau[result.Tableau.GetLength(0) - 1, rhs];

            result.IsOptimal = true;
        }

        // ---------------- Public helper ----------------

        public string ResolveAfterRHSChange(int constraint, double newRHS)
        {
            int rhs = result.Tableau.GetLength(1) - 1;

            result.Tableau[constraint, rhs] = newRHS;
            result.RHS[constraint] = newRHS;

            return Solve();
        }

    }
}
