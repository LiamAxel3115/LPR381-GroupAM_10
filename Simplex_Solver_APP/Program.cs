using Simplex_Solver_APP.Analysis;
using Simplex_Solver_APP.File_handler;
//import modules
using Simplex_Solver_APP.Model;
using Simplex_Solver_APP.Solvers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Simplex_Solver_APP
{
    public static class Program
    {
        //private static readonly List<Solvers> algorithms = new()
        //{
        //    new PrimalSimplexSolver(),
        //    new RevisedPrimalSimplexSolver(),
        //    new BranchAndBoundSimplexSolver(),
        //    new CuttingPlaneSolver(),
        //    new BranchAndBoundKnapsackSolver(),
        //};
        private static Formulation model;
        private static File_writer writer;
        private static Optimal lastOptimal;

        [STAThread]
        static void Main(string[] args)
        {
            startup();
            Console.WriteLine("\n===================================================================");
            Console.WriteLine(" \t \t \t WELCOME TO SIMPLEX SOLVER \t \t \t ");
            Console.WriteLine("===================================================================\n");

            //load the model from file
            string inputFile = InputFile();
            string outputFile = OutputFile();

            writer = new File_writer(outputFile, false, false);

            while (!LoadModel(inputFile))
            {
                Console.WriteLine("Cannot continue. Model invalid.\n");
                inputFile = InputFile();
            }
            writer.WriteModelSummary(model);
            Console.WriteLine("Model is begin written to OUTPUT file. ");

            Menu();
            writer.Dispose();
        }

        // functional  code blocks
        enum algorithms
        {
            Primal = 1,
            revised,
            Branch_And_Bound,
            Cutting_plane,
            Napsack,
            sensitivity,
            Duality,
            exit
        }
        private static void Menu()
        {
            bool terminate = false;
            while (!terminate)
            {
                Console.WriteLine("\t \t -------------------- \t MENU \t --------------------- \t \t\n");

                Console.WriteLine(" \t 1)  Solve using Primal Simplex ");
                Console.WriteLine(" \t 2)  Solve using Revised Primal Simplex ");
                Console.WriteLine(" \t 3)  Solve using Branch & Bound Simplex ");
                Console.WriteLine(" \t 4)  Solve using Cutting Plane ");
                Console.WriteLine(" \t 5)  Solve using Branch & Bound Knapsack ");
                Console.WriteLine(" \t 6)  Sensitivity Analysis ");
                Console.WriteLine(" \t 7)  Duality ");
                Console.WriteLine(" \t 8)  exit ");
                Console.Write(" Select (1-8) from the MENU: ");

                int input_param;

                if (!int.TryParse(Console.ReadLine().Trim(), out input_param))
                {
                    Console.WriteLine("Please enter a number between 1 and 8.\n");
                    continue;
                }

                algorithms option = (algorithms)input_param;

                switch (option)
                {
                    case algorithms.Primal:
                        // RunSolver(() => new PrimalSimplexSolver().Solve(model, writer));
                        PrimalSimplexSolver solver = new PrimalSimplexSolver();

                        RunSolver(() => solver.Solve(model, writer));

                        lastOptimal = solver.GetLastResult();
                        break;
                    case algorithms.revised:
                        // RunSolver(() => new PrimalSimplexSolver().Solve(model, writer));
                        RevisedPrimalSimplexSolver Rsolver = new RevisedPrimalSimplexSolver();

                        RunSolver(() => Rsolver.Solve(model, writer));

                        break;
                    case algorithms.Branch_And_Bound:
                        // RunSolver(() => new PrimalSimplexSolver().Solve(model, writer));
                        BranchAndBoundSolver BBsolver = new BranchAndBoundSolver();

                        RunSolver(() => BBsolver.Solve(model, writer));

                        break;
                    case algorithms.Cutting_plane:
                        // RunSolver(() => new PrimalSimplexSolver().Solve(model, writer));
                        CuttingPlaneSolver CPsolver = new CuttingPlaneSolver();

                        RunSolver(() => CPsolver.Solve(model, writer));

                        break;
                    case algorithms.Napsack:
                        RunSolver(() => new BranchAndBoundKnapsackSolver().Solve(model, writer));
                        break;
                    case algorithms.sensitivity:
                        showSensitivity();
                        break;
                    case algorithms.Duality:
                        ShowDuality();
                        break;
                    case algorithms.exit:
                        terminate = true;
                        Shutdown();
                        break;
                    default:
                        break;
                }



            }
        }
        // Runs a solver, reporting progress/errors to the console while the
        // solver itself writes its full working (canonical form, tableau
        // iterations, solution, etc.) to the OUTPUT file via 'writer'.
        private static void RunSolver(Action solve)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Solving... results are being written to the OUTPUT file.\n");
                Console.ResetColor();

                solve();


                writer.WriteLine();
                writer.WriteLine("========================================================\n");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nDone. See the OUTPUT file for the full working.\n");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Solver error: " + ex.Message);
                Console.ResetColor();
                writer.WriteLine("Solver error: " + ex.Message);
            }
        }

        private static string InputFile()
        {
            Console.WriteLine("Press any key to Select the INPUT text file");
            Console.ReadKey();
            Thread.Sleep(1000);
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select INPUT file";
                dialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(dialog.FileName);
                    Console.ResetColor();
                    return dialog.FileName;
                }
            }
            return "";
        }

        private static string OutputFile()
        {
            Console.WriteLine("\nPress any key to Select the OUTPUT text file");
            Console.ReadKey();
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select OUTPUT file";
                dialog.Filter = "Text files (*.txt)|*.txt";
                dialog.CheckFileExists = true;
                dialog.Multiselect = false;

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine(dialog.FileName);
                    Console.ResetColor();
                    return dialog.FileName;
                }
            }
            return "";
        }
        private static bool LoadModel(string inputfile)
        {
            try
            {
                model = File_Reader.readfile(inputfile);

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n------------------- \t   MODEL \t ---------------------\n");
                Console.ResetColor();
                Console.WriteLine(model.Summariser());
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("--------------------------------------------------------------------\n");
                Console.ResetColor();
                return true;
            }
            catch (Exception exception)
            {
                // Handle only the expected exceptions
                if (exception is FormatException || exception is FileNotFoundException || exception is IOException)
                {
                    Console.WriteLine("Cant read input file: " + exception.Message);

                    return false;
                }
                throw;
            }
        }
        private static void Shutdown()
        {
            // spinner animation
            string[] spinner = { "/", "-", "\\", "|" };
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("\nFinalizing shutdown...");
            Console.ResetColor();
            for (int i = 0; i < 40; i++)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write($"\rProcessing ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{spinner[i % spinner.Length]}");
                Thread.Sleep(50);
            }
            Thread.Sleep(1000);

            // clear spinner and replace message
            Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Shutting down simplex solver...");
            Thread.Sleep(1500);

            // progress bar animation
            int total = 30;

            for (int i = 0; i <= total; i++)
            {
                int percent = (int)((i / (double)total) * 100);
                string bar = new string('■', i).PadRight(total, ' ');
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\r[{bar}] ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{percent}%");

                Thread.Sleep(100);
            }
            Console.ResetColor();

            // final message
            Console.ForegroundColor = ConsoleColor.Yellow;
            Thread.Sleep(2000);
            Console.WriteLine("\nOptimization engine successfully powered off.");
            Console.ForegroundColor = ConsoleColor.Blue;
            Thread.Sleep(1500);
            Console.WriteLine("Return when you're ready to solve again.");
            Console.ResetColor();
        }
        private static void startup()
        {
            string ts() => DateTime.Now.ToString("HH:mm:ss");

            // BOOT sequence
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[{ts()}] [BOOT] Simplex solver starting...");
            Thread.Sleep(400);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{ts()}] [ OK ] Loaded constraint manager");
            Thread.Sleep(300);

            Console.WriteLine($"[{ts()}] [ OK ] Loaded pivot engine");
            Thread.Sleep(300);

            Console.WriteLine($"[{ts()}] [ OK ] Loaded objective function handler");
            Thread.Sleep(300);

            Console.WriteLine($"[{ts()}] [ OK ] System checks passed");
            Thread.Sleep(1050);

            // Spinner
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.Write($"\n[{ts()}] [INIT] Finalizing startup ");

            string[] spinner = { "/", "-", "\\", "|" };
            for (int i = 0; i < 40; i++)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write($"\r[{ts()}] [INIT] Finalizing startup ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{spinner[i % spinner.Length]}");
                Thread.Sleep(100);
            }

            // Clear spinner
            Console.Write("\r" + new string(' ', Console.WindowWidth) + "\r");

            Thread.Sleep(450);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[{ts()}] [READY] Simplex solver online.\n");

            Console.ResetColor();

        }
        public static void ShowDuality()
        {
            if (lastOptimal == null || !lastOptimal.IsOptimal)
            {
                Console.WriteLine("Run Primal Simplex first.");
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Building and solving the Dual Model...");
            Console.ResetColor();

            PrimalSimplexSolver solver = new PrimalSimplexSolver();

            // Rebuild the solved LP relaxation
            solver.Solve(model, writer);

            Duality dual = new Duality(solver.SolvedModel);

            writer.WriteLine();
            writer.WriteLine(dual.DisplayDual());

            Formulation dualModel = dual.BuildDual();

            writer.WriteLine();
            writer.WriteLine("Solving the Dual Model...");
            writer.WriteLine();

            PrimalSimplexSolver dualSolver = new PrimalSimplexSolver();
            dualSolver.Solve(dualModel, writer);

            writer.WriteLine();
            writer.WriteLine(
                dual.VerifyDuality(
                    lastOptimal.ObjectiveValue,
                    dualSolver.GetLastResult().ObjectiveValue));

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Dual model solved. See OUTPUT.txt.");
            Console.ResetColor();
        }
        public static void showSensitivity()
        {
            if (lastOptimal == null || !lastOptimal.IsOptimal)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Run Primal Simplex first.");
                Console.ResetColor();
                return;
            }

            SensitivityAnalysis analyser = new SensitivityAnalysis(lastOptimal);

            bool back = false;

            while (!back)
            {
                Console.WriteLine();
                Console.WriteLine("================================================");
                Console.WriteLine("          SENSITIVITY ANALYSIS MENU");
                Console.WriteLine("================================================");
                Console.WriteLine("1. Display range of Non-Basic Variable");
                Console.WriteLine("2. Apply change to Non-Basic Variable");
                Console.WriteLine("3. Display range of Basic Variable");
                Console.WriteLine("4. Apply change to Basic Variable");
                Console.WriteLine("5. Display RHS range");
                Console.WriteLine("6. Apply RHS change");
                Console.WriteLine("7. Display Non-Basic Column range");
                Console.WriteLine("8. Apply Non-Basic Column change");
                Console.WriteLine("9. Add New Activity");
                Console.WriteLine("10. Add New Constraint");
                Console.WriteLine("11. Display Shadow Prices");
                Console.WriteLine("12. Display Reduced Costs");
                Console.WriteLine("13. Return to Main Menu");
                Console.Write("Select: ");

                int choice;

                if (!int.TryParse(Console.ReadLine(), out choice))
                    continue;

                writer.WriteLine();
                writer.WriteLine("================================================");
                writer.WriteLine($"Sensitivity Option {choice}");
                writer.WriteLine("================================================");

                switch (choice)
                {
                    case 1:
                        {
                            int v = AskVariable();
                            writer.WriteLine(analyser.DisplayNonBasicRange(v));
                            break;
                        }

                    case 2:
                        {
                            int v = AskVariable();

                            Console.Write("New objective coefficient: ");
                            double c = double.Parse(Console.ReadLine());

                            writer.WriteLine(analyser.ApplyNonBasicVariableChange(v, c));
                            break;
                        }

                    case 3:
                        {
                            int v = AskVariable();
                            writer.WriteLine(analyser.DisplayBasicRange(v));
                            break;
                        }

                    case 4:
                        {
                            int v = AskVariable();

                            Console.Write("New objective coefficient: ");
                            double c = double.Parse(Console.ReadLine());

                            writer.WriteLine(analyser.ApplyBasicVariableChange(v, c));
                            break;
                        }

                    case 5:
                        {
                            int c = AskConstraint();
                            writer.WriteLine(analyser.DisplayRHSRange(c));
                            break;
                        }

                    case 6:
                        {
                            int c = AskConstraint();

                            Console.Write("New RHS: ");
                            double rhs = double.Parse(Console.ReadLine());

                            writer.WriteLine(analyser.ApplyRHSChange(c, rhs));
                            break;
                        }

                    case 7:
                        {
                            int c = AskConstraint();
                            int v = AskVariable();

                            writer.WriteLine(analyser.DisplayColumnRange(c, v));
                            break;
                        }

                    case 8:
                        {
                            int c = AskConstraint();
                            int v = AskVariable();

                            Console.Write("New coefficient: ");
                            double value = double.Parse(Console.ReadLine());

                            writer.WriteLine(analyser.ApplyColumnChange(c, v, value));
                            break;
                        }

                    case 9:
                        {
                            Console.Write("Objective coefficient: ");
                            double obj = double.Parse(Console.ReadLine());

                            List<double> coeff = new List<double>();

                            for (int i = 0; i < model.Constraint.Count; i++)
                            {
                                Console.Write($"Coefficient in c{i + 1}: ");
                                coeff.Add(double.Parse(Console.ReadLine()));
                            }

                            writer.WriteLine(
                                analyser.AddActivity(
                                    obj,
                                    coeff,
                                    Sign_Restriction.Positive));

                            break;
                        }

                    case 10:
                        {
                            List<double> coeff = new List<double>();

                            for (int i = 0; i < model.VarCount; i++)
                            {
                                Console.Write($"Coefficient for x{i + 1}: ");
                                coeff.Add(double.Parse(Console.ReadLine()));
                            }

                            Console.Write("Relation (<=, >=, =): ");

                            string relation = Console.ReadLine();

                            Equality_Sign sign =
                                relation == "<=" ?
                                Equality_Sign.LessThanOrEqual :
                                relation == ">=" ?
                                Equality_Sign.GreaterThanOrEqual :
                                Equality_Sign.Equal;

                            Console.Write("RHS: ");

                            double rhs = double.Parse(Console.ReadLine());

                            writer.WriteLine(
                                analyser.AddConstraint(
                                    coeff,
                                    sign,
                                    rhs));

                            break;
                        }

                    case 11:
                        {
                            writer.WriteLine(analyser.DisplayShadowPrices());
                            break;
                        }

                    case 12:
                        {
                            writer.WriteLine(analyser.DisplayReducedCosts());
                            break;
                        }

                    case 13:
                        {
                            back = true;
                            break;
                        }
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Result written to OUTPUT.txt");
                Console.ResetColor();
            }
        }
        private static int AskVariable()
        {
            while (true)
            {
                Console.Write($"Variable (1-{model.VarCount}): ");

                if (int.TryParse(Console.ReadLine(), out int choice))
                {
                    if (choice >= 1 && choice <= model.VarCount)
                        return choice - 1;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Please enter a number between 1 and {model.VarCount}.");
                Console.ResetColor();
            }
        }

        private static int AskConstraint()
        {
            while (true)
            {
                Console.Write($"Constraint (1-{model.Constraint.Count}): ");

                if (int.TryParse(Console.ReadLine(), out int constraint) &&
                    constraint >= 1 &&
                    constraint <= model.Constraint.Count)
                {
                    // Return zero-based index for internal use
                    return constraint - 1;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Please enter a number between 1 and {model.Constraint.Count}.");
                Console.ResetColor();
            }
        }
    }
}

