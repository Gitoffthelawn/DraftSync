using System;
using System.Windows.Forms;

namespace NppSync.Forms
{
    public enum CloseNoteResult { Dismiss, DeleteEverywhere, Cancel }

    public partial class CloseNoteDialog : Form
    {
        public CloseNoteResult Choice { get; private set; } = CloseNoteResult.Cancel;

        public CloseNoteDialog(string noteName)
        {
            InitializeComponent();
            labelMessage.Text = $"Close synced note \"{noteName}\"?";
        }

        private void btnDismiss_Click(object sender, EventArgs e)
        {
            Choice = CloseNoteResult.Dismiss;
            Close();
        }

        private void btnDeleteEverywhere_Click(object sender, EventArgs e)
        {
            Choice = CloseNoteResult.DeleteEverywhere;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            Choice = CloseNoteResult.Cancel;
            Close();
        }
    }
}
