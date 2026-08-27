using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplex_Solver_APP.Analysis
{
    public class PriceOut
    {
        private readonly Optimal result;
        private readonly BasisAnalysis basis;

        public PriceOut(Optimal optimal)
        {
            result = optimal;
            basis = new BasisAnalysis(optimal);
        }

        public double[] DualPrices()
        {
            double[,] BInv = basis.InverseBasis();

            int m = result.ConstraintCount;
            double[] cB = new double[m];

            for (int i = 0; i < m; i++)
            {
                /* string name = result.ColumnNames[result.Basis[i]];

                 if (result.VariableValues.ContainsKey(name))
                     cB[i] = result.VariableValues[name];
                 else
                     cB[i] = 0;
                */
                cB[i] = ObjectiveCoefficient(result.Basis[i]);
            }

            double[] y = new double[m];

            for (int j = 0; j < m; j++)
                for (int i = 0; i < m; i++)
                    y[j] += cB[i] * BInv[i, j];

            return y;
        }

        public double ReducedCost(int column)
        {
            double[] y = DualPrices();

            double value = 0;

            for (int i = 0; i < result.ConstraintCount; i++)
                value += y[i] * result.Tableau[i, column];

            return value - ObjectiveCoefficient(column);
        }

        public (double increase, double decrease) NonBasicRange(int column)
        {
            double rc = ReducedCost(column);

            return (double.PositiveInfinity, Math.Abs(rc));
        }

        public (double increase, double decrease) BasicRange(int column)
        {
            return (double.PositiveInfinity, double.PositiveInfinity);
        }

        public double ObjectiveCoefficient(int column)
        {
            //string name = result.ColumnNames[column];

            if (result.ObjCoefficients != null && column < result.ObjCoefficients.Length)
                return result.ObjCoefficients[column];

            return 0;
        }

        public double UpdatedReducedCost(int column, double newCoefficient)
        {
            return ReducedCost(column) - (newCoefficient - ObjectiveCoefficient(column));
        }

        public bool BasisRemainsOptimal(int column, double newCoefficient)
        {
            return UpdatedReducedCost(column, newCoefficient) >= -1e-9;
        }
    }
}
