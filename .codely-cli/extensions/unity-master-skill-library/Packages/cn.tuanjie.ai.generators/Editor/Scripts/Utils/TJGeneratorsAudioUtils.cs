#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace TJGenerators.Utils
{
    /// <summary>
    /// 音频资产工具方法：创建合法 WAV 占位文件并导入为 AudioClip。
    /// </summary>
    public static class TJGeneratorsAudioUtils
    {
        /// <summary>
        /// 在指定路径创建最短合法静音 WAV 并导入为 AudioClip，避免 Unity/FSBTool 将零长度或零采样视为无效。
        /// 生成时后端返回文生音频多为 MP4、音效多为 MP3 等，将保存到同基名、对应扩展名的文件。
        /// </summary>
        public static string CreateBlankAudioClip(string path)
        {
            path = Path.ChangeExtension(path, ".wav");
            string absolutePath = PathUtils.ToAbsoluteAssetPath(path);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            byte[] wavBytes = CreateShortestValidWav();
            File.WriteAllBytes(absolutePath, wavBytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            // Refresh ensures the asset is fully registered before callers call LoadAssetAtPath
            AssetDatabase.Refresh();
            TJGeneratorsGenerationLabel.EnableLabel(TJGeneratorsAssetReference.FromPath(path));
            return path;
        }

        /// <summary>
        /// 生成最短合法 WAV：44 字节头 + 最少静音采样（若干 16-bit 0），满足 FSBTool 非零长度要求。
        /// </summary>
        private static byte[] CreateShortestValidWav()
        {
            const int sampleRate = 44100;
            const short numChannels = 1;
            const short bitsPerSample = 16;
            int byteRate = sampleRate * numChannels * (bitsPerSample / 8);
            short blockAlign = (short)(numChannels * (bitsPerSample / 8));
            const int numSamples = 256;
            int dataSize = numSamples * numChannels * (bitsPerSample / 8);
            int chunkSize = 36 + dataSize;
            int totalSize = 44 + dataSize;
            var buffer = new byte[totalSize];
            int offset = 0;
            void Write(byte[] src) { for (int i = 0; i < src.Length; i++) buffer[offset++] = src[i]; }
            void WriteLe(int value, int bytes)
            {
                for (int i = 0; i < bytes; i++) { buffer[offset++] = (byte)(value & 0xFF); value >>= 8; }
            }
            Write(new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
            WriteLe(chunkSize, 4);
            Write(new byte[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            Write(new byte[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
            WriteLe(16, 4);
            WriteLe(1, 2);
            WriteLe(numChannels, 2);
            WriteLe(sampleRate, 4);
            WriteLe(byteRate, 4);
            WriteLe(blockAlign, 2);
            WriteLe(bitsPerSample, 2);
            Write(new byte[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
            WriteLe(dataSize, 4);
            for (int i = 0; i < dataSize; i++)
                buffer[offset++] = 0;
            return buffer;
        }
    }
}
#endif
