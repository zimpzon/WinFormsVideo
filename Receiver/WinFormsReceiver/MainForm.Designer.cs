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
            videoPanel = new WinFormsReceiver.Components.VideoPanel();
            labelStatus = new Label();
            btnPlayVideo = new Button();
            labelVideoSize = new Label();
            btnShowLog = new Button();
            videoPanel.SuspendLayout();
            SuspendLayout();
            // 
            // videoPanel
            // 
            videoPanel.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            videoPanel.BorderStyle = BorderStyle.Fixed3D;
            videoPanel.Controls.Add(labelStatus);
            videoPanel.Location = new Point(12, 12);
            videoPanel.Name = "videoPanel";
            videoPanel.Size = new Size(636, 480);
            videoPanel.TabIndex = 0;
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
            btnPlayVideo.Location = new Point(17, 496);
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
            labelVideoSize.Location = new Point(397, 503);
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
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.DarkGray;
            ClientSize = new Size(1231, 544);
            Controls.Add(btnShowLog);
            Controls.Add(labelVideoSize);
            Controls.Add(btnPlayVideo);
            Controls.Add(videoPanel);
            MinimumSize = new Size(1000, 400);
            Name = "MainForm";
            Text = "Form1";
            FormClosing += MainForm_FormClosing;
            Resize += MainForm_Resize;
            videoPanel.ResumeLayout(false);
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Components.VideoPanel videoPanel;
        private Button btnPlayVideo;
        private Label labelVideoSize;
        private Button btnShowLog;
        private Label labelStatus;
    }
}
