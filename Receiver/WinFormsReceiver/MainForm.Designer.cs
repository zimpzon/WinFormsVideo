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
            videoPanel1 = new WinFormsReceiver.Components.VideoPanel();
            btnPlayVideo = new Button();
            btnPauseVideo = new Button();
            richTextLog = new RichTextBox();
            SuspendLayout();
            // 
            // videoPanel1
            // 
            videoPanel1.Location = new Point(12, 12);
            videoPanel1.Name = "videoPanel1";
            videoPanel1.Size = new Size(744, 358);
            videoPanel1.TabIndex = 0;
            // 
            // btnPlayVideo
            // 
            btnPlayVideo.Location = new Point(12, 397);
            btnPlayVideo.Name = "btnPlayVideo";
            btnPlayVideo.Size = new Size(178, 34);
            btnPlayVideo.TabIndex = 1;
            btnPlayVideo.Text = "Play";
            btnPlayVideo.UseVisualStyleBackColor = true;
            btnPlayVideo.Click += btnPlayVideo_Click;
            // 
            // btnPauseVideo
            // 
            btnPauseVideo.Enabled = false;
            btnPauseVideo.Location = new Point(196, 397);
            btnPauseVideo.Name = "btnPauseVideo";
            btnPauseVideo.Size = new Size(178, 34);
            btnPauseVideo.TabIndex = 2;
            btnPauseVideo.Text = "Pause";
            btnPauseVideo.UseVisualStyleBackColor = true;
            btnPauseVideo.Click += btnPauseVideo_Click;
            // 
            // richTextLog
            // 
            richTextLog.Location = new Point(12, 474);
            richTextLog.Name = "richTextLog";
            richTextLog.ReadOnly = true;
            richTextLog.Size = new Size(575, 144);
            richTextLog.TabIndex = 3;
            richTextLog.Text = "";
            richTextLog.WordWrap = false;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1368, 724);
            Controls.Add(richTextLog);
            Controls.Add(btnPauseVideo);
            Controls.Add(btnPlayVideo);
            Controls.Add(videoPanel1);
            Name = "MainForm";
            Text = "Form1";
            FormClosing += MainForm_FormClosing;
            ResumeLayout(false);
        }

        #endregion

        private Components.VideoPanel videoPanel1;
        private Button btnPlayVideo;
        private Button btnPauseVideo;
        private RichTextBox richTextLog;
    }
}
