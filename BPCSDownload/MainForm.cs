using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BPCSDownload
{
    public partial class MainForm : Form
    {
        private BPCS bpcs;
        private DateTime startTime;
        private SettingForm settingForm;
        private const uint concurrency = 40;//默认并发数
        public MainForm()
        {
            InitializeComponent();
            //init
            Control.CheckForIllegalCrossThreadCalls = false;
            bpcs = new BPCS(concurrency);
          
            //判断是否已经登录
            if(bpcs.isValidate())
            {
                loginToolStripMenuItem.Enabled = false;
                logoutToolStripMenuItem.Enabled = true;
                loginToolStripMenuItem.Text = bpcs.GetUserName();
            }
            else
                logoutToolStripMenuItem.Enabled = false;

            settingForm = new SettingForm(bpcs.Concurrency);
            settingForm.ConcurrencyChangedEvent = new SettingForm.ConcurrencyChangedEventHander(ConcurrencyChanged);
        }
        private void ConcurrencyChanged(uint concurrency)
        {
            bpcs.Concurrency = concurrency;
        }
        private void loginToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if(bpcs.Login())
            {
                loginToolStripMenuItem.Enabled = false;
                logoutToolStripMenuItem.Enabled = true;
                loginToolStripMenuItem.Text = bpcs.GetUserName();
            }
        }

        private void logoutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bpcs.Logout();
            loginToolStripMenuItem.Enabled = true;
            logoutToolStripMenuItem.Enabled = false;
            loginToolStripMenuItem.Text = "login";
        }

        private void downloadButton_Click(object sender, EventArgs e)
        {
            if (loginToolStripMenuItem.Text == "login")
                MessageBox.Show(this, "please login first!", "Tip", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
            {
                String path = textBox.Text.Trim();
                if (String.IsNullOrEmpty(path))
                {
                    DialogResult result =  MessageBox.Show(this, "Confirm to download all files in root directory?", "Tip", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (result == DialogResult.No)
                        return;
                }
                Task.Factory.StartNew(() =>
                {
                    Download("/apps/UniDrive/" + path);
                });
            }     
        }
        private void Download(String path)
        {
            List<DownloadFile>fileList = bpcs.GetFiles(path);
            if(fileList.Count==0)
            {
                MessageBox.Show(this, "path not exists", "Tip", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            downloadButton.Enabled = false;
            textBox.Enabled = false;
            for (int i = 0; i < fileList.Count;++i )
            {
                DownloadFile file = fileList[i];
                if(file.Isdir==1)//文件夹
                {
                    fileList.AddRange(bpcs.GetFiles(file.Path));
                    continue;
                }
                progressBar.Value = 0;
                progressBar.Maximum = 100;
                String fileName = System.IO.Path.GetFileName(file.Path);
                downloadFilelabel.Text = "download " + fileName +"...";
                statusLabel.Text = "0%";
                speedStatusLabel.Text = "0 b/s";
                bpcs.progressEvent = new BPCS.ProgressEventHander(Progress);
                startTime = DateTime.Now;
                bool ok = bpcs.Download(file);
                if (!ok)
                    MessageBox.Show(string.Format("download {0} failed", fileName));
            }
            downloadFilelabel.Text = "Completed!";
            speedStatusLabel.Text = "0 b/s";
            downloadButton.Enabled = true;
            textBox.Enabled = true;
        }
        private void Progress(long value ,long maxnum)
        {
            
            double totalSeconds = (DateTime.Now - startTime).TotalSeconds;
            speedStatusLabel.Text = SpeedToString(value / totalSeconds);
            //防止溢出(progressBar int)
            if (maxnum > int.MaxValue)
            {
                value /= 100;
                maxnum /= 100;
            }
            progressBar.Maximum = (int)maxnum;
            progressBar.Value = (int)value;
            statusLabel.Text = value * 100 / maxnum + "%";
            
        }
        private String SpeedToString(double speed)
        {
            if (speed < (1 << 10))
            {
                return (int)speed + " b/s";
            }
            else if (speed < (1 << 20))
            {
                speed /= (1 << 10);
                return String.Format("{0} Kb/s",(int)speed);
            }
            else
            {
                speed /= (1 << 20);
                return String.Format("{0} Mb/s",Math.Round(speed,2));
            }
        }

        private void setsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            settingForm.ShowDialog(this);
        }
    }
}
