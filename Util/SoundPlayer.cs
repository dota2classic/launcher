using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform;
using NAudio.Wave;

namespace d2c_launcher.Util;

public static class SoundPlayer
{
    public static void Play(string fileName)
    {
        Task.Run(() =>
        {
            try
            {
                var uri = new Uri($"avares://d2c-launcher/Assets/Sounds/{fileName}");
                using var assetStream = AssetLoader.Open(uri);

                // NAudio needs a seekable stream; copy to MemoryStream if needed.
                Stream stream = assetStream.CanSeek
                    ? assetStream
                    : CopyToMemoryStream(assetStream);

                using (stream)
                {
                    WaveStream reader = Path.GetExtension(fileName).ToLowerInvariant() switch
                    {
                        ".mp3" => new Mp3FileReader(stream),
                        ".wav" => new WaveFileReader(stream),
                        _ => throw new NotSupportedException($"Unsupported audio format: {fileName}")
                    };

                    using (reader)
                    using (var output = new WaveOutEvent())
                    {
                        output.Init(reader);
                        output.Play();
                        while (output.PlaybackState == PlaybackState.Playing)
                            Thread.Sleep(50);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Error($"[SoundPlayer] Failed to play '{fileName}'", ex);
            }
        });
    }

    private static MemoryStream CopyToMemoryStream(Stream source)
    {
        var ms = new MemoryStream();
        source.CopyTo(ms);
        ms.Position = 0;
        return ms;
    }
}
