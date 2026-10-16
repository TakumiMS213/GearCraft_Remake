#!/usr/bin/env python3
"""Offline acquisition model; not a Unity playthrough or combat measurement.

Run from any directory:
  python3 QA/WeaponBalance20261015/economic_model.py --runs 3000 --seed 20261015

Uses only the Python standard library and reads the current Unity asset data.
No game files or saved player state are changed.
"""
import argparse
import hashlib
import itertools
import json
import random
import re
import statistics
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "Assets/GearCraft/ScriptableObject"
SOURCES = {}
MATERIALS = ["Scrap", "Gear", "UpgradeCore", "ModuleCore_lv1", "ModuleCore_lv2", "ModuleCore_lv3"]


def read(path):
    path = Path(path)
    text = path.read_text()
    SOURCES[str(path.relative_to(ROOT))] = hashlib.sha256(text.encode()).hexdigest()
    return text


def scalar(text, name, default=None):
    match = re.search(r"^  " + re.escape(name) + r": (.*)$", text, re.M)
    return match.group(1) if match else default


def block(text, name):
    match = re.search(r"^  " + re.escape(name) + r":\s*\n((?:(?:  - |    ).*\n)*)", text, re.M)
    return match.group(1) if match else ""


def entries(text, name):
    return [dict(re.findall(r"(?:^|\n)  (?:- |  )([\w]+): (.*)", item))
            for item in re.split(r"(?=^  - )", block(text, name), flags=re.M) if item.strip()]


def guid_map(directory):
    result = {}
    for path in directory.glob("*.asset.meta"):
        match = re.search(r"^guid: (\w+)$", read(path), re.M)
        result[match.group(1)] = Path(str(path)[:-5])
    return result


def curve(text, name):
    # Current curves are unweighted cubic Hermite curves (weightedMode = 0).
    section = text.split("  " + name + ":", 1)[1].split("    m_PreInfinity:", 1)[0]
    points = []
    for key in section.split("    - serializedVersion:")[1:]:
        values = dict(re.findall(r"^      (time|value|inSlope|outSlope): (.*)$", key, re.M))
        points.append({name: float(value) for name, value in values.items()})
    return points


def evaluate(points, x):
    if x <= points[0]["time"]:
        return points[0]["value"]
    if x >= points[-1]["time"]:
        return points[-1]["value"]
    for left, right in zip(points, points[1:]):
        if left["time"] <= x <= right["time"]:
            span = right["time"] - left["time"]
            t = (x - left["time"]) / span
            return ((2*t**3-3*t**2+1)*left["value"]
                    +(t**3-2*t**2+t)*span*left["outSlope"]
                    +(-2*t**3+3*t**2)*right["value"]
                    +(t**3-t**2)*span*right["inSlope"])
    raise ValueError(x)


def load_inputs():
    # Record the implementation versions used for the reproduced rules below.
    for relative in (
            "Main/EnemySpawner.cs", "Main/StageFlowManager.cs", "Main/BonusCardsManager.cs",
            "Stage/Application/StageSpawnPlanner.cs", "Data/StageGeneratorSO.cs",
            "Items/MaterialDropper.cs", "Craft/CraftTutorialController.cs", "Craft/CraftManager.cs"):
        read(ROOT / "Assets/GearCraft/Scripts" / relative)
    config = read(DATA / "StageGeneratorConfig/StageGeneratorConfig.asset")
    enemy_paths = guid_map(DATA / "EnemyData")
    pools = {}
    for name in ("normalEnemyPool", "bossPool"):
        pools[name] = []
        for guid in re.findall(r"guid: (\w+)", block(config, name)):
            text = read(enemy_paths[guid])
            pools[name].append({"name": scalar(text, "enemyName"),
                "speed": float(scalar(text, "speed")), "drops": entries(text, "drops")})
    cards = []
    card_paths = guid_map(DATA / "BonusCard")
    scene = read(ROOT / "Assets/GearCraft/Scenes/Main.unity")
    for guid in re.findall(r"guid: (\w+)", block(scene, "cardDataList")):
        path = card_paths[guid]
        text = read(path)
        cards.append({"name": path.stem, "weight": float(scalar(text, "weight")),
                      "required": re.findall(r"^  - (\S+)$", block(text, "requiredWeaponNames"), re.M),
                      "effects": entries(text, "effects")})
    recipes = {}
    for path in sorted((DATA / "CraftRecipe").glob("*.asset")):
        text = read(path)
        recipes[path.stem] = {"unlock_boss_kills": int(scalar(text, "unlockBossKills")),
            "costs": {MATERIALS[int(cost["type"])]: int(cost["amount"]) for cost in entries(text, "costs")},
            "required_weapon": scalar(text, "requiredWeapon"), "display_name": scalar(text, "recipeName")}
    settings = {name: int(scalar(config, name)) for name in
                ("totalStages", "stagesPerRest", "baseEnemyCount", "maxEnemyCount", "firstDayEnemyCount")}
    settings["bossStages"] = [int.from_bytes(bytes.fromhex(scalar(config, "bossStages"))[i:i+4], "little")
                              for i in range(0, len(bytes.fromhex(scalar(config, "bossStages"))), 4)]
    settings["difficulty"] = curve(config, "difficultyCurve")
    settings["drop_curve"] = curve(config, "dropMultiplierCurve")
    return settings, pools, cards, recipes


