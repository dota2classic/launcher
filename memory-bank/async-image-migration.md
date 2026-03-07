# Async Image Migration Plan

## Goal
Replace all manual `Bitmap?`-based avatar loading with `AsyncImageLoader.Avalonia`'s
`ImageLoader.SetSource` / `asyncImageLoader:ImageLoader.Source` attached property.

Package: `AsyncImageLoader.Avalonia` v3.7.0 (already in csproj).
Built-in RAM caching via `RamCachedWebImageLoader` (default).

## Pattern

**Before (manual):**
- Model holds `Bitmap? AvatarImage` (ObservableProperty)
- ViewModel fetches URL → `HttpImageService.LoadBitmapAsync` → sets `AvatarImage`
- XAML: `<Image Source="{Binding AvatarImage}"/>` with null visibility toggle

**After (AsyncImageLoader):**
- Model holds `string? AvatarUrl` (plain get-only property)
- ViewModel sets URL at construction time — no async fetch needed
- XAML: `<Image asyncImageLoader:ImageLoader.Source="{Binding AvatarUrl}"/>`, no visibility toggle
- Initials fallback circle sits behind the Image in a Panel — shows while loading, covered when image arrives

**XAML namespace:**
```xml
xmlns:asyncImageLoader="clr-namespace:AsyncImageLoader;assembly=AsyncImageLoader.Avalonia"
```

For `ImageBrush` (ellipse avatars), use `asyncImageLoader:ImageBrushLoader.Source="{Binding AvatarUrl}"`.

---

## All sites: DONE ✅

| Site | Status |
|------|--------|
| `ChatPanel.axaml` / `ChatMessageView` / `ChatViewModel` | ✅ Done |
| `PartyPanel.axaml` / `PartyMemberView` / `PartyViewModel` / `BackendApiService` | ✅ Done |
| `AcceptGameModal.axaml` / `RoomPlayerView` / `RoomViewModel` / `BackendApiService` | ✅ Done |
| `NotificationArea.axaml` / `PartyInviteNotificationViewModel` / `NotificationAreaViewModel` | ✅ Done |
| `InviteModal` / `InviteCandidateView` / `BackendApiService` / `InviteModalPreviewControl.axaml` / `MainLauncherView.axaml` | ✅ Done |

---

## What STAYS as Bitmap (cannot use AsyncImageLoader)

| Site | Reason |
|------|--------|
| `MainLauncherViewModel.AvatarImage` | Loaded from Steam SDK RGBA bytes via `SteamAvatarHelper.FromUser(u)`, not a URL |
| `LauncherHeader.axaml` | Bound to above |
| `MainLauncherView.axaml` (profile section) | Same source |

---

## Cleanup completed

- `TryLoadAvatarAsync` removed from `BackendApiService`
- `LoadInviteAvatarsAsync` removed from `BackendApiService`
- `LoadBitmapAsync` removed from `IHttpImageService` / `HttpImageService` / `StubHttpImageService`
- `IHttpImageService` now only has `LoadBytesAsync` (used by `ChatViewModel` for emoticon GIF bytes)
- `using Avalonia.Media.Imaging` removed from `BackendApiService`, `IBackendApiService`, `PreviewStubs`, `PartyMemberView`, `RoomPlayerView`, `InviteCandidateView`, `PartyInviteNotificationViewModel`, `RoomViewModel`

---

## Preview tool path fix (done)

Both `tools/preview.ps1` and `tools/screenshot.ps1` were fixed to use
`bin\Debug\net10.0-windows\d2c-launcher.exe` (was incorrectly `net10.0`).
Screenshot delay bumped to 3000ms to allow async images to load.
