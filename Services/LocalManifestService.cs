using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using d2c_launcher.Models;

namespace d2c_launcher.Services;

public class LocalManifestService : ILocalManifestService
{
    private static readonly int Parallelism = Math.Min(Environment.ProcessorCount, 8);

    public async Task<GameManifest> BuildAsync(
        string gameDirectory,
        IProgress<(int done, int total)>? progress = null,
        CancellationToken ct = default)
    {
        var root = new DirectoryInfo(gameDirectory);
        var allFiles = root.GetFiles("*", SearchOption.AllDirectories);
        var files = new GameManifestFile[allFiles.Length];
        var done = 0;

        await Parallel.ForEachAsync(
            Enumerable(allFiles),
            new ParallelOptions { MaxDegreeOfParallelism = Parallelism, CancellationToken = ct },
            (item, _) =>
            {
                var (file, index) = item;
                var relativePath = Path.GetRelativePath(gameDirectory, file.FullName)
                    .Replace('\\', '/');

                files[index] = new GameManifestFile
                {
                    Path = relativePath,
                    Hash = ComputeMd5(file.FullName),
                    Size = file.Length,
                    Mode = "exact",
                };

                var d = Interlocked.Increment(ref done);
                progress?.Report((d, allFiles.Length));
                return ValueTask.CompletedTask;
            });

        return new GameManifest { Files = [..files] };
    }

    private static System.Collections.Generic.IEnumerable<(FileInfo, int)> Enumerable(FileInfo[] files)
    {
        for (var i = 0; i < files.Length; i++)
            yield return (files[i], i);
    }

    private static string ComputeMd5(string filePath)
    {
        using var stream = new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072);
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(stream)).ToLowerInvariant();
    }
}
