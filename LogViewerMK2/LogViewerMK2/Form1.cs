using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
// waveファイルの操作に必要

namespace LogViewerMK2
{
    public partial class Form1 : Form
    {

        string nowplaypath;
        string nowplayname;
        bool mediaready = false;
        bool digestflag = false;
        double medialength;
        List<string> scenes_start_l = new List<string>();
        List<string> scenes_end_l = new List<string>();
        int[] digest_meta;
        double timer_stop_time;
        double timer_jump_time;
        int digest_number;

        List<double[]> pickuptimes = new List<double[]>();

        public Form1()
        {
            InitializeComponent();
        }

        private void axWindowsMediaPlayer1_Enter(object sender, EventArgs e)
        {

        }

        private void 動画の読み込みToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string[] files_tmp = new string[4];
            string[] paths_tmp = new string[4];
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Multiselect = true;

            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                files_tmp = openFileDialog1.SafeFileNames;
                paths_tmp = openFileDialog1.FileNames;

                for (int i = 0; i < files_tmp.Length && i < 4; i++)
                {
                    video_add(paths_tmp[i], files_tmp[i]);
                    mediaready = true;
                }
            }

        }
        void video_add(string video_path, string video_name)
        {
            axWindowsMediaPlayer1.URL = video_path;
            nowplaypath = video_path;
            nowplayname = video_name;
            mediaready = true;
            digestflag = false;
            add_digestscene_to_list("");
            medialength = axWindowsMediaPlayer1.currentMedia.duration;

            string meta_folda = "digest_meta/";
            meta_folda += System.IO.Path.GetFileNameWithoutExtension(video_name);

            if (!System.IO.Directory.Exists(@meta_folda))
            {
                System.IO.DirectoryInfo di = System.IO.Directory.CreateDirectory(@meta_folda);
            }
        }
        private void add_digestscene_to_list(string filepath)
        {
            string stBaseName = System.IO.Path.GetFileNameWithoutExtension(nowplayname);
            if (filepath.Length == 0)
            {
                filepath = "./digest_meta/" + stBaseName + "/" + stBaseName + "_digestmeta.txt";
            }

            try
            {

                using (StreamReader sr = new StreamReader(filepath, Encoding.GetEncoding("Shift_JIS")))
                {

                    listView1.Items.Clear();

                    //全行読み込み(行単位のstring配列)
                    string data = sr.ReadToEnd();
                    //カンマ区切りで出力
                    string[] stArrayData = data.Split('\t', '\r', '\n');

                    scenes_start_l.AddRange(stArrayData);
                    string tmp_listname;

                    int tmp_time;

                    for (int i = 0; i + 1 < stArrayData.Length; i += 4)
                    {
                        double[] tmp_pickuptimes = new double[2];
                        tmp_pickuptimes[0] = double.Parse(stArrayData[i]);
                        tmp_pickuptimes[1] = double.Parse(stArrayData[i+1]);
                        pickuptimes.Add(tmp_pickuptimes);

                        string[] tmp_list = { "", "", "", "" };
                        tmp_listname = "No.";
                        tmp_listname += i / 4 + 1;
                        tmp_list[1] += i / 4 + 1;

                        //                        tmp_listname += "\t｜ ";
                        if (i / 4 + 1 >= 10)
                        {
                            tmp_listname += " ｜ ";
                        }
                        else
                        {
                            tmp_listname += "　｜ ";
                        }

                        tmp_time = (int)double.Parse(stArrayData[i]) / 60;

                        tmp_listname += tmp_time.ToString("00");
                        tmp_listname += "：";
                        tmp_list[2] += tmp_time.ToString("00");
                        tmp_list[2] += "：";

                        tmp_time = (int)double.Parse(stArrayData[i]) % 60;
                        tmp_listname += tmp_time.ToString("00");
                        tmp_list[2] += tmp_time.ToString("00");

                        tmp_listname += "―";

                        tmp_time = (int)double.Parse(stArrayData[i + 1]) / 60;
                        tmp_listname += tmp_time.ToString("00");
                        tmp_listname += "：";
                        tmp_list[3] += tmp_time.ToString("00");
                        tmp_list[3] += "：";


                        tmp_time = (int)double.Parse(stArrayData[i + 1]) % 60;
                        tmp_listname += tmp_time.ToString("00");
                        tmp_list[3] += tmp_time.ToString("00");

                        listView1.Items.Add(new ListViewItem(tmp_list));

                        scenes_start_l[i / 4] = stArrayData[i];
                    }
                }
                using (StreamReader sr = new StreamReader(filepath, Encoding.GetEncoding("Shift_JIS")))
                {
                    //全行読み込み(行単位のstring配列)
                    string data = sr.ReadToEnd();
                    //カンマ区切りで出力
                    string[] stArrayData2 = data.Split('\t', '\r', '\n');
                    int movietime = (int)double.Parse(stArrayData2[0]);

                    //メタデータをもとにして各時刻（秒単位）で次の等倍再生の開始時刻を持つ配列digest_meta[i]を定義
                    //digest_meta[i] は等倍だと-1が入っていて，倍速区間は次の等倍区間の最初が入っている
                    int tmp = 0;
                    digest_meta = new int[movietime + 1];
                    for (int i = 3; i + 1 < stArrayData2.Length; i += 4)
                    {
                        for (int j = tmp; j < (int)double.Parse(stArrayData2[i]); j++)
                        {
                            if (i + 4 < stArrayData2.Length)
                            {
                                digest_meta[j] = (int)double.Parse(stArrayData2[i]);
                            }
                            else
                            {
                                digest_meta[j] = movietime;
                            }
                        }
                        tmp = (int)double.Parse(stArrayData2[i + 1]);

                        for (int j = (int)double.Parse(stArrayData2[i]); j < (int)double.Parse(stArrayData2[i + 1]); j++)
                        {
                            digest_meta[j] = -1;
                        }
                    }
                }
            }
            catch
            {
            }

        }

        private void メタファイルの読み込みMToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Multiselect = true;

            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                add_digestscene_to_list(openFileDialog1.FileNames[0]);
            }
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                //string time = scenes_start_l[int.Parse(listView1.SelectedItems[0].Text) - 1];
                //axWindowsMediaPlayer1.Ctlcontrols.currentPosition = double.Parse(time);
                axWindowsMediaPlayer1.Ctlcontrols.currentPosition = pickuptimes[listView1.SelectedItems[0].Index][0];
                axWindowsMediaPlayer1.Ctlcontrols.play();

                timer_stop_time = pickuptimes[listView1.SelectedItems[0].Index][1];

                if (checkBox1.Checked == false)
                {
                    timer1.Enabled = true;
                }
                else
                {
                    timer2.Enabled = true;
                }
            }

        }


        private double DecimalToFull(string time)
        {
            string[] stArrayData = time.Split('：');
            return (double.Parse(stArrayData[0])*60 + double.Parse(stArrayData[1]));
        }
        string FullToDecimal(double time)
        {
            return (int)(time / 60) + "：" + time % 60;  
        }

        private void ファイルToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void listView1_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.AllowedEffect;
        }
        private void listView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            listView1.DoDragDrop((ListViewItem)e.Item, DragDropEffects.Move);
        }
        private void listView1_DragOver(object sender, DragEventArgs e)
        {
            // Retrieve the client coordinates of the mouse pointer.
            Point targetPoint =listView1.PointToClient(new Point(e.X, e.Y));

            // Retrieve the index of the item closest to the mouse pointer.
            int targetIndex = listView1.InsertionMark.NearestIndex(targetPoint);

            // Confirm that the mouse pointer is not over the dragged item.
            if (targetIndex > -1)
            {
                // Determine whether the mouse pointer is to the left or
                // the right of the midpoint of the closest item and set
                // the InsertionMark.AppearsAfterItem property accordingly.
                Rectangle itemBounds = listView1.GetItemRect(targetIndex);
                if (targetPoint.X > itemBounds.Left + (itemBounds.Width / 2))
                {
                    listView1.InsertionMark.AppearsAfterItem = true;
                }
                else
                {
                    listView1.InsertionMark.AppearsAfterItem = false;
                }
            }

            // Set the location of the insertion mark. If the mouse is
            // over the dragged item, the targetIndex value is -1 and
            // the insertion mark disappears.
            listView1.InsertionMark.Index = targetIndex;
        }
        // Removes the insertion mark when the mouse leaves the control.
        private void listView1_DragLeave(object sender, EventArgs e)
        {
            listView1.InsertionMark.Index = -1;
        }

        private void listView1_DragDrop(object sender, DragEventArgs e)
        {
            // Retrieve the index of the insertion mark;
            int targetIndex = listView1.InsertionMark.Index;

            // If the insertion mark is not visible, exit the method.
            if (targetIndex == -1)
            {
                return;
            }

            // If the insertion mark is to the right of the item with
            // the corresponding index, increment the target index.
            if (listView1.InsertionMark.AppearsAfterItem)
            {
                targetIndex++;
            }

            // Retrieve the dragged item.
            ListViewItem draggedItem =
                (ListViewItem)e.Data.GetData(typeof(ListViewItem));

            // Insert a copy of the dragged item at the target index.
            // A copy must be inserted before the original item is removed
            // to preserve item index values. 
            listView1.Items.Insert(
                targetIndex, (ListViewItem)draggedItem.Clone());

            // Remove the original copy of the dragged item.
            listView1.Items.Remove(draggedItem);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (axWindowsMediaPlayer1.Ctlcontrols.currentPosition > timer_stop_time)
            {
                axWindowsMediaPlayer1.Ctlcontrols.pause();
                timer1.Enabled = false;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            listView1.CheckBoxes = true;
            timer1.Interval = 10;
            timer2.Interval = 10;
        }

        private void listView1_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            double digesttime = 0;
            for (int i = 0; i < listView1.Items.Count; i++)
            {
                if (listView1.Items[i].Checked == true)
                {
                    digesttime += DecimalToFull(listView1.Items[i].SubItems[3].Text) - DecimalToFull(listView1.Items[i].SubItems[2].Text);
                }
            }
            textBox1.Text = ((int)digesttime / 60).ToString();
            textBox2.Text = ((int)digesttime % 60).ToString();
        }

        private void button1_Click(object sender, EventArgs e)
        {
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            if (axWindowsMediaPlayer1.Ctlcontrols.currentPosition > timer_stop_time)
            {
                int selectindex = -1;
                for (int i = listView1.SelectedItems[0].Index+1  ; i < listView1.Items.Count; i++)
                {
                    if (listView1.Items[i].Checked == true)
                    {
                        selectindex = i;
                        break;
                    }
                }
                if (selectindex == -1)
                {
                    checkBox1.Checked = false;
                }
                else
                {
                    timer2.Enabled = false;
                    int tmpindex = listView1.SelectedItems[0].Index;
                    listView1.Items[tmpindex].Selected = false;
                    listView1.Items[selectindex].Selected = true;
                }
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked == false)
            {
                axWindowsMediaPlayer1.Ctlcontrols.pause();
                timer2.Enabled = false;
            }
            else
            {
                this.listView1.Focus();
                int selected_first = -1;
                for (int i = 0; i < listView1.Items.Count; i++)
                {
                    if (listView1.Items[i].Checked == true)
                    {
                        selected_first = i;
                        break;
                    }
                }
                if (selected_first != -1)
                {
                    listView1.Items[selected_first].Selected = true;
                }
            }
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
        }

        private void button2_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < listView1.Items.Count; i++)
            {
                listView1.Items[i].Checked = true;
            }
        }

        private void 全選択SToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < listView1.Items.Count; i++)
            {
                listView1.Items[i].Checked = true;
            }
        }

        private void 全解除ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < listView1.Items.Count; i++)
            {
                listView1.Items[i].Checked = false;
            }
        }


    }

}
