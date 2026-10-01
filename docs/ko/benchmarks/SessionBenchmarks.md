```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
Intel Core 7 150U, 1 CPU, 12 logical and 10 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  Job-WPUNPV : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

IterationCount=3  IterationTime=100ms  LaunchCount=1  
WarmupCount=1  

```
| Method          | Characters | Observe | Mean        | Error        | StdDev      | Gen0   | Gen1   | Allocated |
|---------------- |----------- |-------- |------------:|-------------:|------------:|-------:|-------:|----------:|
| **RequestResponse** | **32**         | **False**   | **32,170.0 ns** |  **32,314.4 ns** | **1,771.26 ns** | **0.4223** |      **-** |    **3662 B** |
| Stream100       | 32         | False   |    824.1 ns |     414.0 ns |    22.69 ns | 0.0161 |      - |     147 B |
| **RequestResponse** | **32**         | **True**    | **37,533.8 ns** | **125,905.0 ns** | **6,901.28 ns** | **0.3108** |      **-** |    **3662 B** |
| Stream100       | 32         | True    |  1,411.2 ns |   3,644.7 ns |   199.78 ns | 0.0229 |      - |     160 B |
| **RequestResponse** | **1024**       | **False**   | **43,163.6 ns** |  **51,231.1 ns** | **2,808.15 ns** | **0.9569** |      **-** |    **7660 B** |
| Stream100       | 1024       | False   |  3,143.3 ns |   8,154.1 ns |   446.96 ns | 0.3476 | 0.0535 |    2234 B |
| **RequestResponse** | **1024**       | **True**    | **49,300.1 ns** |  **73,250.2 ns** | **4,015.09 ns** | **0.9238** | **0.4619** |    **7639 B** |
| Stream100       | 1024       | True    |  4,124.0 ns |   9,158.1 ns |   501.99 ns | 0.3561 | 0.3264 |    2245 B |
