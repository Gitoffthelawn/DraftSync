namespace DraftSync.Forms
{
    partial class SettingsForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblSyncFolder = new System.Windows.Forms.Label();
            this.txtSyncFolder = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.lblMachineId = new System.Windows.Forms.Label();
            this.txtMachineId = new System.Windows.Forms.TextBox();
            this.lblExtension = new System.Windows.Forms.Label();
            this.cmbExtension = new System.Windows.Forms.ComboBox();
            this.lblAutoSave = new System.Windows.Forms.Label();
            this.nudAutoSave = new System.Windows.Forms.NumericUpDown();
            this.lblPoll = new System.Windows.Forms.Label();
            this.nudPoll = new System.Windows.Forms.NumericUpDown();
            this.chkAutoOpen = new System.Windows.Forms.CheckBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.nudAutoSave)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudPoll)).BeginInit();
            this.SuspendLayout();

            int lw = 140, tx = 150, tw = 200, bh = 24, row = 10, gap = 34;

            // Sync Folder
            this.lblSyncFolder.Text = "Sync Folder:";
            this.lblSyncFolder.Location = new System.Drawing.Point(10, row);
            this.lblSyncFolder.Size = new System.Drawing.Size(lw, bh);
            this.txtSyncFolder.Location = new System.Drawing.Point(tx, row);
            this.txtSyncFolder.Size = new System.Drawing.Size(tw, bh);
            this.btnBrowse.Text = "Browse…";
            this.btnBrowse.Location = new System.Drawing.Point(tx + tw + 6, row);
            this.btnBrowse.Size = new System.Drawing.Size(70, bh);
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            row += gap;

            // Machine ID
            this.lblMachineId.Text = "Machine ID:";
            this.lblMachineId.Location = new System.Drawing.Point(10, row);
            this.lblMachineId.Size = new System.Drawing.Size(lw, bh);
            this.txtMachineId.Location = new System.Drawing.Point(tx, row);
            this.txtMachineId.Size = new System.Drawing.Size(tw, bh);
            row += gap;

            // File Extension
            this.lblExtension.Text = "File Extension:";
            this.lblExtension.Location = new System.Drawing.Point(10, row);
            this.lblExtension.Size = new System.Drawing.Size(lw, bh);
            this.cmbExtension.Location = new System.Drawing.Point(tx, row);
            this.cmbExtension.Size = new System.Drawing.Size(100, bh);
            this.cmbExtension.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            row += gap;

            // Auto-save delay
            this.lblAutoSave.Text = "Auto-save delay (sec):";
            this.lblAutoSave.Location = new System.Drawing.Point(10, row);
            this.lblAutoSave.Size = new System.Drawing.Size(lw, bh);
            this.nudAutoSave.Location = new System.Drawing.Point(tx, row);
            this.nudAutoSave.Size = new System.Drawing.Size(60, bh);
            this.nudAutoSave.Minimum = 1;
            this.nudAutoSave.Maximum = 30;
            this.nudAutoSave.Value = 3;
            row += gap;

            // Poll interval
            this.lblPoll.Text = "Poll interval (sec):";
            this.lblPoll.Location = new System.Drawing.Point(10, row);
            this.lblPoll.Size = new System.Drawing.Size(lw, bh);
            this.nudPoll.Location = new System.Drawing.Point(tx, row);
            this.nudPoll.Size = new System.Drawing.Size(60, bh);
            this.nudPoll.Minimum = 1;
            this.nudPoll.Maximum = 60;
            this.nudPoll.Value = 5;
            row += gap;

            // Auto-open
            this.chkAutoOpen.Text = "Auto-open remote notes";
            this.chkAutoOpen.Location = new System.Drawing.Point(tx, row);
            this.chkAutoOpen.Size = new System.Drawing.Size(200, bh);
            this.chkAutoOpen.Checked = true;
            row += gap + 8;

            // OK / Cancel
            this.btnOK.Text = "OK";
            this.btnOK.Location = new System.Drawing.Point(tx, row);
            this.btnOK.Size = new System.Drawing.Size(75, 26);
            this.btnOK.Click += new System.EventHandler(this.btnOK_Click);
            this.btnCancel.Text = "Cancel";
            this.btnCancel.Location = new System.Drawing.Point(tx + 85, row);
            this.btnCancel.Size = new System.Drawing.Size(75, 26);
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            this.AcceptButton = this.btnOK;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(450, row + 40);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "DraftSync Settings";

            this.Controls.Add(this.lblSyncFolder);
            this.Controls.Add(this.txtSyncFolder);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.lblMachineId);
            this.Controls.Add(this.txtMachineId);
            this.Controls.Add(this.lblExtension);
            this.Controls.Add(this.cmbExtension);
            this.Controls.Add(this.lblAutoSave);
            this.Controls.Add(this.nudAutoSave);
            this.Controls.Add(this.lblPoll);
            this.Controls.Add(this.nudPoll);
            this.Controls.Add(this.chkAutoOpen);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.btnCancel);

            ((System.ComponentModel.ISupportInitialize)(this.nudAutoSave)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.nudPoll)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label lblSyncFolder;
        private System.Windows.Forms.TextBox txtSyncFolder;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label lblMachineId;
        private System.Windows.Forms.TextBox txtMachineId;
        private System.Windows.Forms.Label lblExtension;
        private System.Windows.Forms.ComboBox cmbExtension;
        private System.Windows.Forms.Label lblAutoSave;
        private System.Windows.Forms.NumericUpDown nudAutoSave;
        private System.Windows.Forms.Label lblPoll;
        private System.Windows.Forms.NumericUpDown nudPoll;
        private System.Windows.Forms.CheckBox chkAutoOpen;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
    }
}
