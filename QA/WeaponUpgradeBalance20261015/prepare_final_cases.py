import csv
from pathlib import Path

folder = Path(__file__).resolve().parent
profiles = {
    "GearCraft": "final_day11_melee",
    "SteamCannon": "final_day11_ranged_fitted_speed",
    "SteamThrower": "final_day11_thrower_fitted_damage",
    "RailCraft": "final_day11_ranged_fitted_speed",
    "SteamGatling": "final_day11_ranged",
}
with (folder / "fitted-final-cases.csv").open("w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["id", "weapon", "stage", "profile", "seed", "maxSeconds", "mode"])
    for weapon in ["SteamCannon", "SteamThrower", "RailCraft"]:
        for module in [0, 20]:
            profile = profiles[weapon] + ("_module20" if module else "")
            w.writerow([f"fitted_{weapon}_module{module}", weapon, 31, profile, 77, 900, "wave"])

native = [
    ["native_GearCraft_day2", "GearCraft", 4, "tutorial_day2", 77],
    ["native_GearCraft_day3", "GearCraft", 7, "early_day3_common", 77],
    ["native_SteamCannon_day4", "SteamCannon", 10, "mid_day4_common", 77],
    ["native_SteamThrower_day4", "SteamThrower", 10, "mid_day4_common", 77],
    ["native_RailCraft_day7", "RailCraft", 19, "late_day7_ranged", 77],
    ["native_SteamGatling_day7", "SteamGatling", 19, "late_day7_ranged", 77],
]
for module in [0, 20]:
    for weapon, base in profiles.items():
        native.append([f"native_{weapon}_final_module{module}", weapon, 31, base + ("_module20" if module else ""), 77])
with (folder / "native-plan.csv").open("w", newline="") as f:
    w = csv.writer(f)
    w.writerow(["label", "weapon", "stage", "profile", "seed"])
    w.writerows(native)
print(f"fitted final: 6 controlled cases; native survival: {len(native)} cases")
