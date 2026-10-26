# Connection lifecycle and recovery

Kable uses a single-lifetime session contract. Closed sessions are disposed and replaced
with new instances. Recovery is performed by an optional higher-level wrapper.
Start with [the usage guide](en/GETTING_STARTED.md) and [failure handling](en/ERROR_HANDLING.md).

## Why FIFO timeout closes the connection

A protocol without correlation IDs cannot distinguish a late reply to request A from
the reply to the next request B. Closing the FIFO connection after a transmitted request
times out or is canceled prevents late replies from being assigned to later requests.

| Situation | Exception | Connection / reuse |
| --- | --- | --- |
| Token already canceled before the request | `OperationCanceledException` | Session remains usable |
| Canceled or timed out while waiting for FIFO lock | Cancellation or `DeviceTimeoutException` | Session remains usable |
| FIFO timeout after transmission | `DeviceTimeoutException` | Closed; create a new client |
| FIFO cancellation after transmission | `OperationCanceledException` | Closed; create a new client |
| Physical disconnection | `DeviceDisconnectedException` for pending requests | Closed; create a new client |
| Correlated request timeout | `DeviceTimeoutException` | Other requests/session can continue |

Check actual equipment state before retrying commands. A failed wait does not prove the
command was never executed. CID generation and reuse must respect protocol late-response rules.
Ownership checks prevent an old request's cleanup from removing a newer request with the
same CID; they cannot identify the generation of an identical CID sent by the equipment.

## Starting and stopping

- Builder returns an unstarted session. Call `StartAsync` once to establish its connection.
- Simple Open and Profile Connect methods return started clients.
- Concurrent StartAsync calls share one connection attempt.
- Canceling a subsequent start caller cancels that caller's wait, not the shared attempt.
- The initial caller's token still applies to the actual connection attempt.
- Once a shared start failure is recorded, uncanceled callers receive the original exception.
  Session cleanup cancellation does not hide that outcome; explicitly canceled callers remain canceled.
- Stop/Dispose end the session lifetime. StartAsync is not a reconnect operation.
- Prefer `await using`/`DisposeAsync`; stop and await application-owned workers first.

## Optional reconnecting wrapper

`ReconnectingKableClient` uses a factory that returns a fresh, connected Simple client for
each attempt. The factory must honor cancellation. Connection deadlines are cooperative.

- Initial connection failure is returned from OpenAsync.
- Subsequent outages are checked at RetryDelay intervals and retried up to MaxAttempts.
- A successful recovery resets the retry count for the next outage.
- Wrapper event subscriptions are retained across replacement clients.
- Requests during recovery fail; commands are not queued or replayed.
- Reconnected means a new connection exists, not that the device operation succeeded.
- RecoveryTask completes on disposal or exhausted retries. Inspect LastError after failure.
- The OpenAsync token covers initial connection. DisposeAsync ends the wrapper's lifetime,
  cancels recovery and waits for its worker.

See [the complete recovery example](en/ERROR_HANDLING.md).

## Event handling

`LineReceived` runs on background processing paths and excludes replies consumed by queries.
Use an observer for complete traffic tracing and a UI dispatcher for UI changes.
Await QueryAsync to handle its failure. Do not synchronously wait for disposal inside an
event handler, because shutdown may need that handler's processing loop to return.
