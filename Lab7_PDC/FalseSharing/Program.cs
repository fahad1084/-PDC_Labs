// Lab 07 - Task 3: False Sharing and Padding

using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    const int IncrementsPerThread = 100_000_000;
    const int CacheLineBytes = 64;
    const int Padding = 16;                    // 16 longs = 128 bytes between counters (> one cache line)

    static int numThreads = 4;

    static double RunThreads(Action<int> body)
    {
        Thread[] threads = new Thread[numThreads];
        Stopwatch sw = Stopwatch.StartNew();
        for (int t = 0; t < numThreads; t++)
        {
            int id = t;                        // private copy for the lambda
            threads[t] = new Thread(() => body(id));
            threads[t].Start();
        }
        foreach (Thread th in threads) th.Join();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    static void Main(string[] args)
    {
        if (args.Length > 0) numThreads = int.Parse(args[0]);

        // ---------- adjacent: 8-byte counters packed next to each other ----------
        long[] adjacent = new long[numThreads];
        double adjacentMs = RunThreads(id =>
        {
            for (int i = 0; i < IncrementsPerThread; i++)
                adjacent[id]++;                // writes to memory every iteration
        });

        // ---------- padded: each counter 128 bytes away from the next ----------
        long[] padded = new long[(numThreads + 1) * Padding];
        double paddedMs = RunThreads(id =>
        {
            int slot = (id + 1) * Padding;     // +1 also keeps slot 0 away from the array header
            for (int i = 0; i < IncrementsPerThread; i++)
                padded[slot]++;
        });

        // ---------- local-accumulate: no shared writes inside the loop ----------
        long[] results = new long[numThreads];
        double localMs = RunThreads(id =>
        {
            long local = 0;
            for (int i = 0; i < IncrementsPerThread; i++)
                local++;
            results[id] = local;               // one write per thread
        });

        bool correct = true;
        for (int t = 0; t < numThreads; t++)
        {
            if (adjacent[t] != IncrementsPerThread) correct = false;
            if (padded[(t + 1) * Padding] != IncrementsPerThread) correct = false;
            if (results[t] != IncrementsPerThread) correct = false;
        }

        Console.WriteLine($"Threads: {numThreads}  Increments per thread: {IncrementsPerThread}  " +
                          $"Cache line (assumed): {CacheLineBytes} bytes  ProcessorCount: {Environment.ProcessorCount}");
        Console.WriteLine($"adjacent          time={adjacentMs:F2} ms");
        Console.WriteLine($"padded            time={paddedMs:F2} ms");
        Console.WriteLine($"local-accumulate  time={localMs:F2} ms");
        Console.WriteLine($"Slowdown adjacent/padded: {adjacentMs / paddedMs:F2}x");
        Console.WriteLine($"Counters correct={(correct ? "YES" : "NO")}");
    }
}