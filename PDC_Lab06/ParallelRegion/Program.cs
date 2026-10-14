// Lab 06 - Task 1: Parallel loop, thread IDs and the degree of parallelism
// Usage: dotnet run --no-build -c Release -- <degree>
// The degree (MaxDegreeOfParallelism) is the .NET counterpart of OMP_NUM_THREADS.
using System.Collections.Concurrent;
using System.Diagnostics;

const int Iterations = 16;
const int SleepMs = 100;

int processors = Environment.ProcessorCount;
int degree = args.Length > 0 ? int.Parse(args[0]) : processors;

var options = new ParallelOptions { MaxDegreeOfParallelism = degree };

// thread ID -> number of iterations that thread executed
var iterationsPerThread = new ConcurrentDictionary<int, int>();
int callingThread = Environment.CurrentManagedThreadId;

Console.WriteLine($"processors={processors} degree={degree} iterations={Iterations} calling_thread={callingThread}");

var sw = Stopwatch.StartNew();
Parallel.For(0, Iterations, options, i =>
{
    int id = Environment.CurrentManagedThreadId;
    iterationsPerThread.AddOrUpdate(id, 1, (_, count) => count + 1);
    Console.WriteLine($"iteration {i,2} on thread {id}");
    Thread.Sleep(SleepMs);
});
sw.Stop();

// Each thread can run one 100 ms iteration at a time, so the ideal time is
// ceil(iterations / degree) * 100 ms.
long predictedMs = (long)Math.Ceiling((double)Iterations / degree) * SleepMs;

var ids = iterationsPerThread.Keys.OrderBy(id => id).ToList();
Console.WriteLine($"distinct_threads={ids.Count}");
Console.WriteLine("thread_ids=" + string.Join(",", ids));
Console.WriteLine("iterations_per_thread=" +
    string.Join(" ", ids.Select(id => $"{id}:{iterationsPerThread[id]}")));
Console.WriteLine($"calling_thread_participated={(iterationsPerThread.ContainsKey(callingThread) ? "YES" : "NO")}");
Console.WriteLine($"elapsed_ms={sw.ElapsedMilliseconds} predicted_ms={predictedMs}");
