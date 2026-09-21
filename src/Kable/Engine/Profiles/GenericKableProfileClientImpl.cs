namespace Kable.Engine.Profiles;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.Observability;
using Kable.Simple;

internal sealed class GenericKableProfileClientImpl<TPeriodic, TAperiodic> : KableProfileClientBase<TPeriodic>, IKableProfileClient<TPeriodic, TAperiodic>
    where TPeriodic : struct, Enum
    where TAperiodic : struct, Enum
{
    private readonly KableProfileConfig<TPeriodic, TAperiodic> _config;

    public GenericKableProfileClientImpl(
        IKableSimpleClient client,
        KableProfileConfig<TPeriodic, TAperiodic> config,
        ICommObserver? observer)
        : base(client, config, observer)
    {
        _config = config;
    }

    internal void Start()
    {
        foreach (var kvp in _config.PeriodicCommands)
        {
            StartPolling(kvp.Key, kvp.Value.RawCommand, kvp.Value.Interval);
        }
    }

    public ValueTask<string> ExecuteAsync(TAperiodic command, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var rawCommand = _config.AperiodicCommands.TryGetValue(command, out var cmd)
            ? cmd
            : command.ToString();

        var effectiveTimeout = timeout ?? _config.DefaultCommandTimeout;
        return Client.QueryAsync(rawCommand, effectiveTimeout, ct);
    }

    public ValueTask<string> ExecuteWithArgsAsync(TAperiodic command, string formatArgs, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var baseCmd = _config.AperiodicCommands.TryGetValue(command, out var cmd)
            ? cmd
            : command.ToString();

        var fullCmd = string.IsNullOrEmpty(formatArgs) ? baseCmd : $"{baseCmd} {formatArgs}";
        var effectiveTimeout = timeout ?? _config.DefaultCommandTimeout;
        return Client.QueryAsync(fullCmd, effectiveTimeout, ct);
    }
}
