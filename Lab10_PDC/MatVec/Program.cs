// Lab 10 - Tasks 2 and 3: Distributed Matrix-Vector Multiplication with Broadcast and Gather,
// and Barrier-Separated Phase Timing
// Row-block decomposition of y = A x: every rank gets a block of rows of A (Scatter) and the
// whole vector x (Broadcast), computes its block of y, and rank 0 collects the blocks (Gather).
// Each phase is timed between two barriers; rank 0 reports the maximum over the ranks.
//
// Run:  mpiexec -n 4 .\bin\Release\net48\MatVec.exe                 (with barriers)
//       mpiexec -n 4 .\bin\Release\net48\MatVec.exe --no-barrier    (without barriers)

using System;
using System.Diagnostics;
using MPI;

class MatVec
{
    const int N = 2048;                                // A is N x N
    static Intracommunicator comm;
    static readonly Stopwatch watch = Stopwatch.StartNew();
    static bool useBarrier = true;                     // --no-barrier switches it off

    // Task 3: every phase starts and ends with a barrier, so all ranks measure the same interval
    static double PhaseStart()
    {
        if (useBarrier)
        {
            comm.Barrier();                            // the phase starts everywhere at the same moment
        }
        return watch.Elapsed.TotalMilliseconds;
    }

    static double PhaseEnd(double t0)
    {
        if (useBarrier)
        {
            comm.Barrier();                            // the slowest rank decides when the phase ends
        }
        return watch.Elapsed.TotalMilliseconds - t0;   // milliseconds
    }

    // usage: mpiexec -n 4 MatVec.exe [--no-barrier]
    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            comm = Communicator.world;
            int rank = comm.Rank, size = comm.Size;
            if (args.Length > 0 && args[0] == "--no-barrier") useBarrier = false;

            int rows = (N + size - 1) / size;          // rows per rank, rounded up
            double[] x = new double[N];                // every rank needs the whole vector
            double[][] blocks = null;                  // rank 0: one block of rows per rank
            double[] yRef = null;

            if (rank == 0)                             // build the problem (not timed)
            {
                for (int j = 0; j < N; j++) x[j] = j % 7 + 1;

                blocks = new double[size][];
                for (int r = 0; r < size; r++)
                {
                    blocks[r] = new double[rows * N];  // rows past N stay 0 (padding)
                    for (int k = 0; k < rows && r * rows + k < N; k++)
                        for (int j = 0; j < N; j++)
                            blocks[r][k * N + j] = ((r * rows + k) + j) % 10 + 1;
                }

                yRef = new double[N];
                for (int i = 0; i < N; i++)            // sequential reference
                {
                    double s = 0;
                    for (int j = 0; j < N; j++) s += (((i + j) % 10) + 1) * x[j];
                    yRef[i] = s;
                }
            }

            // ---- phase 1: distribute the data ----
            double t = PhaseStart();
            comm.Broadcast(ref x, 0);                  // the same vector x to every rank
            double[] aLocal = rank == 0 ? comm.Scatter(blocks, 0)        // a different block of rows to each rank
                                        : comm.Scatter<double[]>(0);
            double tDistribute = PhaseEnd(t);

            // ---- phase 2: local computation ----
            t = PhaseStart();
            double[] yLocal = new double[rows];
            for (int i = 0; i < rows; i++)
            {
                double s = 0;
                for (int j = 0; j < N; j++) s += aLocal[i * N + j] * x[j];
                yLocal[i] = s;
            }
            double tCompute = PhaseEnd(t);

            // ---- phase 3: collect the result ----
            t = PhaseStart();
            double[][] parts = comm.Gather(yLocal, 0); // rank 0 receives one block of y per rank, in rank order
            double tGather = PhaseEnd(t);

            double worstD = comm.Reduce(tDistribute, Operation<double>.Max, 0);
            double worstC = comm.Reduce(tCompute, Operation<double>.Max, 0);
            double worstG = comm.Reduce(tGather, Operation<double>.Max, 0);

            if (rank == 0)
            {
                long wrong = 0;
                for (int i = 0; i < N; i++)
                    if (parts[i / rows][i % rows] != yRef[i]) wrong++;

                Console.WriteLine($"[matvec] ranks={size} n={N} rows_per_rank={rows} " +
                                  $"correct={(wrong == 0 ? "YES" : "NO")}" +
                                  (useBarrier ? "" : "  (no barriers)"));
                Console.WriteLine($"phases (ms): distribute={worstD:F2} compute={worstC:F2} " +
                                  $"gather={worstG:F2} total={worstD + worstC + worstG:F2}");
            }
        }
    }
}