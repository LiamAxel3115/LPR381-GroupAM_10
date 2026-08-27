using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplex_Solver_APP.Model
{
    public enum Formulation_type
    {
        Max,
        Min
    }
    public enum Equality_Sign
    {
        LessThanOrEqual,
        GreaterThanOrEqual,
        Equal
    }
    public enum Sign_Restriction
    {
        Positive,
        Negative,
        urs,
        Int,
        Bin
    }
    public class Formatting
    {
        public static readonly Dictionary<Equality_Sign, string> Comparison =
          new Dictionary<Equality_Sign, string>()
          {
                { Equality_Sign.LessThanOrEqual, "<=" },
                { Equality_Sign.GreaterThanOrEqual, ">=" },
                { Equality_Sign.Equal, "=" }
          };

        public static readonly Dictionary<Sign_Restriction, string> Restrictions =
            new Dictionary<Sign_Restriction, string>()
            {
                { Sign_Restriction.Positive, "+" },
                { Sign_Restriction.Negative, "-" },
                { Sign_Restriction.urs, "urs" },
                { Sign_Restriction.Int, "int" },
                { Sign_Restriction.Bin, "bin" }
            };

        public static readonly Dictionary<Formulation_type, string> Formulation =
            new Dictionary<Formulation_type, string>()
            {
                { Formulation_type.Max, "max" },
                { Formulation_type.Min, "min" }
            };
    }
}
