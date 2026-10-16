namespace Kable.Tests.Fixtures.Protocols;

using System;
using System.Buffers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.Codecs;
using Kable.Core;
using Kable.Tests.Fixtures;

internal static class SoakLineReader
{
    public static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out string line)
    {
        var pos = buffer.PositionOf((byte)0x0A);
        if (pos == null)
        {
            line = string.Empty;
            return false;
        }

        var slice = buffer.Slice(0, pos.Value);
        line = Encoding.ASCII.GetString(BuffersExtensions.ToArray(slice));
        buffer = buffer.Slice(buffer.GetPosition(1, pos.Value));
        return true;
    }
}
