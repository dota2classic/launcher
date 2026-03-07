using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace d2c_launcher.Models;

public sealed partial class MatchmakingModeView : ObservableObject
{
    public int ModeId { get; }
    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private int _inQueue;

    [ObservableProperty]
    private string? _restrictionText;

    public bool IsRestricted => !string.IsNullOrEmpty(RestrictionText);

    partial void OnRestrictionTextChanged(string? value) => OnPropertyChanged(nameof(IsRestricted));

    /// <summary>Abandoning the game results in a penalty (shows shield icon).</summary>
    public bool PenaltyAbandon { get; }
    /// <summary>Empty slots are filled with bots (shows robot icon).</summary>
    public bool FillBots { get; }
    /// <summary>Items may drop after a game (shows trophy icon).</summary>
    public bool HasDrops { get; }
    /// <summary>Game can be left without penalty — opposite of PenaltyAbandon (shows door icon).</summary>
    public bool NoPenaltyAbandon => !PenaltyAbandon;

    public string FlagsTooltip
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            if (PenaltyAbandon)  parts.Add("Покидание игры ведёт к штрафу");
            else                 parts.Add("Можно покинуть игру без штрафа");
            if (HasDrops)        parts.Add("Возможен дроп предметов");
            if (FillBots)        parts.Add("Пустые слоты заполняются ботами");
            return string.Join("\n", parts);
        }
    }

    public MatchmakingModeView(int modeId, string name, bool isSelected = false)
    {
        ModeId = modeId;
        Name = name;
        _isSelected = isSelected;
        (PenaltyAbandon, FillBots, HasDrops) = modeId switch
        {
            1  => (true,  false, true),   // Unranked 5x5
            8  => (true,  false, true),   // Highroom 5x5
            13 => (false, true,  true),   // Turbo
            7  => (false, true,  false),  // Bots
            _  => (false, false, false),
        };
    }

    [RelayCommand]
    private void ToggleSelection()
    {
        if (!IsRestricted)
            IsSelected = !IsSelected;
    }
}
