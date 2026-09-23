using System;
using System.Diagnostics;
using System.Threading;

class Program
{
    const int Iterations = 50;

    // IMPORTANT: update this path to point at your built ProcessLab.exe from Task 1.
    // Build ProcessLab in Release mode first (from inside the ProcessLab folder):
    //     dotnet build -c Release
    // Then find the .exe under ProcessLab\bin\Release\<target-framework>\ProcessLab.exe
    // and paste the correct path below. The <target-framework> folder name (e.g. net8.0,
    // net10.0) depends on your installed SDK — check what actually got created.
    static readonly string ChildExePath =
        @"..\ProcessLab\bin\Release\net10.0\ProcessLab.exe";

    static void Main()
    {
        if (!System.IO.File.Exists(ChildExePath))
        {
            Console.WriteLine($"ERROR: Could not find {ChildExePath}");
            Console.WriteLine("Build ProcessLab in Release mode first, then fix the ChildExePath above.");
            return;
        }

        var processStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++)
        {
            var psi = new ProcessStartInfo
            {
                FileName = ChildExePath,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("--child");

            Process p = Process.Start(psi)!;
            p.WaitForExit();
        }
        processStopwatch.Stop();

        var threadStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < Iterations; i++)
        {
            Thread t = new Thread(() => { /* trivial work */ });
            t.Start();
            t.Join();
        }
        threadStopwatch.Stop();

        double avgProcessMs = processStopwatch.Elapsed.TotalMilliseconds / Iterations;
        double avgThreadMs = threadStopwatch.Elapsed.TotalMilliseconds / Iterations;

        Console.WriteLine($"Average process creation time: {avgProcessMs:F3} ms");
        Console.WriteLine($"Average thread creation time: {avgThreadMs:F3} ms");
        Console.WriteLine($"Process creation was {(avgProcessMs / avgThreadMs):F1}x more expensive than thread creation.");
    }
}