using System;
using System.IO;
using System.Text;

namespace NppSync.Plugin
{
    /// <summary>
    /// Per-note metadata stored in .nppsync/&lt;name&gt;.meta.json
    /// </summary>
    public class NoteMetadata
    {
        public string Name { get; set; }
        public string CreatedBy { get; set; }
        public string CreatedAt { get; set; }
        public string LastModifiedBy { get; set; }
        public string LastModifiedAt { get; set; }
        public bool Deleted { get; set; }

        public static NoteMetadata Create(string name, string machineId)
        {
            string now = DateTime.UtcNow.ToString("o");
            return new NoteMetadata
            {
                Name = name,
                CreatedBy = machineId,
                CreatedAt = now,
                LastModifiedBy = machineId,
                LastModifiedAt = now,
                Deleted = false
            };
        }

        public void UpdateModified(string machineId)
        {
            LastModifiedBy = machineId;
            LastModifiedAt = DateTime.UtcNow.ToString("o");
        }

        // Minimal JSON serialization — no external dependencies.
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"name\": {JsonString(Name)},");
            sb.AppendLine($"  \"createdBy\": {JsonString(CreatedBy)},");
            sb.AppendLine($"  \"createdAt\": {JsonString(CreatedAt)},");
            sb.AppendLine($"  \"lastModifiedBy\": {JsonString(LastModifiedBy)},");
            sb.AppendLine($"  \"lastModifiedAt\": {JsonString(LastModifiedAt)},");
            sb.AppendLine($"  \"deleted\": {(Deleted ? "true" : "false")}");
            sb.Append("}");
            return sb.ToString();
        }

        public static NoteMetadata FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;
            var meta = new NoteMetadata();
            meta.Name = ReadJsonString(json, "name");
            meta.CreatedBy = ReadJsonString(json, "createdBy");
            meta.CreatedAt = ReadJsonString(json, "createdAt");
            meta.LastModifiedBy = ReadJsonString(json, "lastModifiedBy");
            meta.LastModifiedAt = ReadJsonString(json, "lastModifiedAt");
            meta.Deleted = ReadJsonBool(json, "deleted");
            return meta;
        }

        public static NoteMetadata TryLoad(string metaPath)
        {
            try
            {
                if (!File.Exists(metaPath))
                    return null;
                string json = File.ReadAllText(metaPath, Encoding.UTF8);
                return FromJson(json);
            }
            catch
            {
                return null;
            }
        }

        public bool TrySave(string metaPath)
        {
            try
            {
                File.WriteAllText(metaPath, ToJson(), Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string JsonString(string value)
        {
            if (value == null) return "null";
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string ReadJsonString(string json, string key)
        {
            string search = "\"" + key + "\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += search.Length;
            // skip whitespace and colon
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':' || json[idx] == '\t')) idx++;
            if (idx >= json.Length || json[idx] != '"') return null;
            idx++; // skip opening quote
            var sb = new StringBuilder();
            while (idx < json.Length && json[idx] != '"')
            {
                if (json[idx] == '\\' && idx + 1 < json.Length)
                {
                    idx++;
                    switch (json[idx])
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        default: sb.Append(json[idx]); break;
                    }
                }
                else
                {
                    sb.Append(json[idx]);
                }
                idx++;
            }
            return sb.ToString();
        }

        private static bool ReadJsonBool(string json, string key)
        {
            string search = "\"" + key + "\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return false;
            idx += search.Length;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == ':' || json[idx] == '\t')) idx++;
            return idx < json.Length && json[idx] == 't';
        }
    }
}
