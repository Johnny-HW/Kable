```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
Intel Core 7 150U, 1 CPU, 12 logical and 10 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  Job-TGODTF : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

Toolchain=.NET 10 Windows  IterationCount=5  IterationTime=250ms
LaunchCount=2  WarmupCount=3

```
| Method              | Bytes | Mean     | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------- |------ |---------:|---------:|---------:|------:|--------:|----------:|------------:|
| **TemporaryThenOutput** | **64**    | **253.8 ns** | **11.33 ns** |  **7.49 ns** |  **1.00** |    **0.04** |         **-** |          **NA** |
| DirectToOutput      | 64    | 248.0 ns | 14.44 ns |  7.55 ns |  0.98 |    0.04 |         - |          NA |
|                     |       |          |          |          |       |         |           |             |
| **TemporaryThenOutput** | **4096**  | **325.5 ns** | **15.25 ns** |  **9.07 ns** |  **1.00** |    **0.04** |         **-** |          **NA** |
| DirectToOutput      | 4096  | 302.8 ns | 17.84 ns | 10.62 ns |  0.93 |    0.04 |         - |          NA |
