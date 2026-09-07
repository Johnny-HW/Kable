namespace Kable.Tests.Cases.Checksums;

using System;
using Kable.Core.Checksums;
using Xunit;

public class ChecksumsTests
{
    [Fact]
    public void ModbusCrc16_StandardVector_ShouldMatch()
    {
        // Modbus RTU 표준 테스트 프레임: Slave 1, Function 3, Start Addr 0, Points 10
        ReadOnlySpan<byte> frame = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };
        ushort crc = Crc16Modbus.Compute(frame);

        // 52677 = 0xCDD5 (Low: 0xD5, High: 0xCD)
        Assert.Equal(52677, crc);

        // Append 테스트
        Span<byte> txBuffer = stackalloc byte[frame.Length + 2];
        Crc16Modbus.Append(frame, txBuffer);
        Assert.Equal((byte)(crc & 0xFF), txBuffer[^2]); // Low byte
        Assert.Equal((byte)((crc >> 8) & 0xFF), txBuffer[^1]); // High byte

        // Validate 테스트
        Assert.True(Crc16Modbus.Validate(txBuffer));

        // 데이터 오염 시 실패 테스트
        txBuffer[0] = 0xFF;
        Assert.False(Crc16Modbus.Validate(txBuffer));
    }

    [Fact]
    public void Crc16Ccitt_StandardVector_ShouldMatch()
    {
        // "123456789" 표준 벡터
        ReadOnlySpan<byte> data = System.Text.Encoding.ASCII.GetBytes("123456789");
        ushort crc = Crc16Ccitt.Compute(data);

        // XModem 0x1021 (Init: 0x0000) -> 0x31C3
        Assert.Equal(0x31C3, crc);

        Span<byte> frameWithCrc = stackalloc byte[data.Length + 2];
        data.CopyTo(frameWithCrc);
        frameWithCrc[^2] = (byte)(crc >> 8);   // High byte
        frameWithCrc[^1] = (byte)(crc & 0xFF); // Low byte

        Assert.True(Crc16Ccitt.Validate(frameWithCrc));
    }

    [Fact]
    public void ModbusLrc_StandardVector_ShouldMatch()
    {
        // Modbus ASCII 예제 프레임: :010300000001F8\r\n -> 데이터 바이트 { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01 }
        ReadOnlySpan<byte> data = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x01 };
        byte lrc = IndustrialChecksums.ComputeModbusLrc(data);

        // Sum = 0x05, 2의 보수 = -5 = 0xFB
        Assert.Equal(0xFB, lrc);
    }

    [Fact]
    public void XorBcc_ShouldCalculateCorrectly()
    {
        ReadOnlySpan<byte> data = new byte[] { 0x02, 0x30, 0x31, 0x03 }; // STX, '0', '1', ETX
        byte bcc = IndustrialChecksums.ComputeXorBcc(data);

        // 0x02 ^ 0x30 = 0x32
        // 0x32 ^ 0x31 = 0x03
        // 0x03 ^ 0x03 = 0x00
        Assert.Equal(0x00, bcc);
    }

    [Fact]
    public void Sum8_And_TwosComplement_ShouldCalculateCorrectly()
    {
        ReadOnlySpan<byte> data = new byte[] { 0x10, 0x20, 0x05 };
        byte sum = IndustrialChecksums.ComputeSum8(data);
        byte twosComp = IndustrialChecksums.ComputeTwosComplementSum8(data);

        Assert.Equal(0x35, sum);
        Assert.Equal((byte)(0x100 - 0x35), twosComp);
    }
}
