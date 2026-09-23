using System;
using System.Diagnostics;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--child")
        {
            RunAsChild();
        }
        else
        {
            RunAsParent();
        }
    }

    static void RunAsChild()
    {
        Console.WriteLine($"[Child] PID = {Environment.ProcessId}");
        int counter = 100;
        counter += 50;
        Console.WriteLine($"[Child] final counter = {counter}");
    }

    static void RunAsParent()
    {
        Console.WriteLine($"[Parent] PID = {Environment.ProcessId}");
        int counter = 100;
        counter += 1;

        // Path to the currently running executable, so the parent can launch
        // a second copy of itself as the child.
        string exePath = Environment.ProcessPath!;

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("--child");

        Process child = Process.Start(psi)!;
        child.WaitForExit();

        Console.WriteLine($"[Parent] final counter = {counter}");
        Console.WriteLine("[Parent] Parent and child counters were modified independently (separate address spaces).");
    }
}