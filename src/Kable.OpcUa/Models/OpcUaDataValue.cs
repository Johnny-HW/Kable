namespace Kable.OpcUa.Models;

using System;

/// <summary>
/// OPC UA 노드 값 및 품질 상태 불변 모델
/// </summary>
public sealed class OpcUaDataValue
{
    public string NodeId { get; }
    public object? Value { get; }
    public uint StatusCode { get; }
    public DateTime SourceTimestamp { get; }
    public bool IsGood => (StatusCode & 0xC0000000) == 0;

    public OpcUaDataValue(string nodeId, object? value, uint statusCode = 0, DateTime? sourceTimestamp = null)
    {
        NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
        Value = value;
        StatusCode = statusCode;
        SourceTimestamp = sourceTimestamp ?? DateTime.UtcNow;
    }

    public T? GetValue<T>()
    {
        if (Value is null) return default;
        if (Value is T typedVal) return typedVal;
        return (T)Convert.ChangeType(Value, typeof(T));
    }
}
