using System;
using CommunityToolkit.Mvvm.Input;

namespace d2c_launcher.ViewModels;

/// <summary>
/// Toast shown when dota.exe is missing (likely deleted by antivirus).
/// Offers a button to re-run game verification.
/// </summary>
public sealed partial class CorruptedFilesToastViewModel : NotificationViewModel
{
    public string Message { get; } = "Файлы игры повреждены или удалены антивирусом.";

    public RelayCommand VerifyCommand { get; }

    public CorruptedFilesToastViewModel(Action onVerify)
        : base(displaySeconds: 15)
    {
        VerifyCommand = new RelayCommand(() =>
        {
            ForceClose();
            onVerify();
        });
    }
}
