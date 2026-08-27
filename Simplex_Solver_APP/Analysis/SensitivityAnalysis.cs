using Simplex_Solver_APP.File_handler;
// import modules
using Simplex_Solver_APP.Model;
using Simplex_Solver_APP.Solvers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplex_Solver_APP.Analysis
{
    public class SensitivityAnalysis
    {
        private readonly Optimal result;
        private readonly BasisAnalysis basis;

        public SensitivityAnalysis(Optimal solverResult)
        {
            result = solverResult;
            basis = new BasisAnalysis(result);
        }

        private int ObjectiveRow => result.Tableau.GetLength(0) - 1;
        private int RHSColumn => result.Tableau.GetLength(1) - 1;

        private bool IsBasicVariable(int column)
        {
            return result.Basis.Contains(column);
        }

        private string VariableName(int column)
        {
            return result.ColumnNames[column];
        }

        private double ReducedCost(int column)
        {
            return result.Tableau[ObjectiveRow, column];
        }


        public string DisplayShadowPrices()
        {
            StringBuilder output = new StringBuilder();

            output.AppendLine("================================================");
            output.AppendLine("               SHADOW PRICES");
            output.AppendLine("================================================");
            output.AppendLine();

            output.AppendLine("Only original model constraints are shown.");
            output.AppendLine();

            int slackStart = result.VariableCount;
            int originalConstraints = result.OriginalModel.Constraint.Count;

            for (int i = 0; i < originalConstraints; i++)
            {
                output.AppendLine(
                    $"Constraint c{i + 1}: {result.Tableau[ObjectiveRow, slackStart + i]:0.###}");
            }

            return output.ToString();
        }


        public string DisplayReducedCosts()
        {
            StringBuilder output = new StringBuilder();

            output.AppendLine("================================================");
            output.AppendLine("               REDUCED COSTS");
            output.AppendLine("================================================");
            output.AppendLine();

            for (int j = 0; j < result.VariableCount; j++)
            {
                output.AppendLine(
                    $"{VariableName(j)} : {ReducedCost(j):0.###}");
            }

            return output.ToString();
        }


        public string DisplayNonBasicRange(int column)
        {
            StringBuilder output = new StringBuilder();

            output.AppendLine("================================================");
            output.AppendLine("      RANGE OF NON-BASIC VARIABLE");
            output.AppendLine("================================================");
            output.AppendLine();

            if (IsBasicVariable(column))
            {
                output.AppendLine($"{VariableName(column)} is currently BASIC.");
                output.AppendLine("Use Basic Variable Range instead.");

                return output.ToString();
            }

            double rc = ReducedCost(column);

            output.AppendLine($"Variable      : {VariableName(column)}");
            output.AppendLine($"Reduced Cost  : {rc:0.###}");
            output.AppendLine();

            if (Math.Abs(rc) < 0.000001)
            {
                output.AppendLine("The variable has an alternative optimal value.");
            }
            else if (rc > 0)
            {
                output.AppendLine(
                    $"Objective coefficient may decrease by {rc:0.###} before entering the basis.");
            }
            else
            {
                output.AppendLine(
                    $"Objective coefficient may increase by {Math.Abs(rc):0.###} before entering the basis.");
            }

            return output.ToString();
        }


        public string DisplayBasicRange(int column)
        {
            StringBuilder output = new StringBuilder();

            output.AppendLine("================================================");
            output.AppendLine("        RANGE OF BASIC VARIABLE");
            output.AppendLine("================================================");
            output.AppendLine();

            if (!IsBasicVariable(column))
            {
                output.AppendLine($"{VariableName(column)} is not currently basic.");

                return output.ToString();
            }

            var range = basis.BasicRange(column);

            output.AppendLine($"Variable : {VariableName(column)}");
            output.AppendLine();
            output.AppendLine($"Allowable Increase : {range.increase:0.###}");
            output.AppendLine($"Allowable Decrease : {range.decrease:0.###}");

            return output.ToString();
        }


        public string DisplayRHSRange(int constraint)
        {
            StringBuilder output = new StringBuilder();

            output.AppendLine("================================================");
            output.AppendLine("            RHS SENSITIVITY REPORT");
            output.AppendLine("================================================");
            output.AppendLine();

            output.AppendLine($"Original Constraint : c{constraint + 1}");
            output.AppendLine($"Current RHS : {result.OriginalModel.Constraint[constraint].RHS_Value:0.###}");
            output.AppendLine(
                $"Current Shadow Price : {result.Tableau[ObjectiveRow, SlackStart + constraint]:0.###}");
            output.AppendLine();

            output.AppendLine("This report applies only to the original model.");
            output.AppendLine("Binary-bound constraints introduced by the");
            output.AppendLine("LP relaxation are excluded.");

            return output.ToString();
        }
        public string DisplayColumnRange(int row, int column)
        {
            StringBuilder output = new StringBuilder();

            output.AppendLine("================================================");
            output.AppendLine("      NON-BASIC COLUMN RANGE");
            output.AppendLine("================================================");
            output.AppendLine();

            output.AppendLine($"Variable: {result.ColumnNames[column]}");
            output.AppendLine($"Constraint: c{row + 1}");
            output.AppendLine($"Current Coefficient: {result.Tableau[row, column]:0.###}");
            output.AppendLine("Changing this coefficient may require");
            output.AppendLine("re-optimization.");

            return output.ToString();
        }

        public bool ValidVariable(int index)
        {
            return index >= 0 && index < result.VariableCount;
        }

        public bool ValidConstraint(int index)
        {
            return index >= 0 && index < result.ConstraintCount;
        }
        public string ApplyNonBasicVariableChange(int variableIndex, double newCoefficient)
        {
            Formulation modified = CloneModel();

            modified.Obj_Func_coefficients[variableIndex] = newCoefficient;

            return ResolveModel(
                modified,
                $"Applied objective coefficient change to x{variableIndex + 1} = {newCoefficient:0.###}");
        }


        public string ApplyBasicVariableChange(int variableIndex, double newCoefficient)
        {
            Formulation modified = CloneModel();

            modified.Obj_Func_coefficients[variableIndex] = newCoefficient;

            return ResolveModel(
                modified,
                $"Applied basic variable change to x{variableIndex + 1} = {newCoefficient:0.###}");
        }

        public string ApplyRHSChange(int constraintIndex, double newRHS)
        {
            Formulation modified = CloneModel();

            modified.Constraint[constraintIndex].SetRHS(newRHS);

            return ResolveModel(
                modified,
                $"Applied RHS change on constraint c{constraintIndex + 1} = {newRHS:0.###}");
        }

        public string ApplyColumnChange(
            int constraintIndex,
            int variableIndex,
            double newValue)
        {
            Formulation modified = CloneModel();

            modified.Constraint[constraintIndex]
                    .Constraint_Coefficients[variableIndex] = newValue;

            return ResolveModel(
                modified,
                $"Applied coefficient change A[{constraintIndex + 1},{variableIndex + 1}] = {newValue:0.###}");
        }


        public string AddActivity(
            double objectiveCoefficient,
            List<double> coefficients,
            Sign_Restriction restriction)
        {
            Formulation modified = CloneModel();

            modified.Obj_Func_coefficients.Add(objectiveCoefficient);
            modified.Sign_Restrictions.Add(restriction);
            modified.VarCount++;

            for (int i = 0; i < modified.Constraint.Count; i++)
                modified.Constraint[i].Constraint_Coefficients.Add(coefficients[i]);

            return ResolveModel(modified, "Added a new activity.");
        }

        public string AddConstraint(
            List<double> coefficients,
            Equality_Sign sign,
            double rhs)
        {
            Formulation modified = CloneModel();

            modified.Constraint.Add(
                new Conditions(coefficients, sign, rhs));

            return ResolveModel(modified, "Added a new constraint.");
        }

        private Formulation CloneModel()
        {
            Formulation clone = new Formulation();

            clone.Objective = result.OriginalModel.Objective;
            clone.VarCount = result.OriginalModel.VarCount;

            clone.Obj_Func_coefficients =
                new List<double>(result.OriginalModel.Obj_Func_coefficients);

            clone.Sign_Restrictions =
                new List<Sign_Restriction>(result.OriginalModel.Sign_Restrictions);

            clone.Constraint = new List<Conditions>();

            foreach (Conditions c in result.OriginalModel.Constraint)
            {
                clone.Constraint.Add(
                    new Conditions(
                        new List<double>(c.Constraint_Coefficients),
                        c.Inequality,
                        c.RHS_Value));
            }

            return clone;
        }

        private string ResolveModel(Formulation model, string heading)
        {
            StringBuilder output = new StringBuilder();

            output.AppendLine("================================================");
            output.AppendLine("      SENSITIVITY RE-OPTIMIZATION");
            output.AppendLine("================================================");
            output.AppendLine();
            output.AppendLine(heading);
            output.AppendLine();

            PrimalSimplexSolver solver = new PrimalSimplexSolver();

            File_writer tempWriter =
                new File_writer(System.IO.Path.GetTempFileName(), false, false);

            solver.Solve(model, tempWriter);

            Optimal solved = solver.GetLastResult();

            tempWriter.Dispose();

            output.AppendLine("Status");

            if (solved.IsOptimal)
                output.AppendLine("Optimal");
            else if (solved.IsUnbounded)
                output.AppendLine("Unbounded");
            else if (solved.IsInfeasible)
                output.AppendLine("Infeasible");

            output.AppendLine();
            output.AppendLine($"Objective = {solved.ObjectiveValue:0.###}");
            output.AppendLine();
            output.AppendLine("Variable Values");

            foreach (var variable in solved.VariableValues)
                output.AppendLine($"{variable.Key} = {variable.Value:0.###}");

            return output.ToString();
        }
        private int OriginalConstraintCount
        {
            get { return result.OriginalModel.Constraint.Count; }
        }

        private int SlackStart
        {
            get { return result.VariableCount; }
        }
    }

}

