// Lab 08 - Task 2: Message Passing vs Shared Memory: the Same Sum Two Ways
// The same array of 10,000,000 integers is summed three ways:
//   MPI collective      - Scatter the chunks, every rank sums its chunk, Reduce the partial sums
//   MPI point-to-point  - rank 0 Sends each chunk, every rank sums it and Sends its partial sum back
//   shared memory (TPL) - rank 0 alone uses as many threads as there are ranks, all reading one array
// Each variant is repeated 3 times and the average time is printed by rank 0.
//
// Run:  mpiexec -n 4 .\bin\Release\net48\SumCompare.exe     (ranks must divide 10,000,000)

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MPI;

class Program
{
    const int N = 10_000_000;
    const int Repeats = 3;
    const int DataTag = 1;
    const int ResultTag = 2;

    static Intracommunicator comm = null!;
    static int rank, size;
    static int[] data = null!;                 // the full array exists ONLY on rank 0

    static long SumOf(int[] values)
    {
        long sum = 0;
        for (int i = 0; i < values.Length; i++) sum += values[i];
        return sum;
    }

    // Rank 0 cuts the array into one chunk per rank.
    static int[][] SplitIntoChunks()
    {
        int chunkSize = N / size;
        int[][] chunks = new int[size][];
        for (int r = 0; r < size; r++)
        {
            chunks[r] = new int[chunkSize];
            Array.Copy(data, r * chunkSize, chunks[r], 0, chunkSize);
        }
        return chunks;
    }

    // ---------- variant 1: collective operations ----------
    static long CollectiveVariant()
    {
        int[][] chunks = rank == 0 ? SplitIntoChunks() : null!;

        int[] myChunk = comm.Scatter<int[]>(chunks, 0);             // every rank receives its own chunk
        long localSum = SumOf(myChunk);                             // work on private memory only
        long total = comm.Reduce<long>(localSum, Operation<long>.Add, 0); // partial sums added at rank 0

        return total;                                               // meaningful on rank 0 only
    }

    // ---------- variant 2: point-to-point messages ----------
    static long PointToPointVariant()
    {
        if (rank == 0)
        {
            int[][] chunks = SplitIntoChunks();

            for (int r = 1; r < size; r++)
                comm.Send<int[]>(chunks[r], r, DataTag);            // explicit send of each chunk

            long total = SumOf(chunks[0]);                          // rank 0 sums its own chunk

            for (int r = 1; r < size; r++)
                total += comm.Receive<long>(r, ResultTag);          // explicit receive of each partial sum

            return total;
        }
        else
        {
            int[] myChunk = comm.Receive<int[]>(0, DataTag);
            comm.Send<long>(SumOf(myChunk), 0, ResultTag);
            return 0;
        }
    }

    // ---------- variant 3: shared memory on rank 0 ----------
    static long SharedMemoryVariant()
    {
        if (rank != 0) return 0;                                    // other ranks simply wait

        int threads = size;
        int chunkSize = N / threads;
        int[] shared = data;                                        // one array, visible to every thread
        long total = 0;

        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, t =>
        {
            long local = 0;
            int end = (t + 1) * chunkSize;
            for (int i = t * chunkSize; i < end; i++)
                local += shared[i];                                 // every thread reads the SAME array
            Interlocked.Add(ref total, local);
        });
        return total;
    }

    static void Measure(string name, Func<long> variant, long reference)
    {
        double totalMs = 0;
        bool correct = true;

        for (int r = 0; r < Repeats; r++)
        {
            comm.Barrier();                                         // start all ranks together
            Stopwatch sw = Stopwatch.StartNew();
            long result = variant();
            comm.Barrier();                                         // stop when every rank is done
            sw.Stop();

            totalMs += sw.Elapsed.TotalMilliseconds;
            if (rank == 0 && result != reference) correct = false;
        }

        if (rank == 0)
            Console.WriteLine($"{name,-22} avg_time={totalMs / Repeats,10:F2} ms   correct={(correct ? "YES" : "NO")}");
    }

    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            comm = Communicator.world;
            rank = comm.Rank;
            size = comm.Size;

            if (N % size != 0)
            {
                if (rank == 0) Console.WriteLine($"The number of ranks ({size}) must divide {N}. Use 1, 2, 4 or 8.");
                return;
            }

            long reference = 0;
            if (rank == 0)
            {
                Random rng = new Random(2026);
                data = new int[N];
                for (int i = 0; i < N; i++) data[i] = rng.Next(0, 1000);
                reference = SumOf(data);

                Console.WriteLine($"Ranks/threads: {size}  Elements: {N}  Repeats: {Repeats}  " +
                                  $"Processor: {MPI.Environment.ProcessorName}  Logical cores: {System.Environment.ProcessorCount}");
                Console.WriteLine($"Reference sum: {reference}");
            }

            Measure("MPI collective", CollectiveVariant, reference);
            Measure("MPI point-to-point", PointToPointVariant, reference);
            Measure("Shared memory (TPL)", SharedMemoryVariant, reference);
        }
    }
}