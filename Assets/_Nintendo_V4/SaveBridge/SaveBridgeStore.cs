using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.Scripting;

/// <summary>Serializer-independent key/value container committed as one Switch save file.</summary>
public sealed class SaveBridgeStore
{
    [Serializable, Preserve] public sealed class Entry { public string key; public string value; }
    [Serializable, Preserve] private sealed class Document
    {
        public string format;
        public int version;
        public List<Entry> entries;
    }
    private readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
    public bool Dirty { get; private set; }
    public bool HasKey(string key) => values.ContainsKey(key);
    public string Get(string key, string fallback = "") => values.TryGetValue(key, out var value) ? value : fallback;
    public void Set(string key, string value)
    {
        if (string.IsNullOrEmpty(key)) throw new ArgumentException("Save key cannot be empty.", nameof(key));
        value ??= "";
        if (values.TryGetValue(key, out var previous) && previous == value) return;
        values[key] = value; Dirty = true;
    }
    public void Delete(string key) { if (values.Remove(key)) Dirty = true; }
    public void Clear() { if (values.Count > 0) { values.Clear(); Dirty = true; } }
    public byte[] Encode()
    {
        using var buffer = new MemoryStream();
        using (var text = new StreamWriter(buffer, new UTF8Encoding(false), 4096, true))
        using (var writer = new JsonTextWriter(text))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("format"); writer.WriteValue("FreehangarSaveBridge");
            writer.WritePropertyName("version"); writer.WriteValue(2);
            writer.WritePropertyName("entries"); writer.WriteStartArray();
            foreach (var pair in values.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WritePropertyName("key"); writer.WriteValue(pair.Key);
                writer.WritePropertyName("value"); writer.WriteValue(Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Value)));
                writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject();
        }
        return buffer.ToArray();
    }
    public static SaveBridgeStore Decode(byte[] bytes)
    {
        var store = new SaveBridgeStore();
        // Only NXSave's explicit missing-file result represents a new profile.
        if (bytes == null) return store;
        if (bytes.Length == 0) throw new InvalidDataException("The Switch save file is empty.");
        Document document;
        // The original raw-string format can contain escaped NUL characters.
        // JsonUtility truncates those strings; preserve them during migration.
        try { document = JsonConvert.DeserializeObject<Document>(new UTF8Encoding(false, true).GetString(bytes)); }
        catch (JsonException e) { throw new InvalidDataException("Invalid Switch save document.", e); }
        if (document == null || document.format != "FreehangarSaveBridge" || (document.version != 1 && document.version != 2) || document.entries == null)
            throw new InvalidDataException("Unsupported or damaged Switch save file; existing data has not been replaced.");
        foreach (var entry in document.entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.key) || entry.value == null || store.values.ContainsKey(entry.key))
                throw new InvalidDataException("Invalid or duplicate save entry.");
            store.values.Add(entry.key, entry.value);
        }
        // Two earlier writers both used version 1. Encoding was applied to the
        // entire document, so detect it once, never independently per entry.
        var decoded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in document.entries)
        {
            try { decoded.Add(entry.key, new UTF8Encoding(false, true).GetString(Convert.FromBase64String(entry.value))); }
            catch (Exception e) when (document.version == 1 && (e is FormatException || e is DecoderFallbackException))
            {
                return store; // Legacy raw values; no write or reset during loading.
            }
            catch (FormatException e) { throw new InvalidDataException("Invalid save value encoding.", e); }
            catch (DecoderFallbackException e) { throw new InvalidDataException("Invalid save value text.", e); }
        }
        foreach (var pair in decoded) store.values[pair.Key] = pair.Value;
        return store;
    }
    public bool Flush(Func<byte[], bool, bool> write, bool force)
    {
        if (!Dirty) return true;
        if (!write(Encode(), force)) return false;
        Dirty = false;
        return true;
    }
}
