using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NppSync.Plugin
{
    /// <summary>
    /// Maintains the local list of notes dismissed by this machine.
    /// Stored in NppSync.dismissed.json in the plugin config directory.
    /// </summary>
    public class DismissedList
    {
        private readonly string _filePath;
        private readonly HashSet<string> _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public DismissedList(string filePath)
        {
            _filePath = filePath;
        }

        public bool IsDismissed(string noteName) => _names.Contains(noteName);

        public void Dismiss(string noteName)
        {
            _names.Add(noteName);
            Save();
        }

        public void Restore(string noteName)
        {
            _names.Remove(noteName);
            Save();
        }

        public void RestoreAll()
        {
            _names.Clear();
            Save();
        }

        public IEnumerable<string> GetAll() => _names;

        public void Load()
        {
            _names.Clear();
            if (!File.Exists(_filePath))
                return;
            try
            {
                string json = File.ReadAllText(_filePath, Encoding.UTF8);
                // Parse "dismissed": [ { "name": "x", ... }, ... ]
                int arrStart = json.IndexOf('[');
                int arrEnd = json.LastIndexOf(']');
                if (arrStart < 0 || arrEnd < 0) return;
                string arr = json.Substring(arrStart + 1, arrEnd - arrStart - 1);
                // Find each "name" value
                int idx = 0;
                while (idx < arr.Length)
                {
                    int nameKey = arr.IndexOf("\"name\"", idx, StringComparison.Ordinal);
                    if (nameKey < 0) break;
                    idx = nameKey + 6;
                    // skip to colon, then to opening quote
                    while (idx < arr.Length && arr[idx] != '"') idx++;
                    if (idx >= arr.Length) break;
                    idx++; // skip opening quote
                    int nameEnd = arr.IndexOf('"', idx);
                    if (nameEnd < 0) break;
                    string name = arr.Substring(idx, nameEnd - idx);
                    if (!string.IsNullOrWhiteSpace(name))
                        _names.Add(name);
                    idx = nameEnd + 1;
                }
            }
            catch { /* corrupt file — start fresh */ }
        }

        private void Save()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"dismissed\": [");
                bool first = true;
                string now = DateTime.UtcNow.ToString("o");
                foreach (string name in _names)
                {
                    if (!first) sb.AppendLine(",");
                    sb.Append($"    {{ \"name\": \"{Esc(name)}\", \"dismissedAt\": \"{now}\" }}");
                    first = false;
                }
                if (!first) sb.AppendLine();
                sb.AppendLine("  ]");
                sb.Append("}");
                File.WriteAllText(_filePath, sb.ToString(), Encoding.UTF8);
            }
            catch { /* best-effort */ }
        }

        private static string Esc(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
