using System;
using System.Threading;

class Program
{
    const int NumThreads = 4;
    const int IncrementsPerThread = 250_000;

    static int counter = 0; // shared state
    static readonly object gate = new object(); // used only with --lock
    static bool useLock = false;
    static int[][] seenLog = new int[NumThreads][]; // private log per thread

    static void Worker(int id)
    {
        int[] log = seenLog[id];
        for (int i = 0; i < IncrementsPerThread; i++)
        {
            if (useLock)
            {
                lock (gate) // used in Task 3
                {
                    int seen = counter;         // (1) load
                    log[i] = seen;               // private record of the load
                    counter = seen + 1;           // (2) add and (3) store
                }
            }
            else
            {
                int seen = counter;             // (1) load
                log[i] = seen;                   // private record of the load
                counter = seen + 1;               // (2) add and (3) store
            }
        }
    }

    static void Main(string[] args)
    {
        useLock = args.Length > 0 && args[0] == "--lock";

        int total = NumThreads * IncrementsPerThread;
        Thread[] threads = new Thread[NumThreads];

        for (int t = 0; t < NumThreads; t++)
        {
            seenLog[t] = new int[IncrementsPerThread];
            int id = t;
            threads[t] = new Thread(() => Worker(id));
            threads[t].Start();
        }

        foreach (Thread th in threads) th.Join();

        // Analysis: how many increments loaded each value?
        int[] readCount = new int[total + 1];
        for (int t = 0; t < NumThreads; t++)
            for (int i = 0; i < IncrementsPerThread; i++)
                readCount[seenLog[t][i]]++;

        int collisions = 0; // loads of a value that was already loaded
        for (int v = 0; v <= total; v++)
            if (readCount[v] > 1) collisions += readCount[v] - 1;

        string mode = useLock ? "with lock" : "no synchronization";
        Console.WriteLine($"Mode: {mode}");
        Console.WriteLine($"Total increments: {total}");
        Console.WriteLine($"Final counter: {counter}");
        Console.WriteLine($"Lost updates: {total - counter}");
        Console.WriteLine($"Collisions: {collisions}");

        // Print the first 5 values that were loaded by more than one increment,
        // and for each, which thread(s)/loop index loaded it.
        Console.WriteLine();
        Console.WriteLine("First 5 colliding values:");
        int printed = 0;
        for (int v = 0; v <= total && printed < 5; v++)
        {
            if (readCount[v] > 1)
            {
                Console.WriteLine($"\nValue {v} was loaded {readCount[v]} times, by:");
                for (int t = 0; t < NumThreads; t++)
                {
                    for (int i = 0; i < IncrementsPerThread; i++)
                    {
                        if (seenLog[t][i] == v)
                        {
                            Console.WriteLine($"  Thread {t}, loop index {i}");
                        }
                    }
                }
                printed++;
            }
        }

        if (printed == 0)
        {
            Console.WriteLine("  (no collisions found in this run)");
        }
    }
}