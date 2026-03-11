# UI Style System

This document covers the shared visual language for the launcher's panel blocks and other reusable style primitives.

---

## Panel Blocks

Every content panel (chat, party, game search, profile stats, etc.) is built from three composable classes defined globally in `App.axaml`.

### Classes

| Class | Element | What it does |
|-------|---------|--------------|
| `Block` | `Border` | Outer panel container — dark background, border |
| `BlockHead` | `Border` | 36px header bar at the top of a block |
| `BlockTitle` | `TextBlock` | Header label text inside `BlockHead` |

### Token values

| Token | Value |
|-------|-------|
| Block background | `#13171D` |
| Block border | `#252E3A` / 1px |
| BlockHead background | `#0A0C0F` |
| BlockHead bottom border | `#252E3A` / 1px |
| BlockHead padding | `14,0` |
| BlockTitle font size | `FontSizeXS` (10) |
| BlockTitle weight | `SemiBold` |
| BlockTitle letter-spacing | `2` |
| BlockTitle foreground | `#D9D9D9` |

### Usage

```xml
<Border Classes="Block">
    <Grid RowDefinitions="36,*">

        <!-- Header -->
        <Border Grid.Row="0" Classes="BlockHead">
            <TextBlock Classes="BlockTitle" Text="МОЙ БЛОК"/>
        </Border>

        <!-- Content -->
        <StackPanel Grid.Row="1" Margin="14,12">
            ...
        </StackPanel>

    </Grid>
</Border>
```

The header can contain extra elements alongside the title (right-aligned link, action button, stats text, etc.) by wrapping in a `Grid`:

```xml
<Border Classes="BlockHead">
    <Grid ColumnDefinitions="*,Auto">
        <TextBlock Classes="BlockTitle" Text="ЛУЧШИЕ ГЕРОИ"/>
        <TextBlock Grid.Column="1" Text="Показать все →" Foreground="#4A9EE0"
                   FontSize="11" VerticalAlignment="Center" Cursor="Hand"/>
    </Grid>
</Border>
```

### Current usages

| Panel | File |
|-------|------|
| ЧАТ | `Views/Components/ChatPanel.axaml` |
| ГРУППА | `Views/Components/PartyPanel.axaml` |
| ПОИСК ИГРЫ | `Views/Components/GameSearchPanel.axaml` |
| ОТЗЫВЫ, СТАТИСТИКА ЗА СЕЗОН, ЛУЧШИЕ ГЕРОИ | `Views/Components/ProfilePanel.axaml` |

---

## Font Sizes

Defined as `x:Double` resources in `App.axaml` and referenced via `{DynamicResource ...}`.

| Key | Value |
|-----|-------|
| `FontSizeXS` | 10 |
| `FontSizeSM` | 11 |
| `FontSizeBase` | 12 |
| `FontSizeMD` | 13 |
| `FontSizeLG` | 14 |
| `FontSizeXL` | 16 |
| `FontSize2XL` | 18 |
| `FontSize3XL` | 22 |
| `FontSize4XL` | 28 |

---

## Font Families

| Key | Usage |
|-----|-------|
| `NotoSans` | Default UI font (set globally on all `TextBlock` and `TemplatedControl`) |
| `TrajanPro3` | Reserved for single full-screen "hero" titles only — the big centered word or phrase on loading/download/setup screens (e.g. "DOTACLASSIC" on `GameDownloadView`, `LoadingView`, `SelectGameView`, `LaunchSteamFirstView`). Minimum font size ~22px, one prominent text element per screen. |

### TrajanPro3 — when to use vs. when not to use

**Use TrajanPro3:**
- One large display title on a full-screen transition view (loading, game download, select game, Steam offline)
- The text must be the visual focal point of the entire screen, not one item among many

**Do NOT use TrajanPro3:**
- Stat values or labels inside panels (MMR, win rate, KDA, kills, etc.)
- Player names or avatar initials
- Sub-tab labels or navigation text
- Table headers or column labels
- Any context where multiple text elements share the same visual weight

Everything in panels, stats, names, tabs, and tables uses the default `NotoSans`.

---

## Modal Header

All overlay modals use the shared `ModalHeader` component (`Views/Components/ModalHeader.axaml`) for their title bar. Do **not** hand-roll modal headers.

### Visual tokens

| Property | Value |
|----------|-------|
| Background | `#0c0f13` |
| Bottom border | `#222830` / 1px |
| Height | `48px` |
| Title font size | `FontSizeBase` (12) |
| Title weight | `Bold` |
| Title letter-spacing | `3` |
| Title foreground | `#d9dde0` |
| Title casing | ALL CAPS |
| Close `✕` font size | `FontSizeLG` (14) |
| Close `✕` foreground | `#606870` |

### Usage

```xml
<components:ModalHeader Title="МОЙ МОДАЛ"/>
```

The component raises `ModalHeader.CloseRequestedEvent` (bubbling) when the user clicks `✕`. Wire it up in the nearest parent that owns the close logic:

```csharp
// In constructor or InitializeComponent post-init:
InviteModalPanel.AddHandler(ModalHeader.CloseRequestedEvent, OnCloseInviteModal);
```

Or, if a wrapper component (like `SettingsPanel`) intercepts and re-raises its own event:

```csharp
AddHandler(ModalHeader.CloseRequestedEvent, (s, e) => {
    e.Handled = true;
    RaiseEvent(new RoutedEventArgs(CloseRequestedEvent, this));
});
```

### Current usages

| Modal | Title | File |
|-------|-------|------|
| Настройки | `НАСТРОЙКИ` | `Views/Components/SettingsPanel.axaml` |
| Пригласить игрока | `ВЫБРАТЬ ИГРОКА` | `Views/MainLauncherView.axaml` |

---

## Sub-tab Bar (Profile page)

Scoped to `ProfilePanel.axaml` — not global.

| Class | Element | Notes |
|-------|---------|-------|
| `SubTab` | `Border` | Tab item container, `Hand` cursor |
| `SubTabActive` | `Border` | Adds `#D4A843` bottom underline |
| `SubTabText` | `TextBlock` | Muted label (`#556070`, NotoSans) |
| `SubTabTextActive` | `TextBlock` | Active label color (`#D4A843`) |
