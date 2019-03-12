using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Audio_Visualizer.Properties;
using Microsoft.Win32;
using NAudio.Wave;
using Color = System.Drawing.Color;

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
        private WaveIn _waveIn;
        private WasapiLoopbackCapture _wasapiLoopbackCapture;
        private BufferedWaveProvider _bufferedWaveProvider;

        private TypeOfInput _typeOfInput;
        private Color _backgroundColor;

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

            Height = SystemParameters.PrimaryScreenHeight * 96.0 /
                     (int) Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop", "LogPixels", 96);
            Width = SystemParameters.PrimaryScreenWidth * 96.0 /
                    (int) Registry.GetValue("HKEY_CURRENT_USER\\Control Panel\\Desktop", "LogPixels", 96);

            Visualizer.Height = Height;
            Visualizer.Width = Width;

            _wasapiLoopbackCapture = new WasapiLoopbackCapture();
            _wasapiLoopbackCapture.DataAvailable += _wasapiLoopbackCapture_DataAvailable;

            _waveIn = new WaveIn
            {
                WaveFormat = _wasapiLoopbackCapture.WaveFormat,
                BufferMilliseconds = (int) (Audio.BufferSize / (double) Audio.Rate * 1000.0)
            };
            _waveIn.DataAvailable += _waveIn_DataAvailable;

            _bufferedWaveProvider = new BufferedWaveProvider(_wasapiLoopbackCapture.WaveFormat)
            {
                BufferLength = Audio.BufferSize * 2,
                DiscardOnBufferOverflow = true
            };

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
                if (_typeOfInput != TypeOfInput.None)
                    _data = Audio.PlotAudioData(_bufferedWaveProvider);
                else if (_data != null)
                    for (var i = 0; i < _data.Length; i++)
                        _data[i] *= 0.9;

                DrawSpectrum();

                Thread.Sleep(10);
            }
        }

        /// <summary>
        ///     Create Classic View
        /// </summary>
        private void CreateClassicView()
        {
            if (_data == null) return;

            var thread = new Thread(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    var changeRgb = ChangeRgbState.Rubd;

                    uint r = 0, g = 0, b = 255;

                    for (var i = 0; i < _data.Length / 2; i++)
                    {
                        if (r == 255)
                            changeRgb = ChangeRgbState.Gurd;
                        else if (g == 255)
                            changeRgb = ChangeRgbState.Bugd;
                        else if (b == 255)
                            changeRgb = ChangeRgbState.Rubd;

                        switch (changeRgb)
                        {
                            case ChangeRgbState.Bugd:
                                b += 3;
                                g -= 3;
                                break;
                            case ChangeRgbState.Gurd:
                                g += 3;
                                r -= 3;
                                break;
                            case ChangeRgbState.Rubd:
                                r += 3;
                                b -= 3;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        var canvas = new Canvas
                        {
                            Width = Visualizer.Width / _data.Length,
                            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
                                Convert.ToByte(127),
                                Convert.ToByte(r),
                                Convert.ToByte(g),
                                Convert.ToByte(b)
                            )),
                            Height = 0,
                            Name = "Canvas" + i
                        };

                        Canvas.SetBottom(canvas, 1);
                        Canvas.SetLeft(canvas, i * canvas.Width * 2);

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

        /// <summary>
        ///     Function doing after loading interface and modules of application
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
        }

        #region Settings

        /// <summary>
        ///     Load application settings
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                _typeOfInput = (TypeOfInput)Enum.Parse(typeof(TypeOfInput), Settings.Default.TypeOfInput);
            }
            catch (Exception)
            {
                _typeOfInput = TypeOfInput.None;
            }
            finally
            {
                ChangeChecked(_typeOfInput.ToString(), AudioInputControl);
            }

            try
            {
                _backgroundColor = Settings.Default.Color;
            }
            catch (Exception)
            {
                _backgroundColor = Color.FromArgb(255, 0, 0, 0);
            }
            finally
            {
                ChangeChecked($"{Convert.ToInt32(_backgroundColor.A / 255 * 100)}%", TransparentlyControl);
            }
        }

        /// <summary>
        ///     Update settings file
        /// </summary>
        /// <param name="data"></param>
        private static void UpdateSettings(Enum data)
        {
            Settings.Default[data.GetType().ToString().Replace("Audio_Visualizer.", "")] = data.ToString();
            Settings.Default.Save();
        }

        private static void UpdateSettings(Color data)
        {
            Settings.Default[data.GetType().ToString().Replace("System.Drawing.", "")] = data;
            Settings.Default.Save();
        }

        #endregion

        #endregion

        #region Audio

        /// <summary>
        ///     Get audio from default audio stream;
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
        ///     Get audio from stream
        /// </summary>
        /// <param name="e">Audio stream data</param>
        private void GetAudio(WaveInEventArgs e)
        {
            _bufferedWaveProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);

            if (_isCreated != true) CreateClassicView();
        }

        #endregion

        #region Draw

        /// <summary>
        ///     Draw spectrum of audio
        /// </summary>
        private void DrawSpectrum()
        {
            if (_data == null) return;

            if (_data != null && _lastData != null)
                for (var i = 0; i < _data.Length; i++)
                    if (_data[i] < _lastData[i])
                        _data[i] = _lastData[i] * 0.9f;
                    else if (_data[i] > _lastData[i])
                        _lastData[i] = _lastData[i] * 1.1f;

            Dispatcher.Invoke(() =>
            {
                foreach (var obj in Visualizer.Children)
                    if (obj is Canvas canvas)
                        canvas.Height = _data[Convert.ToInt32(canvas.Name.Replace("Canvas", ""))] * Visualizer.Height / 16;
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
                if (obj is MenuItem item)
                    item.IsChecked = item.Header.ToString() == text;
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
                    _typeOfInput = TypeOfInput.None;
                    break;
                case "Microphone":
                    _typeOfInput = TypeOfInput.Microphone;
                    try
                    {
                        _waveIn?.StartRecording();
                        _wasapiLoopbackCapture?.StopRecording();
                    }
                    catch (Exception exp)
                    {
                        MessageBox.Show($"Cannot start capture audio from input device.\n{exp.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    break;
                case "System":
                    _waveIn?.StopRecording();
                    _wasapiLoopbackCapture?.StartRecording();
                    _typeOfInput = TypeOfInput.System;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            UpdateSettings(_typeOfInput);

            ChangeChecked(item.Header.ToString(), item.Parent);
        }

        /// <summary>
        ///     Change opacity of background
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TransparentlyControl_Checked(object sender, RoutedEventArgs e)
        {
            if (!(sender is MenuItem item)) return;

            switch (item.Header.ToString())
            {
                case "0%":
                    _backgroundColor = Color.FromArgb(1, 0, 0, 0);
                    break;
                case "25%":
                    _backgroundColor = Color.FromArgb(63, 0, 0, 0);
                    break;
                case "50%":
                    _backgroundColor = Color.FromArgb(127, 0, 0, 0);
                    break;
                case "75%":
                    _backgroundColor = Color.FromArgb(191, 0, 0, 0);
                    break;
                case "100%":
                    _backgroundColor = Color.FromArgb(255, 0, 0, 0);
                    ;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Dispatcher.Invoke(() =>
            {
                Visualizer.Background = new SolidColorBrush(new System.Windows.Media.Color
                {
                    A = _backgroundColor.A,
                    R = _backgroundColor.R,
                    G = _backgroundColor.G,
                    B = _backgroundColor.B
                });
            });

            UpdateSettings(_backgroundColor);

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
 * TODO: 1. Advanced choose device options
 */