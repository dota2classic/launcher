# Code Review — 2026-03-12

**Scope:** Last 4 commits — settings-as-modal refactor (`a7ad1c0`→`98a83d1`)

---

## Needs Attention (5 issues)

**1. [Performance] Unsubscribed event handlers on singleton services — `MainLauncherViewModel.cs:135,149,163,187`**
`queueSocketService.OnlineUpdated`, `_steamManager.OnUserUpdated`, `_cvarProvider.CvarChanged`, and `SocketSoundCoordinator` are all wired up but never torn down in `Dispose()`. Since these services are singletons that outlive the VM, if the VM is ever recreated the old instance can't be GC'd and handlers fire on stale objects.

**2. [Performance] `PropertyChanged` unsubscription targets new VM, not old — `MainLauncherView.axaml.cs:27-30`**
```csharp
if (_vmPropertyChangedHandler != null && DataContext is MainLauncherViewModel oldVm)
```
At this point `DataContext` is already the **new** value. The unsubscription silently does nothing on the old VM.

**3. [Quality] `Width="500" Height="500"` hardcoded on `SettingsPanel` — `MainLauncherView.axaml:70-73`**
Fixed pixel size bypasses `ModalOverlay`'s own `MinWidth`/`MaxWidth` constraints and ignores the `UiScale` setting the user can change inside that very panel. Remove both; let the overlay's `Border` own sizing constraints (add `MaxHeight` there if needed).

**4. [Code] `OpenSettings()` bypasses video/DLC refresh side-effects in `ToggleSettings()` — `MainLauncherViewModel.cs:242-244`**
Any call to `OpenSettings()` directly will show a stale settings panel (skips `LoadFromVideoTxt`, `RefreshFromVideoProvider`, `LoadDlcPackagesAsync`). Consolidate into one open path or remove `OpenSettings()` as dead API.

**5. [Security/Reliability] Fire-and-forget tasks swallow exceptions silently — `MainLauncherViewModel.cs:132,158,160,189,239`**
`_ = RefreshInGameCountAsync()` in a timer tick, `_ = Party.RefreshPartyAsync()`, etc. have no exception handling. A transient network error will become an unobserved exception every 5 seconds.

---

## Suggestions (7 items)

**1. [UX] Escape key doesn't close modals — `ModalOverlay`** *(HIGH impact, LOW effort)*
Add a `KeyDown` handler for `Key.Escape` → invoke `CloseCommand`. Standard UX expectation, missing entirely.

**2. [Quality] Extract invite modal content to `InvitePanel` component** *(MED impact, MED effort)*
Settings uses a proper `SettingsPanel` component; invite modal dumps 20 lines of inline XAML including a code-behind click handler in `MainLauncherView`. Inconsistent and untestable in preview mode.

**3. [Code] `CloseSettingsRelay` naming is a workaround smell** *(LOW impact, LOW effort)*
Make `CloseSettings()` private and let CommunityToolkit generate `CloseSettingsCommand` naturally. Removes the `CloseSettings` + `CloseSettingsRelay` double-naming.

**4. [Quality] Backdrop dismiss fires on any mouse button, not just left-click — `ModalOverlay.axaml.cs:50`** *(LOW impact, LOW effort)*
Add `if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;`. Also: `e.Handled = true` should be inside the `if` block, not unconditional.

**5. [Style] `SettingsPanel` styles are local, not global** *(MED impact, HIGH effort)*
Named classes like `section-label`, `setting-label`, `dir-btn` in `UserControl.Styles` should live in `App.axaml` per project style conventions. Current state means they can't be reused.

**6. [Code] `LoggedInAsText` uses English in a Russian-only UI — `MainLauncherViewModel.cs:81`** *(LOW impact, LOW effort)*
`"Logged in as: "` and `"Steam offline or not logged in."` — translate to Russian.

**7. [Perf] Initial `AvatarImage` bitmap never disposed — `MainLauncherViewModel.cs:110`** *(LOW impact, LOW effort)*
`Dispose()` only disposes the updated avatar, not the one set at construction if `OnUserUpdated` never fires.

---

## All Clear

- **Tests:** 195 passed, 0 failed
- **Build:** 0 errors, 0 warnings
- **Linter:** Clean
- **Broken references:** None — `LauncherTab.Settings`, `IsSettingsTabActive`, and old `NavigateTo(Settings)` calls fully removed
- **Change atomicity:** Well-scoped; the ModalOverlay abstraction is immediately justified by 2 uses

---

## Verdict: Needs Attention

Fix items 2 (`PropertyChanged` unsubscribes wrong VM) and 3 (`Width/Height` hardcode) before shipping — they're easy one-liners. Items 1 (event leak) and 5 (fire-and-forget) are pre-existing issues that the refactor didn't introduce but surfaced during review — worth tracking.
