"""Reproducible stage-specific upgrade comparisons. No production edits."""
import csv
from pathlib import Path

F = Path(__file__).resolve().parent
WEAPONS = {"GearCraft": 4, "SteamCannon": 10, "SteamThrower": 10, "RailCraft": 19, "SteamGatling": 19}

def representative(weapon, stage):
    if stage <= 6:
        return "tutorial_day2"
    if stage <= 9:
        return "early_day3_common"
    if stage <= 18:
        return "mid_day4_common"
    return ("final_day11_" if stage == 31 else "late_day7_") + ("melee" if weapon == "GearCraft" else "ranged")

def write(name, rows):
    with (F / (name + "-cases.csv")).open("w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["id", "weapon", "stage", "profile", "seed", "maxSeconds", "mode"])
        w.writerows(rows)

route = []
for weapon, first in WEAPONS.items():
    for stage in range(first, 32):
        route.append([f"route_{weapon}_{stage}", weapon, stage, representative(weapon, stage), 77, 900, "wave"])
write("route", route)
print(f"route: {len(route)} cases")

probes = []
def add(weapon, stage, profile, seed=77, mode="wave", limit=900):
    probes.append([f"probe_{weapon}_{stage}_{profile}_{seed}_{mode}", weapon, stage, profile, seed, limit, mode])

for profile in ["plate_only", "early_day3_speed"]:
    add("GearCraft", 9, profile)
for stage in [10, 18]:
    add("GearCraft", stage, "mid_day4_speed")
    for weapon in ["SteamCannon", "SteamThrower"]:
        for suffix in ["speed", "size", "precision"]:
            add(weapon, stage, "mid_day4_" + suffix)
for weapon in WEAPONS:
    for profile in ["plate_only", "late_day7_common"]:
        add(weapon, 19, profile)
    if weapon != "GearCraft":
        add(weapon, 19, "late_day7_precision")
        add(weapon, 19, "late_day7_melee")
        if weapon != "SteamThrower":
            add(weapon, 19, "late_day7_reflect")
for weapon in WEAPONS:
    for seed in [78, 79]:
        add(weapon, 28, representative(weapon, 28), seed)
for weapon in ["RailCraft", "SteamGatling"]:
    add(weapon, 28, "late_day7_common")
for weapon in WEAPONS:
    for profile in ["plate_only", "late_day7_common", representative(weapon, 31)]:
        add(weapon, 31, profile, mode="boss", limit=60)
    if weapon != "GearCraft":
        add(weapon, 31, "late_day7_ranged", mode="boss", limit=60)
for weapon in ["RailCraft", "SteamGatling"]:
    add(weapon, 31, "late_day7_precision", mode="boss", limit=60)
for weapon in ["RailCraft", "SteamCannon"]:
    add(weapon, 31, "late_day7_reflect", mode="boss", limit=60)
# Bridge to the previous late-game module condition. These have additional
# module costs (UpgradeCore12 + ModuleCore_lv2 x4), outside the parts budget.
for weapon in WEAPONS:
    add(weapon, 31, representative(weapon, 31) + "_module20", mode="boss", limit=60)
write("probes", probes)
print(f"probes: {len(probes)} cases (60s boss windows are censored observations)")
write("route-fix", [["route_GearCraft_4", "GearCraft", 4, "tutorial_day2", 77, 900, "wave"]])
