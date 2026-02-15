using System.Diagnostics;
using System.IO;

if (args.Length < 2)
    return 2;

var targetPath = args[0];
var workingDirectory = args[1];
var launchArguments = args.Length > 2 ? args[2] : string.Empty;

if (string.IsNullOrWhiteSpace(targetPath) || !File.Exists(targetPath))
    return 3;

if (string.IsNullOrWhiteSpace(workingDirectory) || !Directory.Exists(workingDirectory))
    return 4;

var psi = new ProcessStartInfo
{
    FileName = targetPath,
    WorkingDirectory = workingDirectory,
    UseShellExecute = true
};

if (!string.IsNullOrWhiteSpace(launchArguments))
    psi.Arguments = launchArguments;

Process.Start(psi);
return 0;
