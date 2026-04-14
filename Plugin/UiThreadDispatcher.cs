using System;
using System.Runtime.InteropServices;
using Kbg.NppPluginNET.PluginInfrastructure;

namespace DraftSync.Plugin
{
    /// <summary>
    /// Marshals work from background threads (FolderWatcher, AutoSaver callbacks)
    /// to the Notepad++ main UI thread by posting a custom Windows message.
    ///
    /// Usage:
    ///   UiThreadDispatcher.Post(() => { /* code that runs on UI thread */ });
    /// </summary>
    public class UiThreadDispatcher : System.Windows.Forms.NativeWindow, IDisposable
    {
        // An application-defined window message
        private const int WM_DRAFTSYNC_INVOKE = 0x8000 + 42;
        private const int WM_COMMAND        = 0x0111;
        private const int WM_CLOSE          = 0x0010;
        private const uint IDM_FILE_NEW     = 40000 + 1000 + 1; // NppMenuCmd.IDM_FILE_NEW

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private static UiThreadDispatcher _instance;

        /// <summary>
        /// Set this to intercept Ctrl+N. Return true to swallow the event (prevent Npp's default
        /// new-tab behaviour); return false to let it pass through.
        /// </summary>
        public static Func<bool> OnNewFileRequested;

        // Queue of pending actions — accessed from multiple threads
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _queue =
            new System.Collections.Concurrent.ConcurrentQueue<Action>();

        public static void Initialize()
        {
            if (_instance != null) return;
            _instance = new UiThreadDispatcher();
            // Attach to the Notepad++ main window so WndProc receives messages posted to it
            _instance.AssignHandle(PluginBase.nppData._nppHandle);
        }

        public static void Post(Action action)
        {
            if (_instance == null) return;
            _instance._queue.Enqueue(action);
            PostMessage(PluginBase.nppData._nppHandle, WM_DRAFTSYNC_INVOKE, IntPtr.Zero, IntPtr.Zero);
        }

        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            if (m.Msg == WM_DRAFTSYNC_INVOKE)
            {
                while (_queue.TryDequeue(out Action action))
                {
                    try { action(); }
                    catch { /* don't crash the message pump on plugin errors */ }
                }
                return;
            }

            // WM_CLOSE fires before Npp starts closing individual tabs, so we set
            // isShuttingDown here to suppress our per-tab close dialog during app exit.
            if (m.Msg == WM_CLOSE)
            {
                Kbg.NppPluginNET.Main.isShuttingDown = true;
                base.WndProc(ref m);
                return;
            }

            // Intercept Ctrl+N (IDM_FILE_NEW) so we can create a synced note instead
            // of Notepad++'s default blank unsaved tab.
            if (m.Msg == WM_COMMAND &&
                ((uint)m.WParam.ToInt64() & 0xFFFF) == IDM_FILE_NEW &&
                OnNewFileRequested != null)
            {
                try
                {
                    bool handled = OnNewFileRequested();
                    if (handled) return; // swallow — don't let Npp create its own blank tab
                }
                catch { }
            }

            base.WndProc(ref m);
        }

        public void Dispose()
        {
            ReleaseHandle();
            _instance = null;
        }
    }
}
