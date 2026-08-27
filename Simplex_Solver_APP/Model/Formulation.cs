using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplex_Solver_APP.Model
{
    public class Formulation
    {

        public Formulation_type Objective { get; set; }
        public Equality_Sign Comparison { get; set; }
        public List<double> Obj_Func_coefficients { get; set; } = new List<double>();
        public List<Conditions> Constraint { get; set; } = new List<Conditions>();
        public List<Sign_Restriction> Sign_Restrictions { get; set; } = new List<Sign_Restriction>();

        //public int VarCount => Obj_Func_coefficients.Count;
        public int VarCount
        {
            get { return Obj_Func_coefficients.Count; }
            set { }
        }
        public int ConstraintCount => Constraint.Count;

        // Integer variables or binary variables
        public bool IsIntegerProgram =>
            Sign_Restrictions.Any(check => check == Sign_Restriction.Int || check == Sign_Restriction.Bin);
        public void Validate()
        {
            if (VarCount == 0)
                throw new FormatException("Model has no decision variables.");

            if (ConstraintCount == 0)
                throw new FormatException("Model has no constraints.");

            if (Sign_Restrictions.Count != VarCount)
                throw new FormatException(
                    $"Expected {VarCount} sign restrictions (one per decision variable) " +
                    $"but found {Sign_Restrictions.Count}.");

            for (int i = 0; i < Constraint.Count; i++)
            {
                var c = Constraint[i];
                if (c.Constraint_Coefficients.Count != VarCount)
                    throw new FormatException(
                        $"Constraint {i + 1} has {c.Constraint_Coefficients.Count} coefficients, " +
                        $"expected {VarCount} (one per decision variable).");
            }
        }
        public string Summariser()
        {
            var summary = new StringBuilder();

            summary.AppendLine($"Type\t: {(IsIntegerProgram ? "Integer Programming" : "Linear Programming")} model");
            summary.AppendLine($"\nObjective\t: {Formatting.Formulation[Objective]} " + string.Join(" ", Obj_Func_coefficients.Select((c, i) => $"{(c >= 0 ? "+" : "")}{c:0.###}x{i + 1}")));
            summary.AppendLine($"Variables\t: {VarCount}");
            summary.AppendLine($"\nConstraints\t: {ConstraintCount}");

            for (int i = 0; i < Constraint.Count; i++)
                summary.AppendLine($"  c{i + 1}: {Constraint[i]}");

            summary.AppendLine("\nSign restrictions:");
            summary.AppendLine("  " + string.Join(" ", Sign_Restrictions.Select((s, i) => $"x{i + 1}={Formatting.Restrictions[s]}")));

            return summary.ToString();
        }
    }
}
