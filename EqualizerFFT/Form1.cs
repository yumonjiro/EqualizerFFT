using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Numerics;
using MathNet.Numerics.IntegralTransforms;
using MathNet.Numerics;

using NAudio.Wave;
using System.Diagnostics;

namespace EqualizerFFT
{
    public partial class Form1: Form
    {
        // --- NAudio関連 ---
        private IWaveIn waveIn; // マイク入力インターフェース (IWaveInにするとWasapiなどにも対応しやすい)
        private BufferedWaveProvider bufferedWaveProvider; // リングバッファ的にデータを溜める
        private const int RATE = 44100; // サンプリングレート (Hz)
        private const int BUFFER_MILLISECONDS = 50; // マイクからのバッファリング時間(ms)

        // --- FFT関連 ---
        private const int FFT_LENGTH = 1024 * 2; // FFTの点数 (2のべき乗、増やせば周波数分解能↑、処理負荷↑)
        private Complex[] fftBuffer = new Complex[FFT_LENGTH]; // FFT計算用バッファ
        private double[] window = null; // 窓関数データ
        private List<float> sampleBuffer = new List<float>(); // マイクからのサンプルを一時的に溜めるバッファ

        // --- 表示関連 ---
        private double[] frequencies = null;
        private double[] magnitudes = null;
        private int maxMagnitudeIndex = -1;
        private bool isProcessing = false; // 処理中フラグ
        private System.Threading.Timer processingTimer; // 定期的に処理を実行するタイマー

        //UI
        private TrackBar bassBoostTrackBar;
        private Label bassLabel;
        private Button button1;
        private Button stopButton;
        private TextBox textBox1;
        private Panel spectrumPanel;

        public Form1()
        {
            InitializeComponent();
            // --- デザイナで配置したコントロールへの参照を取得 ---
            // (デザイナで付けた名前に合わせてください)
            this.bassBoostTrackBar = Controls.OfType<TrackBar>().FirstOrDefault(tb => tb.Name == "bassBoostTrackBar");
            this.bassLabel = Controls.OfType<Label>().FirstOrDefault(lbl => lbl.Name == "label1"); // 例: デザイナで label1 になっている場合
            this.button1 = Controls.OfType<Button>().FirstOrDefault(btn => btn.Name == "button1");
            this.stopButton = Controls.OfType<Button>().FirstOrDefault(btn => btn.Name == "stopButton");
            this.textBox1 = Controls.OfType<TextBox>().FirstOrDefault(txt => txt.Name == "textBox1");
            this.spectrumPanel = Controls.OfType<Panel>().FirstOrDefault(pnl => pnl.Name == "spectrumPanel");
            // spectrumPanel の Paint イベントハンドラを登録

            window = Window.Hann(FFT_LENGTH);

            this.spectrumPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.spectrumPanel_Paint);
            this.button1.Click += new System.EventHandler(this.StartButton_Click);
            this.stopButton.Click += StopButton_Click; // 停止ボタン
            this.FormClosing += Form1_FormClosing; // フォームを閉じるときの処理
            if (this.bassBoostTrackBar != null)
            {
                this.bassBoostTrackBar.Scroll += new
                    System.EventHandler(this.bassBoostTrackBar_Scroll);
                    UpdateBassLabel();
                
            }
            // DoubleBuffered を有効にして描画のちらつきを抑える
            // this.spectrumPanel.DoubleBuffered = true;
         
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            if (isProcessing) return; // 既に開始している場合は何もしない

