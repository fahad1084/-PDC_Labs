// Lab 09 - Task 1: Two-Rank Ping-Pong Benchmark

using System;
using System.Diagnostics;
using MPI;

class Program
{
    const int PingTag = 1;
    const int PongTag = 2;
    const int Trials = 5;                      // each measurement repeated 5 times
    const int WarmUpRoundTrips = 50;

    static readonly int[] Sizes = { 0, 1, 8, 64, 256, 1024, 4096, 16384, 65536, 262144 };

    // Fewer round trips for big messages so every size takes a similar time.
    static int RoundTripsFor(int size)
    {
        if (size <= 1024) return 1000;
        if (size <= 16384) return 500;
        if (size <= 65536) return 200;
        return 100;
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

            if (rank == 0)
            {
                Console.WriteLine($"Ping-pong between rank 0 and rank 1 on {MPI.Environment.ProcessorName}  " +
                                  $"(logical cores: {System.Environment.ProcessorCount}, trials per size: {Trials})");
                Console.WriteLine("size(bytes)  iterations  mean_RTT(us)  RTT/2(us)  min_trial(us)  max_trial(us)  correct");
            }

            foreach (int size in Sizes)
            {
                // payload with a known pattern so rank 0 can check what comes back
                byte[] payload = new byte[size];
                for (int i = 0; i < size; i++) payload[i] = (byte)(i * 7 + 3);

                int roundTrips = RoundTripsFor(size);
                bool correct = true;
                double sumRtt = 0, minRtt = double.MaxValue, maxRtt = 0;

                // warm-up: same exchange, not timed
                for (int i = 0; i < WarmUpRoundTrips; i++) Exchange(comm, rank, payload);

                for (int trial = 0; trial < Trials; trial++)
                {
                    comm.Barrier();                                    // start both ranks together
                    Stopwatch sw = Stopwatch.StartNew();
                    byte[] last = payload;
                    for (int i = 0; i < roundTrips; i++)
                        last = Exchange(comm, rank, payload);
                    sw.Stop();

                    double rttMicros = sw.Elapsed.TotalMilliseconds * 1000.0 / roundTrips;
                    sumRtt += rttMicros;
                    minRtt = Math.Min(minRtt, rttMicros);
                    maxRtt = Math.Max(maxRtt, rttMicros);

                    if (rank == 0 && !SameBytes(last, payload)) correct = false;
                }

                if (rank == 0)
                {
                    double mean = sumRtt / Trials;
                    Console.WriteLine($"{size,11}  {roundTrips,10}  {mean,12:F2}  {mean / 2,9:F2}  {minRtt,13:F2}  {maxRtt,13:F2}  {(correct ? "YES" : "NO")}");
                }
            }
        }
    }

    // One round trip. Rank 0: send then receive. Rank 1: receive then send it back.
    static byte[] Exchange(Intracommunicator comm, int rank, byte[] payload)
    {
        if (rank == 0)
        {
            comm.Send<byte[]>(payload, 1, PingTag);
            return comm.Receive<byte[]>(1, PongTag);
        }
        else
        {
            byte[] received = comm.Receive<byte[]>(0, PingTag);
            comm.Send<byte[]>(received, 0, PongTag);
            return received;
        }
    }

    static bool SameBytes(byte[] a, byte[] b)
    {
        if (a == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}