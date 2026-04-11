using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Kbg.NppPluginNET.PluginInfrastructure;
using NppDemo.Utils;
using NppSync.Forms;
using NppSync.Plugin;
using static Kbg.NppPluginNET.PluginInfrastructure.Win32;

namespace Kbg.NppPluginNET
{
    /// <summary>
    /// NppSync plugin entry point.
    /// All methods called by the Notepad++ plugin infrastructure run on the UI thread.
    /// </summary>
    class Main
    {
        // ── Plugin identity ────────────────────────────────────────────────────
        internal const string PluginName = "NppSync";
        public static readonly string PluginConfigDirectory =
            Path.Combine(Npp.notepad.GetConfigDirectory(), PluginName);

        // ── Plugin state ───────────────────────────────────────────────────────
        public static bool isShuttingDown = false;

        private static Settings _settings;
        private static TabManager _tabs;
        private static DismissedList _dismissed;
        private static FileSyncService _fileSync;
        private static AutoSaver _autoSaver;
        private static FolderWatcher _watcher;
        private static UiThreadDispatcher _dispatcher;
        private static System.Threading.Timer _scanTimer;
        private static System.Threading.Timer _statusBarTimer;

        /// <summary>Track the active buffer's file path for Save As detection.</summary>
        private static string _activeFilePath = null;
        /// <summary>Buffer ID → file path map, to detect Save As across buffer activations.</summary>
        private static System.Collections.Generic.Dictionary<IntPtr, string> _bufferPaths =
            new System.Collections.Generic.Dictionary<IntPtr, string>();

        // ── Menu command indices ───────────────────────────────────────────────
        private static int CmdIdNewNote = 0;
        private static int CmdIdDeleteNote = 1;
        private static int CmdIdRenameNote = 2;
        private static int CmdIdOpenSyncFolder = 3;
        private static int CmdIdRestoreAll = 4;
        private static int CmdIdShowDismissed = 5;
        // separator at 6
        private static int CmdIdSettings = 7;
        private static int CmdIdAbout = 8;

        // ── CommandMenuInit ────────────────────────────────────────────────────

        static internal void CommandMenuInit()
        {
            PluginBase.SetCommand(CmdIdNewNote,
                "New Synced Note",
                CmdNewNote,
                new ShortcutKey(true, true, false, Keys.N)); // Ctrl+Shift+N

            PluginBase.SetCommand(CmdIdDeleteNote,
                "Delete This Note Everywhere",
                CmdDeleteNote,
                new ShortcutKey(true, true, false, Keys.D)); // Ctrl+Shift+D

            PluginBase.SetCommand(CmdIdRenameNote,
                "Rename This Note",
                CmdRenameNote);

            PluginBase.SetCommand(CmdIdOpenSyncFolder,
                "Open Sync Folder",
                CmdOpenSyncFolder);

            PluginBase.SetCommand(CmdIdRestoreAll,
                "Restore All Dismissed Notes",
                CmdRestoreAll);

            PluginBase.SetCommand(CmdIdShowDismissed,
                "Show Dismissed Notes",
                CmdShowDismissed);

            PluginBase.SetCommand(6, "---", null);

            PluginBase.SetCommand(CmdIdSettings,
                "Settings",
                CmdSettings);

            PluginBase.SetCommand(CmdIdAbout,
                "About",
                CmdAbout);
        }

        static internal void SetToolBarIcons() { /* no toolbar icons for now */ }

        // ── Notification handler ───────────────────────────────────────────────

        public static void OnNotification(ScNotification notification)
        {
            if (isShuttingDown) return;
            uint code = notification.Header.Code;

            switch (code)
            {
            case (uint)NppMsg.NPPN_READY:
                OnReady();
                break;

            case (uint)NppMsg.NPPN_SHUTDOWN:
                // PluginCleanUp handles this
                break;

            case (uint)NppMsg.NPPN_FILEOPENED:
                OnFileOpened(notification.Header.IdFrom);
                break;

            case (uint)NppMsg.NPPN_FILESAVED:
                OnFileSaved(notification.Header.IdFrom);
                break;

            case (uint)NppMsg.NPPN_FILEBEFORECLOSE:
                OnFileBeforeClose(notification.Header.IdFrom);
                break;

            case (uint)NppMsg.NPPN_BUFFERACTIVATED:
                OnBufferActivated(notification.Header.IdFrom);
                break;

            case (uint)SciMsg.SCN_MODIFIED:
                // Zero-cost: just reset debounce timer
                if (_activeFilePath != null && _tabs != null && _tabs.IsManaged(_activeFilePath))
                    _autoSaver?.OnModified(_activeFilePath);
                break;
            }
        }

