using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Kable.ProtocolPack.Models;

namespace Kable.ProtocolPack.Loading;

/// <summary>
/// YAML document encode/decode for protocol pack files.
/// </summary>
public static class ProtocolPackYaml
{
    private static readonly IDeserializer CachedDeserializer = CreateDeserializer();
    private static readonly ISerializer CachedSerializer = CreateSerializer();

    public static T DeserializeDocument<T>(string yaml)
    {
        if (yaml == null)
        {
            throw new ArgumentNullException(nameof(yaml));
        }

        try
        {
            return CachedDeserializer.Deserialize<T>(yaml);
        }
        catch (YamlException ex)
        {
            throw new ProtocolPackException("Failed to parse YAML: " + ex.Message, ex);
        }
    }

    public static IReadOnlyList<T> DeserializeDocuments<T>(string yaml)
    {
        if (yaml == null)
        {
            throw new ArgumentNullException(nameof(yaml));
        }

        var results = new List<T>();
        try
        {
            using var reader = new StringReader(yaml);
            var parser = new Parser(reader);
            parser.Consume<StreamStart>();
            while (parser.Accept<DocumentStart>(out _))
            {
                var item = CachedDeserializer.Deserialize<T>(parser);
                if (item != null)
                {
                    results.Add(item);
                }
            }
        }
        catch (YamlException ex)
        {
            throw new ProtocolPackException("Failed to parse YAML: " + ex.Message, ex);
        }

        return results;
    }

    public static IReadOnlyList<MapSpec> DeserializeMapDocuments(string yaml)
    {
        if (yaml == null)
        {
            throw new ArgumentNullException(nameof(yaml));
        }

        var results = new List<MapSpec>();
        try
        {
            using var reader = new StringReader(yaml);
            var tracker = new ScalarKeyTrackingParser(new Parser(reader), "value");
            tracker.Consume<StreamStart>();
            while (tracker.Accept<DocumentStart>(out _))
            {
                tracker.ResetDocumentState();
                var map = CachedDeserializer.Deserialize<MapSpec>(tracker);
                if (map == null)
                {
                    continue;
                }

                if (tracker.HasTrackedKey)
                {
                    map.HasScalarValue = true;
                    if (map.Value == null)
                    {
                        map.Value = tracker.TrackedScalarValue ?? (object)true;
                    }
                }

                results.Add(map);
            }
        }
        catch (YamlException ex)
        {
            throw new ProtocolPackException("Failed to parse YAML: " + ex.Message, ex);
        }

        return results;
    }

    public static string SerializeDocument<T>(T value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return CachedSerializer.Serialize(value);
    }

    public static string SerializeDocuments<T>(IReadOnlyList<T> values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        using var writer = new StringWriter();
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0)
            {
                writer.WriteLine("---");
                writer.WriteLine();
            }

            writer.Write(CachedSerializer.Serialize(values[i]));
        }

        return writer.ToString();
    }

    internal static IDeserializer CreateDeserializer()
    {
        return new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    internal static ISerializer CreateSerializer()
    {
        return new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(
                DefaultValuesHandling.OmitNull |
                DefaultValuesHandling.OmitDefaults |
                DefaultValuesHandling.OmitEmptyCollections)
            .DisableAliases()
            .Build();
    }

    private sealed class ScalarKeyTrackingParser : IParser
    {
        private readonly IParser _inner;
        private readonly string _trackedKey;
        private int _depth;
        private bool _expectingRootKey;
        private bool _capturingValue;

        public ScalarKeyTrackingParser(IParser inner, string trackedKey)
        {
            _inner = inner;
            _trackedKey = trackedKey;
        }

        public bool HasTrackedKey { get; private set; }
        public string? TrackedScalarValue { get; private set; }
        public ParsingEvent? Current => _inner.Current;

        public void ResetDocumentState()
        {
            _depth = 0;
            _expectingRootKey = false;
            _capturingValue = false;
            HasTrackedKey = false;
            TrackedScalarValue = null;
        }

        public bool MoveNext()
        {
            if (!_inner.MoveNext())
            {
                return false;
            }

            switch (_inner.Current)
            {
                case MappingStart:
                    _depth++;
                    if (_depth == 1)
                    {
                        _expectingRootKey = true;
                    }
                    else
                    {
                        _capturingValue = false;
                    }
                    break;
                case SequenceStart:
                    _depth++;
                    _capturingValue = false;
                    break;
                case MappingEnd:
                case SequenceEnd:
                    _depth--;
                    _expectingRootKey = _depth == 1;
                    break;
                case Scalar scalar when _depth == 1:
                    if (_expectingRootKey)
                    {
                        if (string.Equals(scalar.Value, _trackedKey, StringComparison.Ordinal))
                        {
                            HasTrackedKey = true;
                            _capturingValue = true;
                        }
                        _expectingRootKey = false;
                    }
                    else
                    {
                        if (_capturingValue)
                        {
                            TrackedScalarValue = scalar.Value;
                            _capturingValue = false;
                        }
                        _expectingRootKey = true;
                    }
                    break;
            }

            return true;
        }
    }
}
