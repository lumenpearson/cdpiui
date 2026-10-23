using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Update
{
    public partial class MainWindow : Form
    {
        public MainWindow()
        {
            InitializeComponent();

            Text = Strings.WindowTitle;
            label2.Text = Strings.Heading;
            button1.Text = Strings.CancelButton;
            StatusLabel.Text = Strings.Preparing;

            InstallHelper.Instance.ProgressChanged += ProgressChanged;
            InstallHelper.Instance.InstallationStateChanged += InstallationStateChanged;
            InstallHelper.Instance.CurrentFileChanged += CurrentFileChanged;
            InstallHelper.Instance.ErrorOccurred += ErrorOccurred;

            this.FormClosing += Form_FormClosing;

            // Started in the constructor, the installation reported progress before the handle
            // existed: InvokeRequired was false and the controls were changed from the worker.
            this.Shown += (sender, e) => InstallHelper.Instance.StartInstall();
        }

        private void ProgressChanged(double progress)
        {
            int _progress = (int)(progress * 100);
            if (ProgressBar.InvokeRequired)
            {
                ProgressBar.Invoke(new Action(() => ProgressBar.Value = _progress));
            }
            else
            {
                ProgressBar.Value = _progress;
            }
        }

        private void SetStatus(string status)
        {
            if (StatusLabel.InvokeRequired)
            {
                StatusLabel.Invoke(new Action(() => StatusLabel.Text = status));
            }
            else
            {
                StatusLabel.Text = status;
            }
        }

        private void InstallationStateChanged(InstallState state)
        {
            switch (state)
            {
                case (InstallState.Prepare):
                    SetStatus(Strings.Preparing);
                    break;
                case (InstallState.Unpack):
                    SetStatus(Strings.Unpacking);
                    break;
                case (InstallState.Copy):
                    SetStatus(Strings.CopyingFiles);
                    break;
                case (InstallState.Finalize):
                    SetStatus(Strings.Finalizing);
                    break;
                case (InstallState.Completed):
                    SetStatus(Strings.Complete);
                    EnableCloseButton();
                    button1.Click += (s, e) => Application.Exit();
                    break;
                case (InstallState.Error):
                    SetStatus(Strings.ExceptionHappens);
                    EnableCloseButton();
                    button1.Click += (s, e) => Application.Exit();
                    break;
            }
        }

        private void EnableCloseButton()
        {
            if (button1.InvokeRequired)
            {
                button1.Invoke(new Action(EnableCloseButton));
                return;
            }
            button1.Text = Strings.CloseButton;
            button1.Enabled = true;
        }

        private void CurrentFileChanged(string filename)
        {
            SetStatus($"{filename}");
        }

        private void ErrorOccurred(InstallError installError, string error)
        {
            // Raised on the installation thread: the message box is shown on the UI thread and
            // owned by this window, so it cannot open behind it.
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => ErrorOccurred(installError, error)));
                return;
            }
            MessageBox.Show(this, string.Format(Strings.ErrorMessage, error), Strings.ErrorTitle,
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }

        private void Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (InstallHelper.Instance.FinishStatus == InstallError.None)
                e.Cancel = true;
        }
    }
}
