# Release Cycle

## Channels

The launcher uses two Velopack release channels hosted on GitHub Releases:

| Channel | Trigger | GitHub Release | Audience |
|---------|---------|----------------|----------|
| `nightly` | Every push to `master` | Pre-release, tag `nightly` (overwritten each time) | Testers |
| `win` (stable) | Manual `v*.*.*` tag push | Versioned release | All users |

## Nightly Builds

Every merged commit to `master` automatically produces a nightly build:

- Packed with `--channel nightly`, version `0.0.{github.run_number}`
- Overwrites the single `nightly` pre-release on GitHub
- Users with `NightlyUpdates: true` in their settings receive these updates automatically

## Stable Releases

Stable releases are triggered manually:

```bash
git tag v1.2.3
git push origin v1.2.3
```

The CI workflow:
1. Builds and publishes `d2c-launcher` + `SteamBridge`
2. Packs with Velopack (stable channel, version from tag)
3. Creates a versioned GitHub Release via `vpk upload github`

## Opting Into Nightly Updates

Set `"NightlyUpdates": true` in `%APPDATA%\d2c-launcher\launcher_settings.json`.

The launcher will then use `ExplicitChannel = "nightly"` and `AllowVersionDowngrade = true`,
so it follows the nightly channel regardless of which channel it was originally installed from.

Default is `false` — all users get stable updates.

## How Velopack Channels Work

- The channel is embedded into the app at pack time (`vpk pack --channel nightly`)
- By default, `UpdateManager` searches for `releases.{channel}.json` matching the installed channel
- `UpdateOptions.ExplicitChannel` overrides this at runtime (used for the nightly opt-in setting)
- Nightly versions (`0.0.x`) are always lower than any stable version (`1.x.x`),
  so `AllowVersionDowngrade = true` is required when switching stable → nightly

## CI Workflow Location

`.github/workflows/build.yml`
