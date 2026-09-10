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
            btnPlayVideo.Location = new Point(17, 502);
            btnPlayVideo.Name = "btnPlayVideo";
            btnPlayVideo.Size = new Size(64, 32);
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
            labelVideoSize.Location = new Point(397, 509);
            labelVideoSize.Name = "labelVideoSize";
            labelVideoSize.Size = new Size(155, 25);
            labelVideoSize.TabIndex = 4;
            labelVideoSize.Text = "0 x 0 (native 0 x 0)";
            labelVideoSize.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // btnShowLog
            // 
            btnShowLog.Location = new Point(1001, 461);
            btnShowLog.Name = "btnShowLog";
            btnShowLog.Size = new Size(112, 34);
            btnShowLog.TabIndex = 5;
            btnShowLog.Text = "Log...";
            btnShowLog.UseVisualStyleBackColor = true;
            btnShowLog.Click += btnShowLog_Click;
            // 
            // videoView1
            // 
            videoView1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            videoView1.BackColor = Color.Black;
            videoView1.Location = new Point(17, 12);
            videoView1.MediaPlayer = null;
            videoView1.Name = "videoView1";
            videoView1.Size = new Size(842, 413);
            videoView1.TabIndex = 6;
            videoView1.Text = "videoView1";
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.DarkGray;
            ClientSize = new Size(1192, 550);
            Controls.Add(videoView1);
            Controls.Add(btnShowLog);
            Controls.Add(labelVideoSize);
            Controls.Add(btnPlayVideo);
            MinimumSize = new Size(1000, 400);
            Name = "MainForm";
            Text = "Form1";
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
    }
}
