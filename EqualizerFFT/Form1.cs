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

namespace EqualizerFFT
{
    public partial class Form1: Form
    {
        //FFT結果を保持するメンバ変数
        private double[] frequencies = null;
        private double[] magnitudes = null;
        private int N = 1024;

        private int sampleRate = 44100;
        private int maxMagnitudeIndex = -1;
        public Form1()
        {
            InitializeComponent();
            // spectrumPanel の Paint イベントハンドラを登録
            this.spectrumPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.spectrumPanel_Paint);
            // DoubleBuffered を有効にして描画のちらつきを抑える
            // this.spectrumPanel.DoubleBuffered = true;
         
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
        private void spectrumPanel_Paint(object sender, PaintEventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            double signalFrequency = 440;

            Complex[] samples = new Complex[N];
            for(int i = 0; i< N; i++)
            {
                double time = (double)i / sampleRate;
                double realPart = Math.Sin(2 * Math.PI * signalFrequency * time);
                samples[i] = new Complex(realPart, 0);
            }

            Fourier.Forward(samples, FourierOptions.NoScaling);

            textBox1.Clear();
            textBox1.AppendText("周波数(Hz)\t振幅" + Environment.NewLine);
            textBox1.AppendText("--------------------" + Environment.NewLine);

            int outputLength = N / 2 + 1;
            this.magnitudes = new double[outputLength];
            this.frequencies = new double[outputLength];

            double maxMagnitude = 0;

            for (int i = 0; i < outputLength; i++)
            {
                frequencies[i] = (double)i * sampleRate / N;
                double magnitude = samples[i].Magnitude;
                if( i == 0 || i == N/2)
                {
                    magnitudes[i] = magnitude / N;
                }
                else
                {
                    magnitudes[i] = 2.0 * magnitude / N;
                }
                if (this.magnitudes[i] > maxMagnitude)
                {
                    maxMagnitude = this.magnitudes[i];
                    this.maxMagnitudeIndex = i;
                }
                
            }
            textBox1.Clear();
            textBox1.AppendText($"Max Magnitude: {maxMagnitude:F4} {Environment.NewLine}");
            textBox1.AppendText("Freq (Hz)\tMagnitude" + Environment.NewLine);
            textBox1.AppendText("--------------------------" + Environment.NewLine);
            for (int i = 0; i < outputLength; i++)
            {
                textBox1.AppendText($"{this.frequencies[i]:F2}\t{this.magnitudes[i]:F4}" + Environment.NewLine);
            }

            int targetIndex = (int)Math.Round(signalFrequency * N / sampleRate);
            if(targetIndex >= 0 && targetIndex < outputLength)
            {
                textBox1.AppendText($"入力信号周波数 {signalFrequency} Hz に最も近い周波数成分 ({frequencies[targetIndex]:F2} Hz) の振幅: {magnitudes[targetIndex]:F4}{Environment.NewLine}");
            }
            this.spectrumPanel.Invalidate();
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;

            g.Clear(spectrumPanel.BackColor);

            //FFT 結果がまだ計算されていない場合は何もしない
            if(magnitudes == null || frequencies == null || magnitudes.Length == 0)
            {
                return;
            }

            int panelWidth = spectrumPanel.ClientSize.Width;
            int panelHeight = spectrumPanel.ClientSize.Height;

            using (Pen whitePen = new Pen(Color.White))
            using (Brush whiteBrush = new SolidBrush(Color.White))
            {
                //各周波数成分に対して棒を描画
                int numBars = magnitudes.Length;
                float barWidth = (float)panelWidth / numBars;
                if (barWidth < 1.0f) barWidth = 1.0f;

                //振幅の最大値を決定

                double maxMagnitudeForScaling = magnitudes.Max();
                if (maxMagnitudeForScaling <= 0) maxMagnitudeForScaling = 1.0;

                for (int i = 0; i < numBars; i++)
                {
                    float barHeight = (float)(magnitudes[i] / maxMagnitudeForScaling * panelHeight);

                    float x = (float)i / numBars * panelWidth;

                    float y = panelHeight - barHeight - 20;

                    g.FillRectangle(whiteBrush, x, y, barWidth, barHeight);
                }

                if (this.maxMagnitudeIndex != -1 && this.frequencies != null && this.maxMagnitudeIndex < this.frequencies.Length)
                {
                    // ピーク周波数を取得
                    float peakFrequency = (float)this.frequencies[this.maxMagnitudeIndex];
                    string frequencyLabel = $"{peakFrequency:F1} Hz"; // 表示する文字列 (小数点以下1桁)

                    // ラベルを描画するX座標を計算 (ピークの棒の中心あたり)
                    float peakBarX = (float)this.maxMagnitudeIndex / numBars * panelWidth;
                    float labelX = peakBarX + barWidth / 2; // 棒の中心を狙う

                    // ラベルのサイズを測定
                    Font drawFont = this.Font; // フォームのデフォルトフォントを使用
                    SizeF labelSize = g.MeasureString(frequencyLabel, drawFont);

                    // X座標の調整 (ラベルがパネル右端からはみ出ないように)
                    if (labelX + labelSize.Width / 2 > panelWidth) // ラベルの中心が右端を超えそうな場合
                    {
                        labelX = panelWidth - labelSize.Width; // 右端に寄せる
                    }
                    else if (labelX - labelSize.Width / 2 < 0) // ラベルの中心が左端を超えそうな場合
                    {
                        labelX = 0; // 左端に寄せる
                    }
                    else // それ以外は中央揃え
                    {
                        labelX -= labelSize.Width / 2;
                    }


                    // ラベルを描画するY座標 (パネルの下部に表示)
                    float labelY = panelHeight - labelSize.Height - 2; // 下端から少し上に配置

                    // ラベルを描画 (目立つように黄色で)
                    using (Brush textBrush = new SolidBrush(Color.Yellow))
                    {
                        g.DrawString(frequencyLabel, drawFont, textBrush, labelX, labelY);
                    }
                }
            }
        }
    }
}
