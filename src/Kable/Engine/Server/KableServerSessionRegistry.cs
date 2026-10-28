namespace Kable.Engine;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Kable.Core;

/// <summary>
/// 1:N 서버 활성 세션 스레드 세이프 등록소
/// </summary>
internal sealed class KableServerSessionRegistry<TMessage>
{
    private readonly ConcurrentDictionary<string, KableServerSession<TMessage>> _sessions = new();

    public int Count => _sessions.Count;

    public IReadOnlyCollection<IServerSession<TMessage>> Sessions
        => new List<IServerSession<TMessage>>(_sessions.Values);

    public bool TryAdd(KableServerSession<TMessage> session)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        return _sessions.TryAdd(session.ConnectionId, session);
    }

    public bool TryRemove(string connectionId, out KableServerSession<TMessage>? session)
    {
        return _sessions.TryRemove(connectionId, out session);
    }

    public bool TryGet(string connectionId, out KableServerSession<TMessage>? session)
    {
        return _sessions.TryGetValue(connectionId, out session);
    }

}