            try
            {
                // --- マイク入力の初期化 ---
                // WaveInEvent: WinMM API を使用。多くの環境で動作。
                waveIn = new WaveInEvent
                {
                    DeviceNumber = 0, // 0番目のマイクデバイスを使用 (通常はデフォルト)
                    WaveFormat = new WaveFormat(RATE, 1), // モノラル, 指定したサンプルレート
                    BufferMilliseconds = BUFFER_MILLISECONDS
                };

                // データが利用可能になったときのイベントハンドラ
                waveIn.DataAvailable += WaveIn_DataAvailable;

                // データをリングバッファ的に蓄積するプロバイダ
                // (今回は直接Listに入れるので必須ではないが、再生等で便利)
                bufferedWaveProvider = new BufferedWaveProvider(waveIn.WaveFormat);
                bufferedWaveProvider.BufferDuration = TimeSpan.FromSeconds(2); // 2秒分のバッファ

                // マイク入力開始
                waveIn.StartRecording();
                Debug.WriteLine("Mic started.");

                // --- 定期処理タイマーの開始 ---
                // 指定時間ごと (例: 20ms) に ProcessBuffer を呼び出す
                int timerIntervalMs = 20;
                processingTimer = new System.Threading.Timer(ProcessBufferCallback, null, timerIntervalMs, timerIntervalMs);

                isProcessing = true;
                button1.Enabled = false; // 開始ボタンを無効化
                stopButton.Enabled = true; // 停止ボタンを有効化
            }
            catch (Exception ex)
            {
                MessageBox.Show($"マイクの開始に失敗しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                CleanupResources(); // リソース解放
            }
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            if (!isProcessing) return;

            CleanupResources();

            isProcessing = false;
            button1.Enabled = true; // 開始ボタンを有効化
            stopButton.Enabled = false; // 停止ボタンを無効化
            Debug.WriteLine("Mic stopped.");
        }

        // フォームが閉じられるときにリソースを解放
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            CleanupResources();
        }

        // マイクなどのリソースを解放するメソッド
        private void CleanupResources()
        {
            // タイマー停止
            processingTimer?.Dispose();
            processingTimer = null;

            // マイク入力停止・破棄
            waveIn?.StopRecording();
            if (waveIn != null)
            {
                waveIn.DataAvailable -= WaveIn_DataAvailable; // イベントハンドラ解除
                waveIn.Dispose();
                waveIn = null;
            }
            bufferedWaveProvider = null; // 必要に応じて破棄
            sampleBuffer.Clear(); // バッファクリア
        }


