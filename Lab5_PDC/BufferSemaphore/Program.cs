// Lab 05 - Task 2: Multiple Producers and Consumers with Semaphores

using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    const int TotalItems = 400_000;            // divisible by 1, 2 and 4

    static int numProducers = 2;
    static int numConsumers = 2;
    static int bufferSize = 8;
    static bool deadlockExperiment = false;

    // ---- shared state ----
    static int[] buffer = Array.Empty<int>();
    static int inPos = 0;
    static int outPos = 0;
    static readonly object gate = new object();

    static SemaphoreSlim emptySlots = null!;   // free slots in the buffer
    static SemaphoreSlim fullSlots = null!;    // items waiting in the buffer

    static int[] seen = Array.Empty<int>();    // seen[id] = how many times item id was consumed

    static void Producer(int id, int itemsToProduce)
    {
        int firstId = id * itemsToProduce;     // every producer has its own id range

        for (int i = 0; i < itemsToProduce; i++)
        {
            int item = firstId + i;

            emptySlots.Wait();                 // 1. wait for a free slot (blocks if full)

            lock (gate)                        // 2. protect the index update
            {
                buffer[inPos] = item;
                inPos = (inPos + 1) % bufferSize;
            }

            fullSlots.Release();               // 3. tell consumers one item is ready
        }
    }

    static void Consumer(int itemsToConsume)
    {
        for (int i = 0; i < itemsToConsume; i++)
        {
            int item;

            if (!deadlockExperiment)
            {
                fullSlots.Wait();              // 1. wait for an item (blocks if empty)

                lock (gate)                    // 2. protect the index update
                {
                    item = buffer[outPos];
                    outPos = (outPos + 1) % bufferSize;
                }
            }
            else
            {
                // WRONG ORDER (experiment only): lock first, then wait.
                // The consumer sleeps on the semaphore while holding the lock,
                // so the producer can never get in to add an item.
                lock (gate)
                {
                    fullSlots.Wait();
                    item = buffer[outPos];
                    outPos = (outPos + 1) % bufferSize;
                }
            }

            emptySlots.Release();              // 3. tell producers one slot is free

            Interlocked.Increment(ref seen[item]);
        }
    }

    static void Main(string[] args)
    {
        if (args.Length >= 3)
        {
            numProducers = int.Parse(args[0]);
            numConsumers = int.Parse(args[1]);
            bufferSize = int.Parse(args[2]);
        }
        deadlockExperiment = args.Length >= 4 && args[3] == "deadlock";

        if (TotalItems % numProducers != 0 || TotalItems % numConsumers != 0)
        {
            Console.WriteLine($"Producers and consumers must divide {TotalItems} evenly.");
            return;
        }

        buffer = new int[bufferSize];
        seen = new int[TotalItems];
        emptySlots = new SemaphoreSlim(bufferSize, bufferSize);   // all slots free
        fullSlots = new SemaphoreSlim(0, bufferSize);             // no items yet

        int perProducer = TotalItems / numProducers;
        int perConsumer = TotalItems / numConsumers;

        Thread[] producers = new Thread[numProducers];
        Thread[] consumers = new Thread[numConsumers];

        if (deadlockExperiment)
            Console.WriteLine("Deadlock experiment: consumer locks BEFORE fullSlots.Wait(). Press Ctrl+C to stop.");

        Stopwatch sw = Stopwatch.StartNew();

        for (int c = 0; c < numConsumers; c++)
        {
            consumers[c] = new Thread(() => Consumer(perConsumer));
            consumers[c].Start();
        }
        for (int p = 0; p < numProducers; p++)
        {
            int id = p;                        // private copy for the lambda
            producers[p] = new Thread(() => Producer(id, perProducer));
            producers[p].Start();
        }

        foreach (Thread t in producers) t.Join();
        foreach (Thread t in consumers) t.Join();
        sw.Stop();

        // verification: every item id must have been consumed exactly once
        int lost = 0, duplicated = 0;
        for (int i = 0; i < TotalItems; i++)
        {
            if (seen[i] == 0) lost++;
            else if (seen[i] > 1) duplicated += seen[i] - 1;
        }

        Console.WriteLine($"Config: producers={numProducers} consumers={numConsumers} capacity={bufferSize}");
        Console.WriteLine($"Items: {TotalItems}");
        Console.WriteLine($"Lost: {lost}");
        Console.WriteLine($"Duplicated: {duplicated}");
        Console.WriteLine($"Time: {sw.Elapsed.TotalMilliseconds:F2} ms");
    }
}