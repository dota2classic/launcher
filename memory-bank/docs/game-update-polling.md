# Game Update Polling

## Overview

Issue #167 adds update detection while the launcher stays open.

The launcher now keeps an in-memory verified manifest snapshot for the active game directory and compares it against fresh remote manifests on a timer. This avoids rescanning local files repeatedly.

## Baseline Snapshot

`MainWindowViewModel` owns the current verified local baseline for the session.

- `GameDownloadViewModel` fetches the selected remote package manifests during normal verification.
- After a successful verification/download, it reports the combined remote manifest back to `MainWindowViewModel`.
- That combined manifest becomes the in-memory installed snapshot.

The snapshot is not persisted to disk. It only lives for the current launcher session.

## Periodic Check Flow

`MainWindowViewModel` starts a timer once verification has succeeded, the app is back in `AppState.Launcher`, and a verified snapshot exists.

On each poll:

1. Fetch the latest selected package manifests from CDN.
2. Combine them into one remote manifest.
3. Diff remote vs. the in-memory snapshot with `ManifestDiffService`.
4. If any files would need downloading, mark `update pending`.

No local disk scan happens during this timer-based check.

## UI Behavior

When a remote update is detected:

- `MainLauncherViewModel` sets `IsGameUpdatePending`.
- `GameLaunchViewModel` disables normal launch from idle state and changes the button text to `Обновить`.
- `MainLauncherView` shows a modal asking the user to install the update.
- If Dota is currently running and the user accepts, the launcher stops the game first and then enters the normal verification/download flow.

If verification succeeds again, the snapshot is replaced and the pending-update state is cleared.

## Shared Remote Manifest Loading

Remote CDN manifest loading now lives in `IRemoteManifestService` / `RemoteManifestService` instead of being embedded directly in `GameDownloadViewModel`.

This keeps foreground/background verification and periodic remote-only update polling on the same code path.
