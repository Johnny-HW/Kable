namespace Kable.Tests.Cases.Configuration;

using System;
using System.IO.Ports;
using Kable.Codecs;
using Kable.Configuration;
using Kable.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class KableDeviceOptionsTests
{
    [Fact]
    public void UseOptions_Tcp_BuildsSessionSuccessfully()
    {
        var options = new KableDeviceOptions
        {
            DeviceId = "ALIGNER_01",
            Transport = "Tcp",
            Host = "192.168.1.50",
            Port = 8080,
            TimeoutMs = 5000
        };

        var session = new KableClientBuilder<string>()
            .UseOptions(options)
            .UseCodec(new AsciiLineCodec())
            .Build();

        Assert.NotNull(session);
    }

    [Fact]
    public void UseOptions_Serial_BuildsSessionSuccessfully()
    {
        var options = new KableDeviceOptions
        {
            DeviceId = "ROBOT_01",
            Transport = "Serial",
            PortName = "COM4",
            BaudRate = 115200,
            Parity = "Even",
            DataBits = 7,
            StopBits = "Two"
        };

        Assert.Equal(Parity.Even, options.GetParity());
        Assert.Equal(StopBits.Two, options.GetStopBits());

        var session = new KableClientBuilder<string>()
            .WithOptions(options)
            .UseCodec(new AsciiLineCodec())
            .Build();

        Assert.NotNull(session);
    }

    [Fact]
    public void UseOptions_NamedPipe_BuildsSessionSuccessfully()
    {
        var options = new KableDeviceOptions
        {
            DeviceId = "IPC_01",
            Transport = "NamedPipe",
            PipeName = "test_efem_pipe"
        };

        var session = new KableClientBuilder<string>()
            .UseOptions(options)
            .UseCodec(new AsciiLineCodec())
            .Build();

        Assert.NotNull(session);
    }

    [Fact]
    public void UseOptions_Simulator_BuildsSessionSuccessfully()
    {
        var options = new KableDeviceOptions
        {
            DeviceId = "SIM_01",
            Transport = "Simulator"
        };

        var session = new KableClientBuilder<string>()
            .UseOptions(options)
            .UseCodec(new AsciiLineCodec())
            .Build();

        Assert.NotNull(session);
    }

    [Fact]
    public void UseOptions_InvalidTransport_ThrowsNotSupportedException()
    {
        var options = new KableDeviceOptions
        {
            Transport = "UnknownTransport"
        };

        Assert.Throws<NotSupportedException>(() =>
        {
            new KableClientBuilder<string>()
                .UseOptions(options)
                .UseCodec(new AsciiLineCodec())
                .Build();
        });
    }

    [Fact]
    public void AddKableSession_WithOptions_RegistersInServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddKable();

        var options = new KableDeviceOptions
        {
            DeviceId = "DI_DEVICE",
            Transport = "Simulator"
        };

        services.AddKableSession<string>(options, sp => new AsciiLineCodec());

        using var sp = services.BuildServiceProvider();
        var session = sp.GetService<Kable.Engine.IDeviceSession<string>>();

        Assert.NotNull(session);
    }
}
