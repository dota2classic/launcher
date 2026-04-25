# D2C Launcher — Agent Instructions

## Memory Bank

At the start of **every session**, read all files in `memory-bank/`:

| File | What it covers |
|------|---------------|
| [`memory-bank/projectbrief.md`](memory-bank/projectbrief.md) | Project identity and scope |
| [`memory-bank/productContext.md`](memory-bank/productContext.md) | Why it exists, user journey, UX goals |
| [`memory-bank/systemPatterns.md`](memory-bank/systemPatterns.md) | Architecture, coding patterns, key files |
| [`memory-bank/techContext.md`](memory-bank/techContext.md) | Tech stack, build commands, dev tools |
| [`memory-bank/activeContext.md`](memory-bank/activeContext.md) | Current focus and recent changes |
| [`memory-bank/progress.md`](memory-bank/progress.md) | Feature status and known gaps |

As you work:
- Update `memory-bank/activeContext.md` when focus shifts or significant progress is made
- Update `memory-bank/progress.md` when features complete or new issues are found
- Add new files to `memory-bank/docs/` when you discover domain knowledge; link them from `CLAUDE.md` Documentation table and from the table in `memory-bank/techContext.md`

---

## Developer Tools

```powershell
# Component preview (no Steam needed) — use powershell, not pwsh
powershell -ExecutionPolicy Bypass -File tools/preview.ps1 <ComponentName>

# HTML screenshot (headless Chrome)
powershell -ExecutionPolicy Bypass -File tools/screenshot-html.ps1 path/to/file.html
```

After UI changes, run preview and use the `Read` tool on the screenshot path to verify visually before declaring done.

Registry: `Preview/PreviewRegistry.cs` — stubs: `Preview/PreviewStubs.cs`

---

## Localization

- Never hardcode Russian strings — use `I18n.T("section.key")` in C# or `{l:T 'section.key'}` in XAML
- Add strings to `Resources/Locales/ru.json`; `Resources/Strings.cs` is legacy — do not add new entries there
- See `memory-bank/docs/localization.md` for full details

---

## Documentation

Technical deep-dives live in `memory-bank/docs/`. When you discover non-trivial domain knowledge, write a new `.md` there and add a row to this table:

| File | Topic |
|------|-------|
| [source-engine-launch.md](memory-bank/docs/source-engine-launch.md) | Source 1 launch mechanics (`-flag` vs `+command`) |
| [source-engine-config-persistence.md](memory-bank/docs/source-engine-config-persistence.md) | `config.cfg`, `FCVAR_ARCHIVE`, `host_writeconfig` |
| [settings-architecture.md](memory-bank/docs/settings-architecture.md) | CvarMapping, BindMapping, SettingsViewModel, adding settings |
| [game-update-manifest.md](memory-bank/docs/game-update-manifest.md) | Manifest format, `exact`/`existing` modes, update flow |
| [client-dll-patching.md](memory-bank/docs/client-dll-patching.md) | Binary patching of `client.dll`, PE layout, sync strategy |
| [preview-workflow.md](memory-bank/docs/preview-workflow.md) | Component preview tool and HTML screenshot tool |
| [release-cycle.md](memory-bank/docs/release-cycle.md) | Release channels, CI workflow, Velopack channel mechanics |
| [ui-style-system.md](memory-bank/docs/ui-style-system.md) | Global style classes, font size tokens, font families |
| [integration-testing-plan.md](memory-bank/docs/integration-testing-plan.md) | Testing strategy, NSubstitute, Avalonia.Headless.XUnit, WireMock |
| [taskbar-icon-investigation.md](memory-bank/docs/taskbar-icon-investigation.md) | Issue #79: taskbar icon blank — what worked (AUMID), what didn't |
| [dota2com-url-suppression.md](memory-bank/docs/dota2com-url-suppression.md) | Issue #81: suppressing dota2.com store panel |
| [codestyle.md](memory-bank/docs/codestyle.md) | C# code style: event handler subscriptions |
| [localization.md](memory-bank/docs/localization.md) | I18n system, `ru.json` structure, webapp sync |

---

## Skill Retrospective

After completing any skill (e.g. `/fix-issue`, `/code-review`), if there were human interventions or course corrections, **post a PR comment** in this format. Skip if the task was straightforward.

```
## Skill retrospective

**What required human input:** <brief description>
**Root cause:** <why it wasn't caught upfront>
**Suggested improvement:**
> <exact instruction to add or change>
**Reasoning:** <why this prevents recurrence>
```

---

## Do Not

- Edit `Generated/DotaclassicApiClient.g.cs` manually — regenerate from the OpenAPI spec
- Add platform-specific code for non-Windows targets — this is Windows-only
- Commit secrets or tokens
- Use `Task.Result` or `.Wait()` — always `await` async methods
- Register new ViewModels as singletons — they should be transient or manually created
