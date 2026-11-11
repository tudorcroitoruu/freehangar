using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

// Match Unity's field-based DTO contract, including Vector/Quaternion fields,
// without creating a full uncompressed world JSON string or UTF-8 byte array.
public static class CompressedSaveWriter
{
    private sealed class FieldsOnlyResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization serialization)
        {
            return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => !f.IsStatic && !f.IsNotSerialized && (f.IsPublic || f.IsDefined(typeof(SerializeField), true)))
                .Select(f => { var p = base.CreateProperty(f, MemberSerialization.Fields); p.Readable = true; p.Writable = true; return p; }).ToList();
        }
    }
    private static readonly IContractResolver Resolver = new FieldsOnlyResolver();
    public static byte[] Compress(PlayerData data)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, System.IO.Compression.CompressionLevel.Fastest, true))
        using (var text = new StreamWriter(gzip, new UTF8Encoding(false), 4096))
        using (var json = new JsonTextWriter(text))
            new JsonSerializer { ContractResolver = Resolver, Culture = System.Globalization.CultureInfo.InvariantCulture }.Serialize(json, data);
        return buffer.ToArray();
    }
}