        static internal void PluginCleanUp()
        {
            isShuttingDown = true;
            _autoSaver?.FlushAll();
            _watcher?.Stop();
            _autoSaver?.Dispose();
            _dispatcher?.Dispose();
        }

        // ── NPPN_READY ─────────────────────────────────────────────────────────

        private static void OnReady()
        {
            // Ensure config directory exists
            if (!Directory.Exists(PluginConfigDirectory))
                Directory.CreateDirectory(PluginConfigDirectory);

            // Load settings
            _settings = new Settings(PluginConfigDirectory);
            _settings.Load();

            // Ensure machine ID
            if (string.IsNullOrWhiteSpace(_settings.MachineId))
            {
                _settings.MachineId = Settings.GenerateMachineId();
                _settings.Save();
            }

            // Init sub-systems
            _tabs = new TabManager();
            _dismissed = new DismissedList(_settings.DismissedFilePath);
            _dismissed.Load();
            _fileSync = new FileSyncService(_settings);
            _autoSaver = new AutoSaver(_settings);
            _dispatcher = new UiThreadDispatcher();
            UiThreadDispatcher.Initialize();

            // Wire auto-saver callback (called on ThreadPool — must dispatch to UI thread)
            _autoSaver.OnSaveDue = filePath =>
                UiThreadDispatcher.Post(() => SaveManagedTab(filePath));

            // Intercept Ctrl+N: create a synced note instead of a blank Npp tab.
            // Return true to swallow the event; false to fall through to Npp's default.
            UiThreadDispatcher.OnNewFileRequested = () =>
            {
                if (!_settings.SyncFolderValid) return false; // no sync folder — let Npp handle it
                CmdNewNote();
                return true;
            };

            // Start watcher
            _watcher = new FolderWatcher(_settings);
            _watcher.NoteAppeared += noteName =>
                UiThreadDispatcher.Post(() => OnRemoteNoteAppeared(noteName));
            _watcher.NoteChanged += noteName =>
                UiThreadDispatcher.Post(() => OnRemoteNoteChanged(noteName));
            _watcher.NoteDeleted += noteName =>
                UiThreadDispatcher.Post(() => OnRemoteNoteDeleted(noteName));

            if (!_settings.SyncFolderValid)
            {
                MessageBox.Show(
                    "NppSync: sync folder not found or not configured.\nPlease set it via Plugins > NppSync > Settings.",
                    "NppSync", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _fileSync.EnsureMetaFolder();

            // Startup scan: wait for Google Drive to settle, then open all notes.
            // Stored in a static field to prevent the GC from collecting the timer.
            int delayMs = _settings.StartupDelaySec * 1000;
            _scanTimer = new System.Threading.Timer(_ =>
                UiThreadDispatcher.Post(StartupScan), null, delayMs, System.Threading.Timeout.Infinite);
        }

        private static void StartupScan()
        {
            if (!_settings.SyncFolderValid) return;
            _fileSync.EnsureMetaFolder();

            // Seed the watcher BEFORE opening files so we don't re-fire "appeared" for existing notes
            _watcher.Start();

            string[] txtFiles = Directory.GetFiles(
                _settings.SyncFolder, "*" + _settings.FileExtension);

            // Collect already-open paths to avoid duplicates
            string[] openPaths = Npp.notepad.GetOpenFileNames();
            var openSet = new System.Collections.Generic.HashSet<string>(
                openPaths, StringComparer.OrdinalIgnoreCase);

            foreach (string filePath in txtFiles)
            {
                string noteName = Path.GetFileNameWithoutExtension(filePath);

                // Skip dismissed
                if (_dismissed.IsDismissed(noteName)) continue;

                // Check metadata for deleted flag
                var meta = _fileSync.ReadMetadata(noteName);
                if (meta != null && meta.Deleted) continue;

                // Skip if already open
                if (openSet.Contains(filePath)) continue;
                if (_tabs.IsManaged(filePath)) continue;

                if (_settings.OpenRemoteTabsAutomatically)
                {
                    OpenFileInBackground(filePath);
                    _tabs.Register(filePath, noteName);
                    _autoSaver.Track(filePath);
                    ShowStatusBar($"NppSync: opened \"{noteName}\"");
                }
            }
        }

        // ── NPPN_FILEOPENED ────────────────────────────────────────────────────

        private static void OnFileOpened(IntPtr bufferId)
        {
            // NPPN_FILEOPENED can fire before NPPN_READY during session restore — guard against null state.
            if (_tabs == null) return;

            // We get this for both Ctrl+N (new unsaved) AND for files opened from disk.
            // If the file is already in the sync folder (opened by StartupScan or remote watcher),
            // register it but don't re-generate a name.
            string filePath = Npp.notepad.GetFilePath(bufferId);

            // Already managed (e.g. opened by StartupScan/watcher)
            if (_tabs.IsManaged(filePath)) return;

            if (_settings.SyncFolderValid &&
                !string.IsNullOrEmpty(filePath) &&
                filePath.StartsWith(_settings.SyncFolder, StringComparison.OrdinalIgnoreCase))
            {
                // File opened from the sync folder by some other means — register it
                string noteName = Path.GetFileNameWithoutExtension(filePath);
                _tabs.Register(filePath, noteName);
                _autoSaver.Track(filePath);
                return;
            }

            // Any other file (unsaved new tabs, etc.) — not a synced note, ignore.
        }

        /// <summary>
        /// Assigns a generated name to an unsaved new buffer and saves it to the sync folder.
        /// </summary>
        private static void CreateSyncedNote(IntPtr bufferId)
        {
            string noteName = NameGenerator.Generate(_settings.SyncFolder, _settings.FileExtension);
            string filePath = Path.Combine(_settings.SyncFolder, noteName + _settings.FileExtension);

            // Save the empty buffer to the sync folder path.
            // NPPM_SAVECURRENTFILEAS requires the buffer to be the active one.
            // Make sure the new buffer is active first (it should be, since NPPN_FILEOPENED
            // fires after the buffer is activated), then save.
            bool saved = (int)SendMessage(
                PluginBase.nppData._nppHandle,
                (uint)NppMsg.NPPM_SAVECURRENTFILEAS,
                1, // 1 = silent (no dialog)
                filePath) != 0;

            if (!saved)
                return; // Failed to save — leave as normal unsaved tab

            // Create metadata
            var meta = NoteMetadata.Create(noteName, _settings.MachineId);
            _fileSync.EnsureMetaFolder();
            _fileSync.WriteMetadata(meta);

            // Register
            _tabs.Register(filePath, noteName);
            _autoSaver.Track(filePath);
        }

        // ── NPPN_FILESAVED ─────────────────────────────────────────────────────

        private static void OnFileSaved(IntPtr bufferId)
        {
            if (_tabs == null) return;

            string newPath = Npp.notepad.GetFilePath(bufferId);

            // Check if this buffer was previously tracked under a different path (Save As)
            if (_bufferPaths.TryGetValue(bufferId, out string oldPath) && oldPath != newPath)
            {
                if (_tabs.IsManaged(oldPath))
                    HandleNoteGraduation(oldPath, newPath);
            }

            // Update our path record for this buffer
            if (!string.IsNullOrEmpty(newPath))
                _bufferPaths[bufferId] = newPath;
        }

        private static void HandleNoteGraduation(string oldPath, string newPath)
        {
            _tabs.TryGet(oldPath, out var tab);
            string oldName = tab?.NoteName ?? Path.GetFileNameWithoutExtension(oldPath);

            bool movedInsideSyncFolder = !string.IsNullOrEmpty(newPath) &&
                newPath.StartsWith(_settings.SyncFolder, StringComparison.OrdinalIgnoreCase);

            // Stop auto-saving under the old path
            _autoSaver.Untrack(oldPath);
            _tabs.Unregister(oldPath);

            if (movedInsideSyncFolder)
            {
                // Rename: re-register under the new path
                string newName = Path.GetFileNameWithoutExtension(newPath);
                _fileSync.DeleteNote(oldName);
                _fileSync.DeleteMetadata(oldName);

                var meta = NoteMetadata.Create(newName, _settings.MachineId);
                _fileSync.WriteMetadata(meta);

                _tabs.Register(newPath, newName);
                _autoSaver.Track(newPath);
            }
            else
            {
                // Graduated — remove from sync folder entirely
                _fileSync.DeleteNote(oldName);
                _fileSync.DeleteMetadata(oldName);
                // Tab is now a regular Notepad++ file — no further tracking needed
            }
        }

        // ── NPPN_FILEBEFORECLOSE ───────────────────────────────────────────────

        private static void OnFileBeforeClose(IntPtr bufferId)
        {
            if (_tabs == null) return;

            // During app shutdown, Notepad++ closes all tabs — skip the dialog and just save.
            if (isShuttingDown)
            {
                string fp = Npp.notepad.GetFilePath(bufferId);
                if (_tabs.IsManaged(fp)) SaveManagedTab(fp);
                return;
            }

            string filePath = Npp.notepad.GetFilePath(bufferId);
            if (!_tabs.IsManaged(filePath)) return;

            _tabs.TryGet(filePath, out var tab);
            string noteName = tab?.NoteName ?? Path.GetFileNameWithoutExtension(filePath);

            // Auto-save current content first (keeps buffer clean — no Npp save dialog)
            SaveManagedTab(filePath);

            // Show our custom close dialog
            using (var dlg = new CloseNoteDialog(noteName))
            {
                dlg.ShowDialog();
                switch (dlg.Choice)
                {
                case CloseNoteResult.Dismiss:
                    _dismissed.Dismiss(noteName);
                    _autoSaver.Untrack(filePath);
                    _tabs.Unregister(filePath);
                    break;

                case CloseNoteResult.DeleteEverywhere:
                    _autoSaver.Untrack(filePath);
                    _tabs.Unregister(filePath);
                    _fileSync.MarkDeleted(noteName);
                    _fileSync.DeleteNote(noteName);
                    break;

                case CloseNoteResult.Cancel:
                    // We can't cancel NPPN_FILEBEFORECLOSE from a plugin notification.
                    // The best we can do is re-open the file immediately after close.
                    // Schedule a re-open on the next message pump cycle.
                    string pathToReopen = filePath;
                    UiThreadDispatcher.Post(() =>
                    {
                        if (!_tabs.IsManaged(pathToReopen))
                        {
                            Npp.notepad.OpenFile(pathToReopen);
                            _tabs.Register(pathToReopen, Path.GetFileNameWithoutExtension(pathToReopen));
                            _autoSaver.Track(pathToReopen);
                        }
                    });
                    break;
                }
            }
        }

        // ── NPPN_BUFFERACTIVATED ───────────────────────────────────────────────

        private static void OnBufferActivated(IntPtr bufferId)
        {
            // NPPN_BUFFERACTIVATED can fire before NPPN_READY during session restore — guard against null state.
            if (_tabs == null) return;

            // Flush the previously active managed tab before switching
            if (_activeFilePath != null && _tabs.IsManaged(_activeFilePath))
                _autoSaver.FlushNow(_activeFilePath);

            string filePath = Npp.notepad.GetFilePath(bufferId);
            _activeFilePath = filePath;
            _bufferPaths[bufferId] = filePath;

            // Re-create Scintilla gateway for the new active buffer
            Npp.editor = new ScintillaGateway(PluginBase.GetCurrentScintilla());
        }

        // ── Remote events (all called on UI thread via UiThreadDispatcher) ─────

        private static void OnRemoteNoteAppeared(string noteName)
        {
            if (!_settings.OpenRemoteTabsAutomatically) return;

            string filePath = _fileSync.NoteFilePath(noteName);

            // Duplicate check
            if (_tabs.IsManaged(filePath)) return;
            string[] openPaths = Npp.notepad.GetOpenFileNames();
            foreach (string p in openPaths)
                if (string.Equals(p, filePath, StringComparison.OrdinalIgnoreCase)) return;

            if (_dismissed.IsDismissed(noteName)) return;

            // Check metadata — non-blocking single attempt only (never sleep on UI thread).
            // If meta is unavailable, proceed anyway; the tabs/openFiles check above already
            // prevents re-opening a note we own. If meta IS available, honour echo and delete flags.
            var meta = _fileSync.ReadMetadata(noteName);
            if (meta != null)
            {
                if (string.Equals(meta.CreatedBy, _settings.MachineId, StringComparison.OrdinalIgnoreCase))
                    return; // Echo: we created this note on this machine
                if (meta.Deleted) return;
            }

            OpenFileInBackground(filePath);
            _tabs.Register(filePath, noteName);
            _autoSaver.Track(filePath);
            ShowStatusBar($"NppSync: opened remote note \"{noteName}\"");
        }

        private static void OnRemoteNoteChanged(string noteName)
        {
            string filePath = _fileSync.NoteFilePath(noteName);
            if (!_tabs.IsManaged(filePath)) return;
            if (!_tabs.TryGet(filePath, out var tab)) return;

            // Anti-echo: if we wrote this file recently, ignore
            if (tab.IsEcho()) return;

            // Check metadata — skip if we're the last modifier
            var meta = _fileSync.ReadMetadata(noteName);
            if (meta != null &&
                string.Equals(meta.LastModifiedBy, _settings.MachineId, StringComparison.OrdinalIgnoreCase))
                return;

            // Update the Scintilla buffer.
            // NOTE: SCI_SETTEXT clears undo history — known v1 limitation.
            string content = _fileSync.ReadNote(noteName);
            if (content == null) return;

            // For inactive tabs: silently reload from disk so Notepad++ never shows
            // the "file modified by external program" dialog.
            if (!string.Equals(_activeFilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                SendMessage(PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_RELOADFILE, 0, filePath);
                ShowStatusBar($"NppSync: updated \"{noteName}\" from remote", 10000);
                return;
            }

            // Active tab: update the Scintilla buffer directly (faster, preserves cursor/scroll).
            IntPtr currentScintilla = PluginBase.GetCurrentScintilla();
            var sci = new ScintillaGateway(currentScintilla);

            int curPos = sci.GetCurrentPos();
            int firstLine = sci.GetFirstVisibleLine();

            sci.SetText(content);
            sci.SetSavePoint();
            ShowStatusBar($"NppSync: updated \"{noteName}\" from remote");

            // Restore cursor/scroll as closely as possible
            int len = (int)sci.GetLength();
            sci.SetCurrentPos(Math.Min(curPos, len));
            sci.SetFirstVisibleLine(firstLine);
        }

        private static void OnRemoteNoteDeleted(string noteName)
        {
            string filePath = _fileSync.NoteFilePath(noteName);
            if (!_tabs.IsManaged(filePath)) return;

            // Close silently — remote deletion is intentional
            _autoSaver.Untrack(filePath);
            _tabs.Unregister(filePath);

            ShowStatusBar($"NppSync: note \"{noteName}\" was deleted remotely");

            // Close the tab (this will trigger NPPN_FILEBEFORECLOSE again —
            // since we've unregistered from _tabs, that handler will be a no-op)
            SendMessage(PluginBase.nppData._nppHandle,
                (uint)NppMsg.NPPM_MENUCOMMAND, 0, NppMenuCmd.IDM_FILE_CLOSE);
        }

        // ── Save helper ────────────────────────────────────────────────────────

        private static void SaveManagedTab(string filePath)
        {
            if (!_tabs.TryGet(filePath, out var tab)) return;

            bool isActive = string.Equals(_activeFilePath, filePath, StringComparison.OrdinalIgnoreCase);

            if (isActive)
            {
                // Active tab: check size limit before saving
                int len = (int)Npp.editor.GetLength();
                if (len > 1024 * 1024)
                {
                    MessageBox.Show(
                        $"Note \"{tab.NoteName}\" exceeds 1 MB. Auto-save skipped.",
                        "NppSync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Use NPPM_SAVECURRENTFILE (Notepad++'s own Ctrl+S equivalent) so that
                // Notepad++ writes the file itself and does NOT show the
                // "file modified by external program — reload?" dialog.
                // NPPM_SAVECURRENTFILE also automatically calls SCI_SETSAVEPOINT internally.
                SendMessage(PluginBase.nppData._nppHandle,
                    (uint)NppMsg.NPPM_SAVECURRENTFILE, 0, 0);

                tab.LastLocalWrite = DateTime.UtcNow;
            }
            else
            {
                // Non-active tab: SCN_MODIFIED never fires for inactive buffers, so there
                // is no unsaved content to flush — nothing to do here.
                return;
            }

            // Update metadata (written to .nppsync/<name>.meta.json — not open in Npp,
            // so no reload dialog risk from this write).
            var meta = _fileSync.ReadMetadata(tab.NoteName) ??
                       NoteMetadata.Create(tab.NoteName, _settings.MachineId);
            meta.UpdateModified(_settings.MachineId);
            _fileSync.WriteMetadata(meta);
        }

        // ── Background file open ───────────────────────────────────────────────

        /// <summary>
        /// Opens a file without stealing focus from the currently active tab.
        /// Saves the current buffer position, opens the file (which activates it),
        /// then immediately reactivates the previous buffer.
        /// </summary>
        private static void OpenFileInBackground(string filePath)
        {
            // Capture where we are now
            IntPtr prevBufferId = SendMessage(
                PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_GETCURRENTBUFFERID, 0, 0);

            int prevPos = (int)SendMessage(
                PluginBase.nppData._nppHandle, (uint)NppMsg.NPPM_GETPOSFROMBUFFERID,
                prevBufferId, 0);

            // Open the new file (becomes active momentarily)
            Npp.notepad.OpenFile(filePath);

            // Restore the previous tab if we had one
            if (prevBufferId != IntPtr.Zero && prevPos >= 0)
            {
                int prevView  = (prevPos >> 30) & 0x3;
                int prevIndex = prevPos & 0x3FFFFFFF;
                SendMessage(PluginBase.nppData._nppHandle,
                    (uint)NppMsg.NPPM_ACTIVATEDOC, prevView, prevIndex);
            }
        }

        // ── Status bar ─────────────────────────────────────────────────────────

        /// <summary>
        /// Shows a transient message in the Notepad++ status bar (DocType section).
        /// Automatically clears back to empty after <paramref name="durationMs"/> milliseconds.
        /// </summary>
        private static void ShowStatusBar(string message, int durationMs = 20000)
        {
            Npp.notepad.SetStatusBarSection(message, StatusBarSection.DocType);

            _statusBarTimer?.Dispose();
            _statusBarTimer = new System.Threading.Timer(_ =>
                UiThreadDispatcher.Post(() =>
                    Npp.notepad.SetStatusBarSection("", StatusBarSection.DocType)),
                null, durationMs, System.Threading.Timeout.Infinite);
        }

        // ── Menu commands ──────────────────────────────────────────────────────

        private static void CmdNewNote()
        {
            if (!CheckSyncFolder()) return;

            // Create a new empty file in the sync folder
            string noteName = NameGenerator.Generate(_settings.SyncFolder, _settings.FileExtension);
            string filePath = Path.Combine(_settings.SyncFolder, noteName + _settings.FileExtension);

            File.WriteAllText(filePath, "", Encoding.UTF8);

            var meta = NoteMetadata.Create(noteName, _settings.MachineId);
            _fileSync.EnsureMetaFolder();
            _fileSync.WriteMetadata(meta);

            Npp.notepad.OpenFile(filePath);
            _tabs.Register(filePath, noteName);
            _autoSaver.Track(filePath);
        }

        private static void CmdDeleteNote()
        {
            string filePath = Npp.notepad.GetCurrentFilePath();
            if (!_tabs.IsManaged(filePath))
            {
                MessageBox.Show("The current tab is not a synced NppSync note.",
                    "NppSync", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _tabs.TryGet(filePath, out var tab);
            string noteName = tab?.NoteName ?? Path.GetFileNameWithoutExtension(filePath);

            var result = MessageBox.Show(
                $"Delete \"{noteName}\" everywhere (all machines)?",
                "NppSync — Delete Note", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (result != DialogResult.Yes) return;

            _autoSaver.Untrack(filePath);
            _tabs.Unregister(filePath);
            _fileSync.MarkDeleted(noteName);
            _fileSync.DeleteNote(noteName);

            SendMessage(PluginBase.nppData._nppHandle,
                (uint)NppMsg.NPPM_MENUCOMMAND, 0, NppMenuCmd.IDM_FILE_CLOSE);
        }

        private static void CmdRenameNote()
        {
            string filePath = Npp.notepad.GetCurrentFilePath();
            if (!_tabs.IsManaged(filePath))
            {
                MessageBox.Show("The current tab is not a synced NppSync note.",
                    "NppSync", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!CheckSyncFolder()) return;

            _tabs.TryGet(filePath, out var tab);
            string oldName = tab?.NoteName ?? Path.GetFileNameWithoutExtension(filePath);

            // Save current content
            SaveManagedTab(filePath);

            string newName = NameGenerator.Generate(_settings.SyncFolder, _settings.FileExtension);
            string newPath = Path.Combine(_settings.SyncFolder, newName + _settings.FileExtension);

            // Copy file content to new name
            string content = _fileSync.ReadNote(oldName) ?? "";
            File.WriteAllText(newPath, content, Encoding.UTF8);

            // Create metadata for new name
            var meta = NoteMetadata.Create(newName, _settings.MachineId);
            _fileSync.WriteMetadata(meta);

            // Delete old files
            _fileSync.DeleteNote(oldName);
            _fileSync.DeleteMetadata(oldName);

            // Untrack old, open new
            _autoSaver.Untrack(filePath);
            _tabs.Unregister(filePath);

            Npp.notepad.OpenFile(newPath);
            _tabs.Register(newPath, newName);
            _autoSaver.Track(newPath);

            // Close the old tab
            SendMessage(PluginBase.nppData._nppHandle,
                (uint)NppMsg.NPPM_MENUCOMMAND, 0, NppMenuCmd.IDM_FILE_CLOSE);
        }

        private static void CmdOpenSyncFolder()
        {
            if (!_settings.SyncFolderValid)
            {
                MessageBox.Show("Sync folder is not configured or does not exist.",
                    "NppSync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            System.Diagnostics.Process.Start("explorer.exe", _settings.SyncFolder);
        }

        private static void CmdRestoreAll()
        {
            _dismissed.RestoreAll();
            MessageBox.Show(
                "Dismissed notes list cleared.\nRestart Notepad++ to re-open previously dismissed notes.",
                "NppSync", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void CmdShowDismissed()
        {
            var names = new System.Collections.Generic.List<string>(_dismissed.GetAll());
            if (names.Count == 0)
            {
                MessageBox.Show("No dismissed notes.", "NppSync — Dismissed Notes",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            // Simple list dialog — each line shows the note name with a Restore option
            // For v1 this is a read-only list; users use "Restore All" to get them back
            string list = string.Join("\r\n", names);
            MessageBox.Show(
                "Dismissed notes on this machine:\r\n\r\n" + list +
                "\r\n\r\nUse 'Restore All Dismissed Notes' to re-open all of them.",
                "NppSync — Dismissed Notes",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private static void CmdSettings()
        {
            if (_settings == null)
            {
                MessageBox.Show("Plugin has not finished initializing.", "NppSync",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            using (var form = new SettingsForm(_settings))
            {
                form.ShowDialog();
                if (form.Saved)
                {
                    // If sync folder changed, restart watcher and re-initialize
                    _fileSync = new FileSyncService(_settings);
                    _fileSync.EnsureMetaFolder();
                    _watcher?.Stop();
                    _watcher = new FolderWatcher(_settings);
                    _watcher.NoteAppeared += noteName =>
                        UiThreadDispatcher.Post(() => OnRemoteNoteAppeared(noteName));
                    _watcher.NoteChanged += noteName =>
                        UiThreadDispatcher.Post(() => OnRemoteNoteChanged(noteName));
                    _watcher.NoteDeleted += noteName =>
                        UiThreadDispatcher.Post(() => OnRemoteNoteDeleted(noteName));
                    if (_settings.SyncFolderValid)
                        StartupScan();
                }
            }
        }

        private static void CmdAbout()
        {
            MessageBox.Show(
                "NppSync v1.0\r\n\r\n" +
                "Syncs unsaved scratch notes across machines via a shared folder.\r\n\r\n" +
                "Machine ID: " + (_settings?.MachineId ?? "not initialized"),
                "NppSync — About",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static bool CheckSyncFolder()
        {
            if (_settings != null && _settings.SyncFolderValid) return true;
            MessageBox.Show(
                "NppSync sync folder is not configured or does not exist.\nPlease configure it via Plugins > NppSync > Settings.",
                "NppSync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }
}
