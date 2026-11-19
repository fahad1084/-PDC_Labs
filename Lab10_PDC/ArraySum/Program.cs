// Lab 10 - Task 1: Distributed Array Sum with Scatter and Reduce
// Pattern: scatter, compute locally, reduce.
// Rank 0 builds the array (padded with zeros to a multiple of the number of ranks),
// computes the sequential reference, splits the array into one chunk per rank and
// scatters it. Every rank sums its chunk and finds its maximum; two reductions combine
// the partial results on rank 0, which checks them against the reference.
//
// Run:  mpiexec -n 4 .\bin\Release\net48\ArraySum.exe      (also -n 1, 2, 3 and 8)

using System;
using System.Diagnostics;
using MPI;

class ArraySum
{
    const int N = 10_000_003;                          // not divisible by most rank counts

    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            Intracommunicator comm = Communicator.world;
            int rank = comm.Rank, size = comm.Size;

            int chunk = (N + size - 1) / size;         // elements per rank, rounded up
            long padded = (long)chunk * size;          // array padded with zeros to fit
            int[][] chunks = null;                     // the pieces rank 0 distributes
            long refSum = 0;
            int refMax = int.MinValue;

            if (rank == 0)
            {
                int[] data = new int[padded];          // elements N .. padded-1 stay 0
                for (int i = 0; i < N; i++) data[i] = (int)(((long)i * 7919) % 100003);

                for (int i = 0; i < N; i++)            // sequential reference
                {
                    refSum += data[i];
                    if (data[i] > refMax) refMax = data[i];
                }

                chunks = new int[size][];
                for (int r = 0; r < size; r++)
                {
                    chunks[r] = new int[chunk];
                    Array.Copy(data, (long)r * chunk, chunks[r], 0, chunk);
                }
            }

            comm.Barrier();
            Stopwatch sw = Stopwatch.StartNew();

            // ---- scatter: every rank receives its own chunk ----
            int[] mine = rank == 0 ? comm.Scatter(chunks, 0)       // root passes one chunk per rank
                                   : comm.Scatter<int[]>(0);       // the others only name the root

            // ---- compute locally: sum and maximum of this rank's chunk ----
            long localSum = 0;
            int localMax = int.MinValue;
            for (int i = 0; i < mine.Length; i++)
            {
                localSum += mine[i];
                if (mine[i] > localMax) localMax = mine[i];
            }

            // ---- reduce: combine the partial results on rank 0 ----
            long total = comm.Reduce(localSum, Operation<long>.Add, 0);
            int gmax = comm.Reduce(localMax, Operation<int>.Max, 0);

            comm.Barrier();
            double ms = sw.Elapsed.TotalMilliseconds;

            if (rank == 0)
            {
                Console.WriteLine($"[array-sum] ranks={size} n={N} chunk={chunk} " +
                                  $"padding={padded - N}");
                bool ok = total == refSum && gmax == refMax;
                Console.WriteLine($"sum={total} reference={refSum} max={gmax} " +
                                  $"reference_max={refMax} " +
                                  $"correct={(ok ? "YES" : "NO")} time={ms:F2} ms");
            }
        }
    }
}