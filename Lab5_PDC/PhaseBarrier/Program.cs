// Lab 05 - Task 4: Phase Barrier for Worker Threads

using System;
using System.Diagnostics;
using System.Threading;

// A reusable barrier built from a lock and a condition (Monitor.Wait / PulseAll).
class PhaseBarrier
{
    private readonly int parties;              // threads that must arrive
    private int waiting = 0;                   // threads that have arrived in this generation
    private long generation = 0;               // increases every time the barrier opens
    private readonly object gate = new object();

    public PhaseBarrier(int parties)
    {
        this.parties = parties;
    }

    public void SignalAndWait()
    {
        lock (gate)
        {
            long myGeneration = generation;    // the generation I arrived in
            waiting++;

            if (waiting == parties)
            {
                // last thread to arrive: open the barrier for everyone
                waiting = 0;
                generation++;
                Monitor.PulseAll(gate);
            }
            else
            {
                // wait until MY generation is over (not just until someone pulses)
                while (myGeneration == generation)
                {
                    Monitor.Wait(gate);
                }
            }
        }
    }
}

class Program
{
    const int Cells = 800_000;                 // divisible by 4 and 8
    const int Rounds = 100;
    const int Modulus = 1_000_003;

    static int[] current = new int[Cells];
    static int[] next = new int[Cells];

    static int numWorkers = 4;
    static bool useBarrier = true;
    static PhaseBarrier barrier = null!;

    static void Initialise(int[] grid)
    {
        for (int i = 0; i < Cells; i++)
            grid[i] = (i * 31 + 7) % 1000;
    }

    // New value of cell i in a given round: depends on the cell and both neighbours.
    static int Update(int[] grid, int i, int round)
    {
        int left = grid[(i + Cells - 1) % Cells];
        int right = grid[(i + 1) % Cells];
        return (left + 2 * grid[i] + right + round) % Modulus;
    }

    // Single-threaded reference result.
    static int[] ComputeReference()
    {
        int[] cur = new int[Cells];
        int[] nxt = new int[Cells];
        Initialise(cur);

        for (int round = 0; round < Rounds; round++)
        {
            for (int i = 0; i < Cells; i++) nxt[i] = Update(cur, i, round);
            for (int i = 0; i < Cells; i++) cur[i] = nxt[i];
        }
        return cur;
    }

    static void Worker(int id)
    {
        int start = id * (Cells / numWorkers);         // this worker's own slice
        int end = start + (Cells / numWorkers);

        for (int round = 0; round < Rounds; round++)
        {
            // phase 1: compute (reads current[], including neighbours' edge cells)
            for (int i = start; i < end; i++)
                next[i] = Update(current, i, round);

            if (useBarrier) barrier.SignalAndWait();   // everyone finished computing

            // phase 2: publish (writes current[])
            for (int i = start; i < end; i++)
                current[i] = next[i];

            if (useBarrier) barrier.SignalAndWait();   // everyone finished publishing
        }
    }

    static void Main(string[] args)
    {
        foreach (string arg in args)
        {
            if (arg == "--no-barrier") useBarrier = false;
            else if (int.TryParse(arg, out int n) && n > 0) numWorkers = n;
        }

        if (Cells % numWorkers != 0)
        {
            Console.WriteLine($"Number of workers must divide {Cells} evenly.");
            return;
        }

        int[] reference = ComputeReference();

        Initialise(current);
        barrier = new PhaseBarrier(numWorkers);
        Thread[] workers = new Thread[numWorkers];

        Stopwatch sw = Stopwatch.StartNew();
        for (int w = 0; w < numWorkers; w++)
        {
            int id = w;                        // private copy for the lambda
            workers[w] = new Thread(() => Worker(id));
            workers[w].Start();
        }
        foreach (Thread t in workers) t.Join();
        sw.Stop();

        int mismatches = 0;
        for (int i = 0; i < Cells; i++)
            if (current[i] != reference[i]) mismatches++;

        Console.WriteLine($"Mode: {(useBarrier ? "with barrier" : "no barrier")}");
        Console.WriteLine($"Workers: {numWorkers}  Cells: {Cells}  Rounds: {Rounds}");
        Console.WriteLine($"Correct: {(mismatches == 0 ? "Y" : "N")}");
        Console.WriteLine($"Mismatching cells: {mismatches}");
        Console.WriteLine($"Time: {sw.Elapsed.TotalMilliseconds:F2} ms");
        Console.WriteLine($"Environment.ProcessorCount: {Environment.ProcessorCount}");
    }
}