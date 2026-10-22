// Lab 08 - Task 1: Verify MPI and Run Hello World
// Every rank prints one line straight away (unordered), then sends the same
// report line to rank 0, which prints all of them in rank order (ordered).
//
// Run:  mpiexec -n 4 .\bin\Release\net48\HelloMpi.exe

using System;
using System.Threading;
using MPI;

class Program
{
    const int ReportTag = 0;

    static void Main(string[] args)
    {
        // MPI.Environment starts MPI for this process and shuts it down when disposed.
        using (new MPI.Environment(ref args))
        {
            Intracommunicator comm = Communicator.world;   // MPI_COMM_WORLD: all ranks started by mpiexec

            // ---- query rank, size and processor name ----
            int rank = comm.Rank;                           // this process's ID: 0 .. size-1
            int size = comm.Size;                           // number of processes in the communicator
            string processorName = MPI.Environment.ProcessorName;

            string line = $"Hello from rank {rank} of {size} on {processorName}";

            // ---- part 1: every rank prints on its own (order decided by the OS) ----
            Console.WriteLine($"[unordered] {line}");
            Console.Out.Flush();

            comm.Barrier();                                 // all ranks have printed

            // ---- part 2: send the report line to rank 0, which prints in rank order ----
            if (rank != 0)
            {
                comm.Send<string>(line, 0, ReportTag);
            }
            else
            {
                Thread.Sleep(300);                          // let the unordered lines reach the console first
                Console.WriteLine();
                Console.WriteLine($"Ordered report collected by rank 0 (communicator size = {size}, " +
                                  $"logical cores = {System.Environment.ProcessorCount}):");
                Console.WriteLine($"[ordered]   {line}");
                for (int source = 1; source < size; source++)
                {
                    string received = comm.Receive<string>(source, ReportTag);
                    Console.WriteLine($"[ordered]   {received}");
                }
            }
        }
    }
}