namespace Kable.Transports.Simulators;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;

/// <summary>
/// 실물 하드웨어 장비 없이도 상위 시퀀스를 개발 및 검증할 수 있는 모의 장비 시뮬레이터.
/// 커맨드 패턴 매칭, 응답 지연(Latency), 에러 주입 등을 지원합니다.
/// </summary>
public sealed class MockHardwareSimulator : IAsyncDisposable
{
    private readonly InMemoryConnectionContext _serverContext;
    private readonly List<Func<string, string?>> _textRules = new();
    private readonly ConcurrentDictionary<string, string> _exactMatches = new(StringComparer.OrdinalIgnoreCase);
    private TimeSpan _simulatedLatency = TimeSpan.Zero;
    private readonly CancellationTokenSource _cts = new();
    private Task? _processingLoopTask;

    public MockHardwareSimulator(InMemoryConnectionContext serverContext)
    {
        _serverContext = serverContext ?? throw new ArgumentNullException(nameof(serverContext));
    }

    /// <summary>
    /// 특정 문자열 커맨드가 들어왔을 때 고정 응답을 매핑합니다.
    /// </summary>
    public MockHardwareSimulator OnCommand(string command, string response)
    {
        _exactMatches[command.Trim()] = response;
        return this;
    }

    /// <summary>
    /// 커맨드 텍스트 기반 동적 처리 규칙을 등록합니다.
    /// </summary>
    public MockHardwareSimulator OnRule(Func<string, string?> rule)
    {
        _textRules.Add(rule);
        return this;
    }

    /// <summary>
    /// 모의 하드웨어 응답 지연 시간(기구 이동/센싱 시간 모사)을 설정합니다.
    /// </summary>
    public MockHardwareSimulator WithLatency(TimeSpan latency)
    {
        _simulatedLatency = latency;
        return this;
    }

    /// <summary>
    /// 시뮬레이터 수신 및 응답 처리 루프를 가동합니다.
    /// </summary>
    public void Start()
    {
        _processingLoopTask = Task.Run(ProcessLoopAsync);
    }

    private async Task ProcessLoopAsync()
    {
        var reader = _serverContext.Input;
        var writer = _serverContext.Output;
        var token = _cts.Token;

        try
        {
            while (!token.IsCancellationRequested && !_serverContext.ConnectionClosed.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(token);
                var buffer = result.Buffer;

                while (TryReadLine(ref buffer, out var lineBytes))
                {
                    string command = Encoding.ASCII.GetString(lineBytes).Trim();
                    string? response = MatchResponse(command);

                    if (response != null)
                    {
                        if (_simulatedLatency > TimeSpan.Zero)
                        {
                            await Task.Delay(_simulatedLatency, token);
                        }

                        byte[] respBytes = Encoding.ASCII.GetBytes(response + "\r\n");
                        await writer.WriteAsync(respBytes, token);
                    }
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted || result.IsCanceled)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    private string? MatchResponse(string command)
    {
        if (_exactMatches.TryGetValue(command, out var match))
        {
            return match;
        }

        foreach (var rule in _textRules)
        {
            var res = rule(command);
            if (res != null) return res;
        }

        // 기본 에코(Echo) 응답
        return $"ECHO:{command}";
    }

    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out byte[] line)
    {
        var position = buffer.PositionOf((byte)'\n');
        if (position == null)
        {
            line = Array.Empty<byte>();
            return false;
        }

        line = buffer.Slice(0, position.Value).ToArray();
        buffer = buffer.Slice(buffer.GetPosition(1, position.Value));
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_processingLoopTask != null)
        {
            try { await _processingLoopTask; } catch { }
        }
        await _serverContext.DisposeAsync();
        _cts.Dispose();
    }
}
