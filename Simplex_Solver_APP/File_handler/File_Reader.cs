using Simplex_Solver_APP.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
//import modules
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplex_Solver_APP.File_handler
{
    public class File_Reader
    {

        public static Formulation readfile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new FormatException(
                    "No input file was selected.");

            if (!File.Exists(path))
                throw new FileNotFoundException($"INPUT File not found in path :  {path}");

            List<string> lines =
                 File.ReadAllLines(path)
                     .Select(line => line.Trim())
                     .Where(line => line.Length > 0)
                     .ToList();

            // model must atleast have a objective function , a constraint and a sign restriction
            if (lines.Count < 3)
            {
                throw new FormatException($"Input file must have at least 3 non-blank lines.\n{path}");
            }

            // read model
            var model = new Formulation();

            //objective function
            ReadObjLine(lines[0], model);

            // constraints
            for (int i = 1; i < lines.Count - 1; i++)
            {
                Conditions constraint = readConditions(lines[i], model.VarCount, i + 1);
                constraint.Index = model.Constraint.Count + 1;
                model.Constraint.Add(constraint);
            }
            // sign restrictions
            ReadSignRestriction(lines[lines.Count - 1], model);
            model.Validate();
            return model;
        }
        private static void ReadObjLine(string line, Formulation model)
        {
            List<string> variables = separate(line);
            if (variables.Count < 2)
            {
                throw new FormatException("objective function of a model must contain a min/max and at least one signed coefficient.");
            }

            string model_type = variables[0].ToLowerInvariant();
            switch (model_type)
            {
                case "max":
                    model.Objective = Formulation_type.Max;
                    break;
                case "min":
                    model.Objective = Formulation_type.Min;
                    break;
                default:
                    throw new FormatException($"Objective line must start with 'max' or 'min', found '{variables[0]}'.");
            }

            for (int i = 1; i < variables.Count; i++)
            {
                model.Obj_Func_coefficients.Add(readSignedCoefficient(variables[i], $"objective coefficient {i}"));
            }
        }
        private static Conditions readConditions(string line, int varCount, int index)
        {
            List<string> variables =
                separate(line);

            if (variables.Count != varCount + 1)
            {
                throw new FormatException(
                    "Line " + index +
                    ": expected " +
                    varCount +
                    " coefficients plus a relation/RHS term." +
                    Environment.NewLine +
                    "Offending line: " +
                    line);
            }

            List<double> coefficients = new List<double>();
            for (int i = 0; i < varCount; i++)
                coefficients.Add(readSignedCoefficient(variables[i], $"line {index} coefficient {i + 1}"));
            string relationAndRHS =
                variables[variables.Count - 1];

            var (relation, rhs) = ReadConditionAndRHS(relationAndRHS, index);
            return new Conditions(coefficients, relation, rhs);
        }
        private static void ReadSignRestriction(string line, Formulation model)
        {
            var variables = separate(line);

            if (variables.Count != model.VarCount)
            {
                throw new FormatException(
                    "sign restriction missing, there should be one per variable coefficient.");
            }

            foreach (var item in variables)
            {
                Sign_Restriction restriction;
                switch (item.ToLowerInvariant())
                {
                    case "+":
                        restriction = Sign_Restriction.Positive;
                        break;
                    case "-":
                        restriction = Sign_Restriction.Negative;
                        break;
                    case "urs":
                        restriction = Sign_Restriction.urs;
                        break;
                    case "int":
                        restriction = Sign_Restriction.Int;
                        break;
                    case "bin":
                        restriction = Sign_Restriction.Bin;
                        break;
                    default:
                        throw new FormatException($"unrecognised sign restriction sign '{item}'.");
                }

                model.Sign_Restrictions.Add(restriction);
            }
        }

        private static (Equality_Sign relationship, double RHS) ReadConditionAndRHS(string line, int value)
        {
            string inequality;
            string remainder;

            if (line.StartsWith("<="))
            {
                inequality = "<=";
                remainder = line.Substring(2);
            }
            else if (line.StartsWith(">="))
            {
                inequality = ">=";
                remainder = line.Substring(2);
            }
            else if (line.StartsWith("="))
            {
                inequality = "=";
                remainder = line.Substring(1);
            }
            else
            {
                throw new FormatException($"Line {value}: could not find a relation (<=, >=, =) in the final line '{line}'.");
            }

            Equality_Sign relation;
            switch (inequality)
            {
                case "<=":
                    relation = Equality_Sign.LessThanOrEqual;
                    break;
                case ">=":
                    relation = Equality_Sign.GreaterThanOrEqual;
                    break;
                case "=":
                    relation = Equality_Sign.Equal;
                    break;
                default:
                    throw new FormatException($"Unrecognised relation '{inequality}'.");
            }
            double RHS = readSignedCoefficient(remainder, $"line {line} right-hand-side");
            return (relation, RHS);
        }
        private static double readSignedCoefficient(string sign, string value)
        {
            if (!double.TryParse(sign, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var coefficient))
            {
                throw new FormatException($"Could not parse {sign}' as a number ({value}).");
            }
            return coefficient;
        }
        private static List<string> separate(string line)
        {
            return line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries).ToList();
        }
    }
}

