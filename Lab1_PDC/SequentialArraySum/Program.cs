using System.Diagnostics;

int size = 10_000_000;
int[] data = new int[size];

// Initialize array before timing
for (int i = 0; i < size; i++)
{
    data[i] = 1;
}

Stopwatch stopwatch = Stopwatch.StartNew();

long sum = 0;

// Sum all elements of the array
for (int i = 0; i < size; i++)
{
    sum += data[i];
}

stopwatch.Stop();

double elapsedMs = stopwatch.Elapsed.TotalMilliseconds;

Console.WriteLine($"Array size: {size}");
Console.WriteLine($"Sum: {sum}");
Console.WriteLine($"Execution time: {elapsedMs} ms");
