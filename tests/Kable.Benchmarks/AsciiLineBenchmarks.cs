namespace Kable.Benchmarks;

using System.Buffers;
using System.Text;
using BenchmarkDotNet.Attributes;
using Kable.Codecs;

/// <summary>Encoding uses a reused output buffer; decoding necessarily creates a string.</summary>
[MemoryDiagnoser]
public class AsciiLineBenchmarks
{
    [Params(32, 1024)] public int Characters { get; set; }
    [Params(false, true)] public bool Utf8 { get; set; }
    private AsciiLineCodec _codec = null!;
    private ArrayBufferWriter<byte> _output = null!;
    private string _message = null!;
    private ReadOnlySequence<byte> _frame;

    [GlobalSetup]
    public void Setup()
    {
        var encoding = Utf8 ? Encoding.UTF8 : Encoding.ASCII;
        _codec = new AsciiLineCodec(encoding: encoding);
        _message = new string(Utf8 ? '온' : 'A', Characters);
        _output = new ArrayBufferWriter<byte>(encoding.GetByteCount(_message) + 1);
        _frame = new ReadOnlySequence<byte>(encoding.GetBytes(_message + "\n"));
    }

    [Benchmark]
    public int Encode()
    {
        _output.Clear();
        _codec.Encode(_message, _output);
        return _output.WrittenCount;
    }

    [Benchmark]
    public string Decode()
    {
        var frame = _frame;
        _codec.TryDecode(ref frame, out var message);
        return message;
    }
}
