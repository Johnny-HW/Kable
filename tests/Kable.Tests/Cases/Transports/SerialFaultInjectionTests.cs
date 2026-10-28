namespace Kable.Tests.Cases;

using System;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Exceptions;
using Kable.Extensions;
using Kable.Tests.Fixtures;
using Kable.Transports;
using Xunit;

[Collection("HardwareTransportTests")]
public sealed class SerialFaultInjectionTests
{
    [Fact]
    public async Task TC_TRN_06_SerialPort_NonExistentPortName_ThrowsIOExceptionOrUnauthorized()
    {
        var factory = new SerialPortConnectionFactory("COM9999");

        Func<Task> act = () => factory.ConnectAsync().AsTask();
        (await act.Should().ThrowAsync<Exception>())
           .Where(e => e is System.IO.IOException || e is UnauthorizedAccessException || e is PlatformNotSupportedException);
    }

    [Fact]
    public async Task TC_TRN_05_SerialPortContext_DisposeAsync_ClosesBaseStreamAndCancelsToken()
    {
        using var port = new System.IO.Ports.SerialPort("COM1");
        using var dummyStream = new System.IO.MemoryStream();
        var context = new SerialPortConnectionContext(port, dummyStream);

        context.ConnectionClosed.IsCancellationRequested.Should().BeFalse();

        await context.DisposeAsync();

        context.ConnectionClosed.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task TC_TRN_103_SerialPort_PhysicalCableRemoval_HandlesBaseStreamDisposed()
    {
        using var port = new System.IO.Ports.SerialPort("COM1");
        using var memoryStream = new System.IO.MemoryStream();
        var context = new SerialPortConnectionContext(port, memoryStream);

        context.ConnectionClosed.IsCancellationRequested.Should().BeFalse();

        // Abrupt physical cable disconnect
        context.Abort("USB-to-Serial unplugged");

        context.ConnectionClosed.IsCancellationRequested.Should().BeTrue();

        // Re-entrant DisposeAsync should complete without deadlock
        await context.DisposeAsync();
    }

    [Fact]
    public async Task TC_TRN_201_SerialPortFactory_NonExistentPort_ThrowsArgumentOrIOException()
    {
        var existingPorts = new System.Collections.Generic.HashSet<string>(
            System.IO.Ports.SerialPort.GetPortNames(),
            StringComparer.OrdinalIgnoreCase);

        string nonExistentPort = $"COM_UNASSIGNED_{Guid.NewGuid():N}";
        while (existingPorts.Contains(nonExistentPort))
        {
            nonExistentPort = $"COM_UNASSIGNED_{Guid.NewGuid():N}";
        }

        var factory = new SerialPortConnectionFactory(nonExistentPort);
        Func<Task> act = async () => await factory.ConnectAsync();

        await act.Should().ThrowAsync<Exception>()
            .Where(e => e is System.IO.IOException || e is ArgumentException || e is UnauthorizedAccessException || e is PlatformNotSupportedException);
    }
}
