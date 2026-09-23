using System.Diagnostics;

static void InitializeMatrices(double[,] A, double[,] B)
{
    int n = A.GetLength(0);
    for (int i = 0; i < n; i++)
    {
        for (int j = 0; j < n; j++)
        {
            A[i, j] = 1.0;
            B[i, j] = 1.0;
        }
    }
}

static void MultiplySequential(double[,] A, double[,] B, double[,] C)
{
    int n = A.GetLength(0);

    for (int i = 0; i < n; i++)
    {
        for (int j = 0; j < n; j++)
        {
            double sum = 0.0;
            for (int k = 0; k < n; k++)
            {
                sum += A[i, k] * B[k, j];
            }
            C[i, j] = sum;
        }
    }
}

int[] sizes = { 200, 400, 600 };

foreach (int n in sizes)
{
    double[,] A = new double[n, n];
    double[,] B = new double[n, n];
    double[,] C = new double[n, n];

    InitializeMatrices(A, B);

    Stopwatch stopwatch = Stopwatch.StartNew();

    MultiplySequential(A, B, C);

    stopwatch.Stop();

    double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

    Console.WriteLine($"Matrix size: {n} x {n}");
    Console.WriteLine($"Execution time: {elapsedMs} ms");
}
