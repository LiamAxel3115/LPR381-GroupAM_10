using System;
using System.Collections.Generic;
using System.Linq;

using Simplex_Solver_APP.Model;
using Simplex_Solver_APP.File_handler;
using Simplex_Solver_APP.Analysis;

namespace Simplex_Solver_APP.Solvers
{

    public class BranchAndBoundSolver
    {

        private const double EPSILON = 1e-6;

        private class LeafNode
        {
            public int NodeID;
            public int ParentID;
            public string BDescription;
            public Formulation SubModel;

        }



        public void Solve(Formulation model, File_writer writer)
        {

            

        writer.WriteLine("=======================================");
            writer.WriteLine("  BRANCH AND BOUND SIMPLEX ALGORITHM   ");
            writer.WriteLine("=======================================");
            writer.WriteLine();

            if(!model.IsIntegerProgram)
            {
                writer.WriteLine("Model is not an integer program variables are not restricted to Int/Bin.");
                writer.WriteLine("Will give LPs relaxation optimality.");
                writer.WriteLine();
            }
            

            PrimalSimplexSolver primalSolver = new PrimalSimplexSolver();
        Stack<LeafNode> nodeStack = new Stack<LeafNode>();
        int nodeCounter = 0;

        nodeStack.Push(new LeafNode { NodeID = nodeCounter++, ParentID = -1, BDescription = "Root", SubModel = model });


            double bestZ = double.NegativeInfinity;
    double[] bestSolution = null;
    int bestNodeID = -1;
    bool maximize = model.Objective == Formulation_type.Max;

            while (nodeStack.Count > 0)
            {
                LeafNode node = nodeStack.Pop();

                writer.WriteLine("=======================================");
                writer.WriteLine($"Sub-problem: {node.NodeID} (parent: {node.ParentID}):  {node.BDescription}");
                writer.WriteLine("=======================================");

                primalSolver.Solve(node.SubModel, writer);
                Optimal result = primalSolver.GetLastResult();

                if (result == null)
                {
                    writer.WriteLine($"Status: could not be solved cleanly, Pruned.");
                    writer.WriteLine();
                    continue;
                }




                if (result.IsInfeasible)
                {
                    writer.WriteLine($"Status: INFEASIBLE, Pruned.");
                    writer.WriteLine();
                    continue;
                }



                if (result.IsUnbounded)
                {
                    writer.WriteLine($"Status: UNBOUNDED, Pruned.");
                    writer.WriteLine();
                    continue;
                }



                double[] solution = new double[node.SubModel.VarCount];
                for(int j = 0; j < node.SubModel.VarCount; j++)
                {
                    solution[j] = result.VariableValues.TryGetValue("x" + (j + 1), out double v) ? v : 0;
                }




                double comparableZ = maximize ? result.ObjectiveValue : -result.ObjectiveValue;

                if (comparableZ <= bestZ + EPSILON)
                {
                    writer.WriteLine($"Status: BOUNDED ( z = {File_writer.Round3(result.ObjectiveValue)} cannot beat the current best. ), Pruned.");
                    writer.WriteLine();
                    continue;
                }


                int branchVariable = FindFractionalIntegerVariable(model, solution);
                if (branchVariable == -1)
                {

                    writer.WriteLine($"Status: INTEGER-FEASIBLE, Candidate Solution");
                    writer.WriteLine();

                    bestZ = comparableZ;
                    bestSolution = solution;
                    bestNodeID = node.NodeID;
                    continue;
                }




                writer.WriteLine($"Status: Fractional on x{branchVariable + 1} = {File_writer.Round3(solution[branchVariable])}, Branching");
                writer.WriteLine();

                double value = solution[branchVariable];
                Formulation floorChild = WithExtraConstraint(node.SubModel, branchVariable, Equality_Sign.LessThanOrEqual, Math.Floor(value));
                Formulation ceilChild = WithExtraConstraint(node.SubModel, branchVariable, Equality_Sign.GreaterThanOrEqual, Math.Ceiling(value));

                nodeStack.Push(new LeafNode { NodeID = nodeCounter++, ParentID = node.NodeID, BDescription = $"x{branchVariable + 1} <= {Math.Floor(value)}", SubModel = floorChild });
                nodeStack.Push(new LeafNode { NodeID = nodeCounter++, ParentID = node.NodeID, BDescription = $"x{branchVariable + 1} >= {Math.Ceiling(value)}", SubModel = ceilChild });

            }

writer.WriteLine("=======================================");
writer.WriteLine("Best Candidate");
writer.WriteLine("=======================================");

if (bestSolution == null)
{

    writer.WriteLine("There was no Feasible Integer Solution Found.");
    return;
}



for (int j = 0; j < bestSolution.Length; j++)
{
    writer.WriteLine($"x{j + 1} = {File_writer.Round3(bestSolution[j])}");
}

writer.WriteLine($"Sub-problem: {bestNodeID}");
writer.WriteLine($"Optimal Objective Value ({(maximize? "max" : "min")}) = {File_writer.Round3(maximize ? bestZ : -bestZ)}");

}


private int FindFractionalIntegerVariable(Formulation model, double[] solution)
        {
            for (int i = 0; i < model.VarCount; i++)
            {

                Sign_Restriction restriction = model.Sign_Restrictions[i];
                if (restriction != Sign_Restriction.Int && restriction != Sign_Restriction.Bin)
                {
                    continue;
                }

                double roundedValue = Math.Round(solution[i]);
                if (Math.Abs(solution[i] - roundedValue) > EPSILON)
                {
                    return i;
                }

            }

            return -1;
        }




        private Formulation WithExtraConstraint(Formulation model, int variableIndex, Equality_Sign sign, double value)
{

    Formulation cloneModel = new Formulation {

        Objective = model.Objective,
        Obj_Func_coefficients = new List<double>(model.Obj_Func_coefficients),
        Sign_Restrictions = new List<Sign_Restriction>(model.Sign_Restrictions),
        Constraint = new List<Conditions>(model.Constraint)

    };


    double[] coefficients = new double[model.VarCount];
    coefficients[variableIndex] = 1;
    cloneModel.Constraint.Add(new Conditions ( new List<double>(coefficients), sign, value ));
    return cloneModel;

}
    }
}
