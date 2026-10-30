# Fault comparison observations

raw = existing sample adapters, NOT exhaustive native-library testing. guarded = identical application policy for every library, including Kable. NotTested does not mean failure.

No winner score. SafeRejected preserves correctness by sacrificing reuse; CorrectReply preserves both. Caller cancellation deadline is not native timeout behavior. With 20 samples, p99 is the maximum. Latency excludes disposal, which is recorded separately.

| Mode | Library | Scenario | Pass/tested | Outcomes | p95 ms | p99 ms | max cleanup ms | max deadline overshoot ms |
|---|---|---|---:|---|---:|---:|---:|---:|
| guarded | DotNet | cancel-inflight | 20/20 | Pass=20 | 0.117 | 1.135 | 0.041 | 0.000 |
| guarded | DotNet | deadline | 20/20 | Pass=20 | 113.872 | 125.886 | 0.146 | 25.886 |
| guarded | DotNet | disconnect | 20/20 | Pass=20 | 0.324 | 0.508 | 0.310 | 0.000 |
| guarded | DotNet | late-reply | 20/20 | SafeRejected=20 | 0.266 | 0.274 | 0.426 | 0.000 |
| guarded | DotNet | pre-cancel | 20/20 | Pass=20 | 0.068 | 2.226 | 0.697 | 0.000 |
| guarded | DotNet | queued-disconnect | 20/20 | Pass=20 | 0.531 | 1.101 | 0.590 | 0.000 |
| guarded | DotNetty | cancel-inflight | 20/20 | Pass=20 | 0.548 | 1.293 | 1.053 | 0.000 |
| guarded | DotNetty | deadline | 20/20 | Pass=20 | 109.889 | 109.926 | 0.436 | 9.926 |
| guarded | DotNetty | disconnect | 20/20 | Pass=20 | 0.555 | 1.750 | 0.101 | 0.000 |
| guarded | DotNetty | late-reply | 20/20 | SafeRejected=20 | 0.030 | 0.033 | 0.127 | 0.000 |
| guarded | DotNetty | pre-cancel | 20/20 | Pass=20 | 0.058 | 0.940 | 4.254 | 0.000 |
| guarded | DotNetty | queued-disconnect | 20/20 | Pass=20 | 0.285 | 0.652 | 0.074 | 0.000 |
| guarded | Kable | cancel-inflight | 20/20 | Pass=20 | 0.328 | 1.363 | 0.209 | 0.000 |
| guarded | Kable | deadline | 20/20 | Pass=20 | 109.805 | 111.472 | 14.502 | 11.472 |
| guarded | Kable | disconnect | 20/20 | Pass=20 | 0.097 | 0.473 | 0.135 | 0.000 |
| guarded | Kable | late-reply | 20/20 | SafeRejected=20 | 0.138 | 0.198 | 0.083 | 0.000 |
| guarded | Kable | pre-cancel | 20/20 | Pass=20 | 0.042 | 2.268 | 6.922 | 0.000 |
| guarded | Kable | queued-disconnect | 20/20 | Pass=20 | 0.327 | 0.513 | 0.033 | 0.000 |
| guarded | SuperSocket | cancel-inflight | 20/20 | Pass=20 | 0.116 | 0.871 | 0.261 | 0.000 |
| guarded | SuperSocket | deadline | 20/20 | Pass=20 | 109.886 | 110.211 | 15.020 | 10.211 |
| guarded | SuperSocket | disconnect | 20/20 | Pass=20 | 0.189 | 0.495 | 0.005 | 0.000 |
| guarded | SuperSocket | late-reply | 20/20 | SafeRejected=20 | 0.046 | 0.048 | 0.223 | 0.000 |
| guarded | SuperSocket | pre-cancel | 20/20 | Pass=20 | 0.029 | 2.291 | 5.145 | 0.000 |
| guarded | SuperSocket | queued-disconnect | 20/20 | Pass=20 | 0.371 | 0.657 | 0.003 | 0.000 |
| guarded | TouchSocket | cancel-inflight | 20/20 | Pass=20 | 0.162 | 1.133 | 1014.256 | 0.000 |
| guarded | TouchSocket | deadline | 20/20 | Pass=20 | 107.964 | 108.726 | 1015.096 | 8.726 |
| guarded | TouchSocket | disconnect | 20/20 | Pass=20 | 0.129 | 0.813 | 0.050 | 0.000 |
| guarded | TouchSocket | late-reply | 20/20 | SafeRejected=20 | 0.072 | 0.082 | 0.383 | 0.000 |
| guarded | TouchSocket | pre-cancel | 20/20 | Pass=20 | 0.081 | 1.986 | 11.195 | 0.000 |
| guarded | TouchSocket | queued-disconnect | 20/20 | Pass=20 | 0.315 | 0.483 | 0.019 | 0.000 |
| raw | DotNet | cancel-inflight | 20/20 | Pass=20 | 0.193 | 0.617 | 0.046 | 0.000 |
| raw | DotNet | deadline | 20/20 | Pass=20 | 112.936 | 122.994 | 0.169 | 22.994 |
| raw | DotNet | disconnect | 20/20 | Pass=20 | 0.174 | 0.449 | 0.088 | 0.000 |
| raw | DotNet | late-reply | 0/20 | WrongReply=20 | 0.036 | 0.041 | 0.097 | 0.000 |
| raw | DotNet | pre-cancel | 20/20 | Pass=20 | 0.053 | 2.284 | 0.353 | 0.000 |
| raw | DotNet | queued-disconnect | 0/0 | NotTested=20 | 0.000 | 0.000 | 0.000 | 0.000 |
| raw | DotNetty | cancel-inflight | 20/20 | Pass=20 | 0.618 | 1.980 | 1.010 | 0.000 |
| raw | DotNetty | deadline | 20/20 | Pass=20 | 111.059 | 121.744 | 0.399 | 21.744 |
| raw | DotNetty | disconnect | 20/20 | Pass=20 | 0.217 | 1.750 | 0.012 | 0.000 |
| raw | DotNetty | late-reply | 0/20 | WrongReply=20 | 0.028 | 0.039 | 0.105 | 0.000 |
| raw | DotNetty | pre-cancel | 20/20 | Pass=20 | 0.060 | 0.890 | 3.350 | 0.000 |
| raw | DotNetty | queued-disconnect | 0/0 | NotTested=20 | 0.000 | 0.000 | 0.000 | 0.000 |
| raw | Kable | cancel-inflight | 20/20 | Pass=20 | 0.408 | 1.200 | 0.060 | 0.000 |
| raw | Kable | deadline | 20/20 | Pass=20 | 110.817 | 115.445 | 0.138 | 15.445 |
| raw | Kable | disconnect | 20/20 | Pass=20 | 0.314 | 0.642 | 0.092 | 0.000 |
| raw | Kable | late-reply | 20/20 | SafeRejected=20 | 0.100 | 0.143 | 0.101 | 0.000 |
| raw | Kable | pre-cancel | 20/20 | Pass=20 | 0.033 | 3.485 | 8.313 | 0.000 |
| raw | Kable | queued-disconnect | 0/0 | NotTested=20 | 0.000 | 0.000 | 0.000 | 0.000 |
| raw | SuperSocket | cancel-inflight | 20/20 | Pass=20 | 0.160 | 1.661 | 0.271 | 0.000 |
| raw | SuperSocket | deadline | 20/20 | Pass=20 | 109.232 | 109.919 | 0.362 | 9.919 |
| raw | SuperSocket | disconnect | 20/20 | Pass=20 | 0.091 | 0.904 | 0.005 | 0.000 |
| raw | SuperSocket | late-reply | 0/20 | WrongReply=20 | 0.037 | 0.037 | 0.296 | 0.000 |
| raw | SuperSocket | pre-cancel | 20/20 | Pass=20 | 0.069 | 2.135 | 4.881 | 0.000 |
| raw | SuperSocket | queued-disconnect | 0/0 | NotTested=20 | 0.000 | 0.000 | 0.000 | 0.000 |
| raw | TouchSocket | cancel-inflight | 20/20 | Pass=20 | 0.174 | 0.811 | 1015.488 | 0.000 |
| raw | TouchSocket | deadline | 20/20 | Pass=20 | 111.109 | 114.805 | 1015.042 | 14.805 |
| raw | TouchSocket | disconnect | 20/20 | Pass=20 | 0.238 | 0.390 | 0.017 | 0.000 |
| raw | TouchSocket | late-reply | 0/20 | WrongReply=20 | 0.026 | 0.085 | 0.478 | 0.000 |
| raw | TouchSocket | pre-cancel | 20/20 | Pass=20 | 0.064 | 1.544 | 9.074 | 0.000 |
| raw | TouchSocket | queued-disconnect | 0/0 | NotTested=20 | 0.000 | 0.000 | 0.000 | 0.000 |

## Resources after repeated canceled connections

Full GC and 100ms settle between checkpoints. Process-local measurements include server/harness. Deltas are observations, not proof of leaks or leak freedom.

| Mode | Library | Cycles | managed delta B | private delta B | handle delta | thread delta |
|---|---|---:|---:|---:|---:|---:|
| guarded | DotNet | 100 | -7648 | 598016 | 1 | 0 |
| guarded | DotNetty | 100 | 8960 | 155648 | 1 | 0 |
| guarded | Kable | 100 | 13888 | -438272 | 0 | 0 |
| guarded | SuperSocket | 100 | 12888 | 233472 | 1 | 0 |
| guarded | TouchSocket | 100 | -6504 | 294912 | -6 | 1 |
| raw | DotNet | 100 | -7624 | -2527232 | 2 | 0 |
| raw | DotNetty | 100 | 2480 | 933888 | 3 | 0 |
| raw | Kable | 100 | 23680 | -1167360 | 1 | 0 |
| raw | SuperSocket | 100 | 448 | -294912 | 0 | 0 |
| raw | TouchSocket | 100 | -9280 | -204800 | -9 | 0 |