def offer_probability(cards, weapon):
    pool = [card for card in cards if not card["required"] or weapon in card["required"]]
    total = sum(card["weight"] for card in pool)
    none = 0.0
    for choices in itertools.permutations([card for card in pool if card["name"] != "AR"], 3):
        probability, remaining = 1.0, total
        for card in choices:
            probability *= card["weight"] / remaining
            remaining -= card["weight"]
        none += probability
    return {"eligible_cards": len(pool), "weight_sum": total, "ar_per_offer": 1-none,
            "ar_at_least_once_6_offers": 1-none**6, "ar_at_least_once_12_offers": 1-none**12,
            "ar_at_least_once_20_offers": 1-none**20,
            "assumption": "Pool remains unchanged; no weapon-specific once-only cards removed."}


def draw_cards(rng, pool):
    available, result = list(pool), []
    for _ in range(min(3, len(available))):
        pick = rng.random() * sum(card["weight"] for card in available)
        cumulative = 0
        for card in available:
            cumulative += card["weight"]
            if pick <= cumulative:
                result.append(card)
                available.remove(card)
                break
    return result


def spawn_sequence(rng, normal, kills, count, first_day):
    low, advanced = normal[:3], normal[3:6]
    if kills == 0:
        primaries = [rng.choice(low) for _ in range(count)]
    elif kills == 1:
        primaries = [rng.choice(advanced) for _ in range(min(3, count))]
        primaries += [rng.choice(low if rng.random() < 0.8 else advanced) for _ in range(count-len(primaries))]
        rng.shuffle(primaries)
    else:
        burst = min(5, count)
        primaries = [rng.choice(advanced) for _ in range(count-burst)]
        insert = rng.randrange(len(primaries)+1)
        primaries[insert:insert] = [rng.choice(low) for _ in range(burst)]
    result = []
    for enemy in primaries:
        group_size = 1 if first_day else rng.randint(1, 3)
        if enemy["speed"] <= 2 and "g" in enemy["name"].lower():
            for _ in range(max(2, group_size)-1):
                support_pool = low if kills == 0 else (low if kills == 1 and rng.random() < 0.8 else advanced)
                result.append(rng.choice([item for item in support_pool if "g" not in item["name"].lower()]))
            result.append(enemy)
        else:
            result.extend([enemy] * group_size)
    return result


