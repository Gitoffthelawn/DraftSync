using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace DraftSync.Plugin
{
    /// <summary>
    /// Monitors the sync folder for new/modified/deleted files using
    /// FileSystemWatcher with a polling fallback.
    ///
    /// All events are raised on a background thread. Callers MUST marshal
    /// to the UI thread before touching Notepad++/Scintilla APIs.
    /// </summary>
    public class FolderWatcher : IDisposable
    {
        private readonly Settings _settings;
        private FileSystemWatcher _fsw;
        private Timer _pollTimer;
        private readonly Dictionary<string, DateTime> _lastSeenModified =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        // ── Events ──────────────────────────────────────────────────────────────

        /// <summary>A new .txt (or configured extension) appeared in the sync folder.</summary>
        public event Action<string> NoteAppeared;   // arg: note name (no extension)
        /// <summary>An existing .txt was modified.</summary>
        public event Action<string> NoteChanged;    // arg: note name
        /// <summary>A .txt was deleted.</summary>
        public event Action<string> NoteDeleted;    // arg: note name

        public FolderWatcher(Settings settings)
        {
            _settings = settings;
        }

        // ── Start / Stop ───────────────────────────────────────────────────────

        public bool Start()
        {
            if (!_settings.SyncFolderValid) return false;

            // Seed the last-seen map so we don't fire "appeared" for existing files
            SeedLastSeen();

            // FileSystemWatcher
            try
            {
                _fsw = new FileSystemWatcher(_settings.SyncFolder, "*" + _settings.FileExtension)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };
                _fsw.Created += OnFswCreated;
                _fsw.Changed += OnFswChanged;
                _fsw.Deleted += OnFswDeleted;
                _fsw.Error   += OnFswError;
            }
            catch { /* FSW unavailable — fall back to polling only */ }

            // Polling fallback
            int pollMs = _settings.PollIntervalSec * 1000;
            _pollTimer = new Timer(_ => Poll(), null, pollMs, pollMs);
            return true;
        }

        public void Stop()
        {
            _fsw?.Dispose();
            _fsw = null;
            _pollTimer?.Dispose();
            _pollTimer = null;
        }

        // ── FSW handlers ───────────────────────────────────────────────────────

        private void OnFswCreated(object sender, FileSystemEventArgs e) =>
            HandleAppeared(Path.GetFileNameWithoutExtension(e.Name));

        private void OnFswChanged(object sender, FileSystemEventArgs e) =>
            HandleChanged(Path.GetFileNameWithoutExtension(e.Name));

        private void OnFswDeleted(object sender, FileSystemEventArgs e) =>
            HandleDeleted(Path.GetFileNameWithoutExtension(e.Name));

        private void OnFswError(object sender, ErrorEventArgs e)
        {
            // FSW buffer overflow or other error — polling will catch up
        }

        // ── Polling ────────────────────────────────────────────────────────────

        private void Poll()
        {
            if (!_settings.SyncFolderValid) return;
            try
            {
                string ext = _settings.FileExtension;
                var current = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

                foreach (string file in Directory.GetFiles(_settings.SyncFolder, "*" + ext))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    DateTime lastWrite;
                    try { lastWrite = File.GetLastWriteTimeUtc(file); }
                    catch { continue; }
                    current[name] = lastWrite;

                    if (!_lastSeenModified.ContainsKey(name))
                        HandleAppeared(name);
                    else if (lastWrite > _lastSeenModified[name])
                        HandleChanged(name);
                }

                // Check for deletions
                foreach (string name in new List<string>(_lastSeenModified.Keys))
                {
                    if (!current.ContainsKey(name))
                        HandleDeleted(name);
                }

                // Update map
                lock (_lastSeenModified)
                {
                    _lastSeenModified.Clear();
                    foreach (var kv in current)
                        _lastSeenModified[kv.Key] = kv.Value;
                }
            }
            catch { /* transient I/O error — try again next poll */ }
        }

        private void SeedLastSeen()
        {
            if (!_settings.SyncFolderValid) return;
            try
            {
                string ext = _settings.FileExtension;
                foreach (string file in Directory.GetFiles(_settings.SyncFolder, "*" + ext))
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    try { _lastSeenModified[name] = File.GetLastWriteTimeUtc(file); }
                    catch { }
                }
            }
            catch { }
        }

        // ── Event dispatch ─────────────────────────────────────────────────────

        private void HandleAppeared(string noteName)
        {
            lock (_lastSeenModified)
            {
                string path = Path.Combine(_settings.SyncFolder, noteName + _settings.FileExtension);
                try { _lastSeenModified[noteName] = File.GetLastWriteTimeUtc(path); } catch { }
            }
            NoteAppeared?.Invoke(noteName);
        }

        private void HandleChanged(string noteName)
        {
            lock (_lastSeenModified)
            {
                string path = Path.Combine(_settings.SyncFolder, noteName + _settings.FileExtension);
                try { _lastSeenModified[noteName] = File.GetLastWriteTimeUtc(path); } catch { }
            }
            NoteChanged?.Invoke(noteName);
        }

        private void HandleDeleted(string noteName)
        {
            lock (_lastSeenModified)
                _lastSeenModified.Remove(noteName);
            NoteDeleted?.Invoke(noteName);
        }

        // ── IDisposable ────────────────────────────────────────────────────────

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }
}
