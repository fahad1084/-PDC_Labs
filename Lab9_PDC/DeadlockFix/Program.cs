// Lab 09 - Task 3: Fix the Deadlock

using System;
using System.Diagnostics;
using MPI;

class Program
{
    const int ExchangeTag = 7;
    const int ReportTag = 8;
    const int DefaultBytes = 8 * 1024 * 1024;  // 8 MiB, the size that deadlocked in Task 2

    static byte[] MakePayload(int owner, int bytes)
    {
        byte[] data = new byte[bytes];
        for (int i = 0; i < bytes; i++) data[i] = (byte)(owner * 31 + i % 251);
        return data;
    }

    static bool IsPayloadOf(int owner, byte[] data, int bytes)
    {
        if (data == null || data.Length != bytes) return false;
        for (int i = 0; i < bytes; i++)
            if (data[i] != (byte)(owner * 31 + i % 251)) return false;
        return true;
    }

    // 3A: asymmetric order, so one rank is always receiving while the other sends.
    static byte[] ReorderedExchange(Intracommunicator comm, int rank, int peer, byte[] payload)
    {
        if (rank == 0)
        {
            comm.Send<byte[]>(payload, peer, ExchangeTag);
            return comm.Receive<byte[]>(peer, ExchangeTag);
        }
        else
        {
            byte[] received = comm.Receive<byte[]>(peer, ExchangeTag);
            comm.Send<byte[]>(payload, peer, ExchangeTag);
            return received;
        }
    }

    // 3B: post both operations first, then complete both. Neither call blocks,
    // so the order is the same on both ranks and no wait-for cycle can form.
    static byte[] NonBlockingExchange(Intracommunicator comm, int peer, byte[] payload)
    {
        ReceiveRequest receive = comm.ImmediateReceive<byte[]>(peer, ExchangeTag);
        Request send = comm.ImmediateSend<byte[]>(payload, peer, ExchangeTag);

        // Do NOT read the received data or change/reuse payload until both requests are complete.
        RequestList requests = new RequestList();
        requests.Add(receive);
        requests.Add(send);
        requests.WaitAll();

        return (byte[])receive.GetValue();     // safe now: the receive has completed
    }

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

            string mode = args.Length > 0 ? args[0] : "reorder";
            int bytes = args.Length > 1 ? int.Parse(args[1]) : DefaultBytes;
            int peer = 1 - rank;
            byte[] payload = MakePayload(rank, bytes);

            comm.Barrier();
            Stopwatch sw = Stopwatch.StartNew();

            byte[] received = mode == "nonblocking"
                ? NonBlockingExchange(comm, peer, payload)
                : ReorderedExchange(comm, rank, peer, payload);

            sw.Stop();

            bool correct = IsPayloadOf(peer, received, bytes);
            double ms = sw.Elapsed.TotalMilliseconds;

            if (rank == 1)
            {
                comm.Send<double>(ms, 0, ReportTag);
                comm.Send<bool>(correct, 0, ReportTag);
            }
            else
            {
                double ms1 = comm.Receive<double>(1, ReportTag);
                bool correct1 = comm.Receive<bool>(1, ReportTag);

                string name = mode == "nonblocking" ? "Non-blocking pair (3B)" : "Reordered blocking (3A)";
                Console.WriteLine($"{name}  payload={bytes} bytes");
                Console.WriteLine($"  rank 0: completed in {ms:F2} ms, data from rank 1 correct={(correct ? "YES" : "NO")}");
                Console.WriteLine($"  rank 1: completed in {ms1:F2} ms, data from rank 0 correct={(correct1 ? "YES" : "NO")}");
                Console.WriteLine($"completion_time={Math.Max(ms, ms1):F2} ms  data_correct={(correct && correct1 ? "YES" : "NO")}  deadlock_free=YES");
            }
        }
    }
}