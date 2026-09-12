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
            tbVideoUri = new TextBox();
            labPlayTime = new Label();
            labelWithTransparentBackground1 = new WinFormsReceiver.CustomControls.LabelWithTransparentBackground();
            VideoPanel = new WinFormsReceiver.CustomControls.VideoPanelControl();
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
            // VideoPanel
            // 
            VideoPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            VideoPanel.BorderStyle = BorderStyle.FixedSingle;
            VideoPanel.Location = new Point(17, 12);
            VideoPanel.Name = "VideoPanel";
            VideoPanel.Size = new Size(501, 285);
            VideoPanel.TabIndex = 10;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(10F, 22F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.DarkGray;
            ClientSize = new Size(1192, 484);
            Controls.Add(VideoPanel);
            Controls.Add(labelWithTransparentBackground1);
            Controls.Add(labPlayTime);
            Controls.Add(tbVideoUri);
            Controls.Add(btnShowLog);
            Controls.Add(labelVideoSize);
            Controls.Add(btnPlayVideo);
            Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            MinimumSize = new Size(1000, 359);
            Name = "MainForm";
            Text = "Form1";
            FormClosing += MainForm_FormClosing;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnPlayVideo;
        private Label labelVideoSize;
        private Button btnShowLog;
        private Label labelStatus;
        private TextBox tbVideoUri;
        private Label labPlayTime;
        private CustomControls.LabelWithTransparentBackground labelWithTransparentBackground1;
        private CustomControls.VideoPanelControl VideoPanel;
    }
}
