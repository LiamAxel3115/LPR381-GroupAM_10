using Simplex_Solver_APP.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Simplex_Solver_APP.Solvers;
using Simplex_Solver_APP.File_handler;

namespace Simplex_Solver_APP.Analysis
{
    public class Duality
    {
        private readonly Formulation primal;

        public Duality(Formulation solvedModel)
        {
            primal = solvedModel;
        }

        public Formulation BuildDual()
        {
            Formulation dual = new Formulation();

            // Reverse the objective
            dual.Objective =
                primal.Objective == Formulation_type.Max
                    ? Formulation_type.Min
                    : Formulation_type.Max;

            // One dual variable per primal constraint
            for (int i = 0; i < primal.ConstraintCount; i++)
            {
                dual.Obj_Func_coefficients.Add(
                    primal.Constraint[i].RHS_Value);

                dual.Sign_Restrictions.Add(
                    Sign_Restriction.Positive);
            }

            // One dual constraint per primal variable
            for (int j = 0; j < primal.VarCount; j++)
            {
                List<double> column = new List<double>();

                for (int i = 0; i < primal.ConstraintCount; i++)
                {
                    column.Add(
                        primal.Constraint[i]
                              .Constraint_Coefficients[j]);
                }

                Equality_Sign sign =
                    primal.Objective == Formulation_type.Max
                        ? Equality_Sign.GreaterThanOrEqual
                        : Equality_Sign.LessThanOrEqual;

                dual.Constraint.Add(
                    new Conditions(
                        column,
                        sign,
                        primal.Obj_Func_coefficients[j]));
            }

            return dual;
        }

        public string DisplayDual()
        {
            StringBuilder output = new StringBuilder();

            output.AppendLine("================================================");
            output.AppendLine("               DUAL MODEL");
            output.AppendLine("================================================");
            output.AppendLine();

            output.Append(BuildDual().Summariser());

            return output.ToString();
        }

        public string VerifyDuality(
            double primalValue,
            double dualValue)
        {
            StringBuilder output = new StringBuilder();

            output.AppendLine("================================================");
            output.AppendLine("             DUALITY CHECK");
            output.AppendLine("================================================");
            output.AppendLine();

            output.AppendLine(
                "Primal Objective : " +
                primalValue.ToString("0.###"));

            output.AppendLine(
                "Dual Objective   : " +
                dualValue.ToString("0.###"));

            output.AppendLine();

            if (Math.Abs(primalValue - dualValue) < 0.001)
                output.AppendLine("Strong Duality holds.");
            else
                output.AppendLine("Weak Duality only.");

            return output.ToString();
        }
    }
}
