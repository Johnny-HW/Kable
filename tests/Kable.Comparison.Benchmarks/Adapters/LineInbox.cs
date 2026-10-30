using System.Text;
using System.Threading.Channels;
namespace Kable.Comparison.Adapters;

// Test-only FIFO mailbox: exactly one outstanding request per connection.
internal sealed class LineInbox
{
    private readonly Channel<string> _lines = Channel.CreateBounded<string>(16);
    private readonly byte[] _buffer = new byte[65536];
    private int _length;
    public void Receive(ReadOnlySpan<byte> bytes)
    {
        foreach (byte b in bytes)
        {
            if (b == 10)
            {
                Put(Encoding.ASCII.GetString(_buffer, 0, _length));
                _length = 0;
            }
            else
            {
                if (_length == _buffer.Length) throw new InvalidDataException("Frame too large");
                _buffer[_length++] = b;
            }
        }
    }
    public void Put(string line)
    {
        if (!_lines.Writer.TryWrite(line)) throw new InvalidDataException("Unexpected excess responses");
    }
    public Task<string> ReadAsync(CancellationToken ct) => _lines.Reader.ReadAsync(ct).AsTask();
    public void Close(Exception? error = null) => _lines.Writer.TryComplete(error);
}
