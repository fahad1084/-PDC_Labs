using System.Diagnostics;
using System.Threading;

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

int[] sizes = { 200, 400, 600 };

foreach (int n in sizes)
{
    double[,] A = new double[n, n];
    double[,] B = new double[n, n];
    double[,] C = new double[n, n];

    InitializeMatrices(A, B);

    int midpoint = n / 2;

    MatrixWorker worker1 = new MatrixWorker(A, B, C, 0, midpoint);
    MatrixWorker worker2 = new MatrixWorker(A, B, C, midpoint, n);

    Thread t1 = new Thread(new ThreadStart(worker1.Run));
    Thread t2 = new Thread(new ThreadStart(worker2.Run));

    Stopwatch stopwatch = Stopwatch.StartNew();

    t1.Start();
    t2.Start();

    t1.Join();
    t2.Join();

    stopwatch.Stop();

    double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

    Console.WriteLine($"Matrix size: {n} x {n}");
    Console.WriteLine($"Execution time: {elapsedMs} ms");
}

class MatrixWorker
{
    private readonly double[,] A;
    private readonly double[,] B;
    private readonly double[,] C;
    private readonly int startRow;
    private readonly int endRow;

    public MatrixWorker(double[,] A, double[,] B, double[,] C, int startRow, int endRow)
    {
        this.A = A;
        this.B = B;
        this.C = C;
        this.startRow = startRow;
        this.endRow = endRow;
    }

    public void Run()
    {
        int n = A.GetLength(0);

        for (int i = startRow; i < endRow; i++)
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
}