using ReceiverLib;

namespace WinFormsReceiver
{
    public partial class MainForm : Form
    {
        private bool _closing = false;

        public MainForm()
        {
            InitializeComponent();

            Helpers.Log(richTextLog, "Started...");

            Program.Context.VideoReceiver.StateChanged += OnVideoReceiverStateChanged;
            Program.Context.VideoReceiver.ErrorOccurred += OnVideoReceiverErrorOccured;
        }

        private void OnVideoReceiverErrorOccured(object? sender, ReceiverErrorEventArgs e)
        {
            BeginInvoke(() =>
            {
                if (_closing) return;

                Helpers.Log(richTextLog, $"Video receiver reported error: {e.Error.Message}");
                UpdatePlaybackButtonsState();
            });
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            Program.Context.VideoReceiver.StateChanged -= OnVideoReceiverStateChanged;
            _closing = true;
        }

        private void OnVideoReceiverStateChanged(object? sender, ReceiverStateChangedEventArgs e)
        {
            BeginInvoke(() =>
            {
                if (_closing) return;

                Helpers.Log(richTextLog, $"Video receiver state changed: {e.NewState}");
                UpdatePlaybackButtonsState();
            });
        }

        private void UpdatePlaybackButtonsState()
        {
            ReceiverState state = Program.Context.VideoReceiver.State;
            btnPlayVideo.Enabled = state == ReceiverState.Stopped || state == ReceiverState.Paused;
            btnPauseVideo.Enabled = state == ReceiverState.Playing;
        }

        private void btnPlayVideo_Click(object sender, EventArgs e)
        {
            Helpers.Log(richTextLog, "Starting video playback...");
            btnPlayVideo.Enabled = false;
            if (Program.Context.VideoReceiver.State == ReceiverState.Paused)
            {
                Program.Context.VideoReceiver.Resume();
            }
            else
            {
                Program.Context.VideoReceiver.Connect();
            }
        }

        private void btnPauseVideo_Click(object sender, EventArgs e)
        {
            Helpers.Log(richTextLog, "Pausing video playback...");
            btnPauseVideo.Enabled = false;
            Program.Context.VideoReceiver.Pause();
        }
    }
}
