namespace Kable.OpcUa;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.OpcUa.Models;

public interface IOpcUaClientBridge : IAsyncDisposable
{
    bool IsConnected { get; }
    string EndpointUrl { get; }

    Task ConnectAsync(CancellationToken ct = default);
    Task<OpcUaDataValue> ReadNodeValueAsync(string nodeIdString, CancellationToken ct = default);
    Task WriteNodeValueAsync(string nodeIdString, object value, CancellationToken ct = default);
}
