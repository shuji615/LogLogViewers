using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WindowsFormsApplication2
{
    public partial class Configform : Form
    {
        public Configform()
        {
            InitializeComponent();
        }

        private void Configform_Load(object sender, EventArgs e)
        {
            numericUpDown1.Value = Properties.Settings.Default.speed_max;
            numericUpDown2.Value = Properties.Settings.Default.speed_min;
            numericUpDown3.Value = Properties.Settings.Default.digest_time;
            numericUpDown4.Value = Properties.Settings.Default.signalfrequency;
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        //OK
        private void button1_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.Save();
            this.Close();
        }


        //適用
        private void button2_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.Save();
            button2.Enabled = false;
        }

        //キャンセル
        private void button3_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.Reload();
            this.Close();
        }

        //MAX
        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.speed_max = (int)numericUpDown1.Value;
            button2.Enabled = true;
        }

        //MIN
        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.speed_min = (int)numericUpDown2.Value;
            button2.Enabled = true;
        }

        //ダイジェストの時間
        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.digest_time = (int)numericUpDown3.Value;
            button2.Enabled = true;
        }

        private void Configform_FormClosed(object sender, FormClosedEventArgs e)
        {
        }

        private void numericUpDown4_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.signalfrequency = (int)numericUpDown4.Value;
            button2.Enabled = true;
        }
    }
}
