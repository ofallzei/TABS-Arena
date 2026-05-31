using System;
using System.IO;
using System.Media;
using System.Windows;

namespace TABS
{
    internal static class AudioFeedback
    {
        private const string ButtonClickResourcePath = "Assets/mixkit-modern-technology-select-3124.wav";

        private static readonly object SyncRoot = new object();
        private static SoundPlayer _buttonClickPlayer;
        private static byte[] _sourceWavBytes;
        private static double _cachedVolume = -1.0;

        public static void PlayButtonClick()
        {
            if (!AppPrefs.SoundsEnabled || AppPrefs.SoundVolume <= 0.0)
                return;

            try
            {
                lock (SyncRoot)
                {
                    double volume = Clamp(AppPrefs.SoundVolume, 0.0, 1.0);
                    if (_buttonClickPlayer == null || Math.Abs(_cachedVolume - volume) > 0.001)
                        _buttonClickPlayer = CreateButtonClickPlayer(volume);

                    _buttonClickPlayer.Stop();

                    if (_buttonClickPlayer.Stream != null)
                        _buttonClickPlayer.Stream.Position = 0;

                    _buttonClickPlayer.Play();
                }
            }
            catch
            {
                // Sound is optional feedback; button actions should keep working if audio is unavailable.
            }
        }

        public static void RefreshVolume()
        {
            lock (SyncRoot)
            {
                _buttonClickPlayer = null;
                _cachedVolume = -1.0;
            }
        }

        private static SoundPlayer CreateButtonClickPlayer(double volume)
        {
            if (_sourceWavBytes == null)
                _sourceWavBytes = LoadButtonClickWav();

            byte[] wavBytes = ScalePcm16Wav(_sourceWavBytes, volume);
            var stream = new MemoryStream(wavBytes);

            var player = new SoundPlayer(stream);
            player.Load();
            _cachedVolume = volume;
            return player;
        }

        private static byte[] LoadButtonClickWav()
        {
            var resource = Application.GetResourceStream(new Uri(ButtonClickResourcePath, UriKind.Relative));
            if (resource == null)
                throw new FileNotFoundException("Button click sound resource was not found.", ButtonClickResourcePath);

            using (var stream = new MemoryStream())
            {
                resource.Stream.CopyTo(stream);
                return stream.ToArray();
            }
        }

        private static byte[] ScalePcm16Wav(byte[] source, double volume)
        {
            byte[] copy = new byte[source.Length];
            Buffer.BlockCopy(source, 0, copy, 0, source.Length);

            if (volume >= 0.999)
                return copy;

            int dataOffset;
            int dataSize;
            if (!TryFindPcm16Data(copy, out dataOffset, out dataSize))
                return copy;

            int dataEnd = Math.Min(copy.Length, dataOffset + dataSize);
            for (int i = dataOffset; i + 1 < dataEnd; i += 2)
            {
                short sample = BitConverter.ToInt16(copy, i);
                int scaled = (int)Math.Round(sample * volume);
                scaled = Math.Max(short.MinValue, Math.Min(short.MaxValue, scaled));

                copy[i] = (byte)(scaled & 0xFF);
                copy[i + 1] = (byte)((scaled >> 8) & 0xFF);
            }

            return copy;
        }

        private static bool TryFindPcm16Data(byte[] wav, out int dataOffset, out int dataSize)
        {
            dataOffset = 0;
            dataSize = 0;

            if (wav.Length < 44 ||
                ReadAscii(wav, 0, 4) != "RIFF" ||
                ReadAscii(wav, 8, 4) != "WAVE")
                return false;

            bool isPcm16 = false;
            int index = 12;
            while (index + 8 <= wav.Length)
            {
                string chunkId = ReadAscii(wav, index, 4);
                int chunkSize = BitConverter.ToInt32(wav, index + 4);
                int chunkData = index + 8;

                if (chunkSize < 0 || chunkData + chunkSize > wav.Length)
                    return false;

                if (chunkId == "fmt " && chunkSize >= 16)
                {
                    short format = BitConverter.ToInt16(wav, chunkData);
                    short bitsPerSample = BitConverter.ToInt16(wav, chunkData + 14);
                    isPcm16 = format == 1 && bitsPerSample == 16;
                }
                else if (chunkId == "data")
                {
                    dataOffset = chunkData;
                    dataSize = chunkSize;
                    return isPcm16;
                }

                index = chunkData + chunkSize + (chunkSize % 2);
            }

            return false;
        }

        private static string ReadAscii(byte[] bytes, int offset, int count)
        {
            return System.Text.Encoding.ASCII.GetString(bytes, offset, count);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}
