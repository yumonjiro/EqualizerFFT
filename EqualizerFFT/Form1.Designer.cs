namespace EqualizerFFT
{
    partial class Form1
    {
        /// <summary>
        /// 必要なデザイナー変数です。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 使用中のリソースをすべてクリーンアップします。
        /// </summary>
        /// <param name="disposing">マネージド リソースを破棄する場合は true を指定し、その他の場合は false を指定します。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows フォーム デザイナーで生成されたコード

        /// <summary>
        /// デザイナー サポートに必要なメソッドです。このメソッドの内容を
        /// コード エディターで変更しないでください。
        /// </summary>
        private void InitializeComponent()
        {
            this.button1 = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.spectrumPanel = new System.Windows.Forms.Panel();
            this.bassBoostTrackBar = new System.Windows.Forms.TrackBar();
            this.bassLabel = new System.Windows.Forms.Label();
            this.stopButton = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.bassBoostTrackBar)).BeginInit();
            this.SuspendLayout();
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(117, 73);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 0;
            this.button1.Text = "FFT実行";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.StartButton_Click);
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(287, 62);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.textBox1.Size = new System.Drawing.Size(389, 156);
            this.textBox1.TabIndex = 1;
            // 
            // spectrumPanel
            // 
            this.spectrumPanel.BackColor = System.Drawing.Color.Black;
            this.spectrumPanel.Location = new System.Drawing.Point(76, 238);
            this.spectrumPanel.Name = "spectrumPanel";
            this.spectrumPanel.Size = new System.Drawing.Size(600, 200);
            this.spectrumPanel.TabIndex = 2;
            this.spectrumPanel.Paint += new System.Windows.Forms.PaintEventHandler(this.spectrumPanel_Paint);
            // 
            // bassBoostTrackBar
            // 
            this.bassBoostTrackBar.Location = new System.Drawing.Point(117, 173);
            this.bassBoostTrackBar.Minimum = -10;
            this.bassBoostTrackBar.Name = "bassBoostTrackBar";
            this.bassBoostTrackBar.Size = new System.Drawing.Size(104, 45);
            this.bassBoostTrackBar.TabIndex = 3;
            this.bassBoostTrackBar.Scroll += new System.EventHandler(this.bassBoostTrackBar_Scroll);
            // 
            // bassLabel
            // 
            this.bassLabel.AutoSize = true;
            this.bassLabel.Location = new System.Drawing.Point(135, 158);
            this.bassLabel.Name = "bassLabel";
            this.bassLabel.Size = new System.Drawing.Size(61, 12);
            this.bassLabel.TabIndex = 4;
            this.bassLabel.Text = "bass boost";
            // 
            // stopButton
            // 
            this.stopButton.Location = new System.Drawing.Point(117, 116);
            this.stopButton.Name = "stopButton";
            this.stopButton.Size = new System.Drawing.Size(75, 23);
            this.stopButton.TabIndex = 5;
            this.stopButton.Text = "button2";
            this.stopButton.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.stopButton);
            this.Controls.Add(this.bassLabel);
            this.Controls.Add(this.bassBoostTrackBar);
            this.Controls.Add(this.spectrumPanel);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.button1);
            this.Name = "Form1";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.bassBoostTrackBar)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

    }
}

