using System;
using System.Collections.Generic;
using System.IO;

namespace NppSync.Plugin
{
    /// <summary>
    /// Tracks which open Notepad++ tabs are managed by NppSync,
    /// keyed by their file path (lowercased for comparison).
    /// </summary>
    public class TabManager
    {
        private readonly Dictionary<string, ManagedTab> _tabs =
            new Dictionary<string, ManagedTab>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<ManagedTab> All => _tabs.Values;

        public void Register(string filePath, string noteName)
        {
            _tabs[filePath] = new ManagedTab(filePath, noteName);
        }

        public bool IsManaged(string filePath) =>
            !string.IsNullOrEmpty(filePath) && _tabs.ContainsKey(filePath);

        public bool TryGet(string filePath, out ManagedTab tab) =>
            _tabs.TryGetValue(filePath ?? "", out tab);

        public void Unregister(string filePath)
        {
            if (filePath != null)
                _tabs.Remove(filePath);
        }

        public void UpdatePath(string oldPath, string newPath)
        {
            if (_tabs.TryGetValue(oldPath, out var tab))
            {
                _tabs.Remove(oldPath);
                tab.FilePath = newPath;
                tab.NoteName = Path.GetFileNameWithoutExtension(newPath);
                _tabs[newPath] = tab;
            }
        }

        public void Clear() => _tabs.Clear();
    }

    public class ManagedTab
    {
        public string FilePath { get; set; }
        public string NoteName { get; set; }
        /// <summary>UTC timestamp of the last write this machine performed to the sync file.</summary>
        public DateTime LastLocalWrite { get; set; } = DateTime.MinValue;

        public ManagedTab(string filePath, string noteName)
        {
            FilePath = filePath;
            NoteName = noteName;
        }

        /// <summary>
        /// Returns true if a file-system change event on this file's path
        /// looks like an echo of our own recent write (within 2 seconds).
        /// </summary>
        public bool IsEcho() =>
            (DateTime.UtcNow - LastLocalWrite).TotalSeconds < 2.0;
    }
}