def simulate(rng, config, pools, cards, recipes, tutorial_day):
    materials = {name: 0 for name in MATERIALS}
    kills, has_ar, scrap_module, punk = 0, False, False, False
    first_available = {name: None for name in ("GearCraft", "PunkDrive", "SteamCannon", "SteamThrower", "RailCraft", "SteamGatling")}
    daily = []
    reward_pool = [card for card in cards if not card["required"] or "Katana" in card["required"]]
    for day in range(1, (config["totalStages"]-1)//config["stagesPerRest"]+2):
        if day == tutorial_day:
            # First craft tutorial supplements shortages, then asks for another craft.
            for name, cost in recipes["ScrapModule"]["costs"].items():
                materials[name] = max(materials[name], cost) - cost
            scrap_module = True
            materials["Gear"] = max(materials["Gear"], 4)
            materials["Gear"] -= recipes["PunkDrive"]["costs"]["Gear"]
            punk = True
            first_available["PunkDrive"] = day
        # Independent recipe affordability: do not spend these costs or change the test weapon.
        for name in first_available:
            recipe = recipes[name]
            if first_available[name] is not None or name == "PunkDrive":
                continue
            required_available = has_ar if name in ("SteamCannon", "SteamThrower") else True
            if kills >= recipe["unlock_boss_kills"] and required_available and all(
                    materials[mat] >= cost for mat, cost in recipe["costs"].items()):
                first_available[name] = day
        start = (day-1)*config["stagesPerRest"]+1
        before = dict(materials)
        enemy_count_day = 0
        for wave in range(start, min(config["totalStages"], start+config["stagesPerRest"]-1)+1):
            is_boss = wave in config["bossStages"]
            difficulty = evaluate(config["difficulty"], start/config["totalStages"])
            count = config["baseEnemyCount"] if is_boss else (config["firstDayEnemyCount"] if day == 1 else
                    round(config["baseEnemyCount"]+(config["maxEnemyCount"]-config["baseEnemyCount"])*difficulty))
            sequence = spawn_sequence(rng, pools["normalEnemyPool"], kills, count, day == 1)
            if is_boss:
                sequence.append(pools["bossPool"][config["bossStages"].index(wave)])
            enemy_count_day += len(sequence)
            # MaterialDropper samples the curve at raw wave index, not normalized progress.
            drop_multiplier = evaluate(config["drop_curve"], wave)
            for enemy in sequence:
                for drop in enemy["drops"]:
                    if rng.random() > float(drop["dropChance"]):
                        continue
                    amount = rng.randint(int(drop["minAmount"]), int(drop["maxAmount"]))
                    materials[MATERIALS[int(drop["type"])]] += max(1, round(amount*drop_multiplier))
            if is_boss:
                kills = config["bossStages"].index(wave)+1
            if wave % config["stagesPerRest"] != 0 and wave != config["totalStages"]:
                offered = draw_cards(rng, reward_pool)
                ar = next((card for card in offered if card["name"] == "AR"), None)
                def economic_value(card):
                    return sum(float(effect["value"]) * (100 if int(effect.get("materialType", 0)) == 1 else 1)
                               for effect in card["effects"] if int(effect["type"]) == 4)
                selected = ar if ar is not None and not has_ar else max(offered, key=economic_value)
                if selected["name"] == "AR":
                    has_ar = True
                for effect in selected["effects"]:
                    if int(effect["type"]) == 4:
                        materials[MATERIALS[int(effect["materialType"])]] += int(float(effect["value"]))
        if start+config["stagesPerRest"]-1 <= config["totalStages"] and scrap_module:
            materials["Gear"] += 5
        daily.append({"day": day, "scheduled_enemies": enemy_count_day, "ar_owned": has_ar,
                      "earned": {mat: materials[mat]-before[mat] for mat in MATERIALS}, "inventory": dict(materials)})
    return first_available, daily


def summarize(runs, tutorial_day, rng, config, pools, cards, recipes):
    results = [simulate(rng, config, pools, cards, recipes, tutorial_day) for _ in range(runs)]
    days = []
    for i in range(len(results[0][1])):
        records = [result[1][i] for result in results]
        days.append({"day": i+1, "mean_scheduled_enemies": statistics.mean(row["scheduled_enemies"] for row in records),
            "ar_owned_fraction": statistics.mean(row["ar_owned"] for row in records),
            "mean_earned": {mat: statistics.mean(row["earned"][mat] for row in records) for mat in MATERIALS},
            "mean_unspent_inventory": {mat: statistics.mean(row["inventory"][mat] for row in records) for mat in MATERIALS}})
    availability = {}
    for name in results[0][0]:
        values = [result[0][name] for result in results]
        available = [value for value in values if value is not None]
        availability[name] = {"available_before_clear_fraction": len(available)/runs,
            "median_day_if_available": statistics.median(available) if available else None,
            "day_distribution": {str(day): values.count(day)/runs for day in range(1, 12)},
            "not_available_fraction": values.count(None)/runs}
    return {"tutorial_before_day": tutorial_day, "runs": runs, "daily": days, "independent_affordability": availability}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runs", type=int, default=3000)
    parser.add_argument("--seed", type=int, default=20261015)
    parser.add_argument("--output", type=Path, default=Path(__file__).with_name("economic-model-results.json"))
    args = parser.parse_args()
    config, pools, cards, recipes = load_inputs()
    result = {"kind": "Offline economy model; not combat or normal-play measurement", "seed": args.seed,
        "assumptions": [
            "Every scheduled enemy and boss is defeated; every spawned drop is recovered. No expiry/burning/gate loss/death.",
            "Boss-summoned enemies are excluded because their count depends on combat time; their extra income is not modelled.",
            "Two scenarios: first-time tutorial completed before Day 1, or on first return before Day 2.",
            "Tutorial crafts ScrapModule and PunkDrive. No other purchase/reroll/weapon-transform Gear income occurs.",
            "Affordability of each recipe is considered independently against the same unspent balance, not all crafted together.",
            "Reward screen uses Katana pool throughout. Choose first AR whenever offered; otherwise highest Gear reward, then other material quantity, then first card.",
            "AR can be equipped at the workbench once owned; AR materials are not consumed by crafting.",
            "No weapon-specific rewards, upgrade-part purchases, junkCollectorMult, or upgrade tutorial materials are modelled.",
            "Python random is reproducible with its own seed; it does not reproduce Unity.Random sequences.",
            "Enemy groups, unlock thresholds and actual recipe/drop assets are read/reproduced from current source; survival is assumed, never measured."
        ],
        "ar_formula": "P(no AR) = sum over ordered triples i,j,k != AR of wi/W * wj/(W-wi) * wk/(W-wi-wj); P(at least one in n offers) = 1 - P(no AR)^n",
        "ar_offers": {weapon: offer_probability(cards, weapon) for weapon in ("Katana", "GearCraft_Sword")},
        "config": config, "recipes": recipes,
        "scenarios": [summarize(args.runs, day, random.Random(args.seed+day), config, pools, cards, recipes) for day in (1, 2)],
        "source_sha256": SOURCES}
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2)+"\n")
    print(str(args.output.relative_to(ROOT)))
    for scenario in result["scenarios"]:
        print("Tutorial before Day", scenario["tutorial_before_day"])
        for name, row in scenario["independent_affordability"].items():
            print(name, "median", row["median_day_if_available"], "available", round(row["available_before_clear_fraction"], 4))


if __name__ == "__main__":
    main()
