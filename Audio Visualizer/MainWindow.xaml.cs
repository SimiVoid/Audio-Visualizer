using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using NAudio.Wave;
using Accord.Math;

namespace Audio_Visualizer
{
    /// <inheritdoc>
    ///     <cref>
    ///     </cref>
    /// </inheritdoc>
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly int _bufferSize = (int)Math.Pow(2, 11);
        private const int Rate = 44100;

        private WaveIn _waveIn;
        private WasapiLoopbackCapture _wasapiLoopbackCapture;
        private BufferedWaveProvider _bufferedWaveProvider;

        private TypeOfView _typeOfView;
        private TypeOfInput _typeOfInput;

        private double[] _data;
        private bool _isRun;

        private Thread _updateThread;

        #region Main elements of app

        /// <inheritdoc />
        /// <summary>
        ///     Init app
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            Init();
        }

        /// <summary>
        ///     Init all modules
        /// </summary>
        private void Init()
        {
            _isRun = true;

            Height = SystemParameters.PrimaryScreenHeight * 96.0 / (int)Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop", "LogPixels", 96); ;
            Width = SystemParameters.PrimaryScreenWidth * 96.0 / (int)Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop", "LogPixels", 96);

            Visualizer.Height = Height;
            Visualizer.Width = Width;

            _wasapiLoopbackCapture = new WasapiLoopbackCapture();
            _wasapiLoopbackCapture.DataAvailable += _wasapiLoopbackCapture_DataAvailable;

            _waveIn = new WaveIn();
            _waveIn.DataAvailable += _waveIn_DataAvailable;
            _waveIn.WaveFormat = _wasapiLoopbackCapture.WaveFormat;
            _waveIn.BufferMilliseconds = (int)((double)_bufferSize / (double)Rate * 1000.0);

            _bufferedWaveProvider = new BufferedWaveProvider(_wasapiLoopbackCapture.WaveFormat)
            {
                BufferLength = _bufferSize * 2,
                DiscardOnBufferOverflow = true
            };

            _typeOfView = TypeOfView.None;
            _typeOfInput = TypeOfInput.None;

            _updateThread = new Thread(Update);
            _updateThread.TrySetApartmentState(ApartmentState.STA);
            _updateThread.Start();
        }

        #region Audio

        /// <summary>
        /// Get audio from default audio stream;
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _waveIn_DataAvailable(object sender, WaveInEventArgs e)
        {
            GetAudio(e);
        }

        /// <summary>
        ///     Get audio data from system
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _wasapiLoopbackCapture_DataAvailable(object sender, WaveInEventArgs e)
        {
            GetAudio(e);
        }

        /// <summary>
        /// Get audio from stream
        /// </summary>
        /// <param name="e"></param>
        private void GetAudio(WaveInEventArgs e)
        {
            _bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded); 
        }

        /// <summary>
        /// Plot audio data
        /// </summary>
        private void PlotAudioData()
        {
            var frameSize = _bufferSize;
            var audioBytes = new byte[frameSize];

            _bufferedWaveProvider.Read(audioBytes, 0, frameSize);

            if (audioBytes.Length == 0 || audioBytes[frameSize - 2] == 0) return;

            const int bytesPerPoint = 2;
            var graphPointCount = audioBytes.Length / bytesPerPoint;
            var pcm = new double[graphPointCount];
            var fft = new double[graphPointCount];
            var fftReal = new double[graphPointCount / 2];

            for (var i = 0; i < graphPointCount; i++)
            {
                var val = BitConverter.ToInt16(audioBytes, i * 2);

                pcm[i] = (double) val / Math.Pow(2, 16) * 200.0;
            }

            fft = FFT(pcm);

            var fftMaxFrequency = (double)Rate / 2;
            var fftPointSpacingHz = fftMaxFrequency / graphPointCount;

            Array.Copy(fft, fftReal, fftReal.Length);

            _data = fftReal;


        }

        /// <summary>
        /// FFT transformation of audio
        /// </summary>
        /// <param name="data">Audio pcm data</param>
        /// <returns></returns>
        private double[] FFT(IReadOnlyList<double> data)
        {
            var fft = new double[data.Count];
            var fftComplex = new System.Numerics.Complex[data.Count];

            for (var i = 0; i < data.Count; i++)
                fftComplex[i] = new System.Numerics.Complex(data[i], 0.0);

            FourierTransform.FFT(fftComplex, FourierTransform.Direction.Forward);

            for (var i = 0; i < data.Count; i++)
                fft[i] = fftComplex[i].Magnitude;

            return fft;
        }

        #endregion

        /// <summary>
        ///     Update view of data
        /// </summary>
        private void Update()
        {
            while (_isRun)
            {
                PlotAudioData();

                switch (_typeOfView)
                {
                    case TypeOfView.None:
                        break;
                    case TypeOfView.Wave:
                        DrawWave();
                        break;
                    case TypeOfView.Classic:
                        DrawSpectrum();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                Thread.Sleep(20);
            }
        }

        #endregion

        #region Draw

        /// <summary>
        ///     Drawing wave
        /// </summary>
        /// <param name="data">Audio data</param>
        private void DrawWave()
        {
            if (_data == null) return;

            Dispatcher.Invoke(() =>
            {
                Visualizer.Children.Clear();

                
            });          
        }

        /// <summary>
        /// Draw spectrum of audio
        /// </summary>
        private void DrawSpectrum()
        {
            if (_data == null) return;

            Dispatcher.Invoke(new Action(() =>
            {
                Visualizer.Children.Clear();

                for (var i = 0; i < _data.Length; i++)
                {
                    var canvas = new Canvas
                    {
                        Width = Visualizer.Width / Convert.ToDouble(_data.Length),
                        Background = Brushes.WhiteSmoke,
                        Height = _data[i] * Visualizer.Height / 32
                    };

                    Canvas.SetBottom(canvas, 1);
                    Canvas.SetLeft(canvas, i * canvas.Width);

                    Visualizer.Children.Add(canvas);
                }
            }));
        }

        #endregion

        /// <summary>
        ///     Close event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closed(object sender, EventArgs e)
        {
            _isRun = false;

            _wasapiLoopbackCapture?.StopRecording();
            _waveIn?.StopRecording();
        }

        #region Context Menu

        /// <summary>
        ///     Change menu items state
        /// </summary>
        /// <param name="text">Text of menu item</param>
        /// <param name="parent">Parent of menu item</param>
        private static void ChangeChecked(string text, object parent)
        {
            if (!(parent is MenuItem menuItem)) return;

            foreach (var obj in menuItem.Items)
                if (obj is MenuItem item && item.Header.ToString() != text)
                    item.IsChecked = false;
        }

        /// <summary>
        ///     Change options of data visualize
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ModeMenuItem_Checked(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem item)) return;

            switch (item.Header.ToString())
            {
                case "None":
                    _typeOfView = TypeOfView.None;
                    break;
                case "Classic":
                    _typeOfView = TypeOfView.Classic;
                    break;
                case "Wave":
                    _typeOfView = TypeOfView.Wave;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ChangeChecked(item.Header.ToString(), item.Parent);
        }

        /// <summary>
        ///     Change options of audio source
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AudioInputMenuItem_Checked(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem item)) return;

            switch (item.Header.ToString())
            {
                case "None":
                    _typeOfInput = TypeOfInput.None;
                    _waveIn?.StopRecording();
                    _wasapiLoopbackCapture?.StopRecording();
                    break;
                case "Microphone":
                    _typeOfInput = TypeOfInput.Microphone;
                    _waveIn?.StartRecording();
                    _wasapiLoopbackCapture?.StopRecording();
                    break;
                case "System":
                    _typeOfInput = TypeOfInput.System;
                    _typeOfInput = TypeOfInput.Microphone;
                    _waveIn?.StopRecording();
                    _wasapiLoopbackCapture?.StartRecording();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ChangeChecked(item.Header.ToString(), item.Parent);
        }

        /// <summary>
        ///     Context menu close application function
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}