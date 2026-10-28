namespace Kable.OpcUa;

using Kable.OpcUa.Models;
using Opc.Ua;

public sealed partial class OpcUaClientBridge
{
    /// <summary>Returns per-node quality, including bad node IDs; never silently drops partial results.</summary>
    public async Task<IReadOnlyList<OpcUaDataValue>> ReadNodesAsync(IReadOnlyList<string> nodes, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdown.Token);
        ct = linked.Token;
        await _sessionGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!IsConnected) throw new InvalidOperationException("OPC UA session is not connected.");
            if (nodes.Count == 0) return Array.Empty<OpcUaDataValue>();
            var request = new ReadValueIdCollection(nodes.Select(node => new ReadValueId { NodeId = NodeId.Parse(node), AttributeId = Attributes.Value }));
            var reply = await _session!.ReadAsync(null, 0, TimestampsToReturn.Source, request, ct).ConfigureAwait(false);
            if (reply.Results.Count != nodes.Count) throw new InvalidOperationException("OPC UA read result count mismatch.");
            return reply.Results.Select((value, i) => new OpcUaDataValue(nodes[i], value.Value, value.StatusCode.Code, value.SourceTimestamp)).ToArray();
        }
        finally { _sessionGate.Release(); }
    }

    /// <summary>Returns individual write status codes. Failed writes are never automatically retried.</summary>
    public async Task<IReadOnlyList<uint>> WriteNodesAsync(IReadOnlyList<KeyValuePair<string, object>> values, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) != 0, this);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _shutdown.Token);
        ct = linked.Token;
        await _sessionGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!IsConnected) throw new InvalidOperationException("OPC UA session is not connected.");
            if (values.Count == 0) return Array.Empty<uint>();
            var request = new WriteValueCollection(values.Select(value => new WriteValue
            { NodeId = NodeId.Parse(value.Key), AttributeId = Attributes.Value, Value = new DataValue(new Variant(value.Value)) }));
            var reply = await _session!.WriteAsync(null, request, ct).ConfigureAwait(false);
            if (reply.Results.Count != values.Count) throw new InvalidOperationException("OPC UA write result count mismatch.");
            return reply.Results.Select(status => status.Code).ToArray();
        }
        finally { _sessionGate.Release(); }
    }
}
