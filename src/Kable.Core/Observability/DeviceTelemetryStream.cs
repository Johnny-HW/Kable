namespace Kable.Observability;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;

/// <summary>
/// 락프리 및 완전한 약한 결합(Weak Coupling)을 보장하는 장치 텔레메트리/상태 브로드캐스팅 스트림.
/// C# 원시 이벤트(event EventHandler)의 강한 참조 메모리 누수와 스레드 마샬링 병목을 원천 차단합니다.
/// 소비자가 느릴 경우 자동으로 오래된 상태를 버리고(DropOldest) 최신 상태만 유지(Conflation)합니다.
/// </summary>
/// <typeparam name="T">텔레메트리 또는 장비 상태 데이터 모델</typeparam>
public sealed class DeviceTelemetryStream<T> : IDisposable
{
    private readonly int _bufferCapacity;
    private readonly ConcurrentDictionary<Guid, Channel<T>> _subscribers = new();
    private int _isDisposed;

    public int SubscriberCount => _subscribers.Count;

    public DeviceTelemetryStream(int bufferCapacity = 100)
    {
        _bufferCapacity = Math.Max(1, bufferCapacity);
    }

    /// <summary>
    /// 새로운 상태 또는 텔레메트리 데이터를 등록된 모든 구독자에게 락프리로 발행합니다.
    /// 구독자가 없거나 버퍼가 가득 차면 즉시 논블로킹으로 처리됩니다.
    /// </summary>
    public void Publish(T data)
    {
        if (_isDisposed != 0 || _subscribers.IsEmpty) return;

        foreach (var kvp in _subscribers)
        {
            kvp.Value.Writer.TryWrite(data);
        }
    }

    /// <summary>
    /// 약한 결합 방식으로 상태 스트림을 구독합니다.
    /// 열거가 중단되거나 CancellationToken이 취소되면 구독 리소스는 자동으로 정리됩니다.
    /// </summary>
    public async IAsyncEnumerable<T> Subscribe([EnumeratorCancellation] CancellationToken ct = default)
    {
        if (_isDisposed != 0) yield break;

        var subId = Guid.NewGuid();
        var options = new BoundedChannelOptions(_bufferCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };
        var channel = Channel.CreateBounded<T>(options);
        _subscribers.TryAdd(subId, channel);

        try
        {
            var reader = channel.Reader;
            while (!ct.IsCancellationRequested && await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (reader.TryRead(out var item))
                {
                    yield return item;
                }
            }
        }
        finally
        {
            _subscribers.TryRemove(subId, out _);
            channel.Writer.TryComplete();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            foreach (var kvp in _subscribers)
            {
                kvp.Value.Writer.TryComplete();
            }
            _subscribers.Clear();
        }
    }
}
