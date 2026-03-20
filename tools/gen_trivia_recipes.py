#!/usr/bin/env python3
"""Parse items.kv and regenerate item_recipe entries in trivia.json.

Keeps all existing multiple_choice questions and replaces all item_recipe
questions with ones derived directly from the game data.

Usage: python tools/gen_trivia_recipes.py [--dry-run]
"""

import json
import random
import re
import sys
from pathlib import Path

SCRIPT_DIR = Path(__file__).parent
REPO_ROOT  = SCRIPT_DIR.parent
KV_FILE    = SCRIPT_DIR / "items.kv"
TRIVIA_FILE = REPO_ROOT / "Resources" / "trivia.json"

DRY_RUN = "--dry-run" in sys.argv

# ─── Items we can display (have image assets in DotaItemData) ────────────────
KNOWN_ITEMS = {
    "blink", "blades_of_attack", "broadsword", "chainmail", "claymore",
    "helm_of_iron_will", "javelin", "mithril_hammer", "platemail",
    "quarterstaff", "quelling_blade", "ring_of_protection", "gauntlets",
    "slippers", "mantle", "branches", "belt_of_strength", "boots_of_elves",
    "robe", "circlet", "ogre_axe", "blade_of_alacrity", "staff_of_wizardry",
    "ultimate_orb", "gloves", "lifesteal", "ring_of_regen", "sobi_mask",
    "boots", "gem", "cloak", "talisman_of_evasion", "cheese", "magic_stick",
    "magic_wand", "ghost", "clarity", "flask", "dust", "bottle",
    "ward_observer", "ward_sentry", "tango", "courier", "tpscroll",
    "travel_boots", "phase_boots", "demon_edge", "eagle", "reaver",
    "relic", "hyperstone", "ring_of_health", "void_stone", "mystic_staff",
    "energy_booster", "point_booster", "vitality_booster", "power_treads",
    "hand_of_midas", "oblivion_staff", "pers", "poor_mans_shield",
    "bracer", "wraith_band", "null_talisman", "mekansm", "vladmir",
    "buckler", "ring_of_basilius", "pipe", "urn_of_shadows",
    "headdress", "sheepstick", "orchid", "cyclone", "force_staff", "dagon",
    "necronomicon", "ultimate_scepter", "refresher", "assault", "heart",
    "black_king_bar", "shivas_guard", "bloodstone", "sphere", "vanguard",
    "blade_mail", "soul_booster", "hood_of_defiance", "rapier",
    "monkey_king_bar", "radiance", "butterfly", "greater_crit", "basher",
    "bfury", "manta", "lesser_crit", "armlet", "invis_sword",
    "sange_and_yasha", "satanic", "mjollnir", "skadi", "sange",
    "helm_of_the_dominator", "maelstrom", "desolator", "yasha",
    "mask_of_madness", "diffusal_blade", "ethereal_blade", "soul_ring",
    "arcane_boots", "orb_of_venom", "stout_shield", "ancient_janggo",
    "medallion_of_courage", "veil_of_discord",
    "rod_of_atos", "abyssal_blade", "heavens_halberd", "ring_of_aquila",
    "tranquil_boots", "shadow_amulet",
    "lotus_orb", "solar_crest", "guardian_greaves", "aether_lens",
    "dragon_lance", "iron_talon", "blight_stone",
    "crimson_guard", "wind_lace", "moon_shard", "silver_edge", "bloodthorn",
    "echo_sabre", "glimmer_cape", "hurricane_pike", "octarine_core",
    # generic recipe scroll — all recipe items map to the same image
    "recipe",
}

# Trivia targets to skip (upgrades, system items)
SKIP_TARGETS = {
    "travel_boots_2",
    "necronomicon_2", "necronomicon_3",
    "diffusal_blade_2",
    "dagon_2", "dagon_3", "dagon_4", "dagon_5",
    "ward_dispenser",
    "recipe_travel_boots",   # the recipe scroll itself shouldn't be a target
}


# ─── KV parser ───────────────────────────────────────────────────────────────

def parse_recipe_blocks(kv_text: str) -> dict:
    """
    Lightweight KV parser targeting only item_recipe_* blocks.
    Returns { recipe_key: { 'ItemCost': str, 'ItemResult': str,
                             'ItemRequirements': { '01': str, ... } } }
    """
    lines = kv_text.splitlines()
    n = len(lines)
    recipes = {}

    def skip_whitespace_and_comments(start):
        i = start
        while i < n:
            s = lines[i].strip()
            if s and not s.startswith("//"):
                return i
            i += 1
        return n

    def collect_block(start):
        """Collect lines inside a { } block (depth already entered).
        Returns (block_dict, next_index) where block_dict maps keys to
        either a string value or a sub-dict."""
        data = {}
        i = start
        while i < n:
            s = lines[i].strip()
            i += 1
            if not s or s.startswith("//"):
                continue
            if s == "}":
                return data, i
            # Match "key" "value" — value may have trailing comment
            kv = re.match(r'"([^"]+)"\s+"([^"]*)"', s)
            if kv:
                data[kv.group(1)] = kv.group(2)
                continue
            # Match block key "key" followed by {
            bk = re.match(r'"([^"]+)"$', s)
            if bk:
                key = bk.group(1)
                i = skip_whitespace_and_comments(i)
                if i < n and lines[i].strip() == "{":
                    i += 1
                    sub, i = collect_block(i)
                    data[key] = sub
                continue
        return data, i

    i = 0
    while i < n:
        s = lines[i].strip()
        i += 1
        if not s or s.startswith("//"):
            continue

        m = re.match(r'"(item_recipe_[^"]+)"$', s)
        if not m:
            continue

        recipe_key = m.group(1)
        i = skip_whitespace_and_comments(i)
        if i < n and lines[i].strip() == "{":
            i += 1
            block, i = collect_block(i)
            if block.get("ItemRecipe") == "1":
                recipes[recipe_key] = block

    return recipes


