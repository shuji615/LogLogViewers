using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Signal_Process;                                   // FFTに必要
using WAVE_file;                                        // waveファイルの操作に必要

namespace WindowsFormsApplication2
{
    public partial class Form1 : Form
    {

        private string need_pos = "";
        private double fast_speed = 50;
        private int rate = 100;
        private int digest_time = 180;
        private bool[] mediaready = { false, false, false, false };
        private wave wave_file = new wave();
        private const int FFT_arr_size = 5;
        private CheckBox[] fft_chbox = new CheckBox[FFT_arr_size];                      // チェックボックスオブジェクトを配列で宣言
        private int[] fft_points = new int[FFT_arr_size] { 256, 512, 1024, 2048, 4096 };// 実施するFFTのサイズを指定するのに使用する
        //        private SignalBasic sensor;                                                     // 鳴き声検出のための検査クラス
        private double[] offsets = { 0.0, 0.0, 0.0, 0.0 };
        private int[][] volume = new int[4][];//秒単位
        private double mediafrequency = 48.0;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            フルスクリーンToolStripMenuItem.Enabled = false;
            timer1.Interval = rate;
            timer1.Enabled = true;

            // 最小値、最大値を設定
            trackBar1.Minimum = Properties.Settings.Default.speed_min;
            trackBar1.Maximum = Properties.Settings.Default.speed_max;

            // 初期値を設定
            trackBar1.Value = 50;
            fast_speed = trackBar1.Value;

            // 描画される目盛りの刻みを設定
            trackBar1.TickFrequency = 10;

            // スライダーをキーボードやマウス、
            // PageUp,Downキーで動かした場合の移動量設定
            trackBar1.SmallChange = 1;
            trackBar1.LargeChange = 10;

            // 値が変更された際のイベントハンドらーを追加
            trackBar1.ValueChanged += new EventHandler(trackBar1_ValueChanged);
            textBox5.Text = fast_speed.ToString();
            textBox5.Text += "倍速";

            //ダイジェスト全体の尺を定義
            digest_time = Properties.Settings.Default.digest_time;

            if (Properties.Settings.Default.volume_or_time == true)
            {
                radioButton1.Checked = true;
            }
            else
            {
                radioButton2.Checked = true;
            }

            x1 = this.pictureBox2.Left;
            y1 = this.pictureBox2.Top;

            x2 = this.pictureBox2.Right;
            y2 = this.pictureBox2.Bottom;

        }
        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            x1 = this.pictureBox2.Left;
            y1 = this.pictureBox2.Top;

