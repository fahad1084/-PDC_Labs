// Lab 05 - Task 5 (optional): Lock-Free Stack with Compare-and-Swap

using System;
using System.Diagnostics;
using System.Threading;

class Node
{
    public int Value;
    public Node? Next;

    public Node(int value)
    {
        Value = value;
    }
}

class LockFreeStack
{
    private Node? head = null;

    public void Push(int value)
    {
        Node node = new Node(value);

        while (true)
        {
            Node? oldHead = Volatile.Read(ref head);   // 1. read the current head
            node.Next = oldHead;                       // 2. link the new node in front of it

            // 3. install the new node only if head is still oldHead
            if (Interlocked.CompareExchange(ref head, node, oldHead) == oldHead)
                return;
            // otherwise another thread changed head: retry
        }
    }

    public bool TryPop(out int value)
    {
        while (true)
        {
            Node? oldHead = Volatile.Read(ref head);   // 1. read the current head
            if (oldHead == null)
            {
                value = 0;
                return false;                          // stack is empty
            }

            Node? newHead = oldHead.Next;              // 2. the node after it

            // 3. unlink oldHead only if head is still oldHead
            if (Interlocked.CompareExchange(ref head, newHead, oldHead) == oldHead)
            {
                value = oldHead.Value;
                return true;
            }
            // otherwise another thread changed head: retry
        }
    }

    public bool IsEmpty => Volatile.Read(ref head) == null;
}

class Program
{
    static int NumThreads = 4;
    const int NodesPerThread = 250_000;
    const int BatchSize = 100;                 // push a batch, then pop a batch

    static readonly LockFreeStack stack = new LockFreeStack();
    static int[] seen = Array.Empty<int>();    // seen[v] = how many times value v was popped

    static void Worker(int id)
    {
        int nextValue = id * NodesPerThread;   // every thread pushes its own range of values
        int pushed = 0;
        int popped = 0;

        while (popped < NodesPerThread)
        {
            for (int i = 0; i < BatchSize && pushed < NodesPerThread; i++)
            {
                stack.Push(nextValue++);
                pushed++;
            }

            for (int i = 0; i < BatchSize && popped < pushed; i++)
            {
                int value;
                while (!stack.TryPop(out value))
                {
                    Thread.Yield();            // temporarily empty: let the others push
                }
                Interlocked.Increment(ref seen[value]);
                popped++;
            }
        }
    }

    static void Main(string[] args)
    {
        if (args.Length > 0 && int.TryParse(args[0], out int n) && n > 0)
            NumThreads = n;

        int total = NumThreads * NodesPerThread;
        seen = new int[total];

        Thread[] threads = new Thread[NumThreads];

        Stopwatch sw = Stopwatch.StartNew();
        for (int t = 0; t < NumThreads; t++)
        {
            int id = t;                        // private copy for the lambda
            threads[t] = new Thread(() => Worker(id));
            threads[t].Start();
        }
        foreach (Thread th in threads) th.Join();
        sw.Stop();

        int lost = 0, duplicated = 0;
        for (int v = 0; v < total; v++)
        {
            if (seen[v] == 0) lost++;
            else if (seen[v] > 1) duplicated += seen[v] - 1;
        }

        Console.WriteLine($"Threads: {NumThreads}");
        Console.WriteLine($"Nodes (total): {total}");
        Console.WriteLine($"Lost: {lost}");
        Console.WriteLine($"Duplicated: {duplicated}");
        Console.WriteLine($"Stack empty: {(stack.IsEmpty ? "Y" : "N")}");
        Console.WriteLine($"Time: {sw.Elapsed.TotalMilliseconds:F2} ms");
    }
}