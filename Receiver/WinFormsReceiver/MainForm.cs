using ReceiverLib;

namespace WinFormsReceiver
{
    enum PlayButtonState { DoPlay, DoPause };

    public partial class MainForm : Form
    {
        private bool _closing = false;
        private LogForm _logForm = new LogForm();
        private PlayButtonState _playButtonState = PlayButtonState.DoPlay;

        public MainForm()
        {
            InitializeComponent();

            _logForm.LogMessage("Started...");
            UpdateVideoSizeLabel();

            Program.Context.VideoReceiver.StateChanged += OnVideoReceiverStateChanged;
            Program.Context.VideoReceiver.ErrorOccurred += OnVideoReceiverErrorOccured;
        }

        private void OnVideoReceiverErrorOccured(object? sender, ReceiverErrorEventArgs e)
        {
            BeginInvoke(() =>
            {
                if (_closing) return;
                labelStatus.Text = $"An error occured, press play to try again ({e.Error.Kind}). See Log for more information";
                _logForm.LogMessage($"Video receiver reported error: {e.Error.Kind} ({e.Error.Message})", Helpers.LogLevel.Error);
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

                _logForm.LogMessage($"Video receiver state changed: {e.NewState}");
                UpdatePlaybackButtonsState();
                UpdateVideoSizeLabel();
                labelStatus.Text = e.NewState.ToString();
            });
        }

        private void UpdatePlaybackButtonsState()
        {
            const string PlayIcon = "⏵";
            const string PauseIcon = "⏸";

            ReceiverState state = Program.Context.VideoReceiver.State;
            bool canPause = state == ReceiverState.Playing;
            _playButtonState = canPause ? PlayButtonState.DoPause : PlayButtonState.DoPlay;
            btnPlayVideo.Text = canPause ? PauseIcon : PlayIcon;
        }

        private void btnPlayVideo_Click(object sender, EventArgs e)
        {
            _logForm.LogMessage("Play/Pause clicked...");
            if (_playButtonState == PlayButtonState.DoPause)
            {
                Program.Context.VideoReceiver.Pause();
            }
            else
            {
                if (Program.Context.VideoReceiver.State == ReceiverState.Paused)
                {
                    Program.Context.VideoReceiver.Resume();
                }
                else
                {
                    Program.Context.VideoReceiver.Connect();
                    UpdateVideoSizeLabel();
                }
            }
            UpdatePlaybackButtonsState();
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            UpdateVideoSizeLabel();
        }

        void UpdateVideoSizeLabel()
        {
            int nativeW = 0;
            int nativeH = 0;

            if (Program.Context.VideoReceiver.Statistics.Resolution.HasValue)
            {
                (nativeW, nativeH) = Program.Context.VideoReceiver.Statistics.Resolution.Value;
            }

            labelVideoSize.Text = $"{videoPanel.Width} x {videoPanel.Height} (native {nativeW} x {nativeH})"; ;
        }

        private void btnShowLog_Click(object sender, EventArgs e)
        {
            _logForm.Show();
        }
    }
}