            x2 = this.pictureBox2.Right;
            y2 = this.pictureBox2.Bottom;

        }

        int x1, y1;//シークバーの左上
        int x2, y2;//シークバーの右下

        //        string[] files, paths;

        //メディアプレイヤーで再生する動画の場所（パス）を格納する配列
        /*
        List<string> files_l = new List<string>();
        List<string> paths_l = new List<string>();
         */

        string[] files = new string[4];
        string[] paths = new string[4];

        //等倍推奨箇所（発話推定箇所）の開始時刻と終了時刻を格納する配列
        List<string> scenes_start_l = new List<string>();
        List<string> scenes_end_l = new List<string>();

        //現在メディアプレイヤーで再生している動画のパスと動画の名前(拡張子は含まない)
        string[] nowplaypath = new string[4];
        string[] nowplayname = new string[4];

        //ダイジェストモードで参照する配列
        //等倍：-1
        //早送り：次の等倍開始時刻
        //※早送りの時は，数msごとに数秒先に飛ばすという方法を取っているが，
        //そうすると飛ばしすぎてしまう問題が発生するので，
        //飛ばす先の時刻が次の等倍時刻を超えないか評価して，超える場合は次の等倍時刻に飛ばすという方法を取っている
        int[] digest_meta;


        private void ファイルToolStripMenuItem1_Click(object sender, EventArgs e)
        {
        }

        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {

                // ドラッグ中のファイルやディレクトリの取得
                string[] drags = (string[])e.Data.GetData(DataFormats.FileDrop);

                foreach (string d in drags)
                {
                    if (!System.IO.File.Exists(d))
                    {
                        // ファイル以外であればイベント・ハンドラを抜ける
                        return;
                    }
                }
                e.Effect = DragDropEffects.Copy;
            }

        }

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            string[] files_tmp = new string[3];
            string[] paths_tmp;
            // ドラッグ＆ドロップされたファイル
            paths_tmp = (string[])e.Data.GetData(DataFormats.FileDrop);
            for (int i = 0; i < paths_tmp.Length; i++)
            {
                files_tmp[i] = System.IO.Path.GetFileNameWithoutExtension(paths_tmp[i]);
            }
            for (int i = 0; i < paths_tmp.Length && i < 4; i++)
            {
                files[i] = paths_tmp[i];
                video_add(paths_tmp[i], files_tmp[i], i);
            }
        }

        private void tableLayoutPanel1_Paint(object sender, PaintEventArgs e)
        {

        }

        /*
        void calc_volume(int area_number)
        {
            string stBaseName = System.IO.Path.GetFileNameWithoutExtension(nowplayname[area_number]);
            using (StreamReader sr = new StreamReader(@"./digest_meta/" + stBaseName + "/" + stBaseName + "_sound.txt"))
            {
                //全行読み込み(行単位のstring配列)
                string data = sr.ReadToEnd();
                //カンマ区切りで出力
                string[] stArrayData = data.Split('\t', '\r', '\n');
                int tmp = 0;
                volume[area_number] = new int[stArrayData.Length];
                for (int i = 0; i < stArrayData.Length - 1; i += 3)
                {
                    for (int j = i; j <= i + 3 * mediafrequency * 2 && j < stArrayData.Length - 1; j += 3)
                    {
                        tmp += int.Parse(stArrayData[j]) + int.Parse(stArrayData[j + 1]);
                    }
                    volume[area_number][i / 3] = tmp;
                    tmp = 0;
                }
            }
        }
        */ 

        void video_add(string video_path, string video_name, int area_number)
        {
            switch (area_number)
            {
                case 0:
                    textBox6.Text = video_name;
                    axWindowsMediaPlayer1.URL = video_path;
                    nowplaypath[0] = video_path;
                    フルスクリーンToolStripMenuItem.Enabled = true;
                    mediaready[0] = true;
                    digestflag = false;
                    checkBox1.Checked = false;
                    nowplayname[0] = video_name;
                    add_digestscene_to_list(0);
                    medialength = axWindowsMediaPlayer1.currentMedia.duration;

                    string meta_folda = "digest_meta/";
                    meta_folda += System.IO.Path.GetFileNameWithoutExtension(video_name);

                    if (!System.IO.Directory.Exists(@meta_folda))
                    {
                        System.IO.DirectoryInfo di = System.IO.Directory.CreateDirectory(@meta_folda);
                    }
                    try
                    {
                        make_autobar();
                        make_raw_bar();
                    }catch{
                    }
                    break;
            }
        }

        private void axWindowsMediaPlayer1_Enter_1(object sender, EventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {
            axWindowsMediaPlayer1.Ctlcontrols.fastForward();
        }

        private double getmediafreq(int area_number)
        {
            medialength = axWindowsMediaPlayer1.currentMedia.duration;
            string stBaseName = System.IO.Path.GetFileNameWithoutExtension(nowplayname[area_number]);
            using (StreamReader sr = new StreamReader(@"./digest_meta/" + stBaseName + "/" + stBaseName + "_sound.txt"))
            {
                //全行読み込み(行単位のstring配列)
                string data = sr.ReadToEnd();
                //カンマ区切りで出力
                string[] stArrayData = data.Split('\r', '\n');

                return (stArrayData.Length / 2)/medialength;
            }
        }

        public void Run()
        {
            Timer timer = new Timer();
            timer.Tick += new EventHandler(DigestPlay);
            timer.Interval = 1000;
            timer.Enabled = true; // timer.Start()と同じ

            Application.Run(); // メッセージ・ループを開始
        }

        public void DigestPlay(object sender, EventArgs e)
        {

        }

        void make_digest_meta(int area_number)
        {
            if (mediaready[area_number] == true)
            {
                string tmp_argument = nowplaypath[area_number];
                string stBaseName = System.IO.Path.GetFileNameWithoutExtension(nowplayname[area_number]);
                tmp_argument += " ";
                tmp_argument += stBaseName;

                string tmp_filename = "digest_meta/";
                string stBaseName2 = System.IO.Path.GetFileNameWithoutExtension(nowplayname[area_number]);
                tmp_filename += stBaseName2;
                tmp_filename += "/";
                tmp_filename += stBaseName2;
                tmp_filename += "_digestmeta.txt";

                if (File.Exists(tmp_filename))
                {
                    using (StreamReader sr = new StreamReader(tmp_filename, Encoding.GetEncoding("Shift_JIS")))
                    {
                        DialogResult result = MessageBox.Show("既存のダイジェストメタファイルを読み込みます",
                            "過去に作成したダイジェストメタファイルが見つかりました",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information,
                            MessageBoxDefaultButton.Button2);
                    }
                    add_digestscene_to_list(area_number);
                    /*
                    if (!File.Exists("digest_meta/" + stBaseName2 + "/" + stBaseName2 + "_signaltime.txt"))
                    {
                        search_signal_frequency(stBaseName2);
                    }
                    read_offset(area_number);
                    */
                }
                else
                {
                    DialogResult result = MessageBox.Show("新規ダイジェストメタファイルを作成します",
                        "新規作成",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information,
                        MessageBoxDefaultButton.Button2);

                    string meta_folda = "digest_meta/";
                    meta_folda += stBaseName2;
                    System.IO.DirectoryInfo di = System.IO.Directory.CreateDirectory(@meta_folda);

                    System.Diagnostics.Process p = System.Diagnostics.Process.Start("mp4totxt.bat", @tmp_argument);
                    p.WaitForExit();

                    mediafrequency = getmediafreq(0);
                    string tmp_argument2 = stBaseName;
                    tmp_argument2 += " 180 140 3 "+ mediafrequency;

                    System.Diagnostics.Process q = System.Diagnostics.Process.Start("makemoviefix.exe", @tmp_argument2);
                    //ダイジェストの尺 等倍の尺 ピックアップ箇所の最小時間 ピックアップ箇所の最大時間 を指定

                    add_digestscene_to_list(area_number);

                    /*//特定周波数の同期音を検出
                    if(!File.Exists("digest_meta/"+stBaseName2+"/"+stBaseName2+"_signaltime.txt"))
                    {
                        search_signal_frequency(stBaseName2);
                    }
                    read_offset(area_number);
                    */
                }
            }
            else
            {

                DialogResult result = MessageBox.Show("ダイジェストにしたいメディアを読み込んでください。",
                    "メディアが見つかりませんでした",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button2);
            }

        }
        private Button[] buttons;
        private void button4_Click(object sender, EventArgs e)
        {
            //ボタンコントロール配列の作成
            this.buttons = new Button[10];
            for (int i = 0; i < buttons.Length; i++)
            {
                //ボタンコントロールのインスタンス作成
                this.buttons[i] = new Button();

                //プロパティ設定
                this.buttons[i].Name = "btn" + i.ToString();
                this.buttons[i].Text = "ボタン" + i.ToString();
                this.buttons[i].Top = i * 30;
                this.buttons[i].Left = 300;
                this.buttons[i].Size = new System.Drawing.Size(1000, 30);

                //コントロールをフォームに追加
                this.Controls.Add(this.buttons[i]);
                this.buttons[i].BringToFront();
            }

        }

        private bool digestflag = false;

        private void add_digestscene_to_list(int area_number)
        {
            string stBaseName3 = System.IO.Path.GetFileNameWithoutExtension(nowplayname[area_number]);
            string digestmetafilename_sort;
            if (radioButton1.Checked == true)
            {
                digestmetafilename_sort = "./digest_meta/" + stBaseName3 + "/" + stBaseName3 + "_digestmeta_sort.txt";
            }
            else
            {
                digestmetafilename_sort = "./digest_meta/" + stBaseName3 + "/" + stBaseName3 + "_digestmeta.txt";
            }
            string digestmetafilename = "./digest_meta/" + stBaseName3 + "/" + stBaseName3 + "_digestmeta.txt";

            try
            {

                using (StreamReader sr = new StreamReader(digestmetafilename_sort, Encoding.GetEncoding("Shift_JIS")))
                {
                    switch (area_number)
                    {
                        case 0:
                            listView1.Items.Clear();
                            listView2.Items.Clear();
                            break;
                    }
                    //全行読み込み(行単位のstring配列)
                    string data = sr.ReadToEnd();
                    //カンマ区切りで出力
                    string[] stArrayData = data.Split('\t', '\r', '\n');

                    scenes_start_l.AddRange(stArrayData);
                    string tmp_listname;

                    int tmp_time;

                    for (int i = 3; i + 1 < stArrayData.Length; i += 4)
                    {
                        string[] tmp_list = { "", "", "" };
                        tmp_listname = "No.";
                        tmp_listname += i / 4 + 1;
                        tmp_list[0] += i / 4 + 1;

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
                        tmp_list[1] += tmp_time.ToString("00");
                        tmp_list[1] += "：";

                        tmp_time = (int)double.Parse(stArrayData[i]) % 60;
                        tmp_listname += tmp_time.ToString("00");
                        tmp_list[1] += tmp_time.ToString("00");

                        tmp_listname += "―";

                        tmp_time = (int)double.Parse(stArrayData[i + 1]) / 60;
                        tmp_listname += tmp_time.ToString("00");
                        tmp_listname += "：";
                        tmp_list[2] += tmp_time.ToString("00");
                        tmp_list[2] += "：";


                        tmp_time = (int)double.Parse(stArrayData[i + 1]) % 60;
                        tmp_listname += tmp_time.ToString("00");
                        tmp_list[2] += tmp_time.ToString("00");

                        switch (area_number)
                        {
                            case 0:
                                listView1.Items.Add(new ListViewItem(tmp_list));
                                break;
                        }
                        scenes_start_l[i / 4] = stArrayData[i];
                    }
                }
                using (StreamReader sr2 = new StreamReader(digestmetafilename, Encoding.GetEncoding("Shift_JIS")))
                {
                    //全行読み込み(行単位のstring配列)
                    string data2 = sr2.ReadToEnd();
                    //カンマ区切りで出力
                    string[] stArrayData2 = data2.Split('\t', '\r', '\n');
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
                //                listBox2.Items.Clear();
            }

        }



        private void フルスクリーンToolStripMenuItem_Click(object sender, EventArgs e)
        {
            axWindowsMediaPlayer1.fullScreen = true;
        }

        private void 終了ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }


        //以下が定期的に呼ばれる関数
        private void timer1_Tick(object sender, EventArgs e)
        {
            int nowtime = (int)axWindowsMediaPlayer1.Ctlcontrols.currentPosition;

            if (mediaready[0] == true)
            {
                Point p = new Point(x1, y1);
                p.X = x1 + (int)(this.pictureBox1.Width * nowtime / medialength) - this.pictureBox4.Width / 2;
                this.pictureBox4.Location = p;
                if (digest_meta.Length > 0 && digest_meta[nowtime] > 0)
                {
                    textBox10.Visible = false;
                }
                else
                {
                    textBox10.Visible = true;
                }

            }
            if (checkBox1.Checked == true && フルスクリーンToolStripMenuItem.Enabled == true && axWindowsMediaPlayer1.playState == WMPLib.WMPPlayState.wmppsPlaying)
            {

                if (digest_meta[nowtime] < 0)
                {
                    if (textBox1.Visible == true)
                    {
                        textBox1.Visible = false;
                    }
                }
                else
                {
                    if (nowtime + (double)rate / 1000 * (fast_speed - 1) < digest_meta[nowtime])
                    {
                        axWindowsMediaPlayer1.Ctlcontrols.currentPosition = nowtime + (double)rate / 1000 * (fast_speed - 1);
                    }
                    else
                    {
                        axWindowsMediaPlayer1.Ctlcontrols.currentPosition = digest_meta[nowtime];
                    }
                    if (textBox1.Visible == false)
                    {
                        textBox1.Visible = true;
                    }
                }
            }
        }

        private void Form1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Space)
            {
                if (axWindowsMediaPlayer1.playState == WMPLib.WMPPlayState.wmppsPlaying)
                {
                    axWindowsMediaPlayer1.Ctlcontrols.pause();
                }
                else if (axWindowsMediaPlayer1.playState == WMPLib.WMPPlayState.wmppsPaused)
                {
                    axWindowsMediaPlayer1.Ctlcontrols.play();
                }
            }
            if (e.KeyChar == (char)Keys.Escape)
            {
                axWindowsMediaPlayer1.fullScreen = false;
            }
        }


        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            //            digest_flag = true;
            if (mediaready[0] == true && checkBox1.Checked == true)
            {
                string tmp_filename = "digest_meta/";
                string stBaseName = System.IO.Path.GetFileNameWithoutExtension(nowplayname[0]);
                tmp_filename += stBaseName;
                tmp_filename += "/";
                tmp_filename += stBaseName;
                tmp_filename += "_digestmeta.txt";

                try
                {
                    using (StreamReader sr = new StreamReader(tmp_filename, Encoding.GetEncoding("Shift_JIS")))
                    {
                        need_pos = sr.ReadToEnd();
                        digestflag = true;
                    }
                }
                catch
                {
                    DialogResult result = MessageBox.Show("ダイジェストメタファイルを作成しますか？",
                        "ダイジェストメタファイルが見つかりませんでした",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Exclamation,
                        MessageBoxDefaultButton.Button2);
                    if (result == DialogResult.Yes)
                    {
                        //「はい」が選択された時
                        string tmp_argument = nowplaypath[0];
                        string Name = System.IO.Path.GetFileNameWithoutExtension(nowplayname[0]);
                        tmp_argument += " ";
                        tmp_argument += Name;

                        string meta_folda = "digest_meta/";
                        meta_folda += Name;
                        System.IO.DirectoryInfo di = System.IO.Directory.CreateDirectory(@meta_folda);

                        System.Diagnostics.Process p = System.Diagnostics.Process.Start("mp4totxt.bat", @tmp_argument);
                        p.WaitForExit();
                        System.Diagnostics.Process q = System.Diagnostics.Process.Start("makemoviefix.exe", @"""180 140 3 10""");
                        //ダイジェストの尺 等倍の尺 ピックアップ箇所の最小時間 ピックアップ箇所の最大時間 を指定

                    }
                    else if (result == DialogResult.No)
                    {
                        //「いいえ」が選択された時
                        checkBox1.Checked = false;
                    }
                }
            }
            else if (checkBox1.Checked == true)
            {
                DialogResult result = MessageBox.Show("ダイジェスト再生するメディアを読み込んでください。",
                        "ダイジェストメタファイルが見つかりませんでした",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation,
                        MessageBoxDefaultButton.Button2);
                checkBox1.Checked = false;
            }
            if (checkBox1.Checked == false)
            {
                textBox1.Visible = false;
            }

        }

        private void Form1_Resize(object sender, EventArgs e)
        {
        }
        private void button2_KeyDown(object sender, KeyEventArgs e)
        {

        }
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            /*
            if (e.KeyCode == Keys.H && e.Alt == true)
            {
                ヘルプToolStripMenuItem.PerformClick(); ;
            }
            if (e.KeyCode == Keys.A)
            {
                ファイルToolStripMenuItem1.PerformClick();

            }*/

        }

        private void axWindowsMediaPlayer1_KeyPressEvent(object sender, AxWMPLib._WMPOCXEvents_KeyPressEvent e)
        {
            //            axWindowsMediaPlayer1.fullScreen = false;
        }

        private void バージョン情報ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("現在のお使いのバージョンは、ver0.9です。",
                "LogLogPlayerのバージョン情報",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2);

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void textBox4_TextChanged(object sender, EventArgs e)
        {

        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            // TrackBarの値が変更されたらラベルに表示
            fast_speed = trackBar1.Value;
            textBox5.Text = fast_speed.ToString();
            textBox5.Text += "倍速";
            textBox1.Text = "▶▶";
            textBox1.Text += fast_speed.ToString();
            viewdigesttime();

        }
        void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            // TrackBarの値が変更されたらラベルに表示
            fast_speed = trackBar1.Value;
            textBox5.Text = fast_speed.ToString();
            textBox5.Text += "倍速";
        }

        private void textBox5_TextChanged(object sender, EventArgs e)
        {

        }

        private void 設定SToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Configform newconfigform = new Configform();
            newconfigform.ShowDialog();
        }

        private void axWindowsMediaPlayer1_DoubleClickEvent_1(object sender, AxWMPLib._WMPOCXEvents_DoubleClickEvent e)
        {
            string[] files_tmp = new string[4];
            string[] paths_tmp = new string[4];

            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Multiselect = true;

            if (openFileDialog1.ShowDialog(this) == DialogResult.OK)
            {
                files_tmp = openFileDialog1.SafeFileNames;
                paths_tmp = openFileDialog1.FileNames;
                video_add(paths_tmp[0], files_tmp[0], 0);
            }
        }
        private void 動画の読み込みToolStripMenuItem1_Click(object sender, EventArgs e)
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
                    video_add(paths_tmp[i], files_tmp[i], i);
                    mediaready[i] = true;
                    //                    calc_volume(i);
                    switch (i)
                    {
                        case 0:
                            textBox6.Text = files_tmp[i];
                            break;
                    }
                }
            }

        }


        private void button5_Click(object sender, EventArgs e)
        {
            make_digest_meta(0);
        }

        private void axWindowsMediaPlayer1_ClickEvent(object sender, AxWMPLib._WMPOCXEvents_ClickEvent e)
        {
            if (mediaready[0] == true)
            {
                if (axWindowsMediaPlayer1.uiMode == "full")
                {
                    axWindowsMediaPlayer1.uiMode = "none";
                }
                else
                {
                    axWindowsMediaPlayer1.uiMode = "full";
                }
            }

        }
        private void viewdigesttime()
        {
            double natural_time=0,highspeed_time,speed;            
            speed = trackBar1.Value;
            for (int i = 0; i < listView2.Items.Count; i++)
            {
                natural_time += timechanger(listView2.Items[i].SubItems[2].Text) - timechanger(listView2.Items[i].SubItems[1].Text);
            }
            highspeed_time = medialength - natural_time;
            highspeed_time /= speed;

            int digestlength = (int)(natural_time + highspeed_time);
            textBox8.Text = (digestlength / 60).ToString();
            textBox9.Text = (digestlength % 60).ToString("00");

        }

        private void button9_Click(object sender, EventArgs e)
        {
            if (mediaready[0] == true)
                add_digestscene_to_list(0);
            if (mediaready[1] == true)
                add_digestscene_to_list(1);
            if (mediaready[2] == true)
                add_digestscene_to_list(2);
            if (mediaready[3] == true)
                add_digestscene_to_list(3);
        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.volume_or_time = true;
            add_digestscene_to_list(0);
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.volume_or_time = false;
            add_digestscene_to_list(0);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Properties.Settings.Default.Save();

            DateTime dt = DateTime.Now;
            string stBaseName3 = System.IO.Path.GetFileNameWithoutExtension(nowplayname[0]);
            string time = "";
            time += dt.Month.ToString("00") + dt.Day.ToString("00") + dt.Hour.ToString("00") + dt.Minute.ToString("00");
            string savefilename = "./digest_meta/" + stBaseName3 + "/" + stBaseName3 + "_digestmeta_chosen_" + time + ".txt";

            try
            {

                StreamWriter sw = new StreamWriter(savefilename, false, Encoding.ASCII);
                string output = "";
                for (int i = 0; i < listView2.Items.Count; i++)
                {
                    output += listView2.Items[i].SubItems[0].Text + "\t" + timechanger(listView2.Items[i].SubItems[1].Text) + "\t" + timechanger(listView2.Items[i].SubItems[2].Text) + "\r\n";
                }
                sw.Write(output);
                sw.Close();
            }
            catch
            {
            }

        }

        private void 分割幅リセット中央ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //            set_all_splitter_half();
        }

        private void 編集ToolStripMenuItem_Click(object sender, EventArgs e)
        {


        }

        private void 作成時刻からoffset取得ToolStripMenuItem_Click(object sender, EventArgs e)
        {/*
            FileInfo fileinfo_standard = new FileInfo(nowplaypath[0]);
            if (mediaready[0] == true)
            {
                FileInfo fileinfo = new FileInfo(nowplaypath[0]);
                numericUpDown2.Value = 60 - (fileinfo.CreationTime.Minute * 60 + fileinfo.CreationTime.Second) + (fileinfo_standard.CreationTime.Minute + fileinfo_standard.CreationTime.Second);
            }
            if (mediaready[1] == true)
            {
                FileInfo fileinfo = new FileInfo(nowplaypath[1]);
                numericUpDown3.Value = 60 - (fileinfo.CreationTime.Minute * 60 + fileinfo.CreationTime.Second) + (fileinfo_standard.CreationTime.Minute + fileinfo_standard.CreationTime.Second);
            }
            if (mediaready[2] == true)
            {
                FileInfo fileinfo = new FileInfo(nowplaypath[2]);
                numericUpDown4.Value = 60 - (fileinfo.CreationTime.Minute * 60 + fileinfo.CreationTime.Second) + (fileinfo_standard.CreationTime.Minute + fileinfo_standard.CreationTime.Second);
            }
            if (mediaready[3] == true)
            {
                FileInfo fileinfo = new FileInfo(nowplaypath[3]);
                numericUpDown1.Value = 60 - (fileinfo.CreationTime.Minute * 60 + fileinfo.CreationTime.Second) + (fileinfo_standard.CreationTime.Minute + fileinfo_standard.CreationTime.Second);
            }
          */
        }


        bool seek_flag = true;
        int start_area;

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void button14_Click(object sender, EventArgs e)
        {
            axWindowsMediaPlayer1.Ctlcontrols.play();
        }

        private void button15_Click(object sender, EventArgs e)
        {
            axWindowsMediaPlayer1.Ctlcontrols.pause();

        }
        private void makemoviefix()
        {

        }

        double medialength;
        private void axWindowsMediaPlayer1_OpenStateChange(object sender, AxWMPLib._WMPOCXEvents_OpenStateChangeEvent e)
        {
            medialength = axWindowsMediaPlayer1.currentMedia.duration;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            make_autobar();
        }
        private void make_autobar()
        {
            string tmp_filename = "digest_meta/";
            string stBaseName2 = System.IO.Path.GetFileNameWithoutExtension(nowplayname[0]);
            tmp_filename += stBaseName2;
            tmp_filename += "/";
            tmp_filename += stBaseName2;
            //            tmp_filename += "_bar.png";

            if (File.Exists(tmp_filename + "_bar.png"))
            {
                // ピクチャボックスに表示する画像ファイルを指定
                pictureBox1.Image = new Bitmap(tmp_filename + "_bar.png"); // 画像
            }
            else
            {
                string tmp_argument = "2 ";
                tmp_argument += tmp_filename + "_digestmeta.txt" + " " + tmp_filename + "_bar.png" + " 20 1";
                System.Diagnostics.Process p = System.Diagnostics.Process.Start("make_bar_image.exe", @tmp_argument);
                p.WaitForExit();
                // ピクチャボックスに表示する画像ファイルを指定
                try
                {
                    pictureBox1.Image = new Bitmap(tmp_filename + "_bar.png"); // 画像
                }
                catch
                {
                }
            }
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {
            x1 = this.pictureBox1.Left;
            x2 = this.pictureBox1.Right;

            //フォーム上の座標でマウスポインタの位置を取得する
            //画面座標でマウスポインタの位置を取得する
            System.Drawing.Point sp = System.Windows.Forms.Cursor.Position;
            //画面座標をクライアント座標に変換する
            System.Drawing.Point cp = this.PointToClient(sp);
            //X座標を取得する
            int x = cp.X;
            axWindowsMediaPlayer1.Ctlcontrols.currentPosition = medialength * (x - x1) / (x2 - x1);

        }

        private void button2_Click(object sender, EventArgs e)
        {
            string tmp_filename = "digest_meta/";
            string stBaseName = System.IO.Path.GetFileNameWithoutExtension(nowplayname[0]);
            tmp_filename += stBaseName;
            tmp_filename += "/";
            tmp_filename += stBaseName;
            tmp_filename += "_digestmeta.txt";


            if (File.Exists(tmp_filename))
            {
                make_autobar();
                make_raw_bar();

            }else{


                DialogResult result = MessageBox.Show("ダイジェストメタファイルを作成しますか？",                
                    "ダイジェストメタファイルが見つかりませんでした",                    
                    MessageBoxButtons.YesNo,                    
                    MessageBoxIcon.Exclamation,                    
                    MessageBoxDefaultButton.Button2);
                if (result == DialogResult.Yes)
                {
                    //「はい」が選択された時
                    make_digest_meta(0);
                    make_autobar();
                    make_raw_bar();
                }
            }

        }
        private void make_raw_bar()
        {
            string tmp_filename = "digest_meta/";
            string stBaseName2 = System.IO.Path.GetFileNameWithoutExtension(nowplayname[0]);
            tmp_filename += stBaseName2;
            tmp_filename += "/";
            tmp_filename += stBaseName2;
            //            tmp_filename += "_bar.png";

            if (File.Exists(tmp_filename + "_bar_raw.png"))
            {
                // ピクチャボックスに表示する画像ファイルを指定
                pictureBox2.Image = new Bitmap(tmp_filename + "_bar_raw.png"); // 画像
            }
            else
            {
                string tmp_argument = "1 ";
                tmp_argument += tmp_filename + "_sound.txt" + " " + tmp_filename + "_bar_raw.png" + " 20";
                System.Diagnostics.Process p = System.Diagnostics.Process.Start("make_bar_image.exe", @tmp_argument);
                p.WaitForExit();
                // ピクチャボックスに表示する画像ファイルを指定
                pictureBox2.Image = new Bitmap(tmp_filename + "_bar_raw.png"); // 画像
            }


        }

        private void pictureBox2_Click(object sender, EventArgs e)
        {
            x1 = this.pictureBox2.Left;
            x2 = this.pictureBox2.Right;

            //フォーム上の座標でマウスポインタの位置を取得する
            //画面座標でマウスポインタの位置を取得する
            System.Drawing.Point sp = System.Windows.Forms.Cursor.Position;
            //画面座標をクライアント座標に変換する
            System.Drawing.Point cp = this.PointToClient(sp);
            //X座標を取得する
            int x = cp.X;
            axWindowsMediaPlayer1.Ctlcontrols.currentPosition = medialength * (x - x1) / (x2 - x1);

        }

        private void pictureBox3_Click(object sender, EventArgs e)
        {
            x1 = this.pictureBox3.Left;
            x2 = this.pictureBox3.Right;

            //フォーム上の座標でマウスポインタの位置を取得する
            //画面座標でマウスポインタの位置を取得する
            System.Drawing.Point sp = System.Windows.Forms.Cursor.Position;
            //画面座標をクライアント座標に変換する
            System.Drawing.Point cp = this.PointToClient(sp);
            //X座標を取得する
            int x = cp.X;
            axWindowsMediaPlayer1.Ctlcontrols.currentPosition = medialength * (x - x1) / (x2 - x1);
        }

        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView1.SelectedItems.Count > 0)
            {
                string time = scenes_start_l[int.Parse(listView1.SelectedItems[0].Text)-1];
                axWindowsMediaPlayer1.Ctlcontrols.currentPosition = double.Parse(time);
                axWindowsMediaPlayer1.Ctlcontrols.play();
            }

        }

        private void listView2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listView2.SelectedItems.Count > 0)
            {
                string time = scenes_start_l[int.Parse(listView2.SelectedItems[0].Text)-1];
                axWindowsMediaPlayer1.Ctlcontrols.currentPosition = double.Parse(time);
                axWindowsMediaPlayer1.Ctlcontrols.play();
            }
        }

        private void listView2_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(ListViewItem)))
            {
                Point p = this.listView2.PointToClient(new Point(e.X, e.Y));
                ListViewItem item = this.listView2.GetItemAt(p.X, p.Y);
                if (item != null)
                    item.Selected = true;
            }

        }

        private void listView2_DragDrop(object sender, DragEventArgs e)
        {
            // ドラッグできるアイテムが存在するかチェックします。      
            if (e.Data.GetDataPresent(typeof(ListViewItem)))
            {
                ListViewItem srcItem = (ListViewItem)e.Data.GetData(typeof(ListViewItem));
                // 
                Point p = this.listView2.PointToClient(new Point(e.X, e.Y));
                ListViewItem item = this.listView2.GetItemAt(p.X, p.Y);
                int destIndex = this.listView2.Items.IndexOf(item);
                // アイテムが存在しない場所を選択すると、IndexOf() が　-1 を返すので、
                // その場合はリストの最後に追加するようにします。
                if (destIndex == -1)
                    destIndex = this.listView2.Items.Count;
                else if (destIndex > srcItem.Index)
                    // 移動先が自分自身より下の場合、選択した場所より１つ下に挿入する。
                    // 自分自身より上の場合、選択した場所に挿入する。
                    destIndex++;
                // その場所に挿入する。
                ListViewItem newItem = this.listView2.Items.Insert(destIndex, (ListViewItem)srcItem.Clone());
                //newItem.Selected = true;
                // Move の場合には、ソースのアイテムを削除してあげる必要があります。
                // Shift Key (MOVE)
                if ((e.KeyState & 0x4) > 0)
                {
                    this.listView2.Items.Remove(srcItem);
                }
            }

            make_selectedbar();
            viewdigesttime();

        }
        private void make_selectedbar()
        {
            //////////////
            int[] hist = new int[(int)medialength];
            for (int i = 0; i < (int)medialength; i++)
            {
                hist[i] = 0;
            }

            List<cut_area> cut_area1 = new List<cut_area>();
            for (int i = 0; i < listView2.Items.Count; i++)
            {
                cut_area1.Add(new cut_area());
                cut_area1[i].start_time = timechanger(listView2.Items[i].SubItems[1].Text);
                cut_area1[i].end_time = timechanger(listView2.Items[i].SubItems[2].Text);
            }
            cut_area1.Sort((a, b) => a.start_time - b.start_time);

            for (int i = 0; i < listView2.Items.Count; i++)
            {
                for (int j = cut_area1[i].start_time; j < cut_area1[i].end_time; j++)
                {

                    hist[j] = 1;
                }
            }

            Bitmap img = new Bitmap((int)medialength, 40);

            for (int x = 0; x < (int)medialength; x++)
            {
                for (int y = 0; y < 40; y++)
                {
                    //色を決める
                    Color r = Color.FromArgb(0, 255, 0);
                    Color w = Color.FromArgb(255, 255, 255);
                    //1つのピクセルの色を変える
                    if (hist[x] == 0)
                    {
                        img.SetPixel(x, y, w);
                    }
                    else
                    {
                        img.SetPixel(x, y, r);
                    }
                }
            }
            //作成した画像を表示する
            pictureBox3.Image = img;
            //////////////

        }

        private void listView1_ItemDrag(object sender, ItemDragEventArgs e)
        {
            listView1.DoDragDrop((ListViewItem)e.Item, DragDropEffects.Copy | DragDropEffects.Move);
        }

        private void listView2_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(ListViewItem)))
            {
                if ((e.KeyState & 0x4) > 0)    // Shift Key (MOVE)
                    e.Effect = DragDropEffects.Move;
                else if (e.KeyState == 0x1 || (e.KeyState & 0x8) > 0)
                    // Mouse Left or Control Key (COPY)
                    e.Effect = DragDropEffects.Copy;
            }


        }

        private void listView2_ItemDrag(object sender, ItemDragEventArgs e)
        {
            listView2.DoDragDrop((ListViewItem)e.Item, DragDropEffects.Move);
        }

        private int timechanger(string time)
        {
            string[] stArrayData = time.Split('：');
            return int.Parse(stArrayData[0]) * 60 + int.Parse(stArrayData[1]);
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            if (listView2.Items.Count > 0)
            {
                List<cut_area> cut_area1 = new List<cut_area>();
                    for (int i = 0; i < listView2.Items.Count; i++)
                    {
                        cut_area1.Add(new cut_area());
                        cut_area1[i].start_time = timechanger(listView2.Items[i].SubItems[1].Text);
                        cut_area1[i].end_time = timechanger(listView2.Items[i].SubItems[2].Text);
                    }
                    cut_area1.Sort((a, b) => a.start_time - b.start_time);

                int[] cut_timing = new int[2+2*listView2.Items.Count];
                cut_timing[0] = 0;
                int count=1;
                for (int i = 0; i < listView2.Items.Count; i++)
                {
                    
                    cut_timing[count] = cut_area1[i].start_time;
                    count++;
                    cut_timing[count] = cut_area1[i].end_time;
                    count++;
                }
                cut_timing[count] = (int)medialength;
                write_bat(cut_timing, cut_timing.Length-1,trackBar1.Value,0);
                
                string stBaseName2 = System.IO.Path.GetFileNameWithoutExtension(nowplayname[0]);
                string batfilePath = "digest_meta\\" + stBaseName2 + "\\" + stBaseName2 + "_moviefix.bat";

                System.Diagnostics.Process process = System.Diagnostics.Process.Start(@batfilePath, nowplaypath[0]);

                /*
                var startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.FileName = System.Environment.GetEnvironmentVariable("ComSpec");
//                startInfo.CreateNoWindow = true;
                startInfo.UseShellExecute = false;
                startInfo.Arguments = string.Format(@batfilePath +" " + nowplaypath[0]);
                System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo);
                process.WaitForExit();
                 */
            }
        }

        private class cut_area
        {
            public int start_time;
            public int end_time;
            public static bool operator <(cut_area x, cut_area y)
            {
                return x.start_time < y.start_time;
            }
            public static bool operator >(cut_area x, cut_area y)
            {
                return x.start_time > y.start_time;
            }

        }

        private int write_bat(int[] cut_timing, int number_of_block, double speed,int startspeed){//asshukutime:倍速を圧縮した時の総時間

	        int tmp=startspeed;
            //startspeed = {1or0}1:最初から等倍　0：最初は倍速

            string oss_all = "";
            string oss_end = "\n\nffmpeg ";

	        int skip_area=0;
	        int ac_number = 1;
	        int sp_number = 1;


            //////ここから倍速//////////
	        for(int i=0;i<number_of_block;i++){
		        if(cut_timing[i+1]-cut_timing[i] >=	1){
			        if(tmp==1){

                        oss_all += "ffmpeg -ss " + cut_timing[i].ToString() + " -t " + (cut_timing[i + 1] - cut_timing[i]).ToString();
                        oss_all += " -i %1 tmp/ac_" + ac_number.ToString() + ".mp4\r\n\r\n";
                        oss_all += "ffmpeg -i tmp/ac_" + ac_number.ToString() + ".mp4"
					        + " -vf \"scale=640:360,setpts=1*PTS\" -af \"atempo=1.0\" -vcodec libx264 -preset fast -vprofile main -qmax 51 -qmin 10 -keyint_min 0 -r 20 -strict -2"
                            + " tmp/ac_" + ac_number.ToString() + "_1.mp4\r\n\r\n";
                        oss_end += "-i tmp/ac_" + ac_number.ToString() + "_1.mp4 ";
				        tmp=0;
				        ac_number++;
			        }else{

                        oss_all += "ffmpeg -ss " + cut_timing[i].ToString() + " -t " + (cut_timing[i + 1] - cut_timing[i]).ToString();
                        oss_all += " -vol 0 -i %1 tmp/sp_" + sp_number.ToString() + ".mp4\r\n\r\n";
                        oss_all += "ffmpeg -i tmp/sp_" + sp_number.ToString() + ".mp4"
					        + " -vf \"scale=640:360,setpts=1*PTS\" -af \"atempo=1.0\" -vcodec libx264 -preset fast -vprofile main -qmax 51 -qmin 10 -keyint_min 0 -r 20 -strict -2"
                            + " tmp/sp_" + sp_number.ToString() +  "_1.mp4\r\n\r\n";
				        int j;
				        for(j=1;j<=(double)(speed/2.0);j=j*2){
                            oss_all += "ffmpeg -i tmp/sp_" + sp_number.ToString() + "_" + j.ToString() + ".mp4 ";
                            oss_all += "-vf \"scale=640:360,setpts=1/2*PTS\" -af \"atempo=2.0\" -vcodec libx264 -preset fast -vprofile main -qmax 51 -qmin 10 -keyint_min 0 -r 20 -strict -2 " + "tmp/sp_" + sp_number.ToString() + "_" + (j * 2).ToString() + ".mp4\r\n\r\n";
				        }
                        oss_all += "ffmpeg -i tmp/sp_" + sp_number.ToString() + "_" + j.ToString() + ".mp4 ";
                        oss_all += "-vf \"scale=640:360,setpts=" + (j / speed).ToString()  + "*PTS\" -af \"atempo=" + ((double)speed / (double)(j)).ToString() + "\" -vcodec libx264 -preset fast -vprofile main -qmax 51 -qmin 10 -keyint_min 0 -r 20 -strict -2 " + "tmp/sp_" + sp_number.ToString() + "_end.mp4\r\n\r\n";
                        oss_end += "-i tmp/sp_" + sp_number.ToString() + "_end.mp4 ";
				        tmp=1;
				        sp_number++;
			        }
		        }else{
			        skip_area++;
			        tmp = 1 - tmp;
		        }
	        }
            oss_end += "-filter_complex \"concat=n=" + (ac_number + sp_number - 2).ToString() + ":v=1:a=1\" tmp/in_fixed.mp4\r\n\r\n";
             //////ここまで倍速//////////

            string stBaseName2 = System.IO.Path.GetFileNameWithoutExtension(nowplayname[0]);
            string filePath = "./digest_meta/" + stBaseName2 + "/" + stBaseName2 + "_moviefix.bat";

            StreamWriter sw = new StreamWriter(filePath, false, Encoding.ASCII);
            sw.Write(oss_all + oss_end);
            sw.Close();

	        return 0;
        }

        private void listView2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Delete && listView2.SelectedItems.Count > 0)
            {
                listView2.Items.Remove(listView2.SelectedItems[0]);
                make_selectedbar();
                viewdigesttime();
            }
        }

        private void button6_Click_1(object sender, EventArgs e)
        {
            DateTime dt = DateTime.Now;
            string stBaseName3 = System.IO.Path.GetFileNameWithoutExtension(nowplayname[0]);
            string time = "";
            time += dt.Month.ToString("00") + dt.Day.ToString("00") + dt.Hour.ToString("00") + dt.Minute.ToString("00");
            string savefilename = "./digest_meta/" + stBaseName3 + "/" + stBaseName3 + "_digestmeta_chosen_" + time +".txt";

            try
            {

                StreamWriter sw = new StreamWriter(savefilename, false, Encoding.ASCII);
                string output = "";
                for (int i = 0; i < listView2.Items.Count; i++)
                {
                    output += listView2.Items[i].SubItems[0].Text + "\t" + timechanger(listView2.Items[i].SubItems[1].Text) + "\t" + timechanger(listView2.Items[i].SubItems[2].Text) + "\r\n";
                }
                sw.Write(output);
                sw.Close();
                DialogResult result = MessageBox.Show("選択シーンを保存しました",
                    "保存",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button2);


            }
            catch {
                DialogResult result = MessageBox.Show("保存に失敗しました",
                    "失敗",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button2);
            
            }

        }

        private void lineShape1_Click(object sender, EventArgs e)
        {

        }


        private void pictureBox4_Click(object sender, EventArgs e)
        {

        }


    }
}
