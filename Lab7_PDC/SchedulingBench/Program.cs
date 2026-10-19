// Lab 07 - Task 2 and Task 4: Benchmarking Static, Dynamic and Guided Scheduling

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    const int N = 10_000;                      // loop iterations
    const int CostFactor = 12;                 // inner work units per unit of cost
    const int Runs = 3;                        // best of 3

    static int numThreads = 4;
    static bool uniform = false;
    static long[] results = new long[N];

    // ----- the loop body: cost depends on the workload -----
    static long Work(int i)
    {
        int cost = uniform ? CostFactor * N / 2      // same total work as the uneven case
                           : CostFactor * i;         // grows with i
        long acc = 0;
        for (int k = 0; k < cost; k++)
            acc += (k ^ i) & 7;
        return acc;
    }

    // ----- the policies; each worker returns after processing its share -----

    // static: one contiguous block per thread
    static void StaticBlock(int id)
    {
        int chunk = (N + numThreads - 1) / numThreads;
        int start = id * chunk, end = Math.Min(start + chunk, N);
        for (int i = start; i < end; i++) results[i] = Work(i);
    }

    // static,c: chunks of size c dealt out round-robin (thread id, id+T, id+2T, ...)
    static void StaticChunked(int id, int c)
    {
        for (int start = id * c; start < N; start += numThreads * c)
        {
            int end = Math.Min(start + c, N);
            for (int i = start; i < end; i++) results[i] = Work(i);
        }
    }

    // dynamic,c: threads grab the next chunk of size c from a shared counter
    static int nextIteration;
    static void Dynamic(int id, int c)
    {
        while (true)
        {
            int end = Interlocked.Add(ref nextIteration, c);   // claim [end-c, end)
            int start = end - c;
            if (start >= N) break;
            end = Math.Min(end, N);
            for (int i = start; i < end; i++) results[i] = Work(i);
        }
    }

    // guided,c: chunk = remaining / threads (never smaller than c), so chunks shrink over time
    static void Guided(int id, int c)
    {
        while (true)
        {
            int start = Volatile.Read(ref nextIteration);
            if (start >= N) break;

            int remaining = N - start;
            int size = Math.Max(c, (remaining + numThreads - 1) / numThreads);
            int end = Math.Min(start + size, N);

            // claim [start, end) only if nobody else moved the counter in the meantime
            if (Interlocked.CompareExchange(ref nextIteration, end, start) != start)
                continue;                                       // lost the race: try again

            for (int i = start; i < end; i++) results[i] = Work(i);
        }
    }

    // Runs worker(id) on numThreads threads and records each thread's busy time.
    static (double wall, double[] busy) RunThreads(Action<int> worker)
    {
        nextIteration = 0;
        double[] busy = new double[numThreads];
        Thread[] threads = new Thread[numThreads];

        Stopwatch wall = Stopwatch.StartNew();
        for (int t = 0; t < numThreads; t++)
        {
            int id = t;                        // private copy for the lambda
            threads[t] = new Thread(() =>
            {
                Stopwatch sw = Stopwatch.StartNew();
                worker(id);
                busy[id] = sw.Elapsed.TotalMilliseconds;
            });
            threads[t].Start();
        }
        foreach (Thread th in threads) th.Join();
        wall.Stop();
        return (wall.Elapsed.TotalMilliseconds, busy);
    }

    // Parallel.For with the same degree of parallelism; busy time summed per OS thread.
    static (double wall, double[] busy) RunParallelFor()
    {
        var busyByThread = new ConcurrentDictionary<int, double>();
        var options = new ParallelOptions { MaxDegreeOfParallelism = numThreads };

        Stopwatch wall = Stopwatch.StartNew();
        Parallel.For(0, N, options,
            () => Stopwatch.StartNew(),                          // each task starts its own timer
            (i, state, sw) => { results[i] = Work(i); return sw; },
            sw => busyByThread.AddOrUpdate(Environment.CurrentManagedThreadId,
                                           sw.Elapsed.TotalMilliseconds,
                                           (_, old) => old + sw.Elapsed.TotalMilliseconds));
        wall.Stop();
        return (wall.Elapsed.TotalMilliseconds, busyByThread.Values.ToArray());
    }

    static void Report(string name, Func<(double wall, double[] busy)> policy, long reference)
    {
        double bestWall = double.MaxValue;
        double[] bestBusy = Array.Empty<double>();
        bool correct = true;

        for (int r = 0; r < Runs; r++)
        {
            Array.Clear(results);
            var (wall, busy) = policy();
            if (results.Sum() != reference) correct = false;
            if (wall < bestWall) { bestWall = wall; bestBusy = busy; }
        }

        double max = bestBusy.Max(), min = bestBusy.Min();
        Console.WriteLine($"{name,-13} wall={bestWall,9:F2} ms  busy_max={max,9:F2} ms  busy_min={min,9:F2} ms  " +
                          $"imbalance={max / min,6:F2}  correct={(correct ? "YES" : "NO")}");
    }

    static void Main(string[] args)
    {
        if (args.Length > 0) numThreads = int.Parse(args[0]);
        if (args.Length > 1) uniform = args[1] == "uniform";

        // serial reference (run once untimed first so the JIT has fully optimized Work)
        long reference = 0;
        for (int i = 0; i < N; i++) reference += Work(i);

        Stopwatch sw = Stopwatch.StartNew();
        long check = 0;
        for (int i = 0; i < N; i++) check += Work(i);
        sw.Stop();

        Console.WriteLine($"Workload: {(uniform ? "uniform" : "uneven")}  Threads: {numThreads}  Iterations: {N}  " +
                          $"ProcessorCount: {Environment.ProcessorCount}  (best of {Runs} runs)");
        Console.WriteLine($"Serial time: {sw.Elapsed.TotalMilliseconds:F2} ms");

        Report("static",       () => RunThreads(id => StaticBlock(id)),       reference);
        Report("static,1",     () => RunThreads(id => StaticChunked(id, 1)),  reference);
        Report("static,64",    () => RunThreads(id => StaticChunked(id, 64)), reference);
        Report("dynamic,1",    () => RunThreads(id => Dynamic(id, 1)),        reference);
        Report("dynamic,64",   () => RunThreads(id => Dynamic(id, 64)),       reference);
        Report("guided,1",     () => RunThreads(id => Guided(id, 1)),         reference);
        Report("guided,64",    () => RunThreads(id => Guided(id, 64)),        reference);
        Report("Parallel.For", RunParallelFor,                                reference);
    }
}