// Lab 05 - Task 3: Condition-Variable Signaling

using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    const int WaitMilliseconds = 1000;         // how long main "prepares the data"
    const int PublishedValue = 42;

    // ---- shared state (protected by gate) ----
    static readonly object gate = new object();
    static bool dataReady = false;             // the predicate
    static int sharedValue = 0;

    static bool sendSignal = true;             // false only in the lost-signal experiment
    static int numWorkers = 1;

    static int[] timesBlocked = Array.Empty<int>();
    static int[] valueReceived = Array.Empty<int>();
    static long[] spinIterations = Array.Empty<long>();

    // Condition-variable version: sleeps until signaled, uses no CPU while waiting.
    static void Worker(int id)
    {
        lock (gate)
        {
            while (!dataReady)                 // re-check the predicate after every wake-up
            {
                timesBlocked[id]++;
                Monitor.Wait(gate);            // releases gate, sleeps, re-acquires gate
            }
            valueReceived[id] = sharedValue;
        }
    }

    // Polling version: keeps a core busy checking the flag.
    static void SpinWorker(int id)
    {
        long spins = 0;
        while (!Volatile.Read(ref dataReady))
        {
            spins++;
        }
        spinIterations[id] = spins;

        lock (gate)
        {
            valueReceived[id] = sharedValue;
        }
    }

    static void Publish()
    {
        lock (gate)
        {
            sharedValue = PublishedValue;
            dataReady = true;                  // set the flag first ...

            if (sendSignal)                    // ... then wake the waiting thread(s)
            {
                if (numWorkers == 1) Monitor.Pulse(gate);
                else Monitor.PulseAll(gate);
            }
        }
    }

    static void Main(string[] args)
    {
        string mode = args.Length > 0 ? args[0] : "cond";
        if (args.Length > 1) numWorkers = int.Parse(args[1]);
        string option = args.Length > 2 ? args[2] : "";

        bool signalFirst = option == "signal-first";
        sendSignal = option != "no-signal";
        bool spin = mode == "spin";

        timesBlocked = new int[numWorkers];
        valueReceived = new int[numWorkers];
        spinIterations = new long[numWorkers];

        Thread[] workers = new Thread[numWorkers];
        for (int w = 0; w < numWorkers; w++)
        {
            int id = w;                        // private copy for the lambda
            workers[w] = spin ? new Thread(() => SpinWorker(id))
                              : new Thread(() => Worker(id));
        }

        if (!sendSignal)
            Console.WriteLine("Lost-signal experiment: the flag is set but no Pulse is sent. Press Ctrl+C to stop.");

        Stopwatch sw = Stopwatch.StartNew();

        if (signalFirst)
        {
            Publish();                         // signal is sent while nobody is waiting
            foreach (Thread t in workers) t.Start();
        }
        else
        {
            foreach (Thread t in workers) t.Start();
            Thread.Sleep(WaitMilliseconds);    // workers are waiting during this time
            Publish();
        }

        foreach (Thread t in workers) t.Join();
        sw.Stop();

        double cpuMs = Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;

        string scenario = spin ? "spin (polling)"
                        : signalFirst ? "cond, signal-first"
                        : $"cond, {numWorkers} worker(s)";

        Console.WriteLine($"Scenario: {scenario}");
        for (int w = 0; w < numWorkers; w++)
        {
            if (spin)
                Console.WriteLine($"Worker {w}: times blocked in wait = 0, spin iterations = {spinIterations[w]}, value received = {valueReceived[w]}");
            else
                Console.WriteLine($"Worker {w}: times blocked in wait = {timesBlocked[w]}, value received = {valueReceived[w]}");
        }
        Console.WriteLine($"Wall time: {sw.Elapsed.TotalMilliseconds:F2} ms");
        Console.WriteLine($"CPU time: {cpuMs:F2} ms");
    }
}