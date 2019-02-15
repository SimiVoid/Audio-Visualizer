using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using NAudio.Wave;
using Accord.Math;
using System.Diagnostics;

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
        private const int BufferSize = 2048;
        private const int Rate = 44100;

        private WaveIn _waveIn;
        private WasapiLoopbackCapture _wasapiLoopbackCapture;
        private BufferedWaveProvider _bufferedWaveProvider;

        private TypeOfView _typeOfView;

        private double[] _data, _lastData;
        private bool _isRun, _isCreated;

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
            _isCreated = false;

            Height = SystemParameters.PrimaryScreenHeight * 96.0 / (int)Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop", "LogPixels", 96);
            Width = SystemParameters.PrimaryScreenWidth * 96.0 / (int)Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop", "LogPixels", 96);

            Visualizer.Height = Height;
            Visualizer.Width = Width;

            _wasapiLoopbackCapture = new WasapiLoopbackCapture();
            _wasapiLoopbackCapture.DataAvailable += _wasapiLoopbackCapture_DataAvailable;

            _waveIn = new WaveIn
            {
                WaveFormat = _wasapiLoopbackCapture.WaveFormat,
                BufferMilliseconds = (int) (BufferSize / (double) Rate * 1000.0)
            };
            _waveIn.DataAvailable += _waveIn_DataAvailable;

            _bufferedWaveProvider = new BufferedWaveProvider(_wasapiLoopbackCapture.WaveFormat)
            {
                BufferLength = BufferSize * 2,
                DiscardOnBufferOverflow = true
            };

            _typeOfView = TypeOfView.None;

            _updateThread = new Thread(Update);
            _updateThread.TrySetApartmentState(ApartmentState.STA);
            _updateThread.Start();
        }

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

                if (_data != null)
                    for (var i = 0; i < _data.Length; i++)
                    {
                        _data[i] *= 0.9;
                    }

                Thread.Sleep(20);
            }
        }

        /// <summary>
        ///     Create Classic View
        /// </summary>
        private void CreateClassicView()
        {
            if (_data == null) return;

            var thread = new Thread(()=>
            {
                Dispatcher.Invoke(() =>
                {
                    var changeRgb = ChangeRgb.rubd;

                    uint r = 0, g = 0, b = 255;

                    for (var i = 2; i < _data.Length; i += 2)
                    {
                        if (r == 255)
                            changeRgb = ChangeRgb.gurd;
                        else if (g == 255)
                            changeRgb = ChangeRgb.bugd;
                        else if (b == 255)
                            changeRgb = ChangeRgb.rubd;

                        switch (changeRgb)
                        {
                            case ChangeRgb.bugd:
                                b+=3;
                                g-=3;
                                break;
                            case ChangeRgb.gurd:
                                g+=3;
                                r-=3;
                                break;
                            case ChangeRgb.rubd:
                                r+=3;
                                b-=3;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        var canvas = new Canvas
                        {
                            Width = Visualizer.Width / _data.Length * 1.5,
                            Background = new SolidColorBrush(Color.FromArgb(
                                Convert.ToByte(127),
                                Convert.ToByte(r),
                                Convert.ToByte(g),
                                Convert.ToByte(b)
                            )),
                            Height = 0,
                            Name = "Canvas" + i.ToString()
                        };

                        Canvas.SetBottom(canvas, 1);
                        Canvas.SetLeft(canvas, i * canvas.Width / 1.5);

                        Visualizer.Children.Add(canvas);
                    }
                });
            });
            thread.TrySetApartmentState(ApartmentState.STA);
            thread.Start();

            _isCreated = true;
        }

        /// <summary>
        ///     Close application event
        /// </summary>
        /// <param name="sender">Window object</param>
        /// <param name="e">Event arguments</param>
        private void Window_Closed(object sender, EventArgs e)
        {
            _isRun = false;

            _wasapiLoopbackCapture?.StopRecording();
            _waveIn?.StopRecording();
        }

        #endregion

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
        /// <param name="e">Audio stream data</param>
        private void GetAudio(WaveInEventArgs e)
        {
            _bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);

            if (_isCreated != true) CreateClassicView();
        }

        /// <summary>
        /// Plot audio data
        /// </summary>
        private void PlotAudioData()
        {
            const int frameSize = BufferSize;
            var audioBytes = new byte[frameSize];

            _bufferedWaveProvider.Read(audioBytes, 0, frameSize);

            if (audioBytes.Length == 0 || audioBytes[frameSize - 2] == 0) return;

            const int bytesPerPoint = 2;
            var graphPointCount = audioBytes.Length / bytesPerPoint;
            var pcm = new double[graphPointCount];
            var fftReal = new double[graphPointCount / 2];

            for (var i = 0; i < graphPointCount; i++)
            {
                var val = Math.Abs((int)BitConverter.ToInt16(audioBytes, i * 2));

                pcm[i] = 200f * val / 65536;
            }

            Array.Copy(Fft(pcm), fftReal, fftReal.Length);

            _data = fftReal;
        }

        /// <summary>
        /// FFT transformation of audio
        /// </summary>
        /// <param name="data">Audio pcm data</param>
        /// <returns></returns>
        private static double[] Fft(IReadOnlyList<double> data)
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

        #region Draw

        /// <summary>
        ///     Drawing wave
        /// </summary>
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

            if(_data != null && _lastData != null)
                for (var i = 0; i < _data.Length; i++)
                {
                    if (_data[i] < _lastData[i])
                        _data[i] *= 0.9f;
                    else if (_data[i] > _lastData[i])
                        _lastData[i] *= 1.1f;
                }

            Dispatcher.Invoke(() =>
            {
                foreach (var obj in Visualizer.Children)
                {
                    if (obj is Canvas canvas)
                    {
                        canvas.Height = Math.Sqrt(_data[Convert.ToInt32(canvas.Name.Replace("Canvas", ""))]) * Visualizer.Height / 16;
                    }
                }
            });

            _lastData = _data;
        }

        #endregion
        
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
                    _waveIn?.StopRecording();
                    _wasapiLoopbackCapture?.StopRecording();
                    break;
                case "Microphone":
                    _waveIn?.StartRecording();
                    _wasapiLoopbackCapture?.StopRecording();
                    break;
                case "System":
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

/*
 * TODO: 1. Make animation of audio data in classic view
 * TODO: 2. Make wave view
 * TODO: 3. Transparently background in application
 * TODO: 4. App icon
 * TODO: 5. Advanced choose device options
 */