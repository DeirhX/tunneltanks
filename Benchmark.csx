using System.Diagnostics;
using Tunnerer.Core.LevelGen;
using Tunnerer.Core.Types;

var size = new Size(1000, 500);
var gen = new ToastGenerator();

// Warmup
gen.Generate(size, seed: 42, mode: LevelGenMode.Deterministic);
gen.Generate(size, mode: LevelGenMode.Optimized);

const int runs = 5;
double detTotal = 0, optTotal = 0;
var sw = Stopwatch.StartNew();

for (int i = 0; i < runs; i++)
{
    sw.Restart();
    gen.Generate(size, seed: 42, mode: LevelGenMode.Deterministic);
    var detMs = sw.Elapsed.TotalMilliseconds;
    detTotal += detMs;

    sw.Restart();
    gen.Generate(size, mode: LevelGenMode.Optimized);
    var optMs = sw.Elapsed.TotalMilliseconds;
    optTotal += optMs;

    Console.WriteLine($"  Run {i+1}: deterministic={detMs:F1}ms  optimized={optMs:F1}ms");
}

Console.WriteLine($"\nAverage over {runs} runs (1000x500):");
Console.WriteLine($"  Deterministic: {detTotal/runs:F1} ms");
Console.WriteLine($"  Optimized:     {optTotal/runs:F1} ms");
Console.WriteLine($"  Speedup:       {detTotal/optTotal:F2}x");
