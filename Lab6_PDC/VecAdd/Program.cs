// Lab 06 - Task 2: Vector addition with Parallel.ForEach
// Usage: dotnet run --no-build -c Release -- <degree>
using System.Collections.Concurrent;
using System.Diagnostics;

int degree = args.Length > 0 ? int.Parse(args[0]) : Environment.ProcessorCount;
var options = new ParallelOptions { MaxDegreeOfParallelism = degree };

VecMath.Init();
VecMath.VecAdd(options); // warm-up pass (JIT, page faults), not timed

var sw = Stopwatch.StartNew();
for (int pass = 0; pass < VecMath.Passes; pass++)
    VecMath.VecAdd(options);
sw.Stop();

bool correct = VecMath.Verify();
Console.WriteLine($"processors={Environment.ProcessorCount} degree={degree} N={VecMath.N} passes={VecMath.Passes}");
Console.WriteLine($"time_ms={sw.Elapsed.TotalMilliseconds:F2} correct={(correct ? "YES" : "NO")}");

static class VecMath
{
    public const int N = 5_000_000;
    public const int Passes = 20;

    // Shared arrays: every thread reads a and b and writes its own part of c.
    static readonly double[] a = new double[N];
    static readonly double[] b = new double[N];
    static readonly double[] c = new double[N];

    public static void Init()
    {
        for (int i = 0; i < N; i++)
        {
            a[i] = i * 0.5;
            b[i] = i * 2.0;
        }
    }

    public static void VecAdd(ParallelOptions options)
    {
        // Partitioner.Create(0, N) splits the index range into chunks, so each
        // task gets a Tuple (from, to) instead of one delegate call per element.
        // 'static' forbids capturing locals: the lambda can use only its
        // parameter (range), its own locals and static members such as a, b, c, N.
        Parallel.ForEach(Partitioner.Create(0, N), options, static range =>
        {
            for (int i = range.Item1; i < range.Item2; i++) // i is private to this chunk
                c[i] = a[i] + b[i];
        });
        // Parallel.ForEach returns only after every chunk has finished (implicit barrier).
    }

    public static bool Verify()
    {
        for (int i = 0; i < N; i++)
            if (c[i] != a[i] + b[i])
                return false;
        return true;
    }
}