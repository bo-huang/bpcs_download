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
    public partial class ErrorForm : Form
    {
        private int leastSeconds = 5;
        private Timer timer;
        private DialogResult result;
        public ErrorForm()
        {
            InitializeComponent();
            timer = new Timer();
            timer.Interval = 1000;
            timer.Tick += timer_Tick;
        }

        public new DialogResult ShowDialog()
        {
            timer.Start();
            base.ShowDialog();
            return result;
        }

        void timer_Tick(object sender, EventArgs e)
        {
            --leastSeconds;
            tipLabel.Text = String.Format("Close after {0}s...",leastSeconds);
            if(leastSeconds==0)
            {
                timer.Stop();
                result = DialogResult.Yes;
                this.Close();
            }
        }

        private void yesButton_Click(object sender, EventArgs e)
        {
            result = DialogResult.Yes;
            this.Close();
        }

        private void noButton_Click(object sender, EventArgs e)
        {
            result = DialogResult.No;
            this.Close();
        }
    }
}