# ─── Name helpers ─────────────────────────────────────────────────────────────

def strip_prefix(item_name: str) -> str:
    """item_shadow_amulet -> shadow_amulet; item_recipe_foo -> recipe"""
    if item_name.startswith("item_recipe_"):
        return "recipe"
    if item_name.startswith("item_"):
        return item_name[5:]
    return item_name


# ─── Distractor selection ─────────────────────────────────────────────────────

def pick_distractors(target_key: str, exclude: set, pool: list, count: int = 4) -> list:
    """Pick `count` distractors from `pool` not in `exclude`, deterministically."""
    candidates = [x for x in pool if x not in exclude]
    if len(candidates) <= count:
        return candidates
    rng = random.Random(target_key)
    rng.shuffle(candidates)
    return candidates[:count]


# ─── Main ─────────────────────────────────────────────────────────────────────

def main():
    kv_text = KV_FILE.read_text(encoding="utf-8")
    raw_recipes = parse_recipe_blocks(kv_text)

    print(f"Found {len(raw_recipes)} item_recipe_* blocks in KV file")

    # ── Pass 1: collect all valid recipes ────────────────────────────────────
    valid = []
    all_ingredient_keys: set[str] = set()

    for rkey, data in raw_recipes.items():
        result_raw = data.get("ItemResult", "")
        target = strip_prefix(result_raw)

        if target in SKIP_TARGETS:
            print(f"  SKIP (target blacklist): {target}")
            continue
        if target not in KNOWN_ITEMS:
            print(f"  SKIP (target unknown): {target}")
            continue

        reqs = data.get("ItemRequirements", {})
        if not reqs or "01" not in reqs:
            print(f"  SKIP (no requirements): {target}")
            continue

        raw_ings = [x.strip() for x in reqs["01"].split(";") if x.strip()]
        ingredients = [strip_prefix(x) for x in raw_ings]

        # Add recipe scroll if the scroll itself costs gold
        cost = int(data.get("ItemCost", "0") or "0")
        if cost > 0:
            ingredients.append("recipe")

        # Skip if any ingredient is not displayable
        unknown = [x for x in ingredients if x not in KNOWN_ITEMS]
        if unknown:
            print(f"  SKIP (unknown ingredients {unknown}): {target}")
            continue

        # Need at least 2 distinct ingredients to be interesting
        unique_ings = list(dict.fromkeys(ingredients))
        if len(unique_ings) < 2:
            print(f"  SKIP (trivial recipe): {target}")
            continue

        valid.append({
            "key":         rkey,
            "target":      target,
            "ingredients": ingredients,      # may include duplicates
            "unique_ings": unique_ings,
        })

        for ing in unique_ings:
            if ing != "recipe":
                all_ingredient_keys.add(ing)

    print(f"\n{len(valid)} valid recipes after filtering")

    # ── Pass 2: build distractor pool ────────────────────────────────────────
    # Use items that actually appear as ingredients — these are all real components.
    distractor_pool = sorted(all_ingredient_keys)

    # ── Pass 3: emit trivia entries ───────────────────────────────────────────
    recipe_questions = []
    for r in valid:
        exclude = set(r["unique_ings"])
        distractors = pick_distractors(r["target"], exclude, distractor_pool, count=4)

        recipe_questions.append({
            "type":        "item_recipe",
            "id":          f"recipe_{r['target']}",
            "targetItem":  r["target"],
            "ingredients": r["ingredients"],
            "distractors": distractors,
        })

    # ── Merge with existing MC questions ─────────────────────────────────────
    trivia_json = json.loads(TRIVIA_FILE.read_text(encoding="utf-8"))
    mc_questions = [q for q in trivia_json.get("questions", [])
                    if q.get("type") != "item_recipe"]

    print(f"\n{len(recipe_questions)} recipe questions generated")
    print(f"{len(mc_questions)} existing MC questions preserved")

    trivia_json["questions"] = recipe_questions + mc_questions

    if DRY_RUN:
        print("\n[dry-run] Would write:")
        for q in recipe_questions[:5]:
            print(f"  {q['targetItem']}: {q['ingredients']} | distractors: {q['distractors']}")
        print("  ...")
    else:
        out = json.dumps(trivia_json, ensure_ascii=False, indent=2)
        TRIVIA_FILE.write_text(out, encoding="utf-8")
        print(f"\nWritten to {TRIVIA_FILE}")


if __name__ == "__main__":
    main()
