namespace Kable.Modbus.Rtu;

using System.IO.Ports;

public sealed class ModbusRtuOptions
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public Parity Parity { get; set; } = Parity.Even;
    public int DataBits { get; set; } = 8;
    public StopBits StopBits { get; set; } = StopBits.One;
    public byte DefaultUnitId { get; set; } = 1;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
    /// <summary>Additional minimum gap; cannot shorten the baud-rate-derived protocol minimum.</summary>
    public TimeSpan InterFrameDelay { get; set; } = TimeSpan.Zero;

    internal TimeSpan GetFrameDelay()
    {
        if (string.IsNullOrWhiteSpace(PortName)) throw new ArgumentException("Port name is required.", nameof(PortName));
        if (BaudRate <= 0) throw new ArgumentOutOfRangeException(nameof(BaudRate));
        if (DataBits != 8) throw new ArgumentOutOfRangeException(nameof(DataBits), "RTU requires eight data bits.");
        if (Parity is not (Parity.None or Parity.Even or Parity.Odd) || StopBits is not (StopBits.One or StopBits.Two))
            throw new ArgumentException("Invalid serial parity or stop bits.");
        if (Parity == Parity.None && StopBits != StopBits.Two)
            throw new ArgumentException("RTU without parity requires two stop bits.");
        ValidateUnit(DefaultUnitId);
        ValidateTimeout(Timeout);
        if (InterFrameDelay < TimeSpan.Zero || InterFrameDelay.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(InterFrameDelay));
        int bits = 1 + DataBits + (Parity == Parity.None ? 0 : 1) + (StopBits == StopBits.Two ? 2 : 1);
        double milliseconds = BaudRate > 19200 ? 1.75 : 3500.0 * bits / BaudRate;
        return TimeSpan.FromMilliseconds(Math.Max(milliseconds, InterFrameDelay.TotalMilliseconds));
    }

    internal static void ValidateUnit(byte unit)
    {
        if (unit is < 1 or > 247) throw new ArgumentOutOfRangeException(nameof(unit), "RTU unicast address must be 1..247.");
    }
    internal static void ValidateTimeout(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero || timeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(timeout));
    }
}
