# Localization

All UI strings are stored in `Resources/Locales/ru.json` and accessed via the `I18n` static class.

## File structure

```
Resources/
  Locales/
    ru.json    ← primary locale; synced structure matches webapp i18next files
  Strings.cs   ← thin C# wrapper; all properties delegate to I18n.T()
Services/
  I18n.cs      ← loads JSON embedded resource, provides T() lookup
Util/
  TExtension.cs ← Avalonia MarkupExtension for {l:T 'key'} in XAML
```

## JSON format

Nested i18next-compatible structure. Dot notation is used for lookup keys.

```json
{
  "achievement": {
    "winStreak10": {
      "title": "Стрикер",
      "description": "Победить {cp} игр подряд"
    }
  },
  "notifications": {
    "achievementComplete": "Достижение получено!"
  }
}
```

Placeholder tokens use `{name}` syntax (converted from the webapp's `<cp />` JSX components).

## `I18n.T()` — C# lookup

```csharp
// Simple lookup
I18n.T("notifications.achievementComplete")  // → "Достижение получено!"

// Named placeholder substitution
I18n.T("achievement.winStreak10.description", ("cp", 10))  // → "Победить 10 игр подряд"

// Missing key → returns the key itself (safe fallback)
I18n.T("some.missing.key")  // → "some.missing.key"
```

## `{l:T}` — XAML markup extension

Add the namespace to any AXAML file:

```xml
xmlns:l="clr-namespace:d2c_launcher.Util"
```

Then use in place of a string literal:

```xml
<TextBlock Text="{l:T 'notifications.achievementComplete'}"/>
```

Note: the extension resolves at parse time, so it is suitable for static strings only (no runtime locale switching).

## `Strings.cs` — legacy call sites

Existing `{x:Static res:Strings.X}` XAML bindings and C# references continue to work unchanged — each property simply delegates to `I18n.T()`:

```csharp
// Before
public static string AchievementUnlocked => "Достижение получено!";

// After
public static string AchievementUnlocked => I18n.T("notifications.achievementComplete");
```

## Adding a new string

1. Add the key to `Resources/Locales/ru.json` in the appropriate section.
2. Access it in C# via `I18n.T("section.key")`, or add a property to `Strings.cs` if it is used from XAML via `{x:Static}`.

## Syncing with the webapp

The `achievement.*` section mirrors the webapp's `i18n/ru/achievement_mapping.json`.
The key names and nesting match exactly so the files can be diff'd or copied directly.
Webapp uses `<cp />` JSX component interpolation — store as `{cp}` in `ru.json`.

## `{cp}` and numeric placeholders

Descriptions that vary by progress checkpoint (e.g. "Победить {cp} игр подряд") store `{cp}` in the JSON.
When the value is known at call time, pass it as a named arg:

```csharp
I18n.T("achievement.winStreak10.description", ("cp", progress))
```

For achievement toast notifications, the backend already substitutes checkpoint values in `notification.Content`, so the raw API string is used for the description and only the title is looked up locally.

## Legacy `string.Format` call sites

Some older strings contain `{0}` / `{1}` positional format tokens (e.g. `game.goQueueTitle`, `main.stepFormat`). These are left as-is in the JSON. Call sites continue to use `string.Format(Strings.Key, ...)` — no change needed.
