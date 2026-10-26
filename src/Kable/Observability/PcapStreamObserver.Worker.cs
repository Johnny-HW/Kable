namespace Kable.Observability;

using System;
using System.Threading.Tasks;

public sealed partial class PcapStreamObserver
{
    private void FailLocked(Exception error)
    {
        _lastError ??= error;
        _stopping = true;
        _recording.Writer.TryComplete();
    }
    private async Task RecordAsync()
    {
        try
        {
            while (await _recording.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_recording.Reader.TryRead(out var record))
                {
                    if (LastError != null) break;
                    await _writer.WriteCapturedPacketAsync(record.Payload, record.OriginalLength,
                        record.TimestampUtc).ConfigureAwait(false);
                }
                if (LastError != null) break;
            }
        }
        catch (Exception ex) { lock (_sync) FailLocked(ex); }
        finally
        {
            lock (_sync)
            {
                _stopping = true;
                _recording.Writer.TryComplete();
            }
            while (_recording.Reader.TryRead(out _)) { }
            try { await _writer.DisposeAsync().ConfigureAwait(false); }
            catch (Exception ex) { lock (_sync) FailLocked(ex); }
            _commands.Writer.TryComplete(LastError);
            _periodic.Writer.TryComplete(LastError);
            _alarms.Writer.TryComplete(LastError);
        }
        if (LastError is { } error) throw error;
    }
    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            _stopping = true;
            _recording.Writer.TryComplete();
            return new ValueTask(_disposeTask ??= _worker);
        }
    }
}
