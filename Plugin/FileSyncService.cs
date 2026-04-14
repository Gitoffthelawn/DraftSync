using System;
using System.IO;
using System.Text;
using System.Threading;

namespace DraftSync.Plugin
{
    /// <summary>
    /// Reads and writes note content and metadata to/from the shared sync folder.
    /// All file I/O is wrapped in retry logic to handle transient Google Drive locks.
    /// </summary>
    public class FileSyncService
    {
        private readonly Settings _settings;
        private const int RetryCount = 3;
        private const int RetryDelayMs = 500;

        public FileSyncService(Settings settings)
        {
            _settings = settings;
        }

        // ── Paths ──────────────────────────────────────────────────────────────

        public string NoteFilePath(string noteName) =>
            Path.Combine(_settings.SyncFolder, noteName + _settings.FileExtension);

        public string MetaFilePath(string noteName) =>
            Path.Combine(_settings.MetaFolder, noteName + ".meta.json");

        // ── Ensure folders exist ───────────────────────────────────────────────

        public bool EnsureMetaFolder()
        {
            string meta = _settings.MetaFolder;
            if (meta == null) return false;
            try
            {
                if (!Directory.Exists(meta))
                {
                    Directory.CreateDirectory(meta);
                    File.SetAttributes(meta, FileAttributes.Hidden | FileAttributes.Directory);
                }
                return true;
            }
            catch { return false; }
        }

        // ── Write note ─────────────────────────────────────────────────────────

        /// <summary>
        /// Writes note content to the sync folder.
        /// Returns true on success. Updates tab.LastLocalWrite on success.
        /// </summary>
        public bool WriteNote(ManagedTab tab, string content)
        {
            if (!_settings.SyncFolderValid)
                return false;

            // Guard: skip very large files
            if (Encoding.UTF8.GetByteCount(content) > 1024 * 1024)
                return false;

            string path = NoteFilePath(tab.NoteName);
            bool ok = RetryWrite(() =>
                File.WriteAllText(path, content, Encoding.UTF8));

            if (ok)
                tab.LastLocalWrite = DateTime.UtcNow;

            return ok;
        }

        /// <summary>
        /// Creates or updates a note's metadata file.
        /// </summary>
        public bool WriteMetadata(NoteMetadata meta)
        {
            if (!EnsureMetaFolder())
                return false;
            string path = MetaFilePath(meta.Name);
            return RetryWrite(() =>
                File.WriteAllText(path, meta.ToJson(), Encoding.UTF8));
        }

        // ── Read note ──────────────────────────────────────────────────────────

        public string ReadNote(string noteName)
        {
            string path = NoteFilePath(noteName);
            try
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch { return null; }
        }

        public NoteMetadata ReadMetadata(string noteName)
        {
            string path = MetaFilePath(noteName);
            return NoteMetadata.TryLoad(path);
        }

        /// <summary>
        /// Tries to read metadata, retrying up to 3 times with 500ms gaps.
        /// Used when a .txt appears before its .meta.json (race condition).
        /// </summary>
        public NoteMetadata ReadMetadataWithRetry(string noteName)
        {
            for (int i = 0; i < RetryCount; i++)
            {
                var meta = ReadMetadata(noteName);
                if (meta != null) return meta;
                if (i < RetryCount - 1)
                    Thread.Sleep(RetryDelayMs);
            }
            return null;
        }

        // ── Delete note ────────────────────────────────────────────────────────

        public void DeleteNote(string noteName)
        {
            TryDelete(NoteFilePath(noteName));
        }

        public void DeleteMetadata(string noteName)
        {
            TryDelete(MetaFilePath(noteName));
        }

        public bool MarkDeleted(string noteName)
        {
            var meta = ReadMetadata(noteName);
            if (meta == null)
                meta = NoteMetadata.Create(noteName, _settings.MachineId);
            meta.Deleted = true;
            return WriteMetadata(meta);
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private bool RetryWrite(Action write)
        {
            for (int i = 0; i < RetryCount; i++)
            {
                try
                {
                    write();
                    return true;
                }
                catch (IOException)
                {
                    if (i < RetryCount - 1)
                        Thread.Sleep(RetryDelayMs);
                }
                catch { return false; }
            }
            return false;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { /* best-effort */ }
        }
    }
}
