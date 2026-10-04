using BenchmarkDotNet.Running;
using Cbor.Benchmarks.Comparison;

if (args is ["--verify"])
{
    ComparisonVerification.Run();
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
