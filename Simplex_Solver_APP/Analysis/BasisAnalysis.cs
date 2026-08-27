using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplex_Solver_APP.Analysis
{
    public class BasisAnalysis
    {
        private readonly Optimal result;

        public BasisAnalysis(Optimal optimal)
        {
            result = optimal;
        }

        public (double increase, double decrease) BasicRange(int column)
        {
            int basisRow = Array.IndexOf(result.Basis, column);

            if (basisRow < 0)
                return (0, 0);

            double increase = double.PositiveInfinity;
            double decrease = double.PositiveInfinity;

            int objectiveRow = result.Tableau.GetLength(0) - 1;

            for (int j = 0; j < result.VariableCount; j++)
            {
                if (result.Basis.Contains(j))
                    continue;

                double pivotValue = result.Tableau[basisRow, j];
                double reducedCost = result.Tableau[objectiveRow, j];

                if (Math.Abs(pivotValue) < 1e-9)
                    continue;

                double limit = reducedCost / pivotValue;

                if (pivotValue > 0)
                    decrease = Math.Min(decrease, limit);
                else
                    increase = Math.Min(increase, -limit);
            }

            if (double.IsInfinity(increase))
                increase = double.PositiveInfinity;

            if (double.IsInfinity(decrease))
                decrease = double.PositiveInfinity;

            return (increase, decrease);
        }

        public double[,] InverseBasis()
        {
            int m = result.ConstraintCount;
            int totalCols = result.Tableau.GetLength(1) - 1;   // Exclude RHS

            // Build the current basis matrix B
            double[,] B = new double[m, m];

            for (int i = 0; i < m; i++)
            {
                int basisColumn = result.Basis[i];

                for (int r = 0; r < m; r++)
                    B[r, i] = result.Tableau[r, basisColumn];
            }

            return InvertMatrix(B);
        }
        private double[,] InvertMatrix(double[,] matrix)
        {
            int n = matrix.GetLength(0);

            double[,] aug = new double[n, n * 2];

            // Create [A | I]
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                    aug[i, j] = matrix[i, j];

                aug[i, i + n] = 1;
            }

            // Gauss-Jordan elimination
            for (int i = 0; i < n; i++)
            {
                double pivot = aug[i, i];

                if (Math.Abs(pivot) < 1e-9)
                    throw new Exception("Basis matrix is singular.");

                for (int j = 0; j < 2 * n; j++)
                    aug[i, j] /= pivot;

                for (int r = 0; r < n; r++)
                {
                    if (r == i) continue;

                    double factor = aug[r, i];

                    for (int c = 0; c < 2 * n; c++)
                        aug[r, c] -= factor * aug[i, c];
                }
            }

            // Extract inverse
            double[,] inverse = new double[n, n];

            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    inverse[i, j] = aug[i, j + n];

            return inverse;
        }
    }

}
