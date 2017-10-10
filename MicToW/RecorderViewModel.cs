using System;
using System.Windows.Input;
using System.IO;
using VoiceRecorder.Audio;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight;
using System.Threading;
using System.ComponentModel;
using System.Windows.Forms;
using InputManager;
using System.Diagnostics;
using System.Windows;

namespace VoiceRecorder
{
    class RecorderViewModel : ViewModelBase, IView
    {
        private readonly RelayCommand beginRecordingCommand;
        private readonly RelayCommand stopCommand;
        private readonly IAudioRecorder recorder;
        private float lastPeak;
        private string waveFileName;
        public const string ViewName = "RecorderView";
        private ICommand startWcommand;
        private ICommand stopWcommand;
        bool run = true;
        private BackgroundWorker bw = new BackgroundWorker();
        public float triggerLevel = 70F;
        public float TriggerLevel
        {
            get { return triggerLevel; }
            set { triggerLevel = value; }
        }
        public double wsendtime = 1;
        public double WSendTime
        {
            get { return wsendtime; }
            set { wsendtime = value; }
        }
        public string status;
        public string Status
        {
            get { return status; }
            set { status = value; }
        }
        public int startcount = 1;

        public RecorderViewModel(IAudioRecorder recorder)
        {
            this.recorder = recorder;
            this.recorder.Stopped += OnRecorderStopped;
            beginRecordingCommand = new RelayCommand(BeginRecording,
                () => recorder.RecordingState == RecordingState.Stopped ||
                      recorder.RecordingState == RecordingState.Monitoring);
            stopCommand = new RelayCommand(Stop,
                () => recorder.RecordingState == RecordingState.Recording);
            recorder.SampleAggregator.MaximumCalculated += OnRecorderMaximumCalculated;
            Messenger.Default.Register<ShuttingDownMessage>(this, OnShuttingDown);
            this.startWcommand = new RelayCommand(() =>
            {
                if (startcount == 1)
                {
                    bw.RunWorkerAsync();
                    startcount++;
                }
                
            });
            this.stopWcommand = new RelayCommand(() =>
            {
                Stop();
                startcount = 1;
            });
            bw.DoWork += new DoWorkEventHandler(bw_DoWork);
        }

        void OnRecorderStopped(object sender, EventArgs e)
        {
            Messenger.Default.Send(new NavigateMessage(SaveViewModel.ViewName, 
                new VoiceRecorderState(waveFileName, null)));
        }

        void OnRecorderMaximumCalculated(object sender, MaxSampleEventArgs e)
        {
            lastPeak = Math.Max(e.MaxSample, Math.Abs(e.MinSample));
            RaisePropertyChanged("CurrentInputLevel");
            RaisePropertyChanged("RecordedTime");
        }

        public ICommand BeginRecordingCommand { get { return beginRecordingCommand; } }
        public ICommand StopCommand { get { return stopCommand; } }

        public void Activated(object state)
        {
            BeginMonitoring((int)state);
        }

        private void OnShuttingDown(ShuttingDownMessage message)
        {
            if (message.CurrentViewName == ViewName)
            {
                recorder.Stop();
            }
        }

        public string RecordedTime
        {
            get
            {
                var current = recorder.RecordedTime;
                return String.Format("{0:D2}:{1:D2}.{2:D3}", 
                    current.Minutes, current.Seconds, current.Milliseconds);
            }
        }

        private void BeginMonitoring(int recordingDevice)
        {
            recorder.BeginMonitoring(recordingDevice);
            RaisePropertyChanged("MicrophoneLevel");
        }

        private void BeginRecording()
        {
            waveFileName = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
            recorder.BeginRecording(waveFileName);
            RaisePropertyChanged("MicrophoneLevel");
            RaisePropertyChanged("ShowWaveForm");
        }

        public ICommand StartWcommand { get { return startWcommand; } }
        public ICommand StopWcommand { get { return stopWcommand; } }

        private void bw_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            run = true;
            status = "Running";
            RaisePropertyChanged("Status");           
            while (run == true)
            {
                if (CurrentInputLevel > TriggerLevel)
                {
                    Stopwatch s = new Stopwatch();
                    s.Start();
                    while (s.Elapsed < TimeSpan.FromSeconds(WSendTime))
                    {
                        InputManager.Keyboard.KeyDown(Keys.W);
                        Thread.Sleep(10);
                        InputManager.Keyboard.KeyUp(Keys.W);
                    }
                    s.Stop();

                }
            }
        }

        public void Stop()
        {
                run = false;
                status = "Stopped";
                RaisePropertyChanged("Status");
        }

        public double MicrophoneLevel
        {
            get { return recorder.MicrophoneLevel; }
            set { recorder.MicrophoneLevel = value; }
        }

        public bool ShowWaveForm
        {
            get { return recorder.RecordingState == RecordingState.Recording || 
                recorder.RecordingState == RecordingState.RequestedStop; }
        }

        // multiply by 100 because the Progress bar's default maximum value is 100
        public float CurrentInputLevel { get { return lastPeak * 100; } }

        public SampleAggregator SampleAggregator 
        {
            get
            {
                return recorder.SampleAggregator;
            }
        }
    }
}