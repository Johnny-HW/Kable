namespace Kable.Core;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// 1:N 서버에 접속된 개별 원격 클라이언트 세션 제어 계약
/// </summary>
public interface IServerSession<TMessage> : IAsyncDisposable
{
    string ConnectionId { get; }
    string EndpointDescription { get; }
    bool IsConnected { get; }
    ValueTask SendAsync(TMessage message, CancellationToken ct = default);
    void Disconnect(string reason);
}

/// <summary>
/// 브로드캐스트 전송 결과 정보
/// </summary>
public readonly record struct BroadcastResult(
    int TotalSessions,
    int EnqueuedCount,
    IReadOnlyList<string> FailedConnectionIds);

/// <summary>
/// 서버 세션 라이프사이클 이벤트 인자
/// </summary>
public sealed class ServerSessionEventArgs<TMessage> : EventArgs
{
    public IServerSession<TMessage> Session { get; }
    public string Reason { get; }

    public ServerSessionEventArgs(IServerSession<TMessage> session, string reason = "")
    {
        Session = session;
        Reason = reason;
    }
}

/// <summary>
/// 서버 메시지 수신 이벤트 인자 (발신 세션 포함)
/// </summary>
public sealed class ServerMessageReceivedEventArgs<TMessage> : EventArgs
{
    public IServerSession<TMessage> Session { get; }
    public TMessage Message { get; }

    public ServerMessageReceivedEventArgs(IServerSession<TMessage> session, TMessage message)
    {
        Session = session;
        Message = message;
    }
}

/// <summary>
/// 1:N 고수준 서버 호스트 엔진 계약
/// </summary>
public interface IKableServer<TMessage> : IAsyncDisposable
{
    bool IsRunning { get; }
    int ActiveSessionCount { get; }
    IReadOnlyCollection<IServerSession<TMessage>> ActiveSessions { get; }

    Task StartAsync(CancellationToken ct = default);
    Task StopAsync(TimeSpan timeout = default, CancellationToken ct = default);

    ValueTask SendAsync(string connectionId, TMessage message, CancellationToken ct = default);
    bool TryGetSession(string connectionId, out IServerSession<TMessage>? session);

    ValueTask<BroadcastResult> BroadcastAsync(TMessage message, CancellationToken ct = default);

    event EventHandler<ServerSessionEventArgs<TMessage>>? ClientConnected;
    event EventHandler<ServerSessionEventArgs<TMessage>>? ClientDisconnected;
    event EventHandler<ServerMessageReceivedEventArgs<TMessage>>? MessageReceived;
}
