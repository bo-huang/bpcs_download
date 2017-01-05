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
    public partial class SettingForm : Form
    {
        public uint Concurrency
        {
            get
            {
                return concurrency;
            }
            private set
            {
                concurrency = value;
                if (ConcurrencyChangedEvent != null)
                    ConcurrencyChangedEvent(Concurrency);
            }
        }
        private uint concurrency;
        //委托
        public delegate void ConcurrencyChangedEventHander(uint concurrency);
        public ConcurrencyChangedEventHander ConcurrencyChangedEvent = null;
        public SettingForm(uint concurrency)
        {
            InitializeComponent();
            Concurrency = concurrency;
            concurrencyTextBox.Text = Concurrency.ToString();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            string concurrencyStr = concurrencyTextBox.Text.Trim();
            uint result;
            if (!uint.TryParse(concurrencyStr, out result) || result==0||result > 200)
            {
                MessageBox.Show(this, "Warning", "please input a number between 1-200", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Concurrency = result;
            this.Close();
        }
    }
}
