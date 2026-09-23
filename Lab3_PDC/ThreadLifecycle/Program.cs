using System;
using System.Threading;

class Program
{
    static void Worker()
    {
        Thread.Sleep(200);
    }

    static void Main()
    {
        Thread t = new Thread(Worker);

        // Conceptually: New
        Console.WriteLine($"After creation: {t.ThreadState}");

        t.Start();

        // Conceptually: Runnable/Ready or Running
        Console.WriteLine($"Immediately after Start(): {t.ThreadState}");

        Thread.Sleep(50); // give the worker a moment to reach Thread.Sleep(200)

        // Conceptually: Blocked/Waiting
        Console.WriteLine($"While worker is sleeping: {t.ThreadState}");

        t.Join();

        // Conceptually: Terminated
        Console.WriteLine($"After Join() completes: {t.ThreadState}");
    }
}