namespace Kable.Tests.Cases;

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Kable.Codecs;
using Kable.Core;
using Kable.Engine;
using Kable.Exceptions;
using Kable.Tests.Fixtures;
using Kable.Tests.Fixtures.Codecs;
using Kable.Tests.Fixtures.Engine;
using Xunit;

[Collection("HardwareTransportTests")]
public sealed class SessionRequestReliabilityTests
{
    [Fact]
    public async Task TC_REL_01_StrictFifo_ResponseIsolation_AfterTimeout()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec(delimiter: 0x0A);
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        // 1. 요청 A 발송 및 50ms 타임아웃 유도
        Func<Task> actTimeout = async () =>
            await session.RequestAsync<string>("REQ_A", TimeSpan.FromMilliseconds(50));

        await actTimeout.Should().ThrowAsync<DeviceTimeoutException>();

        // 타임아웃 발생 시 장비 프로토콜 정합성을 위해 연결이 단절되어야 함
        session.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task TC_REL_02_FullDuplex_DuplicateCorrelationId_ThrowsImmediately()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new CorrelationIdLineCodec();
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        var task1 = session.RequestAsync<string>("CID_DUP:REQ_1", TimeSpan.FromSeconds(5));

        // 동일한 CID로 동시에 두 번째 요청 시도시 즉각 예외 발생
        Func<Task> actDup = async () =>
            await session.RequestAsync<string>("CID_DUP:REQ_2", TimeSpan.FromSeconds(5));

        await actDup.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Duplicate in-flight Correlation ID*");

        // 첫 번째 대기자는 손상 없이 응답을 정상 수신해야 함
        await factory.Context.WriteAsciiLineAsync("CID_DUP:RESP_1", 0x0A);
        var res1 = await task1;
        res1.Should().Be("CID_DUP:RESP_1");
    }

    [Fact]
    public async Task TC_REL_04_Cancellation_UnsentQueueItems_CleanlyFaultedOnDisconnect()
    {
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec();
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        // 연결 컨텍스트 Abort
        factory.Context.Abort("Test Force Disconnect");

        // 연결 단절 후 발송 시도 시 DeviceDisconnectedException 발생 검증
        Func<Task> act = async () =>
            await session.RequestAsync<string>("REQ_AFTER_DISCONNECT", TimeSpan.FromSeconds(1));

        await act.Should().ThrowAsync<DeviceDisconnectedException>();
    }
}
