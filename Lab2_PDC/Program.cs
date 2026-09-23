using System;
using System.Threading;

class Program
{
    // Worker method executed by each spawned thread.
    // Receives its own index (boxed as object) so it can identify itself.
    static void Worker(object? arg)
    {
        long id = (long)arg!;

        // Optional (Section 5.5): report the logical CPU this thread is
        // actually executing on at the moment of the call. The OS scheduler
        // may migrate the thread later, so this is only a snapshot.
        int cpu = Thread.GetCurrentProcessorId();

        Console.WriteLine($"Thread {id}: running on logical CPU {cpu}");
    }

    static void Main()
    {
        // Step 2: detect the number of logical cores available to this process.
        int numCores = Environment.ProcessorCount;
        Console.WriteLine($"Detected logical cores: {numCores}");

        // Step 3: dynamically allocate a Thread array sized to the detected
        // core count (never hard-coded).
        Thread[] threads = new Thread[numCores];

        // Step 5: spawn exactly one thread per detected core, passing each
        // thread its own index.
        for (int i = 0; i < numCores; i++)
        {
            long idx = i; // capture a local copy so each thread gets its own identity
            threads[i] = new Thread(() => Worker(idx));
            threads[i].Start();
        }

        // Step 6: join every thread before the program exits.
        for (int i = 0; i < numCores; i++)
        {
            threads[i].Join();
        }

        // Step 7: report the total number of threads created.
        Console.WriteLine($"All {numCores} threads completed.");
    }
}