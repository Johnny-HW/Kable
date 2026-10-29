namespace Kable.Engine;

using System;
using System.Threading;
using Kable.Exceptions;

public sealed partial class KableSession<TMessage>
{
    private CancellationTokenSource CreateRequestDeadline(TimeSpan timeout)
    {
#if NET8_0_OR_GREATER
        return new CancellationTokenSource(timeout, _timeProvider);
#else
        return new CancellationTokenSource(timeout);
#endif
    }

    private static DeviceTimeoutException CreateTimeoutError(TMessage request, TimeSpan timeout)
    {
        string command = "UnknownCommand";
        Exception? formattingError = null;
        try { command = request?.ToString() ?? command; }
        catch (Exception error) { formattingError = error; }
        var result = new DeviceTimeoutException(command, timeout);
        if (formattingError != null) result.Data["CommandFormattingException"] = formattingError;
        return result;
    }
}
