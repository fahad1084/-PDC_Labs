// Lab 09 - Task 4: N-Rank Ring Communication Using Non-Blocking Calls

using System;
using MPI;

class Program
{
    const int TokenTag = 30;
    const int TraceTag = 31;
    const long TokenValue = 2026;

    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            Intracommunicator comm = Communicator.world;
            int rank = comm.Rank;
            int size = comm.Size;

            if (size < 2)
            {
                if (rank == 0) Console.WriteLine("Run with at least 2 ranks: mpiexec -n 4 ...");
                return;
            }

            int next = (rank + 1) % size;              // successor (last rank -> 0)
            int previous = (rank - 1 + size) % size;   // predecessor (rank 0 -> last rank)
            string trace;

            if (rank == 0)
            {
                long[] token = { TokenValue, 0 };      // [value, sequence]
                token[1]++;                            // first hop: rank 0 -> next

                // post the receive for the returning token AND the send, then complete both
                ReceiveRequest receive = comm.ImmediateReceive<long[]>(previous, TokenTag);
                Request send = comm.ImmediateSend<long[]>(token, next, TokenTag);
                RequestList requests = new RequestList();
                requests.Add(receive);
                requests.Add(send);
                requests.WaitAll();                    // only now are both buffers safe to use

                long[] returned = (long[])receive.GetValue();
                trace = $"rank {rank}: prev={previous} next={next}  sent value={TokenValue} seq={token[1]}  " +
                        $"| got back value={returned[0]} seq={returned[1]} from rank {previous}";

                // collect the other ranks' trace lines so the trace prints in rank order
                Console.WriteLine($"Ring with {size} ranks (token value {TokenValue})");
                Console.WriteLine(trace);
                for (int r = 1; r < size; r++)
                    Console.WriteLine(comm.Receive<string>(r, TraceTag));

                bool correct = returned[0] == TokenValue && returned[1] == size;
                Console.WriteLine($"token_value_at_origin={TokenValue}  token_value_returned={returned[0]}  " +
                                  $"messages_forwarded={returned[1]}  expected={size}  correct={(correct ? "YES" : "NO")}");
            }
            else
            {
                // post the receive non-blocking and complete it before touching the token
                ReceiveRequest receive = comm.ImmediateReceive<long[]>(previous, TokenTag);
                receive.Wait();
                long[] token = (long[])receive.GetValue();
                long seqIn = token[1];

                long[] forward = { token[0], seqIn + 1 };   // a new buffer for the outgoing message
                Request send = comm.ImmediateSend<long[]>(forward, next, TokenTag);
                send.Wait();                                // complete the send before finishing

                trace = $"rank {rank}: prev={previous} next={next}  received value={token[0]} seq={seqIn}  " +
                        $"-> forwarded seq={forward[1]} to rank {next}";
                comm.Send<string>(trace, 0, TraceTag);
            }
        }
    }
}