namespace d2c_launcher.Models;

public record DownloadProgress(
    long BytesDownloaded,
    long TotalBytes,
    double SpeedBytesPerSec,
    string CurrentFile,
    int FilesDownloaded,
    int TotalFiles);
