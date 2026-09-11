namespace WinFormsReceiver
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            labelStatus = new Label();
            btnPlayVideo = new Button();
            labelVideoSize = new Label();
            btnShowLog = new Button();
            videoView1 = new LibVLCSharp.WinForms.VideoView();
            tbVideoUri = new TextBox();
            labPlayTime = new Label();
            labelWithTransparentBackground1 = new WinFormsReceiver.CustomControls.LabelWithTransparentBackground();
            gradientTextControl1 = new GradientTextControl();
            ((System.ComponentModel.ISupportInitialize)videoView1).BeginInit();
            SuspendLayout();
            // 
            // labelStatus
            // 
            labelStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            labelStatus.BackColor = Color.Transparent;
            labelStatus.Font = new Font("Cascadia Mono", 10F, FontStyle.Regular, GraphicsUnit.Point, 0);
            labelStatus.ForeColor = Color.Yellow;
            labelStatus.Location = new Point(3, 110);
            labelStatus.Name = "labelStatus";
            labelStatus.Size = new Size(621, 141);
            labelStatus.TabIndex = 0;
            labelStatus.Text = "Press play to start video";
            labelStatus.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // btnPlayVideo
            // 
            btnPlayVideo.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            btnPlayVideo.Font = new Font("Segoe UI Symbol", 20F);
            btnPlayVideo.Location = new Point(17, 442);
            btnPlayVideo.Name = "btnPlayVideo";
            btnPlayVideo.Size = new Size(64, 28);
            btnPlayVideo.TabIndex = 1;
            btnPlayVideo.Text = "⏵";
            btnPlayVideo.UseCompatibleTextRendering = true;
            btnPlayVideo.UseVisualStyleBackColor = true;
            btnPlayVideo.Click += btnPlayVideo_Click;
            // 
            // labelVideoSize
            // 
            labelVideoSize.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            labelVideoSize.AutoSize = true;
            labelVideoSize.Location = new Point(715, 442);
            labelVideoSize.Name = "labelVideoSize";
            labelVideoSize.Size = new Size(210, 22);
            labelVideoSize.TabIndex = 4;
            labelVideoSize.Text = "0 x 0 (native 0 x 0)";
            labelVideoSize.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // btnShowLog
            // 
            btnShowLog.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnShowLog.Location = new Point(1068, 442);
            btnShowLog.Name = "btnShowLog";
            btnShowLog.Size = new Size(112, 30);
            btnShowLog.TabIndex = 5;
            btnShowLog.Text = "Log...";
            btnShowLog.UseVisualStyleBackColor = true;
            btnShowLog.Click += btnShowLog_Click;
            // 
            // videoView1
            // 
            videoView1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            videoView1.BackColor = Color.Black;
            videoView1.Location = new Point(17, 11);
            videoView1.MediaPlayer = null;
            videoView1.Name = "videoView1";
            videoView1.Size = new Size(842, 363);
            videoView1.TabIndex = 6;
            videoView1.Text = "videoView1";
            videoView1.Move += videoView1_Move;
            videoView1.Resize += videoView1_Resize;
            // 
            // tbVideoUri
            // 
            tbVideoUri.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            tbVideoUri.Location = new Point(99, 442);
            tbVideoUri.Name = "tbVideoUri";
            tbVideoUri.Size = new Size(310, 29);
            tbVideoUri.TabIndex = 7;
            tbVideoUri.Text = "rtsp://127.0.0.1:8554/live";
            // 
            // labPlayTime
            // 
            labPlayTime.AutoSize = true;
            labPlayTime.BackColor = Color.Transparent;
            labPlayTime.Location = new Point(374, 442);
            labPlayTime.Name = "labPlayTime";
            labPlayTime.Size = new Size(70, 22);
            labPlayTime.TabIndex = 8;
            labPlayTime.Text = "label1";
            // 
            // labelWithTransparentBackground1
            // 
            labelWithTransparentBackground1.Location = new Point(215, 430);
            labelWithTransparentBackground1.Name = "labelWithTransparentBackground1";
            labelWithTransparentBackground1.Size = new Size(112, 34);
            labelWithTransparentBackground1.TabIndex = 9;
            labelWithTransparentBackground1.Text = "labelWithTransparentBackground1";
            // 
            // gradientTextControl1
            // 
            gradientTextControl1.BoxAlpha = 25;
            gradientTextControl1.Location = new Point(879, 61);
            gradientTextControl1.Name = "gradientTextControl1";
            gradientTextControl1.Size = new Size(600, 375);
            gradientTextControl1.TabIndex = 11;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(10F, 22F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.DarkGray;
            ClientSize = new Size(1192, 484);
            Controls.Add(gradientTextControl1);
            Controls.Add(labelWithTransparentBackground1);
            Controls.Add(labPlayTime);
            Controls.Add(tbVideoUri);
            Controls.Add(videoView1);
            Controls.Add(btnShowLog);
            Controls.Add(labelVideoSize);
            Controls.Add(btnPlayVideo);
            Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            MinimumSize = new Size(1000, 359);
            Name = "MainForm";
            Text = "Form1";
            FormClosing += MainForm_FormClosing;
            Move += MainForm_Move;
            ((System.ComponentModel.ISupportInitialize)videoView1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnPlayVideo;
        private Label labelVideoSize;
        private Button btnShowLog;
        private Label labelStatus;
        private LibVLCSharp.WinForms.VideoView videoView1;
        private TextBox tbVideoUri;
        private Label labPlayTime;
        private CustomControls.LabelWithTransparentBackground labelWithTransparentBackground1;
        private GradientTextControl gradientTextControl1;
    }
}
