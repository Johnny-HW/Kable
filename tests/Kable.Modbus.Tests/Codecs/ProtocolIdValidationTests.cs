namespace Kable.Modbus.Tests.Codecs;

using System.Buffers;
using Kable.Exceptions;
using Kable.Modbus.Codecs;
using Xunit;

public sealed class ProtocolIdValidationTests
{
    [Fact]
    public void InvalidProtocolIdIsRejectedBeforeRouting()
    {
        var sequence = new ReadOnlySequence<byte>(new byte[] { 0, 1, 0, 1, 0, 2, 1, 3 });
        Assert.Throws<ProtocolViolationException>(() => new ModbusTcpCodec().TryDecode(ref sequence, out _));
    }
}
