namespace NppSync.Forms
{
    partial class CloseNoteDialog
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.labelMessage = new System.Windows.Forms.Label();
            this.btnDismiss = new System.Windows.Forms.Button();
            this.btnDeleteEverywhere = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();

            // labelMessage
            this.labelMessage.AutoSize = true;
            this.labelMessage.Location = new System.Drawing.Point(12, 18);
            this.labelMessage.Size = new System.Drawing.Size(360, 15);
            this.labelMessage.Text = "";

            // btnDismiss
            this.btnDismiss.Location = new System.Drawing.Point(15, 55);
            this.btnDismiss.Size = new System.Drawing.Size(90, 26);
            this.btnDismiss.Text = "Dismiss";
            this.btnDismiss.Click += new System.EventHandler(this.btnDismiss_Click);

            // btnDeleteEverywhere
            this.btnDeleteEverywhere.Location = new System.Drawing.Point(120, 55);
            this.btnDeleteEverywhere.Size = new System.Drawing.Size(130, 26);
            this.btnDeleteEverywhere.Text = "Delete Everywhere";
            this.btnDeleteEverywhere.Click += new System.EventHandler(this.btnDeleteEverywhere_Click);

            // btnCancel
            this.btnCancel.Location = new System.Drawing.Point(265, 55);
            this.btnCancel.Size = new System.Drawing.Size(75, 26);
            this.btnCancel.Text = "Cancel";
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);

            // Form
            this.AcceptButton = this.btnDismiss;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(360, 95);
            this.Controls.Add(this.labelMessage);
            this.Controls.Add(this.btnDismiss);
            this.Controls.Add(this.btnDeleteEverywhere);
            this.Controls.Add(this.btnCancel);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "NppSync";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label labelMessage;
        private System.Windows.Forms.Button btnDismiss;
        private System.Windows.Forms.Button btnDeleteEverywhere;
        private System.Windows.Forms.Button btnCancel;
    }
}
