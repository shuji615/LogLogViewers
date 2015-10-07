/*******************************************
 * 信号処理クラス
 * 
 * 更新履歴
 *  2010/5/23   森下整備開始
 *  2011/2/18   名前空間を名づけた
 *              今後：渡されたデータ幅がFFTデータ幅を超えた場合でも、可能なだけ結果を返すようにしたい。
 *  2011/2/26   コンストラクタでのエラー対策と、コメントを入れる。
 *              拡張予定：　帯域指定でのパワー計算値を返すメソッド（引数にサンプリング周波数情報が必須）
 *                          窓関数に三角窓も追加予定
 *  2011/9/30   コメントの見直しを実施
 *  2011/11/15  コメントの見直しを更に実施
 *              関数の一部整理
 *              サンプリング周波数に関してメソッドを追加
 *              FFT後のデータを取り扱いやすくするため、FFTresultクラスを整備
 *              FFTdata構造体を少し改良
 * ***************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Signal_Process
{
    /********************************************************
     * FFT/IFFT関連クラス
     *  開発者：Katsuhiro Morishita(2011.2) Kumamoto-University
     *  
     *  参考文献:ニューメリカル・レシピ・イン・シー
     *           用語はこの書籍に準ずる。
     *  更新履歴：2010/5/23   森下整備開始
     * ******************************************************/
    #region "FFT/IFFT関連クラス"
    /// <summary>
    /// FFTデータ幅
    /// データサイズ(2^n)をこの列挙体を使って宣言すれば間違えない.
    /// </summary>
    public enum FFT_point :int{
        size32 = 32, 
        size64 = 64,
        size128 = 128,
        size256 = 256,
        size512 = 512,
        size1024 = 1024,
        size2048 = 2048,
        size4096 = 4096,
        size8192 = 8192
    }
    /// <summary>
    /// データ格納用構造体
    /// </summary>
    public struct FFTdata
    {
        /************** メンバ変数 *********************/
        /// <summary>実部</summary>
        public double Re;
        /// <summary>虚部</summary>
        public double Im;
        /// <summary>絶対値</summary>
        public double Abs;
        /// <summary>絶対値の対数</summary>
        public double Log;
        /// <summary>パワー ：ところで、パワースペクトル密度 （Power Spectrum Density,PSD）は、Powerを周波数分解能で割れば求まります。</summary>
        public double Power;                            // 最小の分解能の周波数幅におけるパワーの積算値でもある
        /************** メソッド *********************/
        /// <summary>
        /// データをセットする
        /// </summary>
        /// <param name="_Re">実部</param>
        /// <param name="_Im">虚部</param>
        public void Set(double _Re, double _Im)
        {
            this.Re = _Re;
            this.Im = _Im;
            this.Abs = Math.Sqrt(Math.Pow(_Re, 2) + Math.Pow(_Im, 2));
            this.Log = Math.Log10(this.Abs);
            this.Power = Math.Pow(_Re, 2) + Math.Pow(_Im, 2);
        }
    }
    /// <summary>
    /// FFT処理後の観測データを扱うクラス
    /// </summary>
    public class FFTresult
    {
        /**************************** 構造体 *******************************/
        /// <summary>
        /// FFTの処理データと周波数をセットにした構造体
        /// </summary>
        public struct FFTresultPlusFreq
        {
            /// <summary>FFT処理後のデータ</summary>
            public readonly FFTdata data;
            /// <summary>周波数[Hz]</summary>
            public readonly long frequency;
            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="x">FFT処理後のデータ</param>
            /// <param name="freq">周波数[Hz]</param>
            public FFTresultPlusFreq(FFTdata x, long freq)
            {
                this.data = x;
                this.frequency = freq;
            }
        }
        /// <summary>
        /// 帯域を定義する構造体
        /// </summary>
        public struct Band
        {
            /************ メンバ変数 ****************/
            /// <summary>最大周波数[Hz]</summary>
            public readonly long Max;
            /// <summary>最小周波数[Hz]</summary>
            public readonly long Min;
            /************ プロパティ ****************/
            /// <summary>
            /// バンド幅[Hz]
            /// </summary>
            public long BandWidth { get { return this.Max - this.Min; } }
            /// <summary>
            /// 中心周波数[Hz]
            /// </summary>
            public long CenterFrequency { get { return (this.Max - this.Min) / 2 + this.Min; } }
            /************ メソッド ******************/
            /// <summary>
            /// コンストラクタ
            /// </summary>
            /// <param name="max">最大周波数[Hz]</param>
            /// <param name="min">最小周波数[Hz]</param>
            public Band(long max, long min)
            {
                if (max > min && max > 0 && min >= 0)
                {
                    this.Max = max;
                    this.Min = min;
                }
                else { this.Max = 0; this.Min = 0; }
                return;
            }
        }
        /**************************** グローバル変数 *******************************/
        /// <summary>FFT処理データ</summary>
        private FFTdata[] data;
        /// <summary>サンプリング周波数[Hz]</summary>
        private long _fs = 0;
        /// <summary>FFTのサイズ（例：4096 point）</summary>
        private int _fftPoint = 0;
        /**************************** プロパティ *******************************/
        /// <summary>
        /// 観測データ数
        /// </summary>
        public int Length { get { return this.data.Length / 2; } }
        /// <summary>
        /// 表現されている最大の周波数[Hz]
        /// </summary>
        public long MaxFrequency { get { return this._fs; } }
        /// <summary>
        /// 表現されている最小の周波数分解能[Hz]
        /// </summary>
        public long FrequencyResolution { get { return this.GetFreq(1); } }
        /**************************** メソッド *******************************/
        /// <summary>
        /// 結果を文字列で出力する
        /// </summary>
        /// <returns>文字列で表した結果</returns>
        public override string ToString()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder(5000);
            sb.Append("Frequency[Hz]").Append(",").Append("Altitude").Append("\n");
            for(int i = 0; i < this.Length; i++)
                sb.Append(this.GetFreq(i)).Append(",").Append(this.data[i].Log).Append("\n");
            return sb.ToString();
        }
        /// <summary>
        /// 結果をファイルに保存する
        /// </summary>
        /// <param name="fname">ファイル名</param>
        public void Save(string fname = "hoge.csv")
        {
            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(fname, false, System.Text.Encoding.GetEncoding("shift_jis")))
            {
                sw.Write(this.ToString());
            }
            return;
        }
        /// <summary>
        /// 要素番号を入れると、その要素の周波数を計算
        /// </summary>
        /// <param name="index">要素番号</param>
        /// <returns>周波数[Hz]</returns>
        private long GetFreq(int index)
        {
            return (long)index * this._fs / ((long)this._fftPoint - 2);
        }
        /// <summary>
        /// 指定した要素番号のFFTの結果を取得
        /// </summary>
        /// <param name="index">要素番号（0～this.Lenght - 1）</param>
        /// <returns>周波数と結びつけたFFT結果</returns>
        public FFTresultPlusFreq GetData(int index)
        {
            if (index < this.data.Length && index >= 0)                         // 要素番号のチェック
                return new FFTresultPlusFreq(this.data[index], this.GetFreq(index));
            else
                return new FFTresultPlusFreq();
        }
        /// <summary>
        /// 指定周波数がどの要素番号に該当するかを返す
        /// </summary>
        /// <param name="frequency">周波数[Hz]</param>
        /// <returns>要素番号</returns>
        private int GetIndex(long frequency)
        {
            int index = 0;
            if (this._fs != 0 && this._fftPoint != 0)
                index = (int)(frequency * ((long)this._fftPoint - 2) / this._fs);
            return index;
        }
        /// <summary>
        /// 指定帯域におけるパワースペクトル密度 （Power Spectrum Density,PSD）を返す
        /// </summary>
        /// <param name="min_frequency">帯域の下限周波数[Hz]</param>
        /// <param name="max_frequency">帯域の上限周波数[Hz]</param>
        /// <returns>パワースペクトル密度[V^2/Hz]</returns>
        public double GetPSD(long min_frequency, long max_frequency)
        {
            double psd = 0.0;

            if (min_frequency >= 0 && max_frequency >= 0 && min_frequency < max_frequency && this._fs != 0 && this._fftPoint != 0 && max_frequency < (this._fs / 2)) // エラーを防ぐための処置 
            { 
                int max_index = this.GetIndex(max_frequency);           // 最大周波数に対応した要素番号を取得
                int min_index = this.GetIndex(min_frequency);           // 最小周波数に対応した要素番号を取得
                long band_resolution = this.FrequencyResolution;        // 周波数分解能
                for (int i = min_index; i <= max_index; i++) 
                    psd += this.GetData(i).data.Power;                  // 積分
                psd /= ((double)(max_index - min_index + 1) * (double)band_resolution); // 単位周波数あたりに変換
            }
            return psd;
        }
        /// <summary>
        /// 指定帯域におけるパワースペクトル密度 （Power Spectrum Density,PSD）を返す その2
        /// 帯域を指定できる構造体を使用しているので、配列での処理がやりやすくなったと思う。
        /// </summary>
        /// <param name="band">帯域</param>
        /// <returns>パワースペクトル密度[V^2/Hz]</returns>
        public double GetPSD(Band band)
        {
            return this.GetPSD(band.Min, band.Max);
        }
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="setData">観測データ配列</param>
        /// <param name="fs">元データのサンプリング周波数</param>
        /// <param name="FFTpoint">FFTポイント数</param>
        public FFTresult(FFTdata[] setData, long fs, int FFTpoint)
        { 
            this.data = new FFTdata[setData.Length];
            for (int i = 0; i < setData.Length; i++) this.data[i] = setData[i]; // 副作用がないように、一つずつコピー
            this._fs = fs;
            this._fftPoint = FFTpoint;
        }
    }
    /// <summary>
    /// Discrete Fourier Transformクラス     
    /// 
    /// 
    /// 使用する際には、プロジェクトにファイルを追加した後に、ソースヘッダに以下に示すカッコ内のコードを追加して下さい。
    /// 「
    /// using Signal_Process;
    /// 」
    /// </summary>
    public class DFT
    {
        /***********************　定数・列挙体の宣言  *******************************/
        /// <summary>
        /// FFT / IFFT
        /// </summary>
        private enum FFTorIFFT : int { 
            FFTconvert = 1,
            IFFTconvert = -1
        }
        /// <summary>
        /// 窓関数の種類
        /// </summary>
        public enum Window
        {
            Hanning,
            Perzen,
            NoWindow
        }
        /************************　構造体宣言  *******************************/
        
        /***********************　グローバル変数の宣言  *******************************/
        /// <summary>FFTデータ幅</summary>
        private int __FFTpoint;
        /// <summary>使用する窓の種類</summary>
        private Window __WindowKind;
        /// <summary>計算後の配列</summary>
        private FFTdata[] calced_datas;
        /// <summary>外部より渡されたデータを格納する配列</summary>
        private FFTdata[] set_datas;
        /// <summary>データのサンプリング周波数</summary>
        private long frequencyOfSample = 0;
        /****************************　プロパティ　************************************/
        /// <summary>
        /// 窓関数の種別
        /// </summary>
        public Window WindowKind
        {
            get { return this.__WindowKind; }
            set { this.__WindowKind = value; }
        }
        /// <summary>
        /// FFT/IFFTデータ幅
        /// </summary>
        public int FFTsize { get { return this.__FFTpoint; } }
        /// <summary>
        ///  変換後の配列に含まれる最大値を返す
        /// </summary>
        public double Max
        {
            get
            {
                double max = 0;
                int i;

                for (i = 0; i < this.calced_datas.Length; i++) {
                    if (max < this.calced_datas[i].Abs) max = this.calced_datas[i].Abs;
                }
                return max;
            }
        }
        /// <summary>
        /// 変換後の配列に含まれる平均値を返す
        /// </summary>
        public double Mean
        {
            get
            {
                double mean = 0;
                int i;

                for (i = 0; i < this.calced_datas.Length; i++)
                {
                    mean += this.calced_datas[i].Abs;
                }
                return mean / this.calced_datas.Length;
            }
        }

        /****************************　メソッド　************************************/
        /// <summary>
        /// 窓関数を掛ける
        /// </summary>
        /// <param name="data_array">窓関数を掛けたいデータ配列（窓関数を掛けて変形するので注意）</param>
        /// <returns>ウィンドウをかけた後のデータ配列</returns>
        private double[] Windowing(double[] data_array)
        { 
            double wj = 0.0;

            for (int j = 0; j < data_array.Length; j++)
            {
                if (this.__WindowKind == Window.Hanning)                                                // 窓の計数を計算
                    wj = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * (double)j / ((double)data_array.Length - 1)));
                else if (this.__WindowKind == Window.Perzen)
                    wj = 1.0 - Math.Abs(((double)j - 0.5 * ((double)data_array.Length - 1)) / (0.5 * ((double)data_array.Length + 1)));
                data_array[j] *= wj;
            }
            return data_array;
        }
        /// <summary>
        /// データをセットするメソッド。
        /// 指定された窓関数をかけて保持する。
        /// データサイズよりFFTサイズの方が大きい場合は、0で埋める。
        /// 反対に、小さい場合は入るだけしか受け取らない。
        /// </summary>
        private void Dataset(FFTdata[] arr) {
            int i;
            int size;
            double[] temp = new double[arr.Length];

            size = arr.Length;
            if (this.__FFTpoint < arr.Length) size = this.__FFTpoint;   // フィルタにかけるサイズを決定する
            // Reパート
            for (i = 0; i < arr.Length; i++) temp[i] = arr[i].Re;
            temp = this.Windowing(temp);                                // 窓を掛ける
            for (i = 0; i < this.__FFTpoint; i++)                       // 受け渡されたデータをコピー
            {
                if( arr.Length > i )                                    // ある限りコピー
                    this.set_datas[i].Re = temp[i];
                else
                    this.set_datas[i].Re = 0.0;                         // 余ったら0を詰める
            }

            // Imパート
            for (i = 0; i < arr.Length; i++) temp[i] = arr[i].Im;
            temp = this.Windowing(temp);                                // 窓を掛ける
            for (i = 0; i < this.__FFTpoint; i++)                       // 受け渡されたデータをコピー
            {
                if (arr.Length > i)
                    this.set_datas[i].Im = temp[i];                     // ある限りコピー
                else
                    this.set_datas[i].Im = 0.0;                         // 余ったら0を詰める
            }
            return;
        } 
        /// <summary>
        /// データをセットするメソッド2
        /// 指定された窓関数をかけて保持する。
        /// データサイズよりFFTサイズの方が大きい場合は、0で埋める。
        /// </summary>
        private void Dataset(double[] arr) {
            FFTdata[] copy = new FFTdata[arr.Length];

            for (int k = 0; k < arr.Length; k++)
            { 
                copy[k].Re = arr[k];
                copy[k].Im = 0.0;
            }
            this.Dataset(copy);
            return;
        }
        /// <summary>
        /// FFT/IFFTメソッド
        /// </summary>
        /// <param name="data">
        /// 被変換変数。結果が格納される。長さnnの複素配列（実部と虚部を一列に並べたもの）。
        /// 配列の中には、実部・虚部・実部・虚部・実部・虚部・実部・虚部・…と並んでおればよい。
        /// 原本では要素番号1から詰めていることを仮定している。
        /// 実部と虚部が入れ替わっても特に影響は無いはず。
        /// </param>
        /// <param name="nn">data[]に格納されたデータ長を表す。要素数の半分である。2の整数乗である必要がある。</param>
        /// <param name="isign">isign　 1：FFT変換を行う　-1：IFFT変換を行う。結果はnn倍される。</param>
        private void Calc(double[] data, long nn , int isign ){
            long n, mmax, m, j, istep, i;
            double wtemp, wr, wpr, wpi, wi, theta;              // 三角関数の漸化式用
            double tempr, tempi;

            n = nn << 1;                                        // n:データポインタ数の2倍
            j = 1;
            for (i = 1; i < n; i += 2) {                        // ビット反転アルゴリズム
                if (j > i) {                                    // 複素数を交換
                    tempr = data[j]; data[j] = data[i]; data[i] = tempr;                    // 原本では実部を表す
                    tempi = data[j + 1]; data[j + 1] = data[i + 1]; data[i + 1] = tempi;    // 原本では虚部を表す
                }
                m = n >> 1;
                while (m >= 2 && j > m) {
                    j -= m;
                    m >>= 1;
                }
                j += m;
            }
            mmax = 2;                                           // 以下はDanielson-Lanczosアルゴリズムを採用
            while (n > mmax) {                                  // 外側のループはlog2 nn回実行される
                istep = mmax << 1;
                theta = isign * (6.28318530717959 / mmax);      // 三角関数の漸化式の初期値. 2*pi = 6.28…
                wtemp = Math.Sin (0.5 * theta);
                wpr = -2.0 * wtemp * wtemp;
                wpi = Math.Sin(theta);
                wr = 1.0;
                wi = 0.0;
                for (m = 1; m < mmax; m += 2) {
                    for (i = m; i <= n; i += istep) {
                        j = i + mmax;                           // Danielson-Lanczos公式
                        tempr = wr * data[j] - wi * data[j + 1];// 原本通りならreal part
                        tempi = wr * data[j + 1] + wi * data[j];// 原本通りならimaginary part
                        data[j] = data[i] - tempr;
                        data[j + 1] = data[i + 1] - tempi;
                        data[i] += tempr;
                        data[i+1] += tempi;
                    }                                           // 三角関数の漸化式
                    wr = (wtemp = wr) * wpr - wi * wpi + wr;    // ここは処理をひっくり返せばwtempをそのままwrに置き換えられるが、恐らくrealパートから計算したいのだろう。
                    wi = wi * wpr + wtemp * wpi + wi;
                }
                mmax = istep;
            }
            return;
        }
        /**************************************************
         * FFT/IFFT関数
         * 引数：   data[]　被変換変数。結果も格納。長さ2^nの複素配列。
         *          isign　 1：FFT変換を行う　-1：IFFT変換を行う。結果は2^n倍される。
        ****************************************************/
        /// <summary>
        ///     FFT/IFFTメソッド
        /// </summary>
        /*
        public void Calc(FFTdatas[] data, int isign)
        {
            long n, mmax, m, j, istep, i;
            double wtemp, wr, wpr, wpi, wi, theta;              // 三角関数の漸化式用
            double tempr, tempi;

            
            j = 1;
            for (i = 0; i < data.Length; i++)
            {                        // ビット反転アルゴリズム
                if (j > i)
                {                                    // 実部・虚部を交換
                    tempr = data[j].Re; data[j].Re = data[i].Re; data[i].Re = tempr;                    // 原本では実部を表す
                    tempi = data[j].Im; data[j].Im = data[i].Im; data[i].Im = tempi;    // 原本では虚部を表す
                }
                m = data.Length;
                while (m >= 2 && j > m)
                {
                    j -= m;
                    m >>= 1;
                }
                j += m;
            }
            n = data.Length << 1;                                        // n:データポインタ数の2倍
            
            mmax = 2;                                           // 以下はDanielson-Lanczosアルゴリズムを採用
            while (n > mmax)
            {                                  // 外側のループはlog2 nn回実行される
                istep = mmax << 1;
                theta = isign * (6.28318530717959 / mmax);      // 三角関数の漸化式の初期値. 2*pi = 6.28…
                wtemp = Math.Sin(0.5 * theta);
                wpr = -2.0 * wtemp * wtemp;
                wpi = Math.Sin(theta);
                wr = 1.0;
                wi = 0.0;
                for (m = 1; m < mmax; m += 2)
                {
                    for (i = m; i <= n; i += istep)
                    {
                        j = i + mmax;                           // Danielson-Lanczos公式
                        tempr = wr * data[j] - wi * data[j + 1];// 原本通りならreal part
                        tempi = wr * data[j + 1] + wi * data[j];// 原本通りならimaginary part
                        data[j] = data[i] - tempr;
                        data[j + 1] = data[i + 1] - tempi;
                        data[i] += tempr;
                        data[i + 1] += tempi;
                    }                                           // 三角関数の漸化式
                    wr = (wtemp = wr) * wpr - wi * wpi + wr;    // ここは処理をひっくり返せばwtempをそのままwrに置き換えられるが、恐らくrealパートから計算したいのだろう。
                    wi = wi * wpr + wtemp * wpi + wi;
                }
                mmax = istep;
            }
            return;
        }
         * */
        #region "FFTの実行インターフェイス"
        /********************************************
         * 実部のみのデータを解析する
         *  鳥の声を解析するときはこれを呼び出せばよい。
         *  例：
         *      fft a = new fft();                              // このクラスを宣言して、実態も確保
         *      FFTresult result = a.FFT(temp);                 // 実数配列を渡してFFT演算を実行。配列サイズがFFTデータ幅とする必要はない。
         * ******************************************/
        /// <summary>
        /// 受け渡されたデータを使用してFFTを実行する
        /// </summary>
        /// <param name="data">double型の配列に解析したいデータを格納してください</param>
        /// <returns>FFT処理データ</returns>
        public FFTresult FFT(double[] data)
        {
            int i,k;
            double[] data_for_calc = new double[this.__FFTpoint * 2 + 1];

            this.Dataset(data);                                                     // データをセットする
            for (i = 0; i < this.__FFTpoint; i++)
            {
                k = 2 * i + 1;
                data_for_calc[k] = this.set_datas[i].Re;                            // 解析するデータを格納する
                data_for_calc[k + 1] = 0.0;                                         // こっちはimaginaryパート
            }
            this.Calc(data_for_calc, this.__FFTpoint, (int)FFTorIFFT.FFTconvert);   // FFT演算
            for (i = 0; i < this.__FFTpoint; i++)
            {
                k = 2 * i + 1;
                this.calced_datas[i].Set(data_for_calc[k], data_for_calc[k + 1]);   // 結果を格納する
            }
            return new FFTresult(this.calced_datas, this.frequencyOfSample, this.__FFTpoint);
        }
        /// <summary>
        /// FFTを実行する
        /// </summary>
        /// <param name="data">解析したいデータ配列</param>
        /// <param name="frequency">サンプリング周波数[Hz]</param>
        /// <returns>FFT処理データ</returns>
        public FFTresult FFT(double[] data, long frequency)
        {
            this.frequencyOfSample = frequency;
            return this.FFT(data);
        }
        /// <summary>
        /// 実部・虚部を合わせて解析するFFT/IFFTメソッド
        /// </summary>
        /// <param name="data">FFTdatas型の配列に解析したいデータを格納してください</param>
        /// <returns>FFT処理データ</returns>
        public FFTresult FFT(FFTdata[] data) 
        {
            int i, k;
            double[] data_for_calc = new double[this.__FFTpoint * 2 + 1];

            this.Dataset(data);                                                     // データをセットする
            for (i = 0; i < this.__FFTpoint; i++)
            {
                k = 2 * i + 1;
                data_for_calc[k] = this.set_datas[i].Re;                            // 解析するデータを格納する
                data_for_calc[k + 1] = this.set_datas[i].Im;                        // こっちはimaginaryパート
            }
            this.Calc(data_for_calc, this.__FFTpoint, (int)FFTorIFFT.FFTconvert);   // FFT演算
            for (i = 0; i < this.__FFTpoint; i++)
            {
                k = 2 * i + 1;
                this.calced_datas[i].Set(data_for_calc[k], data_for_calc[k + 1]);   // 結果を格納する
            }
            return new FFTresult(this.calced_datas, this.frequencyOfSample, this.__FFTpoint);
        }
        /// <summary>
        /// FFTを実行する
        /// </summary>
        /// <param name="data">解析したいデータ配列</param>
        /// <param name="frequency">サンプリング周波数[Hz]</param>
        /// <returns>FFT処理データ</returns>
        public FFTresult FFT(FFTdata[] data, long frequency)
        {
            this.frequencyOfSample = frequency;
            return this.FFT(data);
        }
        #endregion
        /****************************　初期化/削除　************************************/
        /*****************
         * このクラスのコンストラクタ
         * 
         * 初期化時に使用します。
         * ***************/
        /// <summary>
        /// 　デフォルトでは4096ポイントFFT/IFFT，窓関数の種別としてハミングウィンドウがセットされます。
        /// </summary>
        /// <param name="FFT_point">FFT/IFFTポイント数</param>
        /// <param name="window_kind">窓関数の種類</param>
        public DFT(FFT_point FFT_point = FFT_point.size4096 , Window window_kind = Window.Hanning){
            this.__FFTpoint = (int)FFT_point;
            this.__WindowKind = window_kind;

            this.calced_datas = new FFTdata[this.__FFTpoint];       // サイズに合わせて、インスタンスを生成
            this.set_datas = new FFTdata[this.__FFTpoint];
        }
        /// <summary>
        /// 　デフォルトでは4096ポイントFFT/IFFT，窓関数の種別としてハミングウィンドウがセットされます。
        /// 　指定ポイント数が2^Nではない場合、FFT_point＜2^Nとなる最小の2^Nを採用しますので注意.
        /// </summary>
        /// <param name="FFT_point">FFT/IFFTポイント数</param>
        /// <param name="window_kind">窓関数の種類</param>
        public DFT(int FFT_point = 4096, Window window_kind = Window.Hanning)
        {
            int FFT_point2 = 1;

            while (FFT_point2 < FFT_point) FFT_point2 <<= 1;        // サイズ調整
            this.__FFTpoint = FFT_point2;
            this.__WindowKind = window_kind;
            
            this.calced_datas = new FFTdata[this.__FFTpoint];       // サイズに合わせて、インスタンスを生成
            this.set_datas = new FFTdata[this.__FFTpoint];
        }
    }
    #endregion
}
