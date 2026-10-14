// Lab 06 - Task 3: A data-scoping bug (shared local variable) and its fix
// Usage: dotnet run --no-build -c Release -- <bug|threadlocal|local> <degree>
using System.Diagnostics;

string mode = args.Length > 0 ? args[0] : "bug";
int degree = args.Length > 1 ? int.Parse(args[1]) : Environment.ProcessorCount;
var options = new ParallelOptions { MaxDegreeOfParallelism = degree };

Data.Init();
var sw = Stopwatch.StartNew();

switch (mode)
{
    case "bug":
    {
        // BUG: tmp is declared outside the lambda, so the compiler captures it
        // in one shared closure object. All threads read and write the SAME tmp
        // (the counterpart of forgetting private(tmp) in OpenMP).
        int tmp = 0;
        Parallel.For(0, Data.N, options, i =>
        {
            tmp = Data.input[i] * 3;
            Thread.SpinWait(20); // widens the window between write and read
            Data.output[i] = tmp + 7;
        });
        break;
    }

    case "threadlocal":
    {
        // FIX 1: thread-local state. localInit gives every worker its own tmp
        // (starting at 0, like firstprivate with an initial value), the body
        // receives it and returns it, and localFinally runs once per worker.
        Parallel.For(0, Data.N, options,
            () => 0,                                  // localInit: private tmp per worker
            (i, loopState, tmp) =>
            {
                tmp = Data.input[i] * 3;
                Thread.SpinWait(20);
                Data.output[i] = tmp + 7;
                return tmp;                           // passed to this worker's next iteration
            },
            tmp => { });                              // localFinally: nothing to combine
        break;
    }

    case "local":
    {
        // FIX 2: declare tmp inside the lambda, so each call has its own copy
        // on its own stack (the simplest way to make a variable private).
        Parallel.For(0, Data.N, options, i =>
        {
            int tmp = Data.input[i] * 3;
            Thread.SpinWait(20);
            Data.output[i] = tmp + 7;
        });
        break;
    }

    default:
        Console.WriteLine("mode must be bug, threadlocal or local");
        return;
}

sw.Stop();
int wrong = Data.CountWrong();
Console.WriteLine($"mode={mode} degree={degree} N={Data.N} processors={Environment.ProcessorCount}");
Console.WriteLine($"wrong_elements={wrong} time_ms={sw.ElapsedMilliseconds}");

static class Data
{
    public const int N = 200_000;
    public static readonly int[] input = new int[N];
    public static readonly int[] output = new int[N];

    public static void Init()
    {
        for (int i = 0; i < N; i++)
            input[i] = i;
    }

    public static int CountWrong()
    {
        int wrong = 0;
        for (int i = 0; i < N; i++)
            if (output[i] != input[i] * 3 + 7)
                wrong++;
        return wrong;
    }
}
