using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DraftSync.Plugin
{
    /// <summary>
    /// Plugin settings backed by DraftSync.ini in the Notepad++ plugin config directory.
    /// </summary>
    public class Settings
    {
        private readonly string _iniPath;

        // ── INI section ──────────────────────────────────────────────────────
        private const string Section = "DraftSync";

        // ── Defaults ──────────────────────────────────────────────────────────
        public const string DefaultFileExtension = ".txt";
        public const int DefaultAutoSaveDelaySec = 3;
        public const int DefaultMaxAutoSaveDelaySec = 10;
        public const int DefaultPollIntervalSec = 5;
        public const int DefaultStartupDelaySec = 5;

        // ── Properties ────────────────────────────────────────────────────────
        public string SyncFolder { get; set; } = "";
        public string MachineId { get; set; } = "";
        public string FileExtension { get; set; } = DefaultFileExtension;
        public int AutoSaveDelaySec { get; set; } = DefaultAutoSaveDelaySec;
        public int MaxAutoSaveDelaySec { get; set; } = DefaultMaxAutoSaveDelaySec;
        public int PollIntervalSec { get; set; } = DefaultPollIntervalSec;
        public int StartupDelaySec { get; set; } = DefaultStartupDelaySec;
        public bool OpenRemoteTabsAutomatically { get; set; } = true;

        // ── Derived helpers ───────────────────────────────────────────────────
        public bool SyncFolderValid => !string.IsNullOrWhiteSpace(SyncFolder) && Directory.Exists(SyncFolder);
        public string MetaFolder => SyncFolderValid ? Path.Combine(SyncFolder, ".draftsync") : null;
        public string DismissedFilePath { get; }

        public Settings(string configDir)
        {
            _iniPath = Path.Combine(configDir, "DraftSync.ini");
            DismissedFilePath = Path.Combine(configDir, "DraftSync.dismissed.json");
        }

        // ── Load / Save ───────────────────────────────────────────────────────

        public void Load()
        {
            SyncFolder = ReadIni("SyncFolder", "");
            MachineId = ReadIni("MachineId", "");
            FileExtension = ReadIni("FileExtension", DefaultFileExtension);
            AutoSaveDelaySec = ReadIniInt("AutoSaveDelaySec", DefaultAutoSaveDelaySec);
            MaxAutoSaveDelaySec = ReadIniInt("MaxAutoSaveDelaySec", DefaultMaxAutoSaveDelaySec);
            PollIntervalSec = ReadIniInt("PollIntervalSec", DefaultPollIntervalSec);
            StartupDelaySec = ReadIniInt("StartupDelaySec", DefaultStartupDelaySec);
            OpenRemoteTabsAutomatically = ReadIniBool("OpenRemoteTabsAutomatically", true);
        }

        public void Save()
        {
            WriteIni("SyncFolder", SyncFolder ?? "");
            WriteIni("MachineId", MachineId ?? "");
            WriteIni("FileExtension", FileExtension ?? DefaultFileExtension);
            WriteIni("AutoSaveDelaySec", AutoSaveDelaySec.ToString());
            WriteIni("MaxAutoSaveDelaySec", MaxAutoSaveDelaySec.ToString());
            WriteIni("PollIntervalSec", PollIntervalSec.ToString());
            WriteIni("StartupDelaySec", StartupDelaySec.ToString());
            WriteIni("OpenRemoteTabsAutomatically", OpenRemoteTabsAutomatically ? "true" : "false");
        }

        // ── INI helpers (Win32 API) ────────────────────────────────────────────

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetPrivateProfileString(
            string lpAppName, string lpKeyName, string lpDefault,
            StringBuilder lpReturnedString, uint nSize, string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool WritePrivateProfileString(
            string lpAppName, string lpKeyName, string lpString, string lpFileName);

        private string ReadIni(string key, string defaultValue)
        {
            var sb = new StringBuilder(512);
            GetPrivateProfileString(Section, key, defaultValue ?? "", sb, (uint)sb.Capacity, _iniPath);
            return sb.ToString();
        }

        private int ReadIniInt(string key, int defaultValue)
        {
            string raw = ReadIni(key, defaultValue.ToString());
            return int.TryParse(raw, out int v) ? v : defaultValue;
        }

        private bool ReadIniBool(string key, bool defaultValue)
        {
            string raw = ReadIni(key, defaultValue ? "true" : "false").ToLowerInvariant();
            return raw == "true" || raw == "1" || raw == "yes";
        }

        private void WriteIni(string key, string value)
        {
            WritePrivateProfileString(Section, key, value, _iniPath);
        }

        // ── Machine ID generation ─────────────────────────────────────────────

        public static string GenerateMachineId()
        {
            string computer = Environment.MachineName;
            string hex = new Random().Next(0, 0x10000).ToString("x4");
            return $"{computer}-{hex}";
        }
    }
}
