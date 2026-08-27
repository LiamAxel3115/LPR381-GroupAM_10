using Simplex_Solver_APP.Model;
using System;
using System.Collections.Generic;
// import modules
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Simplex_Solver_APP.File_handler
{
    public class File_writer : IDisposable
    {
        private readonly StreamWriter fileWriter;
        private readonly bool ConsoleWriter;

        public File_writer(string outputFilePath) : this(outputFilePath, true, false) { }
        public File_writer(string outputFilePath, bool consoleWrite, bool append)
        {
            if (string.IsNullOrWhiteSpace(outputFilePath))
            {
                throw new ArgumentException("Output file path cannot be empty.");
            }
            if (!File.Exists(outputFilePath))
            {
                throw new FileNotFoundException("Output file does not exist.", outputFilePath);
            }
            fileWriter = new StreamWriter(outputFilePath, append);
            fileWriter.AutoFlush = true;
            this.ConsoleWriter = consoleWrite;
        }


        public static double Round3(double value)
        {
            return Math.Round(value, 3, MidpointRounding.AwayFromZero);
        }
        public void WriteLine(string text)
        {
            fileWriter.WriteLine(text);
            if (ConsoleWriter) { Console.WriteLine(text); }
        }
        public void WriteLine()
        {
            fileWriter.WriteLine();
            if (ConsoleWriter) { Console.WriteLine(); }
        }
        public void WriteHeader(string title)
        {

        }

        public void WriteModelSummary(Formulation model)
        {
            WriteHeader("Parsed Model");
            string summary = model.Summariser();
            string[] lines = summary.Split('\n');

            foreach (string line in lines)
            {
                WriteLine(line.TrimEnd('\r'));
            }
        }
        public void WriteRow(IEnumerable<string> columnNames, IEnumerable<double> values, int columnWidth)
        {
            List<double> vals = new List<double>(values);
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < vals.Count; i++)
            {
                string formatted = Round3(vals[i]).ToString("0.###");
                sb.Append(formatted.PadLeft(columnWidth));
            }

            WriteLine(sb.ToString());
        }
        public void Dispose()
        {
            fileWriter.Flush();
            fileWriter.Dispose();
        }
    }
}
