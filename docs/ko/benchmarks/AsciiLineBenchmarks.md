```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.6199/23H2/2023Update/SunValley3)
Intel Core 7 150U, 1 CPU, 12 logical and 10 physical cores
.NET SDK 10.0.301
  [Host]     : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2
  Job-XDMRJR : .NET 10.0.9 (10.0.926.27113), X64 RyuJIT AVX2

IterationCount=3  IterationTime=100ms  LaunchCount=1  
WarmupCount=1  

```
| Method | Characters | Utf8  | Mean         | Error      | StdDev     | Gen0   | Allocated |
|------- |----------- |------ |-------------:|-----------:|-----------:|-------:|----------:|
| **Encode** | **32**         | **False** |     **8.630 ns** |  **16.602 ns** |  **0.9100 ns** |      **-** |         **-** |
| Decode | 32         | False |    45.738 ns |  76.478 ns |  4.1920 ns | 0.0190 |     120 B |
| **Encode** | **32**         | **True**  |    **43.749 ns** |  **26.473 ns** |  **1.4511 ns** |      **-** |         **-** |
| Decode | 32         | True  |   101.759 ns |  81.772 ns |  4.4822 ns | 0.0187 |     120 B |
| **Encode** | **1024**       | **False** |    **40.119 ns** |   **8.364 ns** |  **0.4585 ns** |      **-** |         **-** |
| Decode | 1024       | False |   169.926 ns | 572.614 ns | 31.3869 ns | 0.3353 |    2104 B |
| **Encode** | **1024**       | **True**  |   **880.155 ns** | **195.859 ns** | **10.7357 ns** |      **-** |         **-** |
| Decode | 1024       | True  | 1,812.852 ns | 337.208 ns | 18.4835 ns | 0.3191 |    2104 B |
