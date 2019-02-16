using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using NAudio.Wave;

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

        private TypeOfView _typeOfView;
        private TypeOfInput _typeOfInput;

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
                _data = Audio.PlotAudioData(_bufferedWaveProvider);

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
                        _data[i] *= 0.9;

                Thread.Sleep(20);
            }
        }

        /// <summary>
        /// Load application settings
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                _typeOfView = (TypeOfView) Enum.Parse(typeof(TypeOfView), Properties.Settings.Default.TypeOfView);
            }
            catch (Exception)
            {
                _typeOfView = TypeOfView.None;
            }
            finally
            {
                ChangeChecked(_typeOfView.ToString(), ViewControl);
            }

            try
            {
                _typeOfInput = (TypeOfInput) Enum.Parse(typeof(TypeOfInput), Properties.Settings.Default.Input);
            }
            catch (Exception)
            {
                _typeOfInput = TypeOfInput.None;
            }
            finally
            {
                ChangeChecked(_typeOfInput.ToString(), AudioInputControl);
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
                    var changeRgb = ChangeRgb.Rubd;

                    uint r = 0, g = 0, b = 255;

                    for (var i = 0; i < _data.Length; i += 2)
                    {
                        if (r == 255)
                            changeRgb = ChangeRgb.Gurd;
                        else if (g == 255)
                            changeRgb = ChangeRgb.Bugd;
                        else if (b == 255)
                            changeRgb = ChangeRgb.Rubd;

                        switch (changeRgb)
                        {
                            case ChangeRgb.Bugd:
                                b+=3;
                                g-=3;
                                break;
                            case ChangeRgb.Gurd:
                                g+=3;
                                r-=3;
                                break;
                            case ChangeRgb.Rubd:
                                r+=3;
                                b-=3;
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }

                        var canvas = new Canvas
                        {
                            Width = Visualizer.Width / _data.Length,
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
                        Canvas.SetLeft(canvas, i * canvas.Width);

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
        /// Function doing after loading interface and modules of application
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettings();
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
                        _data[i] = _lastData[i] * 0.9f;
                    else if (_data[i] > _lastData[i])
                        _lastData[i] = _lastData[i] * 1.1f;
                }

            Dispatcher.Invoke(() =>
            {
                foreach (var obj in Visualizer.Children)
                {
                    if (obj is Canvas canvas)
                    {
                        canvas.Height = _data[Convert.ToInt32(canvas.Name.Replace("Canvas", ""))] * Visualizer.Height / 16;
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
                if (obj is MenuItem item)
                    item.IsChecked = item.Header.ToString() == text;
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

            Properties.Settings.Default.TypeOfView = _typeOfView.ToString();
            Properties.Settings.Default.Save();

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
                    _typeOfInput = TypeOfInput.None;
                    break;
                case "Microphone":
                    _waveIn?.StartRecording();
                    _wasapiLoopbackCapture?.StopRecording();
                    _typeOfInput = TypeOfInput.Microphone;
                    break;
                case "System":
                    _waveIn?.StopRecording();
                    _wasapiLoopbackCapture?.StartRecording();
                    _typeOfInput = TypeOfInput.System;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Properties.Settings.Default.Input = _typeOfInput.ToString(); 
            Properties.Settings.Default.Save();

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
 * TODO: 1. Make wave view
 * TODO: 2. Transparently background in application
 * TODO: 3. App icon
 * TODO: 4. Advanced choose device options
 * TODO: 5. Make expressions in use 
 */