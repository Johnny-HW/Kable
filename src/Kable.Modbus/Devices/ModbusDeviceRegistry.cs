namespace Kable.Modbus.Devices;

public sealed record ModbusDeviceRegistration(string Name, Func<IModbusCoilMaster> Create);

/// <summary>Owns lazily created masters. Names use ordinal comparison; a failed factory can be retried.</summary>
public sealed class ModbusDeviceRegistry : IAsyncDisposable
{
    private readonly Dictionary<string, ModbusDeviceRegistration> _registrations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IModbusCoilMaster> _masters = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private Task? _disposal;
    private bool _disposed;

    public ModbusDeviceRegistry(IEnumerable<ModbusDeviceRegistration> registrations)
    {
        foreach (var entry in registrations)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Name);
            ArgumentNullException.ThrowIfNull(entry.Create);
            if (!_registrations.TryAdd(entry.Name, entry)) throw new ArgumentException($"Duplicate device name: {entry.Name}");
        }
    }

    public IModbusCoilMaster Get(string name)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_masters.TryGetValue(name, out var master)) return master;
            if (!_registrations.TryGetValue(name, out var registration)) throw new KeyNotFoundException($"Unknown device: {name}");
            master = registration.Create() ?? throw new InvalidOperationException("Device factory returned null.");
            if (_masters.Values.Any(existing => ReferenceEquals(existing, master)))
                throw new InvalidOperationException("Each device name must own a distinct master.");
            _masters.Add(name, master);
            return master;
        }
    }

    public async Task<IModbusCoilMaster> ConnectAsync(string name, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var master = Get(name);
        await master.StartAsync(ct).ConfigureAwait(false);
        return master;
    }

    public ValueTask DisposeAsync()
    {
        lock (_sync)
        {
            _disposed = true;
            return new(_disposal ??= DisposeAllAsync(_masters.Values.ToArray()));
        }
    }
    private static async Task DisposeAllAsync(IModbusCoilMaster[] masters)
    {
        List<Exception>? errors = null;
        foreach (var master in masters)
        {
            try { await master.DisposeAsync().ConfigureAwait(false); }
            catch (Exception error) { (errors ??= []).Add(error); }
        }
        if (errors != null) throw new AggregateException(errors);
    }
}
