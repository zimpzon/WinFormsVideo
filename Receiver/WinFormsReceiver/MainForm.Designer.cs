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
            btnShowLog = new Button();
            tbVideoUri = new TextBox();
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
            btnPlayVideo.Location = new Point(12, 754);
            btnPlayVideo.Name = "btnPlayVideo";
            btnPlayVideo.Size = new Size(64, 64);
            btnPlayVideo.TabIndex = 1;
            btnPlayVideo.Text = "⏵";
            btnPlayVideo.UseCompatibleTextRendering = true;
            btnPlayVideo.UseVisualStyleBackColor = true;
            btnPlayVideo.Click += btnPlayVideo_Click;
            // 
            // btnShowLog
            // 
            btnShowLog.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnShowLog.Location = new Point(1160, 788);
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
            tbVideoUri.Location = new Point(87, 788);
            tbVideoUri.Name = "tbVideoUri";
            tbVideoUri.Size = new Size(310, 29);
            tbVideoUri.TabIndex = 7;
            tbVideoUri.Text = "rtsp://127.0.0.1:8554/live";
            // 
            // VideoPanel
            // 
            VideoPanel.BackColor = Color.Gray;
            VideoPanel.Location = new Point(2, 1);
            VideoPanel.Name = "VideoPanel";
            VideoPanel.Size = new Size(1280, 720);
            VideoPanel.TabIndex = 12;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(10F, 22F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.DarkGray;
            ClientSize = new Size(1284, 830);
            Controls.Add(VideoPanel);
            Controls.Add(tbVideoUri);
            Controls.Add(btnShowLog);
            Controls.Add(btnPlayVideo);
            Font = new Font("Consolas", 9F, FontStyle.Regular, GraphicsUnit.Point, 0);
            FormBorderStyle = FormBorderStyle.Fixed3D;
            MaximizeBox = false;
            MinimumSize = new Size(1000, 359);
            Name = "MainForm";
            Text = "WinFormsVideo";
            FormClosing += MainForm_FormClosing;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button btnPlayVideo;
        private Button btnShowLog;
        private Label labelStatus;
        private TextBox tbVideoUri;
        private CustomControls.VideoPanelControl VideoPanel;
    }
}
