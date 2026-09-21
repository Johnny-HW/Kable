namespace Kable.Engine.Profiles;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.Observability;
using Kable.Simple;

internal sealed class StringKableProfileClientImpl : KableProfileClientBase<string>, IKableProfileClient
{
    private readonly KableProfileConfig _config;

    public StringKableProfileClientImpl(
        IKableSimpleClient client,
        KableProfileConfig config,
        ICommObserver? observer)
        : base(client, config, observer, StringComparer.OrdinalIgnoreCase)
    {
        _config = config;
    }

    internal void Start()
    {
        foreach (var kvp in _config.PeriodicCommands)
        {
            StartPolling(kvp.Key, kvp.Key, kvp.Value);
        }
    }

    public ValueTask<string> ExecuteAsync(string commandOrAlias, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        ThrowIfDisposed();

        var actualCommand = _config.CommandAliases.TryGetValue(commandOrAlias, out var resolved)
            ? resolved
            : commandOrAlias;

        var effectiveTimeout = timeout ?? _config.DefaultCommandTimeout;
        return Client.QueryAsync(actualCommand, effectiveTimeout, ct);
    }
}
