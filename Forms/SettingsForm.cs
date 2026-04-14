using System;
using System.IO;
using System.Windows.Forms;
using DraftSync.Plugin;

namespace DraftSync.Forms
{
    public partial class SettingsForm : Form
    {
        private readonly Settings _settings;
        /// <summary>True if the user saved changes (OK was clicked).</summary>
        public bool Saved { get; private set; }

        public SettingsForm(Settings settings)
        {
            InitializeComponent();
            _settings = settings;
            LoadFromSettings();
        }

        private void LoadFromSettings()
        {
            txtSyncFolder.Text = _settings.SyncFolder;
            txtMachineId.Text = _settings.MachineId;
            // Populate extension combo
            cmbExtension.Items.Clear();
            cmbExtension.Items.AddRange(new[] { ".txt", ".md", ".js", ".py", ".cs" });
            cmbExtension.Text = _settings.FileExtension;
            nudAutoSave.Value = Math.Max(nudAutoSave.Minimum,
                Math.Min(nudAutoSave.Maximum, _settings.AutoSaveDelaySec));
            nudPoll.Value = Math.Max(nudPoll.Minimum,
                Math.Min(nudPoll.Maximum, _settings.PollIntervalSec));
            chkAutoOpen.Checked = _settings.OpenRemoteTabsAutomatically;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Select your DraftSync shared folder (e.g. Google Drive folder)";
                dlg.SelectedPath = txtSyncFolder.Text;
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    txtSyncFolder.Text = dlg.SelectedPath;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            string folder = txtSyncFolder.Text.Trim();
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                MessageBox.Show(
                    $"Sync folder does not exist:\n{folder}\n\nPlease choose an existing folder.",
                    "DraftSync", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string ext = cmbExtension.Text.Trim();
            if (!ext.StartsWith(".")) ext = "." + ext;
            if (ext.Length < 2)
            {
                MessageBox.Show("Please enter a valid file extension.", "DraftSync",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _settings.SyncFolder = folder;
            _settings.MachineId = txtMachineId.Text.Trim();
            _settings.FileExtension = ext;
            _settings.AutoSaveDelaySec = (int)nudAutoSave.Value;
            _settings.PollIntervalSec = (int)nudPoll.Value;
            _settings.OpenRemoteTabsAutomatically = chkAutoOpen.Checked;
            _settings.Save();
            Saved = true;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
