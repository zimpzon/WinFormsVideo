namespace WinFormsReceiver
{
    partial class LogForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            richTextLog = new RichTextBox();
            SuspendLayout();
            // 
            // richTextLog
            // 
            richTextLog.BackColor = SystemColors.Control;
            richTextLog.Dock = DockStyle.Fill;
            richTextLog.Location = new Point(0, 0);
            richTextLog.Name = "richTextLog";
            richTextLog.Size = new Size(1084, 469);
            richTextLog.TabIndex = 0;
            richTextLog.Text = "";
            richTextLog.WordWrap = false;
            // 
            // LogForm
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1084, 469);
            Controls.Add(richTextLog);
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            Name = "LogForm";
            Text = "Log";
            ResumeLayout(false);
        }

        #endregion

        private RichTextBox richTextLog;
    }
}