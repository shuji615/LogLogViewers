/******************************************************************************
 * SignalBasic.cs
 * 
 * [開発者]
 *  森下功啓（Katsuhiro Morishita）
 * 
 * [プログラムの目的と課題]
 *  本プログラムの使用目的は、ノイズフロアから飛び出す音声を検出することです。
 *  識別対象としては当面の間は鳥類です。
 *  
 *  本プログラムの課題は、如何にノイズフロアを認識するのかという点にあります。
 *  カエルが鳴こうが雨が降ろうが風が吹こうがノイズが時間的に変化しようが認識させたいと考えています。
 *  基本的には統計的な考えを入れる必要がありますが、鳥類はかなりバリエーションの多い鳴き方をするため万能なフィルタの設計は不可能です。
 *  長い時間鳴き続ける種類もあり、少々難しい問題を孕んでいます。
 *  
 *  基本コンセプトは、「音源とマイクの距離によらず検出可能でしかもノイズフロアの変動に強い」です。
 * 
 * [プログラム概要]
 *  本ソースコードにおけるSignalBasicクラスは、FFTの結果を渡す事で任意の帯域における鳴き声を検出するためのクラスです。
 *  設計構造の問題から、認識には数秒間の初期化時間が必要です。
 *  初期化時間は、5秒～20秒です。
 *  音声ファイルの先頭から鳴き続けられると、信号はノイズなのか鳴き声なのかは不明なので、初期化時間が長くなります。
 *  
 *  2011/11/18現在では、どんな音源に対しても一定の成績を挙げるものの、万能なパラメータは見つけていません。
 *  現時点では大抵の音源に対して処理可能なパラメータを設定しています。
 *  私自身が使用した感想としては、「発声を検出」とされても時間的に短いものは無視するという処理を後で行うことでノイズの影響を排除可能だと思っています。
 *  
 *  鳥の鳴き声検出の流れは、「鳴き声検出」「音声部分抜き出し」「識別」です。
 *  本プログラムでは「鳴き声検出」部分を担当します。
 *  世の中の主流（少なくとも鳥類の音声研究において）は、音声モデルを「手作業」で作って、SS方なりなんなりして背景雑音の影響を除去するらしいです。
 *  これは音源が異なればもう一度再度モデルを再構築する必要があります。
 *  私はそんな手間はかけたくありません。
 *  もしかすると、人間の言語認識ソフトウェアの分野では既に解決された問題かもしれませんが…。
 *  
 *  本プログラムは、音源ファイルから「鳴き声」らしき部分を検出させて、その後の処理にゴミデータとの識別を任せる形でプログラムをモジュール化する予定です。
 *  従って、本クラスに音源モデルを導入する気はありません。
 * 
 * [検出原理]
 *  一定程度のノイズデータが集まれば、それはガウス分布をしているに違いありません。
 *  平均と標準偏差が計算できれば、入力データがそれまでの分布内に入るかどうかを検定することが可能です。
 *  従って、一定時間の無音期間があればノイズモデルを構築して発声部分を検出することが可能です。
 *
 *  ノイズフロアは数秒周期で変動することも有るので、一度モデルを構築した後に更新が行われないと音声を誤検出してしまいます。
 *  従って、リングバッファを用いてある時間幅だけ観測情報を保持するようにして、モデルを更新します。
 *  リングバッファを用いてノイズモデルを構築するので、リングバッファには、発声部分以外の観測データを入れる必要があります。
 *
 *  ここで、発声部分がどのようなデータであるかを定義しておきます。
 *  一定の周波数帯域におけるノイズとは、ほぼ一定の標準偏差で振幅が変動する信号です。
 *  欲しいのは非常に大きくうねる部分です。
 *  うねりの大きさは、ノイズの振幅との比で表すことができます。
 *
 *  従って、検定では
 *  1) パワースペクトル密度の底はノイズフロアレベル
 *  という仮定を置くことができます。
 *  ついでに以下も仮定しておきます。
 *  2) 無音区間が十分にあって、本当のノイズフロアはいつか確実に得られる
 *
 *  検定では平均値を用いる代わりに、一定時間内の最低値を用います。
 *  プログラムが走り始めた当初は全ての観測データを取る必要があるので、必然として推定される分散は全データ分です。
 *  また、分布がガウス分布であれば最低値≒平均-3σ程度とみなして構いません。
 *  従って、平均の代わりに最低値を用いるとz = (観測値-最低値)/標準偏差でz > 3～5だとほぼうねりのピーク部分であると言えます。
 *  このピークと認識された観測データをリングバッファに入れないことにより、より低いノイズフロアを収集することが可能です。
 *
 *  以上の処理によって、音声データから発声部分を抜き出すことはできるのですが、発声部分と識別されたデータをリングバッファに入れないでいると、必然としてノイズフロアが上昇する場合に対応できません。
 *  また、微小な信号に対しての感度が高くなりすぎます。
 *  これはより小さな値だけを収集していくとそのデータ系列の標準偏差が小さくなるために生じる問題です。
 *  従って、時々は大きな値をリングバッファに入れて、標準偏差が小さくなり過ぎないように工夫する必要があります。
 *  本プログラムでは、発声部分とする閾値とリングバッファに格納する閾値との間にマージンを設けることでこれに対処しています。
 *  つまり、発声部分と識別されても、ある程度ならばリングバッファに入れてしまうということです。 
 * 
 * [今後の開発方針]
 *  1) 強制的に鳴き声（発声）しているということを認識させるメソッドを追加
 *  2) もしかすると、後処理機能を追加するかも。
 *  3) 検出・ノイズフロアの変化応答性能が割と出たのでもう十分かなと個人的には考えている。
 *  
 * [履歴]
 *  2011/11/15  整備開始
 *              とりあえず、信号検出部分を急いで整備した。
 *              コンセプトを昨年の内に考えていた割に、作り始めると意外と時間がかかる。。
 *  2011/11/18　連日整備を続けて、ようやくバグをなくして来たかな？
 *              なお、本クラスは現時点では作りかけの状態ですので将来、大きな改修が行われる可能性があります。
 * ****************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace 鳥の声で種識別
{
    /// <summary>
    /// 音声データの基本的な処理クラス
    /// </summary>
    public class SignalBasic
    {
        /******************* 構造体・クラス ***************************/
        /// <summary>
        /// 信号の検出のためのクラス
        /// 2011/11/15時点では、一度検出されるとその後は検出の感度が鈍るという副作用がある。
        /// これは検出時には値をセットされてもストックしないことで対応可能といえば可能
        /// ただし、グローバル変数にセットされた値を一時的に保持する必要がある。
        /// </summary>
        public class SoundDetection
        {
            /********* 列挙体 ************/
            /// <summary>検出されたエッジの種類</summary>
            public enum Edge { rising, falling, NA}
            /********* メンバ変数 ************/
            /// <summary>
            /// 音声検出閾値（この値の1が1σに相当）
            /// この値が大きいと、感度が下がる傾向にある。
            /// データ先頭からずっと鳴きっぱなしの音声データでは、
            /// 　「信号の標準偏差が非常に大きな音声として認識されるため、かなり大きな鳴き声でも鳴き声と判定されずに通常音声として処理されることが原因で」
            /// フィルタの学習が進まなくなるので注意してください。
            /// 感度の調整は、下のマージンの方でも若干ながら可能です。
            /// </summary>
            private readonly double _threshold = 5.0;
            /// <summary>
            /// 無音区間学習用閾値
            /// 雑音ではなく、「音声」として認識させるためにたった一つの閾値（_threshold）を使用すると、徐々に信号の推定標準偏差が小さくなっていって最終的に感度が高くなり過ぎてしまう。
            /// そこで、「音声」として認識されたとしても若干なら通常音声として処理する必要がある。
            /// そのためのマージンです。
            /// 
            /// この値が大きいと、やっぱり感度が下がる傾向があります。
            /// この値が大きいと、データ先頭からずっと鳴きっぱなしのデータを入力すると通常音声として処理されるデータが増えてしまい、計測したいノイズの推定標準偏差が大きくなりすぎてしまう。
            /// 結果として、いつまでたっても「音声」として認識されないという問題が生じます。
            /// 
            /// 鳴く頻度が低い鳥ならこの値を50程度と大き目にするとノイズフロアのドリフトへの応答が早くなります。
            /// しかし、
            /// 鳴く頻度が高い鳥なら、0.5程度が適正です。
            /// </summary>
            private readonly double _thresholdMargin = 0.5;
            /// <summary>検出されたエッジ</summary>
            private Edge _edge = Edge.NA;
            /// <summary>前方検出フラグ</summary>
            private Boolean _frontDetection = false;
            /// <summary>後方検出フラグ</summary>
            private Boolean _rearDetection = false;
            /// <summary>フィルタリングされた平均</summary>
            private double _meanLevel = 0.0;
            /// <summary>フィルタリングされた標準偏差</summary>
            private double _standardDeviation = 0.0;
            /// <summary>閾値によってフィルタリングされない平均</summary>
            private double _noFiltMeanLevel = 0.0;
            /// <summary>閾値によってフィルタリングされない標準偏差</summary>
            private double _noFilterStandardDeviation = 0.0;
            /// <summary>加算数</summary>
            private double _addCounter = 0;
            /// <summary>書き込みポインタ（といっても、言語のポインタではない）</summary>
            private int _wp = 0;
            /// <summary>読み込みポインタ（といっても、言語のポインタではない）</summary>
            private int _rp = 0;
            /// <summary>閾値によってフィルタリングされないデータストックのための書き込みポインタ（といっても、言語のポインタではない）</summary>
            private int _wp4noFilter = 0;
            /// <summary>平均数：後進平均</summary>
            private int _numOfMean;
            /// <summary>データストックのためのストレージ</summary>
            private double[] _stock;
            /// <summary>閾値によってフィルタリングされないデータストックのためのストレージ</summary>
            private double[] _stock4noFilter;
            /// <summary>PSDの最低値を検出するのに使用するバッファのサイズを決定する時間　デフォルトだと10秒</summary>
            private TimeSpan _timespan4minLevel;
            // バックアップ用の変数
            private double backup_z;
            private double backup_psd = double.PositiveInfinity;
            private double bottom_psd;
            /********* プロパティ ************/
            /// <summary>
            /// 前方音声検出フラグ
            /// 検出されると、tureとなる。
            /// </summary>
            public Boolean FrontDetection { get { return this._frontDetection; } }
            /// <summary>
            /// 後方音声検出フラグ
            /// 検出されると、tureとなる。
            /// </summary>
            public Boolean RearDetection { get { return this._rearDetection; } }
            /// <summary>
            /// 検出されたエッジの種類
            /// </summary>
            public Edge DetectedEdge { get { return this._edge;} }
            /// <summary>
            /// フィルタリングされないデータストレージの中での最小値
            /// </summary>
            private double MinLevel {
                get {
                    double min = double.PositiveInfinity;
                    foreach (double v in _stock4noFilter)
                        if (v < min && v != 0.0) min = v;
                    return min;
                }
            }
            /********* メソッド ************/
            /// <summary>
            /// 強制的に現状を音声が入力されていると認識させる
            /// 2011/11/18現在、未実装
            /// </summary>
            public void RecognizeSignal()
            {
                // 現状におけるデータストックから、分散を計算して、入力値が小さいとみられるデータのみで平均・分散を再計算の後に再度スクリーニングを行い、
                // データストックを再構成する。
                // これで強制的にパラメータを調整可能なはず。
                return;
            }
            /// <summary>
            /// 平均値を計算する
            /// </summary>
            /// <param name="buff">バッファ</param>
            private double CalcMean(double[] buff)
            {
                double sum = 0.0;
                double cnt = 0.0;

                foreach (double v in buff)
                {
                    if (v != 0.0)
                    {
                        sum += v;
                        cnt += 1.0;
                    }
                }
                return sum / cnt;
            }
            /// <summary>
            /// 標準偏差を計算する
            /// </summary>
            /// <param name="buff">バッファ</param>
            /// <param name="mean">平均値</param>
            private double CalcSD(double[] buff, double mean)
            {
                double sum = 0.0;
                double cnt = 0.0;

                foreach (double v in buff)
                {
                    if (v != 0.0)
                    {
                        sum += Math.Pow((v - mean), 2.0);
                        cnt += 1.0;
                    }
                    
                }
                double variance = sum / (cnt - 1.0);
                return Math.Sqrt(variance);
            }
            /// <summary>
            /// 信号に音声が入っているのかを検出する
            /// 平均値は鳥が頻繁になくと信頼性がなくなるので、最小値を用いる方がずっと良い。
            /// なお、チェックは御検出を避けるために規定数以上のデータをセットされた後となる。
            /// </summary>
            /// <param name="PSD">パワースペクトル密度[V^2/Hz]</param>
            /// <returns>入力されたPSDの正規化されたレベル</returns>
            private double Check(double PSD)
            {
                //double z = (PSD - this._meanLevel) / this._standardDeviation;       // ↓の最低値を使った方が確実な気がする。応答は、初めの数秒間を除けば平均の方が早いほどだが。。
                double z = (PSD - this.MinLevel) / this._standardDeviation;       // 分かり易くするために、正規化 ファイルの先頭から鳴き続けるデータに対してはこちらの方が有効
                if (z > this._threshold)
                    this._frontDetection = true;
                else
                    this._frontDetection = false;
                return z;
            }
            /// <summary>
            /// 2つの統計量を比較して、2つの分布が分離するならtrueを返す
            /// </summary>
            /// <param name="mean1">分布1の平均</param>
            /// <param name="mean2">分布2の平均</param>
            /// <param name="sd1">分布1の標準偏差</param>
            /// <param name="sd2">分布2の標準偏差</param>
            /// <returns>2つの分布が分離するならtrue</returns>
            private Boolean CheckStatistic(double mean1, double mean2, double sd1, double sd2)
            {
                Boolean ans = false;
                double u1, u2, d1, d2;

                u1 = 1.0;
                d1 = -1.0;
                u2 = (mean2 - mean1) / sd1 + sd2 / sd1 * 1.0;
                d2 = (mean2 - mean1) / sd1 - sd2 / sd1 * 1.0;
                double amplitude = Math.Log10(sd2);
                // 統計的なものと、分散の比に大きなかい離が生じた場合に更新を促すように設計している
                //if (!((u1 > d2 && d1 < d2) || (u1 > u2 && d1 < u2) || (u1 > u2 && d1 < d2) || (u1 < u2 && d1 > d2)))
                //    ans = true;
                if (Math.Abs(Math.Log10(sd2 / sd1)) > 6.0)
                    ans = true;
                return ans;
            }
            /// <summary>
            /// データをセットする
            /// </summary>
            /// <param name="PSD">パワースペクトル密度[V^2/Hz]</param>
            public void Set(double PSD)
            {
                // とにかくストックするバッファの処理　下の処理より先行する必要がある
                this._stock4noFilter[this._wp4noFilter] = PSD;                                              // ストレージに格納
                this._wp4noFilter = (this._wp4noFilter + 1) % this._stock4noFilter.Length;                  // ライトポイントを更新
                this._noFiltMeanLevel = this.CalcMean(this._stock4noFilter);                                // 平均値を計算させておく（標準偏差よりも先に計算すること）
                this._noFilterStandardDeviation = this.CalcSD(this._stock4noFilter, this._noFiltMeanLevel); // 標準偏差を計算
                // 分布の検定
                if(this.CheckStatistic(this._meanLevel, this._noFiltMeanLevel, this._standardDeviation, this._noFilterStandardDeviation))
                {
                    this._meanLevel = this._noFiltMeanLevel;                                                // 検定の結果、統計量に無視できない乖離が認められるとフィルタ値を更新する
                    this._standardDeviation = this._noFilterStandardDeviation;
                }
                // 観測データをチェックして、正規化した値を取得
                double zf = this.Check(PSD);
                if (PSD < this.backup_psd) this.bottom_psd = PSD;
                this.backup_z = zf;
                // バイアスの把握のためのデータストック
                // 条件は順に、異様に大きくない入力信号であること，発散している場合，必要なデータ量が足りない場合，最低値が平均よりも高い場合
                if ((zf < this._threshold + this._thresholdMargin || zf == double.PositiveInfinity || zf == double.NegativeInfinity) || this._addCounter < this._numOfMean || this.MinLevel > this._meanLevel)  // 学習条件
                {
                    this._stock[this._wp] = PSD;                                                            // ストレージに格納
                    this._rp = this._wp;                                                                    // 最新データの書き込み位置を記憶しておく
                    this._wp = (this._wp + 1) % this._stock.Length;                                         // ライトポイントを更新
                    this._meanLevel = this.CalcMean(this._stock);                                           // 平均値を計算させておく（標準偏差よりも先に計算すること）
                    this._standardDeviation = this.CalcSD(this._stock, this._meanLevel);                    // 標準偏差を計算
                    this._addCounter += 1.0;                                                                // 加算数を加算
                }
                this.backup_psd = PSD;                                                                      // バックアップ
                return;
            }
            /// <summary>
            /// フィルタ状況を文字列で出力
            /// </summary>
            /// <returns>フィルタ情報</returns>
            public override string ToString()
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder(200);
                DateTime t;
                t = DateTime.Now;
                sb.Append(t.Year.ToString() + "/" + t.Month.ToString() + "/" + t.Day.ToString() + " " + t.Hour.ToString() + ":" + t.Minute.ToString() + ":" + t.Second.ToString() + "." + t.Millisecond.ToString("000"));
                sb.Append(",").Append(this._meanLevel.ToString("e3")).Append(",").Append(this._standardDeviation.ToString("e3")).Append(",").Append(this._noFiltMeanLevel.ToString("e3")).Append(",").Append(this._noFilterStandardDeviation.ToString("e3"));
                sb.Append(",").Append(this.backup_z.ToString("0.00"));
                sb.Append(",").Append(this.backup_psd.ToString("e3"));
                sb.Append(",").Append(this.MinLevel.ToString("e3"));
                sb.Append(",").Append(this.bottom_psd.ToString("e3"));
                if (this.FrontDetection)
                    sb.Append(",").Append(this._threshold.ToString());              // 閾値を使うことで、zとの関係上グラフが見やすい
                else
                    sb.Append(",").Append("0.001");
                sb.Append("\n");
                return sb.ToString();
            }
            /// <summary>
            /// 結果をファイルに保存する
            /// </summary>
            /// <param name="fname">ファイル名</param>
            public void Save(string fname = "hoge.csv")
            {
                try { 
                    using (System.IO.StreamWriter sw = new System.IO.StreamWriter(fname, true, System.Text.Encoding.GetEncoding("shift_jis")))
                    {
                        sw.Write(this.ToString());
                    }
                }
                catch { }// 例外処理
                return;
            }
            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="numOfMean">平均数</param>
            /// <param name="samplingPeriod">サンプリング周期</param>
            /// <param name="timespan4minlevel">最低値を検出するための時間幅</param>
            public SoundDetection(int numOfMean, TimeSpan samplingPeriod, TimeSpan timespan4minlevel)
            {
                this._numOfMean = numOfMean;
                this._timespan4minLevel = timespan4minlevel;
                this._stock = new double[numOfMean];                                                                                // 配列サイズを指定
                this._stock4noFilter = new double[(int)(timespan4minlevel.TotalMilliseconds / samplingPeriod.TotalMilliseconds)];   // 配列サイズを指定
            }
        }
        /******************* メンバ変数 ***************************/
        /// <summary>帯域の分解数</summary>
        private int _numberOfPartitions;
        // <summary>正規化された時定数：</summary>
        //private int _timeConstant;
        /// <summary>平均数：後進平均</summary>
        private int _numOfStock;
        /// <summary>音声検出器</summary>
        private SoundDetection[] sensor;
        /// <summary>音声検出器の担当帯域</summary>
        private Signal_Process.FFTresult.Band[] band;
        /// <summary>平均数：後進平均</summary>
        private Boolean _detection = false;
        /// <summary>検出されたエッジ</summary>
        private SoundDetection.Edge _edge = SoundDetection.Edge.NA;
        /******************* プロパティ ***************************/
        /// <summary>
        /// 検出結果
        /// </summary>
        public Boolean Detection { get { return this._detection; } }
        /// <summary>
        /// 検出されたエッジの種類
        /// 多数の検出器があるのに、どうやって知らせたらいいのか？
        /// </summary>
        public SoundDetection.Edge Edge { get { return this._edge; } }
        /// <summary>
        /// PSDの最低値検出窓の時間幅を設定する
        /// 2011/11/18現在、未完成
        /// sensorオブジェクトを全て呼び出して設定する予定
        /// </summary>
        public TimeSpan SetTimeSpan4minLeve { set { } }
        /******************* メソッド ***************************/
        /// <summary>
        /// FFT処理後のデータをセットする
        /// 
        /// </summary>
        /// <param name="data"></param>
        public void Set(Signal_Process.FFTresult data)
        {
            Boolean detection = false;

            for (int i = 0; i < this.sensor.Length; i++)
            {
                //double psd = Math.Log10(data.GetPSD(this.band[i]));               // FFTの結果から、パワー密度を取得
                double psd = data.GetPSD(this.band[i]);                             // FFTの結果から、パワー密度を取得
                this.sensor[i].Set(psd);                                            // 検出器に掛ける
                if(this.sensor[i].FrontDetection)detection = true;                  // 検出結果をチェック（一つでも検出していたらture）
                this._edge = this.sensor[i].DetectedEdge;
            }
            this._detection = detection;                                            // 結果をバックアップ
            return;
        }
        /// <summary>
        /// 指定されたインデックスの帯域における状況をファイルに出力する
        /// 帯域とインデックスを結び付けるメソッドを用意しないと意味があまりないかも
        /// </summary>
        /// <param name="index">インデックス番号</param>
        /// <param name="fname">ファイル名</param>
        public void Save(int index, string fname)
        {
            if (index >= 0 && index < this.sensor.Length)
                this.sensor[index].Save(fname);
            else
                throw new System.FormatException("インデックスの値が不正です");     // 引数に不正があれば例外をスロー
            return;
        }
        /// <summary>
        /// 本クラスが内部に保持している帯域のリストを返す
        /// </summary>
        /// <returns>帯域のリスト</returns>
        public Signal_Process.FFTresult.Band[] GetBands()
        {
            Signal_Process.FFTresult.Band[] bands = new Signal_Process.FFTresult.Band[this.band.Length];
            for (int i = 0; i < this.band.Length; i++ )                             // クローンを作って返す（Bandを構造体で設計しているのでこれでもよい）
                bands[i] = this.band[i];
            return bands;   
        }
        /// <summary>
        /// 検出された帯域情報を返す
        /// </summary>
        /// <returns>帯域情報</returns>
        public Signal_Process.FFTresult.Band[] GetDetectedBands()
        { 
            int i = 0;
            for (int k = 0; k < this.sensor.Length; k++) if (this.sensor[k].FrontDetection == true) i++;    // 信号が検出された帯域数をカウント
            Signal_Process.FFTresult.Band[] bands = new Signal_Process.FFTresult.Band[i];                   // 信号が検出された数だけ帯域を示すクラスを生成
            i = 0;
            for (int k = 0; k < this.sensor.Length; k++)
            {
                if (this.sensor[k].FrontDetection == true)
                {
                    bands[i] = this.band[k];
                    i++;
                }
            }
            return bands;
        }
        /// <summary>
        /// 任意の帯域において発声が検出されているかどうかを返す
        /// 検査対象外の帯域だったり、検出されていなければfalseが返ります。
        /// </summary>
        /// <param name="band">指定帯域</param>
        /// <returns>検出されていればtrue</returns>
        public Boolean CheckDetection(Signal_Process.FFTresult.Band band)
        {
            Boolean ans = false;
            for (int i = 0; i < this.band.Length; i++)                                                      // 本クラス内に用意されている検査器を全てチェック
            {
                if (this.band[i].CenterFrequency > band.Min && this.band[i].CenterFrequency > band.Max && this.sensor[i].FrontDetection == true)
                {
                    ans = true;
                    break;
                }
            }
            return ans;
        }
        /// <summary>
        /// 強制的に、指定帯域で発声があることを認識させる
        /// 2011/11/18 呼び出し先が未実装・・・
        /// </summary>
        /// <param name="band">指定帯域</param>
        public void RecognizeSignal(Signal_Process.FFTresult.Band band)
        {
            for (int i = 0; i < this.band.Length; i++)                                                      // 本クラス内に用意されている検査器を全てチェック
            {
                if (this.band[i].CenterFrequency > band.Min && this.band[i].CenterFrequency > band.Max)
                {
                    this.sensor[i].RecognizeSignal();                                                       // 検査器に発声を認識させる
                }
            }
            return;
        }
        /// <summary>
        /// 初期化
        /// エラー処理付
        /// </summary>
        /// <param name="numberOfPartitions">用意する帯域数（分割数）</param>
        /// <param name="maxFrequency">最大周波数[Hz]<</param>
        /// <param name="minFrequency">最小周波数[Hz]<</param>
        /// <param name="samplingPeriod">検査時間</param>
        /// <param name="timespan">サンプリング周期</param>
        private void Init(int numberOfPartitions, long maxFrequency, long minFrequency, TimeSpan timespan, TimeSpan samplingPeriod)
        {
            int numOfStock = (int)(timespan.TotalSeconds / samplingPeriod.TotalSeconds);    // 用意すべきストック数を計算
            if (numberOfPartitions > 0 && maxFrequency > 0 && minFrequency > 0 && maxFrequency > minFrequency && numOfStock > 0)
            {
                this._numOfStock = numOfStock;
                this._numberOfPartitions = numberOfPartitions;
                this.sensor = new SoundDetection[numberOfPartitions];                       // 配列の大きさを定義
                this.band = new Signal_Process.FFTresult.Band[numberOfPartitions];          // 帯域の数を調整
                long delta_freq = (maxFrequency - minFrequency) / (long)numberOfPartitions; // 一つの帯域あたりの周波数幅を計算
                long lower_freq = minFrequency;                                             // 帯域の下限周波数
                for (int i = 0; i < this.sensor.Length; i++)
                {
                    this.sensor[i] = new SoundDetection(this._numOfStock, samplingPeriod, new TimeSpan(0, 0, 10)); // インスタンス生成
                    long upper_freq = delta_freq * (i + 1) + lower_freq;
                    this.band[i] = new Signal_Process.FFTresult.Band(upper_freq, lower_freq);
                    lower_freq = upper_freq;                                                // 下限周波数を更新
                }
            }
            else
                throw new System.FormatException();                                         // 引数に不正があれば例外をスロー
            return;
        }
        
        // コンストラクタは、クラスのインスタンスを生成（メモリ上に実際に使用する領域が準備されることを指す）したときだけ、初期化のために呼び出されるメソッドです。
        /// <summary>
        /// コンストラクタ
        /// 帯域や分割数、監視する時間幅を指定してください。
        /// </summary>
        /// <param name="numberOfPartitions">帯域分割数</param>
        /// <param name="maxFrequency">最大周波数[Hz]</param>
        /// <param name="minFrequency">最小周波数[Hz]</param>
        /// <param name="timespan">検査時間 時間窓をずらしながら検査するので、応答速度や計算精度に影響します。</param>
        /// <param name="samplingPeriod">サンプリング周期</param>
        public SignalBasic(int numberOfPartitions, long maxFrequency, long minFrequency, TimeSpan timespan, TimeSpan samplingPeriod)
        {
            
            this.Init(numberOfPartitions, maxFrequency, minFrequency, timespan, samplingPeriod);      // 丸投げ
        }
        /// <summary>
        /// コンストラクタ
        /// 最低周波数は0となります。
        /// </summary>
        /// <param name="numberOfPartitions">帯域分割数</param>
        /// <param name="maxFrequency">最大周波数[Hz]</param>
        /// <param name="timespan">検査時間 時間窓をずらしながら検査するので、応答速度や計算精度に影響します。</param>
        /// <param name="samplingPeriod">サンプリング周期</param>
        public SignalBasic(int numberOfPartitions, long maxFrequency, TimeSpan timespan, TimeSpan samplingPeriod)
        {
            this.Init(numberOfPartitions, maxFrequency, 0, timespan, samplingPeriod);                 // 丸投げ
        }
        /// <summary>
        /// コンストラクタ
        /// 帯域構造体を使用したバージョンです。
        /// </summary>
        /// <param name="numberOfpartitions">帯域分割数</param>
        /// <param name="band">検査対象領域</param>
        /// <param name="timespan">検査時間 時間窓をずらしながら検査するので、応答速度や計算精度に影響します。</param>
        /// <param name="samplingPeriod">サンプリング周期</param>
        public SignalBasic(int numberOfPartitions, Signal_Process.FFTresult.Band band, TimeSpan timespan, TimeSpan samplingPeriod)
        { 
            long maxFrequency = band.Max;
            long minFrequency = band.Min;
            this.Init(numberOfPartitions, maxFrequency, minFrequency, timespan, samplingPeriod);      // 丸投げ
        }
    }
}
