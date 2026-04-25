# Active Context

## Current State

All major features are shipped. The launcher is in maintenance/polish mode.

Repository AI workflow files use a shared `.agents/` layout. `.agents/commands/` and `.agents/agents/` are the canonical copies; `.claude/commands` and `.claude/agents` are Windows directory junctions to them.

---

## Recently Completed

| Issue | What was done |
|-------|--------------|
| #176 | Notification sound volume setting |
| #172 | Detect pending remote game updates (#167 follow-up) |
| #170 | Suppress stale ready-check timeout modal |
| #166 | Background startup |
| #165 | Clear vulnerable package warnings |
| #169 | Ready-check decline false positive — explicit `PLAYER_DECLINE_GAME` socket event with `DECLINED`/`TIMEOUT`; modal shown only for `TIMEOUT` |
| #167 | Remote game updates while launcher stays open — in-memory verified manifest snapshot; 3-minute remote poll; launch gated; native toast on update pending |
| #155 | Native Windows matchmaking toasts — actionable toast buttons for party invite and ready check |
| #148 | Streams tab — polls `/v1/stats/twitch`; auto-hides when empty |
| #164 | Build/test warning cleanup |
| #69  | Auto-launch on Windows startup with `--background-start`; background launch stays hidden in tray |

---

## Next Steps / Open Issues

| Issue | Title |
|-------|-------|
| #22 | Steam not being detected — root cause unknown |
| #21 | Setting autorepeat doesn't work — may not be a cvar bug |
| #18 | Parallelize local file scan + remote manifest load |
| #13 | Support chat scrolling |
| #8  | Research crash dump analysis |
| #7  | Handling game crashes |

---

## Known Technical Debt

| Item | Notes |
|------|-------|
| `FakeQueueSocketService` | Planned for integration tests; `FakeSteamManager` is done |
| `QueueSocketService.Dispose()` blocks UI | `Task.Run+.Wait` up to 2s |
| Chat thread ID hardcoded | `"17aa3530-d152-462e-a032-909ae69019ed"` in `ChatViewModel` |
| Keybind settings UI | `config.cfg` bind lines parsed but not exposed in UI |
| Extra launch args UI | `ExtraArgs` field in model; no UI |
