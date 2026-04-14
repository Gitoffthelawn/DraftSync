using System;
using System.Collections.Generic;
using System.Threading;

namespace DraftSync.Plugin
{
    /// <summary>
    /// Manages debounced auto-saves for managed tabs.
    /// Saves after <see cref="Settings.AutoSaveDelaySec"/> of inactivity,
    /// or force-saves after <see cref="Settings.MaxAutoSaveDelaySec"/> regardless.
    ///
    /// IMPORTANT: All methods except <see cref="OnModified"/> must be called from the UI thread.
    /// <see cref="OnModified"/> just resets a timer and is safe to call from anywhere.
    /// The callback (<see cref="OnSaveDue"/>) is invoked on a ThreadPool thread; callers
    /// must marshal to the UI thread before touching Npp/Scintilla APIs.
    /// </summary>
    public class AutoSaver : IDisposable
    {
        private readonly Settings _settings;

        /// <summary>
        /// Invoked when a save is due. Argument is the file path of the tab to save.
        /// Called on a ThreadPool thread — caller must marshal to UI thread.
        /// </summary>
        public Action<string> OnSaveDue { get; set; }

        // Per-tab state
        private class TabState
        {
            public Timer DebounceTimer;
            public Timer ForceTimer;
            public bool Dirty;
        }

        private readonly Dictionary<string, TabState> _states =
            new Dictionary<string, TabState>(StringComparer.OrdinalIgnoreCase);

        public AutoSaver(Settings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Called on every SCN_MODIFIED for a managed tab.
        /// Zero-cost: resets the debounce timer and marks dirty.
        /// SAFE to call from notification handler (does no file I/O).
        /// </summary>
        public void OnModified(string filePath)
        {
            if (!_states.TryGetValue(filePath, out var state))
                return;

            state.Dirty = true;

            // Reset debounce timer
            int debounceMs = _settings.AutoSaveDelaySec * 1000;
            state.DebounceTimer.Change(debounceMs, Timeout.Infinite);
        }

        /// <summary>Start tracking a new managed tab.</summary>
        public void Track(string filePath)
        {
            if (_states.ContainsKey(filePath))
                return;

            int debounceMs = _settings.AutoSaveDelaySec * 1000;
            int maxMs = _settings.MaxAutoSaveDelaySec * 1000;

            var state = new TabState();
            state.DebounceTimer = new Timer(_ => FireSave(filePath), null, Timeout.Infinite, Timeout.Infinite);
            state.ForceTimer = new Timer(_ => FireSave(filePath), null, maxMs, maxMs);
            _states[filePath] = state;
        }

        /// <summary>Stop tracking a tab and dispose its timers.</summary>
        public void Untrack(string filePath)
        {
            if (_states.TryGetValue(filePath, out var state))
            {
                state.DebounceTimer?.Dispose();
                state.ForceTimer?.Dispose();
                _states.Remove(filePath);
            }
        }

        /// <summary>Force-save a tab immediately (e.g. on tab switch or shutdown).</summary>
        public void FlushNow(string filePath)
        {
            if (_states.TryGetValue(filePath, out var state) && state.Dirty)
                FireSave(filePath);
        }

        public void FlushAll()
        {
            foreach (string path in new List<string>(_states.Keys))
                FlushNow(path);
        }

        public void UpdatePath(string oldPath, string newPath)
        {
            if (_states.TryGetValue(oldPath, out var state))
            {
                _states.Remove(oldPath);
                _states[newPath] = state;
            }
        }

        private void FireSave(string filePath)
        {
            if (!_states.TryGetValue(filePath, out var state) || !state.Dirty)
                return;
            state.Dirty = false;
            // Stop the debounce timer until next modification
            state.DebounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
            OnSaveDue?.Invoke(filePath);
        }

        public void Dispose()
        {
            foreach (var state in _states.Values)
            {
                state.DebounceTimer?.Dispose();
                state.ForceTimer?.Dispose();
            }
            _states.Clear();
        }
    }
}
