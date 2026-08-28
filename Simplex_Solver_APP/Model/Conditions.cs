using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Simplex_Solver_APP.Model
{
    public class Conditions
    {

        public List<double> Constraint_Coefficients { get; private set; }
        public Equality_Sign Inequality { get; private set; }
        public double RHS_Value { get; private set; }
        public int Index { get; set; }
        public Conditions(List<double> coefficients, Equality_Sign inequality, double rhs)
        {
            Constraint_Coefficients = coefficients ?? throw new ArgumentNullException(nameof(coefficients)); ;
            Inequality = inequality;
            RHS_Value = rhs;
        }
        public override string ToString()
        {
            var output = new StringBuilder();
            for (int i = 0; i < Constraint_Coefficients.Count; i++)
            {
                output.Append(Constraint_Coefficients[i] >= 0 ? "+" : "-");
                output.Append(Math.Abs(Constraint_Coefficients[i]).ToString("0.###"));
                output.Append($"x{i + 1} ");
            }
            output.Append(Formatting.Comparison[Inequality]);
            output.Append(" ");
            output.Append(RHS_Value.ToString("0.###"));

            return output.ToString();
        }
        public void SetRHS(double newValue)
        {
            RHS_Value = newValue;
        }
    }
}
