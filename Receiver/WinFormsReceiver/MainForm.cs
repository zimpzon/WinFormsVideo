using WinFormsReceiver.Context;

namespace WinFormsReceiver
{
    public partial class MainForm : Form
    {
        private LogForm _logForm = new LogForm();
        private readonly IWinFormsContext _context;

        public MainForm()
        {
            InitializeComponent();

            _context = WinFormsContext.Instance;
            VideoPanel.Initialize();
        }

        private void btnPlayVideo_Click(object sender, EventArgs e)
        {
            _logForm.LogMessage("Starting video");
            _context.Video.Play(new Uri(tbVideoUri.Text));
        }

        private void btnShowLog_Click(object sender, EventArgs _)
        {
            _logForm.Show();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs _)
        {
            _context.Video.Stop();
        }
    }
}
