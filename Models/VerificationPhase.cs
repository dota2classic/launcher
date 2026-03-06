namespace d2c_launcher.Models;

public enum VerificationPhase
{
    /// <summary>Waiting for the user to respond to the Windows Defender exclusion modal.</summary>
    AwaitingDefender,

    /// <summary>Fetching the content registry (package list) over HTTP.</summary>
    FetchingManifest,

    /// <summary>Fetching per-package manifests and merging them into one file list.</summary>
    FetchingPackageManifests,

    /// <summary>Scanning local files and computing checksums.</summary>
    ScanningFiles,

    /// <summary>Comparing local vs remote manifest to find missing/changed files.</summary>
    ComputingDiff,

    /// <summary>Downloading missing or changed files.</summary>
    Downloading,

    /// <summary>Running redistributable installers (DirectX, VC++ runtimes).</summary>
    InstallingRedist,

    /// <summary>All phases completed successfully.</summary>
    Complete,

    /// <summary>A phase failed. The user can retry.</summary>
    Failed,
}
