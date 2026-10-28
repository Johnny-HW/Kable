namespace Kable.Tests.Cases.Engine.Server;

using System;
using System.Buffers;
using FluentAssertions;
using Kable.Builder;
using Kable.Codecs;
using Kable.Tests.Fixtures;
using Xunit;

public sealed class KableServerBuilderTests
{
    [Fact]
    public async System.Threading.Tasks.Task BuildTwice_IsRejected()
    {
        var builder = new KableServerBuilder<string>()
            .WithListener(new TestMemoryConnectionListener()).UseCodec(() => new DummyCodec());
        await using var server = builder.Build();
        Action build = () => builder.Build();
        build.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async System.Threading.Tasks.Task ListenTcp_DoesNotBindUntilBuild()
    {
        using var occupied = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        occupied.Start();
        int port = ((System.Net.IPEndPoint)occupied.LocalEndpoint).Port;
        var builder = new KableServerBuilder<string>().ListenTcp(System.Net.IPAddress.Loopback, port)
            .UseCodec(() => new DummyCodec());
        occupied.Stop();
        await using var server = builder.Build();
    }

    private sealed class DummyCodec : IProtocolCodec<string>
    {
        public bool SupportsCorrelationId => false;
        public bool TryDecode(ref ReadOnlySequence<byte> buffer, out string message) { message = ""; return false; }
        public void Encode(string message, IBufferWriter<byte> output) { }
        public string? ExtractCorrelationId(string message) => null;
        public bool IsAutonomousMessage(string message) => true;
    }

    [Fact]
    public void Build_WithoutListener_ThrowsInvalidOperationException()
    {
        var builder = new KableServerBuilder<string>()
            .UseCodec(() => new DummyCodec());

        var act = () => builder.Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*No listener configured*");
    }

    [Fact]
    public void Build_WithoutCodec_ThrowsInvalidOperationException()
    {
        var builder = new KableServerBuilder<string>()
            .WithListener(new TestMemoryConnectionListener());

        var act = () => builder.Build();
        act.Should().Throw<InvalidOperationException>().WithMessage("*No codec factory configured*");
    }

    [Fact]
    public void Build_WithValidConfiguration_ReturnsKableServerInstance()
    {
        var server = new KableServerBuilder<string>()
            .WithListener(new TestMemoryConnectionListener())
            .UseCodec(() => new DummyCodec())
            .ConfigureOptions(opt => opt.MaxConnections = 500)
            .Build();

        server.Should().NotBeNull();
        server.IsRunning.Should().BeFalse();
    }
}
