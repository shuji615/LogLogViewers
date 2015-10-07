/*********************************************
 * waveファイルを扱うためのクラス
 *  開発者：Katsuhiro Morishita(2011.2) Kumamoto-University
 * 
 * [対応フォーマット]   8/16/24/32 bit ステレオ/モノラル　無圧縮リニアPCM
 * [名前空間]   WAVE_file
 * [クラス名]   wave
 * 
 * [参考文献]   waveフォーマット：http://www.kk.iij4u.or.jp/~kondo/wave/
 *              リニアPCM       ：http://e-words.jp/w/E383AAE3838BE382A2PCM.html
 * 
 * [使用方法]   まず、プロジェクトに本ファイルを追加した後に、ソースヘッダに以下のコードを追加のこと
 *              using WAVE_file;
 *              次に、任意のスコープ位置でクラスオブジェクトのインスタンスを生成します。コード例を以下に示す。
 *              
 * 
 * [更新情報]   2011/2/18  開発開始
 *              2011/2/21  読み込みファイルのチャンネル（モノラル／ステレオ）を外部から参照できるように解放
 *              2011/2/27  File_check()の中身を、byte[] to string エンコーディングメソッドを使用してすっきりさせる
 *              2011/9/30  コメントを一部改訂した
 *              2011/10/7  コメントを充実させた。
 *                         チャンネルが定数で宣言されていたのを列挙対に変更した。
 *                         Open()をファイル名orパスで実行できるメソッドを追加。
 *                         WaveFileHeader構造体に初期化メソッドを追加。
 *                         MusicDatasという恥ずかしい構造体名をMusicUnitに変更。
 *                         音声データを取り扱う新たなクラスMusicDataを新設（機能がでかくなるようなら独立させる予定）
 *                         これに伴い、MusicDataクラスを返すメソッドも新設
 *                         今までは8又は16ビットデータしか処理できなかったが、新たに24/32ビットデータも扱えるように拡張
 *                         拡張のために、音を格納するデータサイズを16bitから32ビットへ変更（int型はデフォルトだと32bitなのだが、もし設定をいじってあればエラーの基。。）
 *              2011/10/8  FLLRチャンクにも対応した。
 *              2011/11/16 ファイル名を参照できるようにプロパティを追加
*********************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;                                        //ファイル操作に必要

namespace WAVE_file
{
    /// <summary>
    /// waveファイルを扱うためのクラス
    /// </summary>
    class wave
    {
        /** 定数宣言 *****************/
        // NA
        /** 列挙対宣言 *****************/
        /// <summary>モノラル/ステレオ</summary>
        public enum Channel : int {Monoral, Stereo }
        /** 構造体/クラス宣言 ***************/
        /// <summary>waveファイルのヘッダ情報を格納する構造体</summary>
        private struct WaveFileHeader
        {
            /// <summary>データ形式（リニアPCMなら1）</summary>
            public Int16 ID;
            /// <summary>チャネル数（モノラル/ステレオ）</summary>
            public Channel Channel;
            /// <summary>サンプリングレート[sampling/sec]</summary>
            public Int32 Sampling_Rate;
            /// <summary>データレート[byte/sec](チャネル数×ブロックサイズ)</summary>
            public Int32 Data_Rate;
            /// <summary>1サンプル当たりのサイズ[byte/sample](分解能で決まる1データサイズ×チャネル)</summary>
            public Int16 Block_Size;
            /// <summary>1サンプル当たりのビット数[bit/sample](分解能)</summary>
            public Int16 Resolution_Bit;
            /// <summary>波形データサイズ[byte]</summary>
            public UInt32 Wave_Data_Size;
            /*****　メソッド　*****/
            /// <summary>初期化する</summary>
            public void Init()
            {
                this.ID = 0;
                this.Channel = Channel.Stereo;
                this.Sampling_Rate = 0;
                this.Data_Rate = 0;
                this.Block_Size = 0;
                this.Resolution_Bit = 0;
                this.Wave_Data_Size = 0;
                return;
            }
        }
        /// <summary>左右の音を格納する構造体</summary>
        public struct MusicUnit {
            /// <summary>左の音</summary>
            public int Left;
            /// <summary>右の音</summary>
            public int Right;
        }
        /// <summary>音声データを格納するクラス</summary>
        public class MusicData
        {
            /****** 列挙体 *******/
            /// <summary>データの入っている方を教えるのに使用する</summary>
            public enum UsableChannel {Right, Left, Both, NA};
            /// <summary>左右を指定するために使用する</summary>
            public enum Channel { Right, Left };
            /****** メンバ変数 *******/
            private MusicUnit[] data;
            /****** プロパティ *******/
            /// <summary>データ長</summary>
            public int Size { get { return this.data.Length; } }
            /// <summary>使用可能なチャンネル</summary>
            public UsableChannel UsableCH 
            {
                get 
                {
                    int i;
                    double l = 0, r = 0;

                    for (i = 0; i < this.Size && i < 100; i++)  // 最大100データを検査
                    {
                        l += Math.Abs((double)this.data[i].Left);
                        r += Math.Abs((double)this.data[i].Right);
                    }
                    if (l > 100 && r > 100)
                        return UsableChannel.Both;
                    else if (l > 100)
                        return UsableChannel.Left;
                    else if (r > 100)
                        return UsableChannel.Right;
                    else
                        return UsableChannel.NA;
                }
            }
            /// <summary>
            /// 指定された片方のデータを返す
            /// </summary>
            /// <param name="ch">Channel列挙体</param>
            /// <returns>double型の配列</returns>
            public double[] GetData(Channel ch)
            {
                int i;
                double[] ans = new double[this.Size];

                for (i = 0; i < this.Size; i++) 
                {
                    if (ch == Channel.Left)
                        ans[i] = data[i].Left;
                    else
                        ans[i] = data[i].Right;
                }
                return ans;
            }
            /****** メソッド *******/
            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="_data">音声データ配列</param>
            public MusicData(MusicUnit[] _data)
            {
                this.data = _data;
                return;
            }
        }
        /*************** グローバル変数の宣言 ************/
        /// <summary>waveファイルのファイル名</summary>
        private string fname = "";
        /// <summary>waveファイルのヘッダ情報を格納する構造体</summary>
        private WaveFileHeader file_info;
        /// <summary>ファイルが開けていればture</summary>
        private Boolean        IsOpenStatus = false;
        /// <summary>ファイル読み込みに使用するストリームオブジェクト</summary>
        private FileStream     fs;
        /// <summary>ファイルをバイナリで読み込むためのオブジェクト</summary>
        private BinaryReader   reader;
        /// <summary>読み込み済みの波形データサイズ[Byte]</summary>
        private UInt32         ReadWaveDataSize = 0;

        /****************************　プロパティ　************************************/
        #region "プロパティ"
        /// <summary>ファイル名</summary>
        public string FileName { get { return this.fname; } }
        /// <summary>
        /// ファイルを開けたか状態をチェック
        /// </summary>
        public Boolean IsOpen {
            get {
                return this.IsOpenStatus;
            }
        }
        /// <summary>
        /// 読み込んだファイルがステレオかどうかを示す
        /// </summary>
        public Channel Ch
        {
            get
            {
                return this.file_info.Channel;
            }
        }
        /// <summary>
        /// データを最後まで読み切るとtrue
        /// </summary>
        public Boolean ReadLimit
        {
            get 
            {
                Boolean ans;

                if (this.ReadWaveDataSize < this.file_info.Wave_Data_Size){
                    ans = false;
                }
                else {
                    ans = true;
                }
                return ans;
            }
        }
        /// <summary>
        /// 読み出し位置までの経過時刻[s]を返す
        /// </summary>
        public double NowTime {
            get { return (double)ReadWaveDataSize/(double)this.file_info.Block_Size/(double)this.file_info.Sampling_Rate; }
        }
        /// <summary>
        ///     サンプリング周波数
        /// </summary>
        public int SamplingRate
        {
            get { return (int)this.file_info.Sampling_Rate; }
        }
        /// <summary>
        ///     量子化分解能
        /// </summary>
        public int Resolution
        {
            get { return (int)this.file_info.Resolution_Bit; }
        }
        #endregion
        /****************************　メソッド　************************************/
        #region
        /// <summary>
        ///     ファイルを閉じる
        /// </summary>
        public void Close(){
            if (this.reader != null)
            {
                this.reader.Close();                                                        // こちらを先に閉じる
                this.fs.Close();                                                            // これが後。リソースを解放しているのか少し怪しい。
                this.fs.Dispose();
                this.file_info.Init();                                                      // ファイル情報を初期化
            }
            this.IsOpenStatus = false;
            return;
        }        
        /// <summary>
        /// wave音楽ファイルであるかをチェックする
        /// </summary>
        /// <returns>8/16 bit リニアPCM形式のwave音楽ファイルでなければ、falseを返す</returns>
        private Boolean File_check(){
            byte[] buf;
            string txt;
            Int16 hoge;
            Int32 fllr_size;
            Boolean ans = true;
            int fmt_chunk_size = 0, fmt_read_size = 0;

            txt = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (ans && txt != "RIFF")ans = false;
            if (ans)buf = reader.ReadBytes(4);                                              // 読み飛ばし
            if (ans) txt = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (ans && txt != "WAVE") ans = false;
            if (ans) txt = Encoding.ASCII.GetString(reader.ReadBytes(4));                   // 
            if (ans && txt != "fmt ") ans = false;
            if (ans) fmt_chunk_size = reader.ReadInt32();                                   // fmtチャンクのサイズを取得.リニアPCMであれば16.
            if (ans) this.file_info.ID             = reader.ReadInt16();
            if (ans && this.file_info.ID != 1) ans = false;                                 // リニアPCMであるかをチェック
            if (ans) {                                                                      // チャンネル（ステレオ/モノラルの判定）
                hoge = reader.ReadInt16();
                if (hoge == 1) this.file_info.Channel = Channel.Monoral; else this.file_info.Channel = Channel.Stereo; 
            }
            if (ans) this.file_info.Sampling_Rate  = reader.ReadInt32();
            if (ans) this.file_info.Data_Rate      = reader.ReadInt32();
            if (ans) this.file_info.Block_Size     = reader.ReadInt16();
            if (ans) this.file_info.Resolution_Bit = reader.ReadInt16();
            if (ans && this.file_info.Resolution_Bit != 8 && this.file_info.Resolution_Bit != 16 && this.file_info.Resolution_Bit != 24 && this.file_info.Resolution_Bit != 32) ans = false;
            while (ans && (fmt_chunk_size - 16 - fmt_read_size) > 0) {                      // fmtチャンクの拡張部分を読み飛ばす
                buf = reader.ReadBytes(1);                                                  // ID == 1なら、存在しないので必要ないが、拡張に備えて設置
                fmt_read_size++;
            }
            if (ans) txt = Encoding.ASCII.GetString(reader.ReadBytes(4));                   // 
            if (ans && txt == "FLLR")                                                       // "data"ではなく、FLLRブロックが続くことがあるようだ。
            {
                fllr_size = reader.ReadInt32();                                             // FLLRブロックサイズを取得
                buf = reader.ReadBytes((int)fllr_size);                                     // 読み飛ばし
            }
            if (ans && txt != "data") txt = Encoding.ASCII.GetString(reader.ReadBytes(4));  // 直前の読み込みデータが"FLLR"なら再読み込み
            if (ans && txt != "data") ans = false;                                          // "data"チャンクかどうか
            if (ans) this.file_info.Wave_Data_Size = reader.ReadUInt32();                   // 波形データサイズを取得
            
            return ans;
        }
        /// <summary>
        /// ダイアログを使ってファイルを開く
        /// </summary>
        /// <remarks>ファイルオープンの状態は、IsOpenプロパティに反映されます</remarks>
        /// <param name="dialog">ダイアログオブジェクト</param>
        /// <returns>開けたかどうかをtrue/falseで返す。成功ならtrue。</returns>
        public Boolean Open(OpenFileDialog dialog){
            Boolean format_match;

            if (this.IsOpenStatus == true) this.Close();                            // もしファイルを既に開いているなら、一度閉じる
            fs = new FileStream(dialog.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            reader = new BinaryReader(fs);
            format_match = File_check();
            if (format_match == false) this.Close();
            if (format_match)
            {
                this.IsOpenStatus = true;
                this.fname = System.IO.Path.GetFileName(dialog.FileName);           // ファイル名をバックアップ
            }
            this.ReadWaveDataSize = 0;
            return format_match;
        }
        /// <summary>
        /// ファイル名（またはパス）の指定でファイルを開く
        /// </summary>
        /// <param name="fname">ファイル名（絶対パス・相対パスでも良い）</param>
        /// <returns></returns>
        public Boolean Open(string fname)
        {
            Boolean format_match = false;

            if (this.IsOpenStatus == true) this.Close();                            // もしファイルを既に開いているなら、一度閉じる
            if (File.Exists(fname))                                                 // ファイルの存在を確認
            {
                fs = new FileStream(fname, FileMode.Open, FileAccess.Read, FileShare.Read);
                reader = new BinaryReader(fs);
                format_match = File_check();
                if (format_match == false) this.Close();                            // フォーマットチェック
                if (format_match)
                {
                    this.IsOpenStatus = true;
                    this.fname = fname;
                }
                this.ReadWaveDataSize = 0;
            }
            return format_match;
        }
        /// <summary>
        /// バイナリデータから、リトルエンディアンの24ビット整数を得る
        /// </summary>
        /// <remarks>
        /// C#のint型が32ビットとうち決めた方法で計算する。
        /// とりあえず結合した後、負であれば残りの上位ビットを全部1で埋めればよい。
        /// ex. 24 bit -3 == 0b(1111 1111 1111 1111 1111 1101)
        ///     これが32ビットに拡張しても、0b(1111 1111 1111 1111 1111 1111 1111 1101)となるだけ。
        /// 生の数の場合は代わりに0で埋めればよい（つまり、何もしなくて良い）。
        /// </remarks>
        /// <param name="r">バイナリのリーダーオブジェクト（既に開いていること）</param>
        /// <returns>int型の変数として返します。</returns>
        private int ReadInt24(BinaryReader r)
        { 
            byte[] hoge = new byte[3];
            uint temp, sign;
            int ans;

            hoge = r.ReadBytes(3);                                              // 3 Byte読み込む
            sign = (uint)hoge[2] >> 7;                                          // 7ビット右シフトして、最上位の符号ビットを得る
            temp = (uint)hoge[0] + ((uint)hoge[1] << 8) + ((uint)hoge[2] << 16);// データを足し合わせて32ビット整数に変換する
            if (sign == (uint)1)                                                // 符号ビットが1なら負
                ans = (int)(temp | (uint)0xff000000);                           // 負なら、最上位バイトを1で埋める
            else
                ans = (int)temp;
            return ans;
        }
        /// <summary>
        ///     指定ポイント数の音声データを読み込む。
        ///     モノラルなら、Left/Rightどちらにも格納される。
        /// </summary>
        /// <param name="size">データサイズ[point]</param>
        /// <returns>ファイルを完読すると、0を書き込んで返す</returns>
        public MusicUnit[] Read(int size)
        {
            MusicUnit[] music = new MusicUnit[size];  // これにデータを格納して返す
            int i;

            if (this.IsOpenStatus) {
                for (i = 0; i < size && this.ReadWaveDataSize < this.file_info.Wave_Data_Size; i++){
                    if (this.ReadWaveDataSize < this.file_info.Wave_Data_Size)
                    {
                        // まず左を埋める
                        if (this.file_info.Resolution_Bit == 8)  music[i].Left = (int)reader.ReadSByte();
                        if (this.file_info.Resolution_Bit == 16) music[i].Left = (int)reader.ReadInt16();
                        if (this.file_info.Resolution_Bit == 24) music[i].Left = this.ReadInt24 (this.reader);
                        if (this.file_info.Resolution_Bit == 32) music[i].Left = (int)reader.ReadInt32();
                        // 次に、ステレオ音源なら右を埋める
                        switch (this.file_info.Channel)
                        {
                            case Channel.Stereo:
                                if (this.file_info.Resolution_Bit == 8)  music[i].Right = (int)reader.ReadSByte();
                                if (this.file_info.Resolution_Bit == 16) music[i].Right = (int)reader.ReadInt16();
                                if (this.file_info.Resolution_Bit == 24) music[i].Right = this.ReadInt24(this.reader);
                                if (this.file_info.Resolution_Bit == 32) music[i].Right = (int)reader.ReadInt32();
                                break;
                            case Channel.Monoral:
                                music[i].Right = music[i].Left;
                                break;
                        }
                        this.ReadWaveDataSize += (UInt32)this.file_info.Block_Size; // 読み込んだデータ量を更新
                    }
                    else {
                        music[i].Left = 0;                                          // 読めるデータが無くなると、0を書き込んで返す
                        music[i].Right = 0;
                    }
                }
            }
            return music;
        }
        /// <summary>
        ///     指定時間分のデータを読み込むのに必要となる配列サイズを返す
        /// </summary>
        /// <param name="time_width">指定時間幅[s]</param>
        /// <returns>指定時間のデータを格納するに足るデータサイズ</returns>
        public int GetArrSize(double time_width) {
            return (int)((double)this.file_info .Sampling_Rate * time_width);
        }
        /// <summary>
        ///     指定時間分の音声データを返す
        /// </summary>
        /// <remarks>オーバーロードを利用して同じ名前でメソッドを定義した</remarks>
        /// <param name="time_width">指定時間幅[s]</param>
        /// <returns>音声データクラス</returns>
        public MusicData Read(double time_width)
        {
            MusicData ans = new MusicData(this.Read(GetArrSize(time_width)));
            return ans;
        }

        /****************************　初期化/削除　************************************/
        /*****************
         * このクラスのデストラクタ
         * ***************/
        /// <summary>
        /// 　デストラクタ.
        /// </summary>
        ~wave(){
            this.Close();
        }
        #endregion
    }
}
