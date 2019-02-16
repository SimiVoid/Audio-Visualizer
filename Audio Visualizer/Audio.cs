using System;
using System.Collections.Generic;
using Accord.Math;
using NAudio.Wave;

namespace Audio_Visualizer
{
    internal class Audio
    {
        public static int BufferSize = 2048;
        public static int Rate = 44100;

        /// <summary>
        /// Plotting audio data
        /// </summary>
        /// <param name="bufferedWaveProvider">Wave buffer</param>
        /// <returns></returns>
        public static double[] PlotAudioData(BufferedWaveProvider bufferedWaveProvider)
        {
            const int frameSize = BufferSize;
            var audioBytes = new byte[frameSize];

            bufferedWaveProvider.Read(audioBytes, 0, frameSize);

            if (audioBytes.Length == 0 || audioBytes[frameSize - 2] == 0) return null;

            const int bytesPerPoint = 2;
            var graphPointCount = audioBytes.Length / bytesPerPoint;
            var pcm = new double[graphPointCount];
            var fftReal = new double[graphPointCount / 2];

            for (var i = 0; i < graphPointCount; i++)
            {
                var val = BitConverter.ToInt16(audioBytes, i * 2);

                if (val < 0) val += 10000;
                else if (val > 0) val -= 10000;

                pcm[i] = 200f * val / 65536;
            }

            Array.Copy(Fft(pcm), fftReal, fftReal.Length);

            return fftReal;
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
    }
}