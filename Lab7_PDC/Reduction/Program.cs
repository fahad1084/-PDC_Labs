// Lab 07 - Task 1: Parallel Sum and Max: Aggregation vs Locks

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

class Program
{
    const int N = 10_000_000;                  // array length
    const int Repeats = 3;                     // each version is timed 3 times and averaged

    static int[] data = Array.Empty<int>();
    static int numThreads = 4;

    // shared results used by racy, lock-each and lock-partial
    static long sharedSum;
    static int sharedMax;
    static readonly object gate = new object();

    // ---------------- serial (reference) ----------------
    static (long sum, int max) SerialVersion()
    {
        long sum = 0;
        int max = int.MinValue;                // identity value for max
        for (int i = 0; i < data.Length; i++)
        {
            sum += data[i];
            if (data[i] > max) max = data[i];
        }
        return (sum, max);
    }

    // Runs body(start, end) on numThreads threads, each on its own block of the array.
    static void RunOnThreads(Action<int, int> body)
    {
        Thread[] threads = new Thread[numThreads];
        int chunk = (data.Length + numThreads - 1) / numThreads;

        for (int t = 0; t < numThreads; t++)
        {
            int start = t * chunk;
            int end = Math.Min(start + chunk, data.Length);
            threads[t] = new Thread(() => body(start, end));
            threads[t].Start();
        }
        foreach (Thread th in threads) th.Join();
    }

    // ---------------- racy: no synchronization ----------------
    static (long sum, int max) RacyVersion()
    {
        sharedSum = 0;
        sharedMax = int.MinValue;

        RunOnThreads((start, end) =>
        {
            for (int i = start; i < end; i++)
            {
                sharedSum += data[i];                          // unsynchronized read-modify-write
                if (data[i] > sharedMax) sharedMax = data[i];  // unsynchronized check-then-act
            }
        });
        return (sharedSum, sharedMax);
    }

    // ---------------- lock-each: critical section per element ----------------
    static (long sum, int max) LockEachVersion()
    {
        sharedSum = 0;
        sharedMax = int.MinValue;

        RunOnThreads((start, end) =>
        {
            for (int i = start; i < end; i++)
            {
                lock (gate)
                {
                    sharedSum += data[i];
                    if (data[i] > sharedMax) sharedMax = data[i];
                }
            }
        });
        return (sharedSum, sharedMax);
    }

    // ---------------- lock-partial: private partials, one lock per thread ----------------
    static (long sum, int max) LockPartialVersion()
    {
        sharedSum = 0;
        sharedMax = int.MinValue;

        RunOnThreads((start, end) =>
        {
            long localSum = 0;                 // identity for +
            int localMax = int.MinValue;       // identity for max
            for (int i = start; i < end; i++)
            {
                localSum += data[i];
                if (data[i] > localMax) localMax = data[i];
            }

            lock (gate)                        // combine once per thread
            {
                sharedSum += localSum;
                if (localMax > sharedMax) sharedMax = localMax;
            }
        });
        return (sharedSum, sharedMax);
    }

    // ---------------- plinq: library thread-local reduction ----------------
    static (long sum, int max) PlinqVersion()
    {
        return data.AsParallel()
            .WithDegreeOfParallelism(numThreads)
            .Aggregate(
                () => (sum: 0L, max: int.MinValue),                       // seed: each task starts with the identities
                (acc, x) => (acc.sum + x, Math.Max(acc.max, x)),          // fold one element into the task's own partial
                (a, b) => (a.sum + b.sum, Math.Max(a.max, b.max)),        // combine two partials at the end
                result => result);                                        // final result
    }

    static void Measure(string name, Func<(long sum, int max)> version, (long sum, int max) reference)
    {
        double totalMs = 0;
        (long sum, int max) result = (0, 0);
        bool sumOk = true, maxOk = true;

        for (int r = 0; r < Repeats; r++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            result = version();
            sw.Stop();
            totalMs += sw.Elapsed.TotalMilliseconds;

            if (result.sum != reference.sum) sumOk = false;
            if (result.max != reference.max) maxOk = false;
        }

        Console.WriteLine($"{name,-13} sum={result.sum,-12} max={result.max,-5} " +
                          $"sum_ok={(sumOk ? "YES" : "NO"),-3} max_ok={(maxOk ? "YES" : "NO"),-3} " +
                          $"avg_time={totalMs / Repeats:F2} ms");
    }

    static void Main(string[] args)
    {
        if (args.Length > 0) numThreads = int.Parse(args[0]);

        // fixed seed so every run uses the same data
        Random rng = new Random(12345);
        data = new int[N];
        for (int i = 0; i < N; i++) data[i] = rng.Next(0, 1_000_000);

        (long sum, int max) reference = SerialVersion();

        Console.WriteLine($"Threads: {numThreads}  Elements: {N}  Repeats per version: {Repeats}  ProcessorCount: {Environment.ProcessorCount}");
        Console.WriteLine($"Reference: sum={reference.sum} max={reference.max}");
        Measure("serial", SerialVersion, reference);
        Measure("racy", RacyVersion, reference);
        Measure("lock-each", LockEachVersion, reference);
        Measure("lock-partial", LockPartialVersion, reference);
        Measure("plinq", PlinqVersion, reference);
    }
}