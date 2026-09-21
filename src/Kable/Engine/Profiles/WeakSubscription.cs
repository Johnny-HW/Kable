namespace Kable.Engine.Profiles;

using System;

internal interface IWeakSubscription<in TCmd>
{
    bool IsTargetAlive { get; }
    bool Invoke(TCmd cmd, string data);
}

internal sealed class WeakSubscriptionImpl<TTarget, TCmd> : IWeakSubscription<TCmd>, IDisposable
    where TTarget : class
{
    private readonly WeakReference<TTarget> _weakTarget;
    private readonly Action<TTarget, TCmd, string> _handler;
    private readonly Action<IWeakSubscription<TCmd>>? _onDispose;
    private bool _isDisposed;

    public bool IsTargetAlive => !_isDisposed && _weakTarget.TryGetTarget(out _);

    public WeakSubscriptionImpl(
        TTarget target,
        Action<TTarget, TCmd, string> handler,
        Action<IWeakSubscription<TCmd>>? onDispose = null)
    {
        _weakTarget = new WeakReference<TTarget>(target);
        _handler = handler;
        _onDispose = onDispose;
    }

    public bool Invoke(TCmd cmd, string data)
    {
        if (_isDisposed) return false;

        if (_weakTarget.TryGetTarget(out var target))
        {
            try
            {
                _handler(target, cmd, data);
                return true;
            }
            catch
            {
                return true;
            }
        }

        return false; // target was GC-collected
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _onDispose?.Invoke(this);
    }
}

internal sealed class WeakSubscriptionWithStateImpl<TTarget, TState, TCmd> : IWeakSubscription<TCmd>, IDisposable
    where TTarget : class
{
    private readonly WeakReference<TTarget> _weakTarget;
    private readonly TState _state;
    private readonly Action<TTarget, TState, TCmd, string> _handler;
    private readonly Action<IWeakSubscription<TCmd>>? _onDispose;
    private bool _isDisposed;

    public bool IsTargetAlive => !_isDisposed && _weakTarget.TryGetTarget(out _);

    public WeakSubscriptionWithStateImpl(
        TTarget target,
        TState state,
        Action<TTarget, TState, TCmd, string> handler,
        Action<IWeakSubscription<TCmd>>? onDispose = null)
    {
        _weakTarget = new WeakReference<TTarget>(target);
        _state = state;
        _handler = handler;
        _onDispose = onDispose;
    }

    public bool Invoke(TCmd cmd, string data)
    {
        if (_isDisposed) return false;

        if (_weakTarget.TryGetTarget(out var target))
        {
            try
            {
                _handler(target, _state, cmd, data);
                return true;
            }
            catch
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _onDispose?.Invoke(this);
    }
}