        // マイクからデータが届いたときの処理
        private void WaveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            // バイト配列データを float (サンプル値 -1.0 ～ 1.0) に変換
            for (int i = 0; i < e.BytesRecorded; i += 2) // 16bit モノラルを想定
            {
                short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i]);
                sampleBuffer.Add(sample / 32768f); // short の最大値で割って正規化
            }

            // (オプション) BufferedWaveProviderにも追加（再生などに使う場合）
            // bufferedWaveProvider?.AddSamples(e.Buffer, 0, e.BytesRecorded);
        }


        // 定期的に呼び出されるデータ処理メソッド (タイマーコールバック)
        private void ProcessBufferCallback(object state)
        {
            // バッファに十分なデータが溜まっているか確認
            if (sampleBuffer.Count < FFT_LENGTH)
            {
                return; // データ不足
            }

            // --- FFT処理 ---
            // 最新の FFT_LENGTH 個のサンプルを取得
            float[] currentSamples = new float[FFT_LENGTH];
            lock (sampleBuffer) // Listへのアクセス中に変更されないようにロック
            {
                // 後ろから FFT_LENGTH 個を取得
                int start = sampleBuffer.Count - FFT_LENGTH;
                for (int i = 0; i < FFT_LENGTH; i++)
                {
                    currentSamples[i] = sampleBuffer[start + i];
                }
                // 古いデータを削除 (例: 半分だけ残すなど、オーバーラップを考慮する場合調整)
                // ここでは単純に処理した分より前のデータを削除
                int removeCount = sampleBuffer.Count - FFT_LENGTH / 2; // 半分オーバーラップさせる例
                if (removeCount > 0) sampleBuffer.RemoveRange(0, removeCount);
                // sampleBuffer.RemoveRange(0, FFT_LENGTH); // オーバーラップなしの場合

            }


            // 1. 窓関数を適用し、Complexバッファに入れる
            for (int i = 0; i < FFT_LENGTH; i++)
            {
                fftBuffer[i] = new Complex(currentSamples[i] * window[i], 0);
            }

            // 2. FFT実行
            Fourier.Forward(fftBuffer, FourierOptions.NoScaling);

            // 3. イコライジング処理
            ApplyEqualization(fftBuffer); // イコライジング処理を別メソッドに

            // 4. 振幅スペクトル計算
            CalculateMagnitudeSpectrum(fftBuffer); // 振幅計算を別メソッドに

            // --- UI更新 ---
            // ワーカースレッドからUIスレッドのコントロールを操作するため Invoke/BeginInvoke が必要
            try
            {
                if (!spectrumPanel.IsDisposed && spectrumPanel.IsHandleCreated)
                {
                    spectrumPanel.BeginInvoke((Action)(() =>
                    {
                        if (!spectrumPanel.IsDisposed) // BeginInvoke が実行されるまでにDisposeされる可能性
                        {
                            spectrumPanel.Invalidate(); // 再描画要求
                                                        // (任意)テキストボックスへの情報表示など
                                                        // UpdateTextBoxInfo();
                        }
                    }));
                }
                if (!textBox1.IsDisposed && textBox1.IsHandleCreated)
                {
                    textBox1.BeginInvoke((Action)(() =>
                    {
                        if (!textBox1.IsDisposed) UpdateTextBoxInfo();
                    }));
                }

            }
            catch (ObjectDisposedException)
            {
                // フォームが閉じられた後などに発生する可能性があるので無視
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UI Update Error: {ex.Message}");
            }
        }

        // イコライジング処理 (周波数領域データに適用)
        private void ApplyEqualization(Complex[] freqDomainData)
        {
            double boostDb = 0;
            // UIスレッドではないので、TrackBarの値は安全に取得できない場合がある
            // -> 回避策１：値をメンバー変数にコピーしておく
            // -> 回避策２：Invoke/DelegateでUIスレッドから値を取得 (やや煩雑)
            // ここでは単純化のため、最後にUIから設定された値を使う想定（より正確には工夫が必要）
            this.Invoke((Action)(() => // UIスレッドで値を取得
            {
                if (bassBoostTrackBar != null) boostDb = bassBoostTrackBar.Value;
            }));


            double gain = Math.Pow(10, boostDb / 20.0);
            double bassFrequencyCutoff = 200.0; // Hz

            int bassIndexCutoff = (int)Math.Round(bassFrequencyCutoff * FFT_LENGTH / RATE);
            if (bassIndexCutoff > FFT_LENGTH / 2) bassIndexCutoff = FFT_LENGTH / 2;

            for (int k = 0; k <= bassIndexCutoff; k++)
            {
                // ゲインを適用
                freqDomainData[k] *= gain;
                // 対称性のため N-k も処理 (実数ゲインなら不要だが念のため)
                if (k > 0 && k < FFT_LENGTH / 2)
                {
                    freqDomainData[FFT_LENGTH - k] *= gain;
                }
            }
            // ★★★ 本来はここで IFFT を実行して加工後の音を作る ★★★
            // Fourier.Inverse(freqDomainData, FourierOptions.Default);
            // freqDomainData の実部が加工後の時間領域信号になる
        }

        // 振幅スペクトルを計算し、メンバー変数に格納
        private void CalculateMagnitudeSpectrum(Complex[] freqDomainData)
        {
            int outputLength = FFT_LENGTH / 2 + 1;

            // 配列が未作成かサイズが違う場合のみ再作成 (効率化)
            if (this.magnitudes == null || this.magnitudes.Length != outputLength)
            {
                this.magnitudes = new double[outputLength];
            }
            if (this.frequencies == null || this.frequencies.Length != outputLength)
            {
                this.frequencies = new double[outputLength];
                for (int i = 0; i < outputLength; i++)
                {
                    this.frequencies[i] = (double)i * RATE / FFT_LENGTH;
                }
            }


            double maxMag = 0;
            int maxIndex = -1;

            for (int i = 0; i < outputLength; i++)
            {
                double mag = freqDomainData[i].Magnitude;
                double scaledMag = 0;
                // スケーリング (Forward=NoScaling, 表示用)
                if (i == 0 || i == FFT_LENGTH / 2)
                    scaledMag = mag / FFT_LENGTH;
                else
                    scaledMag = 2.0 * mag / FFT_LENGTH;

                // 画面表示用にdBスケールなどに変換すると見やすいかも
                 double dB = 20 * Math.Log10(scaledMag);
                 this.magnitudes[i] = dB; // 例

                // 線形スケールのまま表示
                this.magnitudes[i] = scaledMag;


                if (scaledMag > maxMag)
                {
                    maxMag = scaledMag;
                    maxIndex = i;
                }
            }
            this.maxMagnitudeIndex = maxIndex;
        }

        // TrackBar の値が変更された時の処理
        private void bassBoostTrackBar_Scroll(object sender, EventArgs e)
        {
            UpdateBassLabel();
            // リアルタイム処理中は、TrackBarの値を変えるだけで、
            // 次回の ProcessBufferCallback で新しい値が使われる
        }

        // TrackBar の値に応じてラベルを更新
        private void UpdateBassLabel()
        {
            if (bassLabel != null && bassBoostTrackBar != null)
            {
                // 安全のため Invoke を使う
                if (bassLabel.InvokeRequired)
                {
                    bassLabel.BeginInvoke((Action)(() => bassLabel.Text = $"Bass Boost: {bassBoostTrackBar.Value} dB"));
                }
                else
                {
                    bassLabel.Text = $"Bass Boost: {bassBoostTrackBar.Value} dB";
                }
            }
        }

        // テキストボックスに情報を表示 (デバッグ用)
        private void UpdateTextBoxInfo()
        {
            if (magnitudes == null || frequencies == null) return;

            string info = $"FFT Length: {FFT_LENGTH}\n";
            info += $"Sample Buffer Size: {sampleBuffer.Count}\n";
            if (maxMagnitudeIndex != -1 && maxMagnitudeIndex < frequencies.Length)
            {
                info += $"Peak Freq: {frequencies[maxMagnitudeIndex]:F1} Hz\n";
                info += $"Peak Mag: {magnitudes[maxMagnitudeIndex]:F4}\n";
            }
            if (bassBoostTrackBar != null)
            {
                info += $"Bass Boost Setting: {bassBoostTrackBar.Value} dB\n";
            }

            // スレッドセーフにテキストボックスを更新
            if (textBox1.InvokeRequired)
            {
                textBox1.BeginInvoke((Action)(() => {
                    textBox1.Text = info;
                    // スクロールを一番下に移動（新しい情報が見えるように）
                    textBox1.SelectionStart = textBox1.TextLength;
                    textBox1.ScrollToCaret();
                }));
            }
            else
            {
                textBox1.Text = info;
                textBox1.SelectionStart = textBox1.TextLength;
                textBox1.ScrollToCaret();
            }
        }


        // spectrumPanel の Paint イベントハンドラ (変更点は dBスケール対応など考慮)
        private void spectrumPanel_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(spectrumPanel.BackColor);

            if (magnitudes == null || frequencies == null || magnitudes.Length == 0 || !isProcessing)
            {
                // 処理中でない場合やデータがない場合は中央にメッセージ表示などしても良い
                using (Font font = new Font("Arial", 12))
                using (StringFormat sf = new StringFormat())
                using (Brush textBrush = new SolidBrush(Color.Gray))
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString("停止中", font, textBrush, spectrumPanel.ClientRectangle, sf);
                }
                return;
            }

            int panelWidth = spectrumPanel.ClientSize.Width;
            int panelHeight = spectrumPanel.ClientSize.Height;

            using (Pen whitePen = new Pen(Color.White))
            using (Brush whiteBrush = new SolidBrush(Color.White))
            {
                int numBars = magnitudes.Length;
                float barWidth = (float)panelWidth / numBars;
                if (barWidth < 1.0f) barWidth = 1.0f;

                // スケーリング: dBスケールを使う方が見やすいことが多い
                // 例えば -60dB から 0dB の範囲をパネルの高さにマッピングする
                double minDb = -60.0;
                double maxDb = 0.0;
                double dbRange = maxDb - minDb;

                // 線形スケールの場合の最大値（参考）
                double maxLinearMag = magnitudes.Max();
                if (maxLinearMag <= 0) maxLinearMag = 1e-6; // 0除算、Log(0)防止

                for (int i = 0; i < numBars; i++)
                {
                    // --- 線形スケールの場合 ---
                    float linearHeight = (float)(magnitudes[i] / maxLinearMag * panelHeight);
                    if (linearHeight < 0) linearHeight = 0;
                    if (linearHeight > panelHeight) linearHeight = panelHeight; // 念のためクリップ

                    // --- dBスケールの場合 ---
                    // double mag = magnitudes[i] > 1e-9 ? magnitudes[i] : 1e-9; // 非常に小さい値はクリップ
                    // double db = 20 * Math.Log10(mag);
                    // float dbHeight = (float)((db - minDb) / dbRange * panelHeight);
                    // if (dbHeight < 0) dbHeight = 0;
                    // if (dbHeight > panelHeight) dbHeight = panelHeight;

                    // 描画に使う高さを選択 (ここでは線形)
                    float barHeight = linearHeight;
                    // float barHeight = dbHeight; // dBスケールを使う場合

                    float x = (float)i / numBars * panelWidth; // 線形周波数スケール
                    // 対数周波数スケールにする場合は x の計算を変える
                    // float logFreq = (float)Math.Log10(frequencies[i] > 0 ? frequencies[i] : 1.0);
                    // float minLogFreq = (float)Math.Log10(frequencies[1] > 0 ? frequencies[1] : 1.0); // 0Hz除く最低周波数
                    // float maxLogFreq = (float)Math.Log10(frequencies[numBars - 1]);
                    // x = (logFreq - minLogFreq) / (maxLogFreq - minLogFreq) * panelWidth;

                    float y = panelHeight - barHeight;

                    g.FillRectangle(whiteBrush, x, y, barWidth, barHeight);
                }

                // ピーク周波数表示 (Paint処理がUIスレッドなのでInvoke不要)
                if (this.maxMagnitudeIndex != -1 && this.frequencies != null && this.maxMagnitudeIndex < this.frequencies.Length)
                {
                    float peakFrequency = (float)this.frequencies[this.maxMagnitudeIndex];
                    string frequencyLabel = $"{peakFrequency:F1} Hz";
                    Font drawFont = this.Font;
                    SizeF labelSize = g.MeasureString(frequencyLabel, drawFont);
                    float peakBarX = (float)this.maxMagnitudeIndex / numBars * panelWidth; // 線形スケールの場合
                    // 対数スケールの場合は peakBarX の計算も変える
                    float labelX = peakBarX + barWidth / 2;

                    if (labelX + labelSize.Width / 2 > panelWidth) labelX = panelWidth - labelSize.Width;
                    else if (labelX - labelSize.Width / 2 < 0) labelX = 0;
                    else labelX -= labelSize.Width / 2;

                    float labelY = panelHeight - labelSize.Height - 2;

                    using (Brush textBrush = new SolidBrush(Color.Yellow))
                    {
                        g.DrawString(frequencyLabel, drawFont, textBrush, labelX, labelY);
                    }
                }
            }
            }
        }
    }
