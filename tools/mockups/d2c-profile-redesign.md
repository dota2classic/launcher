# Redesign: Profile Page (v4)

## Summary

Redesign the current `ProfileView` to match the v4 mockup. The goal is a clean, readable layout with consistent block components, bright text, and a sticky player header.

See attached mockup: `d2c-profile-v4.html`

---

## New Layout

### Player Header
A persistent bar at the top of the profile content (below the nav tabs) containing:
- Player avatar with online dot
- Username and total match count
- 4 stat chips: **Матчи** (W–L–0), **Доля побед**, **Рейтинг**, **Ранг**

### Profile Sub-Tabs
A tab row directly below the hero header: Общее · Матчи · Достижения · Союзники · Герои · Рекорды

Active tab has a **gold underline**. Inactive tabs are dim. Same style as other tab rows in the app.

### Content: 3 Equal Columns

The "Общее" tab shows one row of 3 equal-width blocks side by side:

| Отзывы | Статистика за сезон | Лучшие герои |
|---|---|---|
| Radar chart | Season stat rows | Hero table with winrate bars |

---

## Block Component

Every block must use the **same consistent structure** matching all other panels in the app:

- **Header bar**: dark background (`#0A0C0F`), `36px` tall, `1px` bottom border, uppercase Cinzel label left-aligned. Optional right-aligned link (e.g. "Показать все →" in blue).
- **Body**: card background (`#13171D`), `1px` border on all sides.
- **No rounded corners** anywhere.

This must look identical in style to the "ЧАТ", "ГРУППА", "ПОИСК ИГРЫ" blocks in the main launcher view.

---

## Text Colors

| Use | Color |
|---|---|
| Primary values, names | `#E2E6EA` — bright, always readable |
| Secondary labels ("Убийств", "Матчи") | `#99A8B4` — medium |
| Column headers, hints | `#556070` — dim |
| Gold accent values (KDA, rating) | `#D4A843` |
| Green (winrate high, online) | `#52B847` |
| Blue (hero names, links) | `#4A9EE0` |
| Red (losses, low winrate) | `#D04535` |

---

## Block Details

### Отзывы
- Pentagon radar chart, centered in the block
- 5 axes: Оптимист, Добряк, Болтун, Клоун, Токсик
- Grid rings at 33% / 66% / 100% in `#252E3A`
- Data polygon: gold fill + gold stroke
- Each axis dot is gold except Токсик which is red
- Axis labels in `#99A8B4`

### Статистика за сезон
- List of stat rows: large Cinzel value on the left, label on the right
- Values: Убийств / Помощи = gold, Смертей = blue, Доля побед = green, Покинутых игр = dim
- A `1px` horizontal divider separates KDA stats (top 3) from general stats (bottom 4)
- Each row has a `1px` bottom border in `#181F28`

### Лучшие герои
- 4 columns: Герой · Матчи · % Побед · KDA
- Column headers: 9px uppercase Cinzel in dim color, dark background row
- Each hero row: small hero image, hero name in blue, match count, winrate % + 2px bar, KDA in gold
- Winrate color + bar color: green ≥ 60%, gold ≥ 50%, red < 50%
- Row hover: background changes to `#1C2230`
- "Показать все →" link in the block header (right side), blue

---

## Acceptance Criteria

- [ ] Player header always visible
- [ ] All 3 blocks use the same header/body structure as other blocks in the app
- [ ] Text is bright — main values at `#E2E6EA` or white, nothing unreadable
- [ ] 3 equal-width columns with `20px` gap between them, `20px` padding around the content area
- [ ] Radar chart renders with correct gold/red dot distinction
- [ ] Season stats divider separates KDA from general stats
- [ ] Hero table winrate bars are color-coded
- [ ] Sub-tabs: gold underline on active, dim on inactive
- [ ] No rounded corners anywhere
