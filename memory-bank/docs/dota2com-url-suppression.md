# Suppressing dota2.com Store Panel (Issue #81)

The in-game store panel loads `https://www.dota2.com/home` (redirect from a bare dota2.com URL), causing high CPU on some systems. Three places needed to be patched.

## Fix 1 & 2 — client.dll binary patch

Nulled all hardcoded `http://` dota2.com URLs and bare domain strings that were used to construct URLs dynamically (e.g. `http://` + `www.dota2.com`).

Tool: `tools/patch-urls.py --null <url>`

Strings nulled:
- All 20 `http://` URLs (store, items, heroes, fantasy, blog, tournaments, etc.)
- Bare `www.dota2.com` and `store.dota2.com.cn` (used with `sprintf("http://%s", ...)`)

The patched `client.dll` must be uploaded to the CDN and the manifest hash updated. Users receive it via normal sync.

## Fix 3 — Loose file override for store_promo_pages

The store panel's promo URL list lives in `scripts/store_promo_pages.txt` inside `pak01_082.vpk`. The VPK binary patch failed because the CRC in `pak01_dir.vpk` no longer matched — the engine silently rejects modified VPK data.

**What worked:** dropping a loose file at `dota/scripts/store_promo_pages.txt`. In Source 1, loose files on disk take priority over VPK content unconditionally.

Content of the override file:
```
"store_promo_pages"
{
	// All store promo URLs disabled (patched by d2c-launcher)
}
```

This file needs to be distributed to all users. Options:
- Add it to the game manifest so it syncs automatically
- Or have the launcher write it on first launch

## Key lesson

Direct VPK data patching doesn't work — the CRC in `pak01_dir.vpk` will mismatch and the engine rejects the entry. Always use loose files to override VPK content in Source 1.
