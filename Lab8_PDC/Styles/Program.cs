// Lab 08 - Task 3: MPI Deployments and Architecture Styles
// Two small programs that follow two architecture styles:
//   master : task farm (client-server). Rank 0 is the server that hands out tasks;
//            the other ranks are clients that ask for work and return results.
//   ring   : token ring (peer-to-peer). Every rank runs the same code and only
//            talks to its two neighbours; a token travels round the ring.
//
// Run:  mpiexec -n 4 .\bin\Release\net48\Styles.exe master
//       mpiexec -n 4 .\bin\Release\net48\Styles.exe ring

using System;
using System.Diagnostics;
using MPI;

class Program
{
    // ---- task farm settings ----
    const int NumTasks = 200;
    const int RequestTag = 10;                 // worker -> master: "I am free, give me work" (carries the worker's rank)
    const int ResultTag = 12;                  // worker -> master: the result of the task it just finished
    const int TaskTag = 11;                    // master -> worker: task id, or NoMoreTasks
    const int NoMoreTasks = -1;

    // ---- ring settings ----
    const int Rounds = 1000;
    const int TokenTag = 20;

    static Intracommunicator comm = null!;
    static int rank, size;

    // The work for one task: tasks have different costs on purpose.
    static long DoTask(int taskId)
    {
        int cost = 200_000 * (1 + taskId % 5);
        long acc = 0;
        for (int k = 0; k < cost; k++) acc += (k ^ taskId) & 15;
        return acc;
    }

    // ================= client-server: task farm =================
    static void TaskFarm()
    {
        if (rank == 0)
        {
            // ---- server (master) ----
            int workers = size - 1;
            int[] served = new int[size];
            bool[] hasOpenTask = new bool[size];         // has this worker got a task whose result is still due?
            int nextTask = 0, finishedWorkers = 0;
            long total = 0;

            Stopwatch sw = Stopwatch.StartNew();
            while (finishedWorkers < workers)
            {
                // a request from ANY worker; the message carries that worker's rank
                int worker = comm.Receive<int>(Communicator.anySource, RequestTag);

                if (hasOpenTask[worker])
                    total += comm.Receive<long>(worker, ResultTag);   // collect the result it sent just before

                if (nextTask < NumTasks)
                {
                    comm.Send<int>(nextTask, worker, TaskTag);        // serve the next task to this client
                    served[worker]++;
                    hasOpenTask[worker] = true;
                    nextTask++;
                }
                else
                {
                    comm.Send<int>(NoMoreTasks, worker, TaskTag);
                    hasOpenTask[worker] = false;
                    finishedWorkers++;
                }
            }
            sw.Stop();

            long reference = 0;
            for (int t = 0; t < NumTasks; t++) reference += DoTask(t);

            Console.WriteLine($"Task farm (client-server)  ranks={size}  workers={workers}  tasks={NumTasks}  time={sw.Elapsed.TotalMilliseconds:F2} ms");
            for (int w = 1; w < size; w++)
                Console.WriteLine($"  worker {w}: served {served[w]} tasks");
            Console.WriteLine($"correct={(total == reference ? "YES" : "NO")}");
        }
        else
        {
            // ---- client (worker) ----
            while (true)
            {
                comm.Send<int>(rank, 0, RequestTag);                  // ask the server for work
                int taskId = comm.Receive<int>(0, TaskTag);
                if (taskId == NoMoreTasks) break;
                comm.Send<long>(DoTask(taskId), 0, ResultTag);        // return the result ...
            }                                                         // ... and ask again
        }
    }

    // ================= peer-to-peer: token ring =================
    static void TokenRing()
    {
        int left = (rank - 1 + size) % size;
        int right = (rank + 1) % size;
        long token = 0;

        comm.Barrier();
        Stopwatch sw = Stopwatch.StartNew();

        for (int round = 0; round < Rounds; round++)
        {
            if (rank == 0)
            {
                token++;                                    // rank 0 starts each round
                comm.Send<long>(token, right, TokenTag);
                token = comm.Receive<long>(left, TokenTag); // the token comes back round the ring
            }
            else
            {
                token = comm.Receive<long>(left, TokenTag); // same code on every other rank:
                token++;                                    // receive from the left neighbour,
                comm.Send<long>(token, right, TokenTag);          // add one, pass to the right neighbour
            }
        }

        sw.Stop();

        if (rank == 0)
        {
            long messages = (long)Rounds * size;
            long expected = messages;                       // every rank adds 1 in every round
            double ms = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"Token ring (peer-to-peer)  ranks={size}  rounds={Rounds}  messages={messages}");
            Console.WriteLine($"time={ms:F2} ms  time_per_message={ms / messages:F4} ms");
            Console.WriteLine($"final token={token} expected={expected}  correct={(token == expected ? "YES" : "NO")}");
        }
    }

    static void Main(string[] args)
    {
        using (new MPI.Environment(ref args))
        {
            comm = Communicator.world;
            rank = comm.Rank;
            size = comm.Size;
            string mode = args.Length > 0 ? args[0] : "master";

            if (size < 2)
            {
                if (rank == 0) Console.WriteLine("Run with at least 2 ranks, e.g. mpiexec -n 4 ...");
                return;
            }

            if (mode == "ring") TokenRing();
            else TaskFarm();
        }
    }
}