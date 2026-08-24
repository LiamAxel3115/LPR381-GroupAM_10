namespace LPR381_Beta
{
    class Program
    {    
        static void Main(string[] args)
        {
            //double[,] matrix = new double[4, 5]; // i= rows, j=columns 
            //Random rand = new Random();
            //for (int i = 0; i<4; i++) // i= rows, j=columns 
            //{
            //    for(int j = 0; j<5; j++)
            //    {
            //        matrix[i, j] = rand.Next(-100,100);                    
            //    }
            //    Console.WriteLine();
            //}

            //Methods.PrintMatrix(matrix);
            //Console.ReadKey();
            //int pRow = Methods.findPColumnPrimal(matrix);
            //Console.WriteLine("Pivot Column: " + pRow);
            //Console.ReadKey();
            //int pColomn = Methods.findPRowPrimal(matrix, pRow);
            //Console.WriteLine("Pivot Row: " + pColomn);
            //Console.WriteLine();
            //Methods.PrintMatrix(Methods.PivotMatrix(matrix, pColomn, pRow));

            //allow input for solved matrix
            Console.WriteLine("Enter the number of rows: ");
            int rows = Convert.ToInt32(Console.ReadLine());
            Console.WriteLine("Enter the number of columns: ");
            int columns = Convert.ToInt32(Console.ReadLine());
            double[,] matrix = new double[rows, columns];
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < columns; j++)
                {
                    Console.WriteLine($"Enter Row {i} column {j}");
                    matrix[i, j] = Convert.ToDouble(Console.ReadLine());
                    Console.Clear();
                }
            }
            Methods.PrintMatrix(matrix);
            Methods.primalSimplex(matrix);
            Methods.cuttingPlane(matrix);

        }

        
        
    }
    class Methods
    {
        static public double[,] cuttingPlane(double[,] matrix)
        {
            matrix = Methods.primalSimplex(matrix);
            int pRow = findPRowPrimal(matrix, findPColumnPrimal(matrix));
            if (pRow == -1)
            {
                Console.WriteLine("Optimal Solution Found");
                return null;
            }
            double[,] newMatrix = new double[matrix.GetLength(0) + 1, matrix.GetLength(1)];// adds a new row to the matrix
            for (int i = 0; i < matrix.GetLength(0); i++)// copies the old matrix into the new matrix
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    newMatrix[i, j] = matrix[i, j];
                }
            }
            for (int i = 0; i < matrix.GetLength(1); i++)// adds the new row to the matrix
            {
                newMatrix[matrix.GetLength(0), i] =- Math.Floor(matrix[pRow, i]);
            }
            PrintMatrix(newMatrix);
            dualSimplex(newMatrix);
            return newMatrix;
        }
        static public double[,] dualSimplex(double[,] matrix)
        {
            int pRow = findPRowDual(matrix);
            if (pRow == -1)
            {
                Console.WriteLine("Optimal Solution Found");
                return primalSimplex(matrix);
            }
            int pColoum = findPColumnDual(matrix,pRow);
            if (pColoum == -1)
            {
                Console.WriteLine("Unbounded Solution");
                return null;
            }
            double[,] newMatrix = PivotMatrix(matrix, pRow, pColoum);
            PrintMatrix(newMatrix);
            dualSimplex(newMatrix);
            return newMatrix;
        }
        static public double[,] primalSimplex(double[,] matrix)
        {
            int pColoum = findPColumnPrimal(matrix);
            if (pColoum == -1)
            {
                Console.WriteLine("Optimal Solution Found");
                return matrix;
            }
            int pRow = findPRowPrimal(matrix, pColoum);
            if (pRow == -1)
            {
                Console.WriteLine("Unbounded Solution");
                return null;
            }
            double[,] newMatrix = PivotMatrix(matrix, pRow, pColoum);
            PrintMatrix(newMatrix);
            primalSimplex(newMatrix);
            return newMatrix;
        }
        static public void PrintMatrix(double[,] matrix)
        {
            for (int i = 0; i < matrix.GetLength(0); i++) // i= rows, j=columns 
            {
                if (i == 0)
                {
                    Console.Write("Z\t");
                }
                else
                {
                    Console.Write("C" + i + "\t");
                }
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    Console.Write(matrix[i, j] + " ");
                }
                Console.WriteLine();
            }
            Console.WriteLine();
        }
        static private double[,] PivotMatrix(double[,] matrix, int pRow, int pColoumn)
        {
            double[,] newMatrix = new double[matrix.GetLength(0), matrix.GetLength(1)];
            for (int i = 0; i < matrix.GetLength(1); i++)
            {
                newMatrix[pRow,i] = matrix[pRow, i] / matrix[pRow,pColoumn];
            }
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    if (i!=pRow)
                    {
                        newMatrix[i, j] = matrix[i, j] - (matrix[i, pColoumn] * newMatrix[pRow, j]);
                    }
                }
                
            }
            return newMatrix;
        }
        static private int findPColumnPrimal(double[,] matrix)
        {
            int pColoum = 0;
            double min =999999999;
            for (int i = 0; i < matrix.GetLength(1)-1; i++)
            {
                if (matrix[0,i]<min)
                {
                    min = matrix[0, i];
                    pColoum = i;
                }
            }
            if (min >=0)
            {
                return -1;
            }
            return pColoum;
        }
        static private int findPRowPrimal(double[,] matrix, int pColoum)
        {
            int pRow = 0;
            double min = 999999999;
            for (int i = 1; i < matrix.GetLength(0); i++)
            {
                if (matrix[i, pColoum] > 0)
                {
                    double ratio = matrix[i, matrix.GetLength(1) - 1] / matrix[i, pColoum];
                    if (ratio < min)
                    {
                        min = ratio;
                        pRow = i;
                    }
                }
            }
            if (min == 999999999)
            {
                return -1;
            }
            return pRow;
        }
        static private int findPColumnDual(double[,] matrix, int pRow)
        {
            int pColomn = 0;
            double min = 999999999;
            for (int i = 0; i < matrix.GetLength(1)-1; i++)
            {
                if (matrix[pRow,i] < 0)
                {
                    double ratio = Math.Abs(matrix[0,i] / matrix[pRow,i]);
                    if (ratio < min)
                    {
                        min = ratio;
                        pColomn = i;
                    }
                }
            }
            if (min == 999999999)
            {
                return -1;
            }
            return pColomn;
        }
        static private int findPRowDual(double[,] matrix)
        {
            int pColoum = 0;
            double min = 999999999;
            for (int i = 1; i < matrix.GetLength(0); i++)
            {
                if (matrix[i, matrix.GetLength(1) - 1] < 0)
                {
                    if (matrix[i, matrix.GetLength(1) - 1] < min)
                    {
                        min = matrix[i, matrix.GetLength(1) - 1];
                        pColoum = i;
                    }
                }
            }
            if (min == 999999999)
            {
                return -1;
            }
            return pColoum;
        }
    }
} 
