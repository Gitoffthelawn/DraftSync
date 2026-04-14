// Stub: DraftSync does not support UI translation.
// This file exists only because PluginInfrastructure/NppPluginNETBase.cs references Translator.
using System.Windows.Forms;

namespace NppDemo.Utils
{
    public static class Translator
    {
        public static string GetTranslatedMenuItem(string name) => name;

        public static void ResetTranslations(bool atStartup) { }

        public static DialogResult ShowTranslatedMessageBox(
            string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon,
            int numFormatArgs = 0, params object[] args)
        {
            string formatted = numFormatArgs > 0 ? string.Format(text, args) : text;
            return MessageBox.Show(formatted, caption, buttons, icon);
        }
    }
}
