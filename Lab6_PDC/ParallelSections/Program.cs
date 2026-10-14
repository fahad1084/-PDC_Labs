// Lab 06 - Task 4: Independent operations with Parallel.Invoke
// (the counterpart of OpenMP sections)
// Usage: dotnet run --no-build -c Release -- <degree>
using System.Diagnostics;

int degree = args.Length > 0 ? int.Parse(args[0]) : Environment.ProcessorCount;
var options = new ParallelOptions { MaxDegreeOfParallelism = degree };

Console.WriteLine($"processors={Environment.ProcessorCount} degree={degree}");

// ---- Serial reference: run A, B and C one after another ----
var sw = Stopwatch.StartNew();
long serialA = Work.A();
double timeA = sw.Elapsed.TotalSeconds;

sw.Restart();
long serialB = Work.B();
double timeB = sw.Elapsed.TotalSeconds;

sw.Restart();
long serialC = Work.C();
double timeC = sw.Elapsed.TotalSeconds;

double serialTotal = timeA + timeB + timeC;
Console.WriteLine($"serial: A={timeA:F3} s  B={timeB:F3} s  C={timeC:F3} s  total={serialTotal:F3} s");

// ---- Parallel: each action stores its own result and its own thread ID ----
long resultA = 0, resultB = 0, resultC = 0;
int threadA = 0, threadB = 0, threadC = 0;

sw.Restart();
Parallel.Invoke(options,
    () =>
    {
        threadA = Environment.CurrentManagedThreadId;
        resultA = Work.A();
    },
    () =>
    {
        threadB = Environment.CurrentManagedThreadId;
        resultB = Work.B();
    },
    () =>
    {
        threadC = Environment.CurrentManagedThreadId;
        resultC = Work.C();
    });
// Implicit barrier: we reach this line only after A, B and C have all finished.
double wall = sw.Elapsed.TotalSeconds;

bool match = resultA == serialA && resultB == serialB && resultC == serialC;
Console.WriteLine($"parallel: wall={wall:F3} s  speedup={serialTotal / wall:F2}");
Console.WriteLine($"threads: A={threadA} B={threadB} C={threadC} (calling thread={Environment.CurrentManagedThreadId})");
Console.WriteLine($"results: A={resultA} B={resultB} C={resultC}");
Console.WriteLine($"results_match={(match ? "YES" : "NO")}");

static class Work
{
    // A: count primes below 5,000,000 by trial division (shortest)
    public static long A()
    {
        long count = 0;
        for (int n = 2; n < 5_000_000; n++)
            if (IsPrime(n)) count++;
        return count;
    }

    // B: sum of the digits of every number below 90,000,000 (longest)
    public static long B()
    {
        long sum = 0;
        for (int n = 0; n < 90_000_000; n++)
        {
            int x = n;
            while (x > 0) { sum += x % 10; x /= 10; }
        }
        return sum;
    }

    // C: total Collatz steps for every start value below 2,500,000 (medium)
    public static long C()
    {
        long steps = 0;
        for (long start = 1; start < 2_500_000; start++)
        {
            long x = start;
            while (x != 1)
            {
                x = (x & 1) == 0 ? x / 2 : 3 * x + 1;
                steps++;
            }
        }
        return steps;
    }

    static bool IsPrime(int n)
    {
        if (n < 4) return n >= 2;
        if (n % 2 == 0) return false;
        for (int d = 3; d * d <= n; d += 2)
            if (n % d == 0) return false;
        return true;
    }
}