// Lab 09 - Task 2: Reproduce the Send-Before-Receive Deadlock

using System;
using System.Diagnostics;
using MPI;

class Program
{
    const int ExchangeTag = 7;
    const int DefaultBytes = 8 * 1024 * 1024;  // 8 MiB

    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            Intracommunicator comm = Communicator.world;
            int rank = comm.Rank;

            if (comm.Size != 2)
            {
                if (rank == 0) Console.WriteLine("Run with exactly 2 ranks: mpiexec -n 2 ...");
                return;
            }

            int bytes = args.Length > 0 ? int.Parse(args[0]) : DefaultBytes;
            int peer = 1 - rank;

            byte[] payload = new byte[bytes];
            for (int i = 0; i < bytes; i++) payload[i] = (byte)(rank + 1);

            comm.Barrier();
            Stopwatch sw = Stopwatch.StartNew();

            Console.WriteLine($"rank {rank}: blocking Send of {bytes} bytes to rank {peer} ...");
            Console.Out.Flush();

            comm.Send<byte[]>(payload, peer, ExchangeTag);           // both ranks send first ...

            Console.WriteLine($"rank {rank}: Send returned after {sw.Elapsed.TotalMilliseconds:F2} ms, now calling Receive");
            Console.Out.Flush();

            byte[] received = comm.Receive<byte[]>(peer, ExchangeTag); // ... and receive second

            Console.WriteLine($"rank {rank}: completed in {sw.Elapsed.TotalMilliseconds:F2} ms, received {received.Length} bytes " +
                              $"(no deadlock for this size: the send was buffered)");
        }
    }
}