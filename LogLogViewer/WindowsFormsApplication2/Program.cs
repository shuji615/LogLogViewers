using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;


namespace WindowsFormsApplication2
{
    static class Program
    {
        /// <summary>
        /// アプリケーションのメイン エントリ ポイントです。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
    class SpeechDialog
    {
        //dllのImport
        //SpeechAPI
        [DllImport("SpeechDialog.dll")]
        public extern static bool SpeechDlg(IntPtr Handle, [MarshalAs(UnmanagedType.LPArray)] byte[] res);
        //音声合成API
        [DllImport("GoogleTTS.dll")]
        public extern static void TTS(string in_str);

        static void main2(string[] args)
        {
            for (int i = 0; i < 2; i++)
            {
                string test = string.Empty;
                byte[] res_byte = new byte[4096];
                bool res;
                res = SpeechDlg(IntPtr.Zero, res_byte);
                //変換(SJISだと"shift_jis")
                string str = System.Text.Encoding.GetEncoding("shift_jis").GetString(res_byte);
                if (res)
                {
                    test = str;
                }

                if (test != "")
                {
                    // 音声認識
                    if (test.Contains("今何時"))
                    {
                        string text = "今、" + DateTime.Now.Hour.ToString() + "時" + DateTime.Now.Minute.ToString() + "分です。";
                        TTS(text);
                    }
                    else
                    {

                        TTS(test);
                    }

                }
            }
        }
    }
}
