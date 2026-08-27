using Simplex_Solver_APP.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplex_Solver_APP.Analysis
{
    public class Optimal
    {
        public bool IsOptimal { get; set; }
        public bool IsUnbounded { get; set; }
        public bool IsInfeasible { get; set; }

        public double ObjectiveValue { get; set; }

        public double[,] Tableau { get; set; }
        public double[] RHS { get; set; }

        // NEW
        public double[] OriginalRHS { get; set; }
        public Formulation OriginalModel { get; set; }

        public int[] Basis { get; set; }

        public List<string> ColumnNames { get; set; }

        public int ConstraintCount { get; set; }
        public int VariableCount { get; set; }

        public Dictionary<string, double> VariableValues { get; set; }

        public List<double[,]> TableauHistory { get; set; }

        public Optimal()
        {
            ColumnNames = new List<string>();
            VariableValues = new Dictionary<string, double>();
            TableauHistory = new List<double[,]>();

            IsOptimal = false;
            IsUnbounded = false;
            IsInfeasible = false;

            ObjectiveValue = 0;
        }

        public static double[,] CloneTableau(double[,] source)
        {
            if (source == null)
                return null;

            return (double[,])source.Clone();
        }

        public static double[] CloneArray(double[] source)
        {
            if (source == null)
                return null;

            return (double[])source.Clone();
        }

        public static int[] CloneArray(int[] source)
        {
            if (source == null)
                return null;

            return (int[])source.Clone();
        }

        public void SaveIteration(double[,] tableau)
        {
            TableauHistory.Add(CloneTableau(tableau));
        }

        public static double[,] BuildTableau(double[,] matrix, double[] rhs)
        {
            if (matrix == null || rhs == null)
                return null;

            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);

            double[,] tableau = new double[rows, cols + 1];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                    tableau[i, j] = matrix[i, j];

                tableau[i, cols] = rhs[i];
            }

            return tableau;
        }

        public string Summariser()
        {
            StringBuilder output = new StringBuilder();

            output.AppendLine("================================================");
            output.AppendLine("            SOLVER RESULT SUMMARY");
            output.AppendLine("================================================");
            output.AppendLine();

            if (IsOptimal)
                output.AppendLine("Status : OPTIMAL");
            else if (IsUnbounded)
                output.AppendLine("Status : UNBOUNDED");
            else if (IsInfeasible)
                output.AppendLine("Status : INFEASIBLE");
            else
                output.AppendLine("Status : UNKNOWN");

            output.AppendLine();
            output.AppendLine("Objective Value : " + ObjectiveValue.ToString("0.###"));
            output.AppendLine();

            output.AppendLine("Variable Values");

            foreach (KeyValuePair<string, double> variable in VariableValues)
            {
                output.AppendLine(
                    "  " +
                    variable.Key +
                    " = " +
                    variable.Value.ToString("0.###"));
            }

            output.AppendLine();
            output.AppendLine("Stored Tableaux : " + TableauHistory.Count);

            return output.ToString();
        }
    }
}
