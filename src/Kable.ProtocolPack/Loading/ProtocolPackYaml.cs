using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using Kable.ProtocolPack.Models;

namespace Kable.ProtocolPack.Loading;

/// <summary>
/// YAML document encode/decode for protocol pack files.
/// </summary>
public static class ProtocolPackYaml
{
    public static T DeserializeDocument<T>(string yaml)
    {
        if (yaml == null)
        {
            throw new ArgumentNullException(nameof(yaml));
        }

        try
        {
            return CreateDeserializer().Deserialize<T>(yaml);
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

        var stream = new YamlStream();
        try
        {
            using (var reader = new StringReader(yaml))
            {
                stream.Load(reader);
            }
        }
        catch (YamlException ex)
        {
            throw new ProtocolPackException("Failed to parse YAML: " + ex.Message, ex);
        }

        var results = new List<T>(stream.Documents.Count);
        var deserializer = CreateDeserializer();
        foreach (var document in stream.Documents)
        {
            if (IsEmptyDocument(document))
            {
                continue;
            }

            var text = SaveSingleDocument(document);
            try
            {
                var item = deserializer.Deserialize<T>(text);
                if (item != null)
                {
                    results.Add(item);
                }
            }
            catch (YamlException ex)
            {
                throw new ProtocolPackException("Failed to parse YAML: " + ex.Message, ex);
            }
        }

        return results;
    }

    public static IReadOnlyList<MapSpec> DeserializeMapDocuments(string yaml)
    {
        var maps = DeserializeDocuments<MapSpec>(yaml);
        MarkScalarValueKeys(yaml, maps);
        return maps;
    }

    public static string SerializeDocument<T>(T value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        return CreateSerializer().Serialize(value);
    }

    public static string SerializeDocuments<T>(IReadOnlyList<T> values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        var serializer = CreateSerializer();
        using (var writer = new StringWriter())
        {
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0)
                {
                    writer.WriteLine("---");
                    writer.WriteLine();
                }

                writer.Write(serializer.Serialize(values[i]));
            }

            return writer.ToString();
        }
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

    private static bool IsEmptyDocument(YamlDocument document)
    {
        if (document.RootNode is YamlScalarNode scalar)
        {
            return string.IsNullOrWhiteSpace(scalar.Value);
        }

        return false;
    }

    private static string SaveSingleDocument(YamlDocument document)
    {
        using (var writer = new StringWriter())
        {
            var single = new YamlStream(document);
            single.Save(writer, assignAnchors: false);
            return writer.ToString();
        }
    }

    private static void MarkScalarValueKeys(string yaml, IReadOnlyList<MapSpec> maps)
    {
        var stream = new YamlStream();
        using (var reader = new StringReader(yaml))
        {
            stream.Load(reader);
        }

        var index = 0;
        foreach (var document in stream.Documents)
        {
            if (IsEmptyDocument(document))
            {
                continue;
            }

            if (index >= maps.Count)
            {
                break;
            }

            if (document.RootNode is YamlMappingNode mapping && HasKey(mapping, "value"))
            {
                maps[index].HasScalarValue = true;
                if (maps[index].Value == null)
                {
                    maps[index].Value = ReadScalar(mapping, "value") ?? true;
                }
            }

            index++;
        }
    }

    private static bool HasKey(YamlMappingNode mapping, string key)
    {
        foreach (var pair in mapping.Children)
        {
            if (pair.Key is YamlScalarNode scalar &&
                string.Equals(scalar.Value, key, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static object? ReadScalar(YamlMappingNode mapping, string key)
    {
        foreach (var pair in mapping.Children)
        {
            if (pair.Key is YamlScalarNode scalar &&
                string.Equals(scalar.Value, key, StringComparison.Ordinal) &&
                pair.Value is YamlScalarNode value)
            {
                return value.Value;
            }
        }

        return null;
    }
}
