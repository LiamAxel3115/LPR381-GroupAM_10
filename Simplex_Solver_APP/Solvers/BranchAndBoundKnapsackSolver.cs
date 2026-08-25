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
    /// Solves a 0/1 Knapsack Integer Programming model (a single "&lt;="
    /// constraint with every decision variable restricted to "bin") using
    /// the Branch and Bound Knapsack algorithm.
    ///
    /// Bounding: items are ranked by value/weight ratio and each
    /// sub-problem is bounded using the classic fractional-knapsack
    /// (LP relaxation) bound.
    ///
    /// Branching: at every node the "Include this item" branch is created
    /// and explored before the "Exclude this item" branch. Every
    /// sub-problem that is created is printed (its bound, feasibility and
    /// fathoming status), all nodes are fathomed (by bound, by
    /// infeasibility, or by reaching a complete/leaf solution), and the
    /// recursive search backtracks to the previous decision once a branch
    /// is exhausted, exactly like a manual Branch & Bound tree.
    /// </summary>
    public class BranchAndBoundKnapsackSolver
    {
        private const double Epsilon = 1e-9;

        private class Item
        {
            public int OriginalIndex;
            public double Value;
            public double Weight;
            public double Ratio;
        }

        public void Solve(Formulation model, File_writer writer)
        {
            writer.WriteLine("========================================================");
            writer.WriteLine("        BRANCH AND BOUND KNAPSACK ALGORITHM               ");
            writer.WriteLine("========================================================");
            writer.WriteLine();

            if (model.ConstraintCount == 0)
            {
                writer.WriteLine("Model has no constraints - cannot apply the knapsack algorithm.");
                return;
            }

            if (model.ConstraintCount > 1)
            {
                writer.WriteLine("Note: this algorithm is defined for single-constraint (knapsack) models.");
                writer.WriteLine("      Constraint c1 is used as the capacity constraint; any other constraints are ignored.");
            }

            bool allBinary = model.Sign_Restrictions.All(r => r == Sign_Restriction.Bin);
            if (!allBinary)
                writer.WriteLine("Note: not every variable is declared 'bin'; all decision variables are treated as 0/1 for this algorithm.");

            var capacityConstraint = model.Constraint[0];
            double capacity = capacityConstraint.RHS_Value;

            bool maximize = model.Objective == Formulation_type.Max;
            double dirSign = maximize ? 1.0 : -1.0; // internally maximize dirSign * f(x)

            int n = model.VarCount;
            var items = new List<Item>();
            for (int j = 0; j < n; j++)
            {
                double value = dirSign * model.Obj_Func_coefficients[j];
                double weight = capacityConstraint.Constraint_Coefficients[j];
                items.Add(new Item
                {
                    OriginalIndex = j,
                    Value = value,
                    Weight = weight,
                    Ratio = weight > Epsilon ? value / weight : double.MaxValue
                });
            }

            // Rank items by value/weight ratio, descending - this both drives the
            // branching order and the fractional-relaxation bound calculation.
            var sorted = items.OrderByDescending(it => it.Ratio).ToList();

            writer.WriteLine();
            writer.WriteLine("Items ranked by value/weight ratio (descending):");
            writer.WriteLine(Row("Item", "Value", "Weight", "Ratio"));
            foreach (var it in sorted)
                writer.WriteLine(Row("x" + (it.OriginalIndex + 1), Fmt(it.Value), Fmt(it.Weight), Fmt(it.Ratio)));

            writer.WriteLine();
            writer.WriteLine("Capacity = " + Fmt(capacity));
            writer.WriteLine();
            writer.WriteLine("Branch and Bound search tree");
            writer.WriteLine("(DFS, Include branch explored before Exclude, backtracking on exhausted branches):");
            writer.WriteLine(Row("Node", "Level", "Item", "Decision") + "  " + Row("Weight", "Value", "Bound", "Status"));

            double bestValue = double.NegativeInfinity;
            int[] bestDecision = null;
            int nodeCounter = 0;
            int[] decisions = new int[n];
            for (int i = 0; i < n; i++) decisions[i] = -1; // -1 = undecided, 0 = excluded, 1 = included

            void PrintNode(int level, string item, string decision, double weight, double value, double bound, string status)
            {
                nodeCounter++;
                writer.WriteLine(Row(nodeCounter.ToString(), level.ToString(), item, decision) +
                                  "  " + Row(Fmt(weight), Fmt(value), Fmt(bound), status));
            }

            // Fractional-knapsack upper bound for the sub-problem starting at "level"
            // with the given cumulative weight/value already committed.
            double Bound(int level, double curWeight, double curValue)
            {
                double bound = curValue;
                double weight = curWeight;
                int j = level;
                while (j < sorted.Count && weight + sorted[j].Weight <= capacity + Epsilon)
                {
                    weight += sorted[j].Weight;
                    bound += sorted[j].Value;
                    j++;
                }
                if (j < sorted.Count && sorted[j].Weight > Epsilon)
                    bound += (capacity - weight) * sorted[j].Ratio; // fractional slice of the next item
                return bound;
            }

            void Search(int level, double curWeight, double curValue)
            {
                if (level == n)
                {
                    bool improved = curValue > bestValue + Epsilon;
                    PrintNode(level, "-", "Leaf", curWeight, curValue, curValue, improved ? "New best" : "Complete");
                    if (improved)
                    {
                        bestValue = curValue;
                        bestDecision = (int[])decisions.Clone();
                    }
                    return;
                }

                double boundHere = Bound(level, curWeight, curValue);
                if (boundHere <= bestValue + Epsilon)
                {
                    PrintNode(level, "x" + (sorted[level].OriginalIndex + 1), "-", curWeight, curValue, boundHere, "Fathomed (bound)");
                    return; // fathomed: cannot beat the current best candidate
                }

                var item = sorted[level];

                // ---- Include branch (explored first) ----
                if (curWeight + item.Weight <= capacity + Epsilon)
                {
                    double includeBound = Bound(level + 1, curWeight + item.Weight, curValue + item.Value);
                    PrintNode(level, "x" + (item.OriginalIndex + 1), "Include",
                              curWeight + item.Weight, curValue + item.Value, includeBound, "Branch");

                    decisions[item.OriginalIndex] = 1;
                    Search(level + 1, curWeight + item.Weight, curValue + item.Value);
                    decisions[item.OriginalIndex] = -1; // backtrack
                }
                else
                {
                    PrintNode(level, "x" + (item.OriginalIndex + 1), "Include",
                              curWeight + item.Weight, curValue + item.Value, curValue, "Fathomed (infeasible)");
                }

                // ---- Exclude branch ----
                double excludeBound = Bound(level + 1, curWeight, curValue);
                PrintNode(level, "x" + (item.OriginalIndex + 1), "Exclude", curWeight, curValue, excludeBound, "Branch");

                decisions[item.OriginalIndex] = 0;
                Search(level + 1, curWeight, curValue);
                decisions[item.OriginalIndex] = -1; // backtrack
            }

            Search(0, 0, 0);

            writer.WriteLine();
            writer.WriteLine("Total sub-problems (nodes) generated: " + nodeCounter);
            writer.WriteLine();
            writer.WriteLine("--- Best Candidate ---");

            if (bestDecision == null)
            {
                writer.WriteLine("No feasible solution was found.");
                return;
            }

            double totalWeight = 0;
            for (int j = 0; j < n; j++)
            {
                double xj = bestDecision[j] == 1 ? 1 : 0;
                totalWeight += xj * capacityConstraint.Constraint_Coefficients[j];
                writer.WriteLine("x" + (j + 1) + " = " + xj);
            }

            double reportedObjective = maximize ? bestValue : -bestValue;
            writer.WriteLine("Total weight used = " + Fmt(totalWeight) + "  (capacity = " + Fmt(capacity) + ")");
            writer.WriteLine("Optimal objective value (" + (maximize ? "max" : "min") + ") = " + Fmt(reportedObjective));
        }

        private static string Fmt(double v) => File_writer.Round3(v).ToString("0.###");

        private static string Row(string a, string b, string c, string d)
        {
            return a.PadRight(8) + b.PadRight(10) + c.PadRight(10) + d.PadRight(16);
        }
    }
}
