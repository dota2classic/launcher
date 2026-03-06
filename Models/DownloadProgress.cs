namespace d2c_launcher.Models;

public record DownloadProgress(
    long BytesDownloaded,
    long TotalBytes,
    double SpeedBytesPerSec,
    string CurrentFile,
    string CurrentPackageName,
    int FilesDownloaded,
    int TotalFiles);
