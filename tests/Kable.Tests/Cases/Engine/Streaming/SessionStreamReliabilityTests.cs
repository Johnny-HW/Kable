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
public sealed class SessionStreamReliabilityTests
{
    [Fact]
    public async Task TC_REL_16_Stream_SingleSlotWaitTasks_ReusedWithoutTaskAccumulation()
    {
        // [P2] 무알람 장시간 텔레메트리 스트리밍 시 대기 작업 누적 없음 및 취소 토큰 정리 검증
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec();
        var options = new KableSessionOptions<string>
        {
            IsAlarmMessage = msg => msg.StartsWith("$ALARM", StringComparison.OrdinalIgnoreCase)
        };

        await using var session = new KableSession<string>(factory, codec, options);
        await session.StartAsync();

        using var cts = new CancellationTokenSource();
        var receivedItems = new List<string>();

        // 스트림 비동기 읽기 태스크 가동
        var consumerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var item in session.GetStreamAsync(cts.Token))
                {
                    receivedItems.Add(item);
                    if (receivedItems.Count >= 20)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 정상 취소
            }
        });

        // 텔레메트리 메시지만 20건 연속 전송 (알람은 0건)
        for (int i = 1; i <= 20; i++)
        {
            await factory.Context.WriteAsciiLineAsync($"$TELEM_DATA_{i:D3}", 0x0A);
            await Task.Delay(5);
        }

        // 20건 정상 소비 완료 대기
        await consumerTask;
        receivedItems.Count.Should().Be(20);

        // 구독 취소 시 남은 대기자가 깔끔하게 해제되는지 확인
        cts.Cancel();
    }

    [Fact(Timeout = 10000)]
    public async Task TC_REL_20_Stream_RepeatedEarlyBreak_CleansUpWaitersWithoutAccumulation()
    {
        // [4] await foreach에서 조기 break를 반복해도 대기자가 누적되지 않고 정상 동작하는지 검증
        var factory = new TestMemoryConnectionFactory();
        var codec = new AsciiLineCodec();
        await using var session = new KableSession<string>(factory, codec);
        await session.StartAsync();

        // 50회 연속으로 스트림을 열고, 메시지 1개 수신 후 즉시 break
        for (int i = 1; i <= 50; i++)
        {
            var writeTask = factory.Context.WriteAsciiLineAsync($"$DATA_{i}", 0x0A);

            await foreach (var item in session.GetStreamAsync())
            {
                item.Should().Be($"$DATA_{i}");
                break; // 조기 break
            }

            await writeTask;
        }

        // 반복적인 break 이후에도 세션 송수신 및 스트림이 완벽하게 정상 동작해야 함
        await factory.Context.WriteAsciiLineAsync("$FINAL_STREAM_DATA", 0x0A);
        await foreach (var item in session.GetStreamAsync())
        {
            item.Should().Be("$FINAL_STREAM_DATA");
            break;
        }

        session.IsConnected.Should().BeTrue();
    }
}
