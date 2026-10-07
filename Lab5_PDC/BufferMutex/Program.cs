// Lab 05 - Task 1: Mutex-Protected Bounded Buffer

using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    static int BufferSize = 8;                 // capacity of the bounded buffer
    const int NumItems = 1_000_000;            // items sent from producer to consumer

    // ---- shared state (protected by gate) ----
    static int[] buffer = new int[BufferSize];
    static int inPos = 0;                      // next slot to write
    static int outPos = 0;                     // next slot to read
    static int count = 0;                      // items currently in the buffer
    static readonly object gate = new object();

    // ---- statistics (each written by one thread only, read after Join) ----
    static long producerRetries = 0;
    static long consumerRetries = 0;
    static long checksumProduced = 0;
    static long checksumConsumed = 0;
    static long orderErrors = 0;

    static void Producer()
    {
        for (int item = 1; item <= NumItems; item++)
        {
            checksumProduced += item;

            while (true)
            {
                bool inserted = false;

                lock (gate)
                {
                    // test and insert in the SAME critical section
                    if (count < BufferSize)
                    {
                        buffer[inPos] = item;
                        inPos = (inPos + 1) % BufferSize;
                        count++;
                        inserted = true;
                    }
                }

                if (inserted) break;           // done with this item

                producerRetries++;             // buffer was full: try again later
                Thread.Yield();
            }
        }
    }

    static void Consumer()
    {
        for (int expected = 1; expected <= NumItems; expected++)
        {
            int item = 0;

            while (true)
            {
                bool removed = false;

                lock (gate)
                {
                    // test and remove in the SAME critical section
                    if (count > 0)
                    {
                        item = buffer[outPos];
                        outPos = (outPos + 1) % BufferSize;
                        count--;
                        removed = true;
                    }
                }

                if (removed) break;

                consumerRetries++;             // buffer was empty: try again later
                Thread.Yield();
            }

            // verification: items must arrive as 1, 2, 3, ...
            checksumConsumed += item;
            if (item != expected) orderErrors++;
        }
    }

    static void Main(string[] args)
    {
        if (args.Length > 0 && int.TryParse(args[0], out int size) && size > 0)
        {
            BufferSize = size;
            buffer = new int[BufferSize];
        }

        Thread producer = new Thread(Producer);
        Thread consumer = new Thread(Consumer);

        Stopwatch sw = Stopwatch.StartNew();
        producer.Start();
        consumer.Start();
        producer.Join();
        consumer.Join();
        sw.Stop();

        bool checksumOk = checksumProduced == checksumConsumed;

        Console.WriteLine($"Buffer size: {BufferSize}");
        Console.WriteLine($"Items: {NumItems}");
        Console.WriteLine($"Checksum produced: {checksumProduced}");
        Console.WriteLine($"Checksum consumed: {checksumConsumed}");
        Console.WriteLine($"Checksum OK: {(checksumOk ? "Y" : "N")}");
        Console.WriteLine($"Order errors: {orderErrors}");
        Console.WriteLine($"Producer retries: {producerRetries}");
        Console.WriteLine($"Consumer retries: {consumerRetries}");
        Console.WriteLine($"Time: {sw.Elapsed.TotalMilliseconds:F2} ms");
    }
}