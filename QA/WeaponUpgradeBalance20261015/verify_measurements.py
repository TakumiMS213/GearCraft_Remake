#!/usr/bin/env python3
"""Read-only verification of committed upgrade benchmark CSV rows.

Writes verification.json/txt beside this script. Safe to rerun during collection;
only complete newline-terminated result records are treated as finished cases.
This checks logged native configuration, not an unlogged per-collision damage trace.
"""
import argparse
import csv
import hashlib
import io
import json
import math
import re
import struct
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_FOLDER = Path(__file__).resolve().parent
EFFECTS = {0: "DamageFlat", 1: "AttackSpeedMult", 2: "BulletDouble", 3: "SpreadReduction",
           4: "SpreadIncrease", 6: "Ricochet", 7: "JunkCollector", 8: "BulletSizeUp", 10: "MagnetRangeUp"}
CRAFTED = {"GearCraft", "GearCraft_Sword", "GearCraft_Axe", "SteamCannon", "SteamThrower", "RailCraft", "SteamGatling"}


def f32(value):
    return struct.unpack("f", struct.pack("f", float(value)))[0]


def scalar(text, field, default=None):
    match = re.search(r"^  " + re.escape(field) + r": (.*)$", text, re.M)
    return match.group(1).strip() if match else default


def assets():
    parts, weapons, enemies = {}, {}, {}
    base = ROOT / "Assets/GearCraft/ScriptableObject"
    for path in (base / "UpgradeParts").glob("*.asset"):
        text = path.read_text(encoding="utf-8-sig")
        block = text.split("  effects:\n", 1)[1].split("  costs:", 1)[0]
        costs = text.split("  costs:\n", 1)[1].split("  rangedOnly:", 1)[0]
        parts[path.stem] = {"ranged_only": scalar(text, "rangedOnly") == "1",
                            "costs": {int(kind): int(amount) for kind, amount in re.findall(r"  - type: (\d+)\n    amount: ([^\n]+)", costs)},
                            "effects": [(EFFECTS[int(kind)], f32(value)) for kind, value in
                                        re.findall(r"  - type: (\d+)\n    value: ([^\n]+)", block)]}
    for path in (base / "WeaponData").glob("*.asset"):
        text = path.read_text(encoding="utf-8-sig")
        weapons[path.stem] = {key: float(scalar(text, key, default)) for key, default in
                             [("weaponType", "0"), ("baseDamage", "20"), ("accDamageMultiplier", "1"),
                              ("flatDamageMultiplier", "-1"), ("spreadAngle", "0"), ("bulletSpeed", "25"),
                              ("coolTime", ".5"), ("useLineHitbox", "0")]}
    for path in (base / "EnemyData").glob("*.asset"):
        text = path.read_text(encoding="utf-8-sig")
        enemies[scalar(text, "enemyName")] = float(scalar(text, "baseHP"))
    return parts, weapons, enemies


def read_csv(path, notes):
    if not path.exists():
        return []
    text = path.read_text(encoding="utf-8-sig")
    if text and not text.endswith("\n"):
        text = text.rsplit("\n", 1)[0] + "\n" if "\n" in text else ""
        notes.append({"file": path.name, "kind": "incomplete_last_line_ignored"})
    rows = []
    for row in csv.DictReader(io.StringIO(text)):
        if None in row or any(value is None for value in row.values()):
            notes.append({"file": path.name, "kind": "malformed_csv_record"})
        else:
            rows.append(row)
    return rows


def close(a, b, tolerance=2e-5):
    return math.isfinite(float(a)) and abs(float(a) - float(b)) <= max(tolerance, abs(float(b)) * 2e-6)


def expected_effects(names, parts, ranged):
    stats = dict(flat_damage=0., cooldown_multiplier=1., double_shot=False, spread_modifier=0.,
                 ricochet=0, bullet_size=1., junk_collector=1., magnet_range=3.)
    effects = []
    for index, name in enumerate(names):
        part = parts[name]
        applied = ranged or not part["ranged_only"]
        for kind, value in part["effects"]:
            effects.append((index, name, part["ranged_only"], applied, kind, value))
            if not applied:
                continue
            if kind == "DamageFlat": stats["flat_damage"] = f32(stats["flat_damage"] + value)
            elif kind == "AttackSpeedMult": stats["cooldown_multiplier"] = f32(stats["cooldown_multiplier"] * value)
            elif kind == "BulletDouble": stats["double_shot"] = True
            elif kind == "SpreadReduction": stats["spread_modifier"] = f32(stats["spread_modifier"] - value)
            elif kind == "SpreadIncrease": stats["spread_modifier"] = f32(stats["spread_modifier"] + value)
            elif kind == "Ricochet": stats["ricochet"] += int(value)
            elif kind == "BulletSizeUp": stats["bullet_size"] = f32(stats["bullet_size"] + value)
            elif kind == "JunkCollector": stats["junk_collector"] = f32(stats["junk_collector"] + value)
            elif kind == "MagnetRangeUp": stats["magnet_range"] = f32(stats["magnet_range"] + value)
    return stats, effects


def verify(folder, label, parts, weapons, enemies, loadouts, replacement_labels=()):
    notes, errors, warnings = [], [], []
    # Results first: sidecars may advance afterward, but those cases stay pending.
    results = read_csv(folder / (label + "-results.csv"), notes)
    plans = read_csv(folder / (label + "-cases.csv"), notes)
    planned = {row["id"]: row for row in plans}
    sides = {}
    for name in ("loadout-stats", "loadout-effects", "spawn-plan", "scheduled-spawns", "spawns"):
        sides[name] = defaultdict(list)
        for row in read_csv(folder / (label + "-" + name + ".csv"), notes):
            sides[name][row["case"]].append(row)
    replacements = []
    for replacement_label in replacement_labels:
        replacement_rows = read_csv(folder / (replacement_label + "-results.csv"), notes)
        replacement_ids = {row["id"] for row in replacement_rows}
        original_rows = {row["id"]: row for row in results}
        for row in replacement_rows:
            replacements.append(dict(case=row["id"], source=replacement_label, original_result=original_rows.get(row["id"]), selected_result=row))
        results = [row for row in results if row["id"] not in replacement_ids] + replacement_rows
        for name in sides:
            alternate = defaultdict(list)
            for row in read_csv(folder / (replacement_label + "-" + name + ".csv"), notes):
                alternate[row["case"]].append(row)
            for case in replacement_ids:
                sides[name][case] = alternate[case]
    committed = Counter(row["id"] for row in results)
    for case, count in committed.items():
        if count != 1: errors.append(dict(case=case, kind="duplicate_result", count=count))
    signatures, position_groups = defaultdict(list), defaultdict(list)
    timing, checked = [], []
    for result in results:
        case = result["id"]
        def fail(kind, **details): errors.append(dict(case=case, kind=kind, **details))
        try:
            stage, seed = int(result["stage"]), int(result["seed"])
            profile, weapon_name = result["profile"], result["weapon"]
            plan = planned.get(case)
            if plan is None:
                fail("case_not_in_manifest")
            else:
                for field in ("weapon", "stage", "profile", "seed"):
                    if result[field] != plan[field]: fail("manifest_mismatch", field=field, logged=result[field], expected=plan[field])
            weapon = weapons["GearCraft_Sword" if weapon_name == "GearCraft" else weapon_name]
            ranged = int(weapon["weaponType"]) == 1
            build = loadouts[profile]
            expected, expected_rows = expected_effects(build["parts"], parts, ranged)
            day = (stage - 1) // 3 + 1
            attribute = 2 * (day - 1)
            acc_coefficient = f32(weapon["accDamageMultiplier"])
            flat_coefficient = f32(weapon["flatDamageMultiplier"] if weapon["flatDamageMultiplier"] >= 0 else acc_coefficient)
            bonus = f32(f32(attribute * acc_coefficient) + f32(expected["flat_damage"] * flat_coefficient)) if ranged else f32(attribute + f32(expected["flat_damage"] * flat_coefficient))
            if weapon_name in CRAFTED: bonus = f32(bonus + build["module"])
            expected.update(STR=attribute, ACC=attribute, base_damage=weapon["baseDamage"],
                            damage_per_hit=f32(f32(weapon["baseDamage"]) + bonus), module_damage=build["module"],
                            base_spread=weapon["spreadAngle"], effective_spread=max(0., f32(weapon["spreadAngle"] + expected["spread_modifier"])),
                            bullet_speed=weapon["bulletSpeed"])
            stat_rows = sides["loadout-stats"][case]
            if len(stat_rows) != 1:
                fail("loadout_stats_count", count=len(stat_rows))
            else:
                stat = stat_rows[0]
                for field in ("weapon", "profile"):
                    if stat[field] != result[field]: fail("loadout_identity_mismatch", field=field)
                for field, value in expected.items():
                    valid = stat[field].lower() == str(value).lower() if isinstance(value, bool) else close(stat[field], value)
                    if not valid: fail("loadout_value_mismatch", field=field, logged=stat[field], expected=value)
            logged_effects = [(int(row["part_index"]), row["asset"], row["ranged_only"] == "True", row["applied"] == "True", row["effect_type"], f32(row["value"])) for row in sides["loadout-effects"][case]]
            if logged_effects != expected_rows: fail("loadout_effect_rows_mismatch", logged=logged_effects, expected=expected_rows)
            multiplier = 2 if ranged and not weapon["useLineHitbox"] and expected["double_shot"] else 1
            if int(result["shots"]) != int(result["attack_events"]) * multiplier:
                fail("shots_vs_attack_events_mismatch", shots=result["shots"], attack_events=result["attack_events"], expected_multiplier=multiplier)
            if int(result["day"]) != day: fail("day_mismatch", expected=day, logged=result["day"])
            for total, components in [("total_including_summons", ["killed", "escaped_or_suicided", "remaining"]),
                                      ("scheduled_count", ["scheduled_killed", "scheduled_escaped", "scheduled_remaining"])]:
                if int(result[total]) != sum(int(result[key]) for key in components): fail("enemy_accounting_mismatch", total=total)
            if result["result"] == "cleared" and (int(result["remaining"]) or int(result["scheduled_remaining"])):
                fail("cleared_with_remaining_enemies")
            for key in ("seconds", "damage", "player_damage", "gate_damage", "boss_seconds", "boss_remaining_hp", "average_game_dt"):
                if not math.isfinite(float(result[key])): fail("nonfinite_result", field=key)
            frame_error = float(result["seconds"]) - int(result["frames"]) * float(result["average_game_dt"])
            if abs(frame_error) > max(.05, float(result["seconds"]) * .00015):
                warnings.append(dict(case=case, kind="frame_time_difference", seconds_difference=frame_error))
            spawn_plan = sorted(sides["spawn-plan"][case], key=lambda row: int(row["index"]))
            scheduled, observed = sides["scheduled-spawns"][case], sides["spawns"][case]
            mode = plan.get("mode", "wave") if plan else "wave"
            intentional_window = case.startswith("window60_") or (mode == "boss" and plan is not None and float(plan["maxSeconds"]) == 60)
            if mode == "wave":
                if [int(row["index"]) for row in spawn_plan] != list(range(len(spawn_plan))): fail("noncontiguous_spawn_indices")
                signatures[(stage, seed)].append((case, [(row["index"], row["name"], row["scheduled_elapsed"], row["last"]) for row in spawn_plan]))
                if len(scheduled) != int(result["scheduled_count"]): fail("scheduled_count_mismatch", logged=len(scheduled), result=result["scheduled_count"])
                if result["result"] == "cleared" and len(spawn_plan) != len(scheduled): fail("cleared_before_full_spawn_plan")
            if len(observed) != int(result["total_including_summons"]): fail("observed_count_mismatch", logged=len(observed), result=result["total_including_summons"])
            if mode == "wave" and len(observed) > len(scheduled) and not any(row["name"] == "BB-413" for row in spawn_plan):
                fail("unexpected_unscheduled_enemies", scheduled=len(scheduled), observed=len(observed), extra=len(observed)-len(scheduled))
            by_index = {int(row["index"]): row for row in spawn_plan}
            matched_observed = set()
            scaling_stage = (day - 1) * 3 + 1
            for row in scheduled:
                index = int(row["index"])
                p = by_index.get(index)
                if not p or any(row[key] != p[key] for key in ("name", "last", "scheduled_elapsed")):
                    fail("scheduled_spawn_vs_plan_mismatch", index=index)
                lateness = float(row["actual_elapsed"]) - float(row["scheduled_elapsed"])
                timing.append(lateness)
                if lateness < -.0002: fail("spawn_before_schedule", index=index, seconds=lateness)
                position_groups[(stage, seed, index, row["name"])].append((case, float(row["x"]), float(row["y"]), float(row["actual_elapsed"])))
                match = next((i for i, observed_row in enumerate(observed) if i not in matched_observed and observed_row["name"] == row["name"] and observed_row["last"] == row["last"] and abs(float(observed_row["elapsed"]) - float(row["actual_elapsed"])) <= .0011), None)
                if match is None:
                    fail("scheduled_spawn_missing_hp_log", index=index)
                    continue
                matched_observed.add(match)
                hp = enemies.get(row["name"])
                if hp is None: fail("unknown_enemy_asset", name=row["name"])
                elif not close(observed[match]["maxhp"], hp * (1 + scaling_stage / 10), .002):
                    fail("scheduled_initial_hp_mismatch", name=row["name"], logged=observed[match]["maxhp"], expected=hp * (1 + scaling_stage / 10))
            extra_observations = len(observed) - len(matched_observed)
            if mode == "boss":
                expected_boss = {9: "IC-408", 18: "BB-413", 30: "AR-227", 31: "EK-9"}.get(stage)
                boss_observations = [row for row in observed if row["name"] == expected_boss]
                if len(boss_observations) != 1 or result["boss_name"] != expected_boss:
                    fail("single_boss_identity_mismatch", expected=expected_boss, logged=result["boss_name"], observations=len(boss_observations))
                for boss_row in boss_observations:
                    expected_hp = enemies[expected_boss] * (1 + scaling_stage / 10)
                    if not close(boss_row["maxhp"], expected_hp, .002):
                        fail("single_boss_initial_hp_mismatch", logged=boss_row["maxhp"], expected=expected_hp)
            expected_hp_sum = sum(enemies.get(row["name"], 0) * (1 + scaling_stage / 10) for row in observed)
            if float(result["damage"]) > expected_hp_sum + max(.1, expected_hp_sum * 1e-5): fail("damage_above_enemy_hp_budget", expected_hp_sum=expected_hp_sum)
            checked.append(dict(case=case, weapon=weapon_name, profile=profile, stage=stage, shots=int(result["shots"]), attack_events=int(result["attack_events"]),
                                expected_projectiles_per_attack=multiplier, expected_initial_damage_per_hit=expected["damage_per_hit"],
                                scheduled_initial_hp_checked=len(matched_observed), additional_first_observed_enemies=extra_observations,
                                scheduled_killed=int(result["scheduled_killed"]), scheduled_count=int(result["scheduled_count"]), escaped=int(result["scheduled_escaped"]),
                                result=result["result"], intentional_window=intentional_window, seconds=float(result["seconds"])))
        except (KeyError, ValueError, TypeError) as exc:
            fail("invalid_or_missing_data", detail=str(exc))
    comparisons = []
    for (stage, seed), entries in signatures.items():
        if len(entries) < 2: continue
        reference = entries[0]
        mismatches = [case for case, sig in entries[1:] if sig != reference[1]]
        comparisons.append(dict(stage=stage, seed=seed, cases=[case for case, _ in entries], mismatches=mismatches))
        if mismatches: errors.append(dict(kind="cross_case_spawn_plan_mismatch", stage=stage, seed=seed, cases=mismatches))
    spreads = [dict(stage=key[0], seed=key[1], index=key[2], name=key[3], cases=[row[0] for row in rows],
                    max_y_spread=max(row[2] for row in rows)-min(row[2] for row in rows), max_x_spread=max(row[1] for row in rows)-min(row[1] for row in rows),
                    actual_elapsed_spread=max(row[3] for row in rows)-min(row[3] for row in rows))
               for key, rows in position_groups.items() if len(rows) > 1]
    return dict(batch=label, expected_cases=len(plans), completed_cases=len(results), pending_case_ids=sorted(set(planned)-set(committed)), replacements=replacements,
                active_sidecar_case_ids=sorted(set(sides["loadout-stats"])-set(committed)),
                checks=checked, errors=errors, warnings=warnings, read_notes=notes, plan_comparisons=comparisons,
                case_spawn_plan_hashes=[dict(stage=stage, seed=seed, case=case, planned_count=len(signature),
                                            sha256=hashlib.sha256(json.dumps(signature, ensure_ascii=False).encode()).hexdigest())
                                       for (stage, seed), entries in signatures.items() for case, signature in entries],
                position_comparison_count=len(spreads), max_spawn_y_spread=max((row["max_y_spread"] for row in spreads), default=None),
                max_spawn_actual_time_spread=max((row["actual_elapsed_spread"] for row in spreads), default=None),
                worst_spawn_position_spreads=sorted(spreads, key=lambda row: row["max_y_spread"], reverse=True)[:10],
                min_schedule_lateness=min(timing, default=None), max_schedule_lateness=max(timing, default=None))


def purchase_verification(folder, loadouts, parts, notes):
    rows = read_csv(folder / "native-purchase-validation.csv", notes)
    errors = []
    for row in rows:
        profile = row["profile"]
        build = loadouts.get(profile)
        if not build:
            errors.append(dict(profile=profile, kind="unknown_profile")); continue
        if int(row["parts"]) != len(build["parts"]) or int(row["purchased"]) != len(build["parts"]):
            errors.append(dict(profile=profile, kind="purchase_count_mismatch"))
        for kind, field in [(0, "spent_scrap"), (1, "spent_gear"), (2, "spent_core")]:
            expected_cost = sum(parts[name]["costs"].get(kind, 0) for name in build["parts"])
            if int(row[field]) != expected_cost:
                errors.append(dict(profile=profile, kind="purchase_cost_mismatch", field=field, logged=row[field], expected=expected_cost))
        expected, _ = expected_effects(build["parts"], parts, True)
        for source, dest in [("flat", "flat_damage"), ("speed", "cooldown_multiplier"), ("double", "double_shot"), ("spread", "spread_modifier"), ("ricochet", "ricochet"), ("size", "bullet_size")]:
            match = row[source].lower() == str(expected[dest]).lower() if isinstance(expected[dest], bool) else close(row[source], expected[dest])
            if not match: errors.append(dict(profile=profile, kind="purchased_effect_mismatch", field=source, logged=row[source], expected=expected[dest]))
    recorded = {r["profile"] for r in rows}
    derived = []
    placements = defaultdict(list)
    for row in read_csv(folder / "loadout-placements.csv", notes):
        placements[row["profile"]].append((row["asset"], row["x"], row["y"], row["rotation"]))
    budgets = {row["profile"]: row for row in read_csv(folder / "loadout-budgets.csv", notes)}
    for profile, build in loadouts.items():
        if profile in recorded or not profile.endswith("_module20"):
            continue
        base = profile.removesuffix("_module20")
        if base not in recorded:
            continue
        if build["parts"] != loadouts[base]["parts"] or build["module"] != 20 or loadouts[base]["module"] != 0:
            errors.append(dict(profile=profile, kind="module_derivative_loadout_mismatch"))
            continue
        if sorted(placements[profile]) != sorted(placements[base]):
            errors.append(dict(profile=profile, kind="module_derivative_placement_mismatch"))
            continue
        if not budgets.get(profile) or not budgets.get(base) or any(budgets[profile][key] != value for key, value in budgets[base].items() if key != "profile"):
            errors.append(dict(profile=profile, kind="module_derivative_parts_budget_mismatch"))
            continue
        derived.append(dict(profile=profile, purchased_parts_verified_by=base, added_module_damage=20,
                            added_module_cost_outside_part_budget={"UpgradeCore": 12, "ModuleCore_lv2": 4},
                            note="パーツの同一性・配置・予算を照合。追加モジュールの購入をnativeで実行した証拠ではない。"))
    return dict(recorded_profiles=len(rows), expected_profiles=len(loadouts), derived_profiles=derived,
                pending_profiles=sorted(set(loadouts)-recorded-{row["profile"] for row in derived}), errors=errors)


def coefficient_verification(folder, weapons, notes):
    rows = read_csv(folder / "damage-coefficient-verification.csv", notes)
    errors = []
    for row in rows:
        name = row["weapon"]
        weapon = weapons[name]
        flat = weapon["flatDamageMultiplier"] if weapon["flatDamageMultiplier"] >= 0 else weapon["accDamageMultiplier"]
        expected = dict(acc_multiplier=weapon["accDamageMultiplier"], flat_override=weapon["flatDamageMultiplier"],
                        damage_acc0_flat0=weapon["baseDamage"], acc_plus1_delta=weapon["accDamageMultiplier"] if weapon["weaponType"] == 1 else 0,
                        flat_plus1_delta=flat, module_plus5_delta=5 if name in CRAFTED else 0)
        for field, value in expected.items():
            if not close(row[field], value): errors.append(dict(weapon=name, kind="native_coefficient_mismatch", field=field, logged=row[field], expected=value))
    return dict(verified_weapon_count=len(rows), weapons=[row["weapon"] for row in rows], errors=errors,
                note="独立したUnity計算値ログの照合。全衝突ダメージの逐次ログではない。")


def punk_verification(folder, parts, loadouts, notes):
    rows = read_csv(folder / "punk-upgrade-calibration.csv", notes)
    log_path = folder / "punk-upgrade-calibration.txt"
    log = log_path.read_text() if log_path.exists() else ""
    progress_path = folder / "punk-upgrade-calibration-progress.txt"
    progress = progress_path.read_text().strip() if progress_path.exists() else "not_started"
    source_match = re.search(r"source_damage=([^ ]+) source_interval=([^ ]+)", log)
    source_damage = float(source_match.group(1)) if source_match else None
    source_interval = float(source_match.group(2)) if source_match else None
    effects = defaultdict(list)
    for row in read_csv(folder / "punk-upgrade-loadout-effects.csv", notes): effects[row["case"]].append(row)
    errors, checked = [], []
    for row in rows:
        case, profile = row["case"], row["profile"]
        def fail(kind, **details): errors.append(dict(case=case, kind=kind, **details))
        if source_damage is None:
            fail("punk_source_parameters_missing"); continue
        expected, expected_effect_rows = expected_effects(loadouts[profile]["parts"], parts, False)
        for field, expected_field in [("flat_damage", "flat_damage"), ("attack_speed", "cooldown_multiplier"), ("bullet_double", "double_shot"), ("spread_modifier", "spread_modifier"), ("ricochet", "ricochet"), ("bullet_size", "bullet_size")]:
            valid = row[field].lower() == str(expected[expected_field]).lower() if isinstance(expected[expected_field], bool) else close(row[field], expected[expected_field])
            if not valid: fail("punk_part_effect_mismatch", field=field, logged=row[field], expected=expected[expected_field])
        logged_effects = [(int(r["part_index"]), r["asset"], r["ranged_only"] == "True", r["applied"] == "True", r["effect_type"], f32(r["value"])) for r in effects[case]]
        if logged_effects != expected_effect_rows: fail("punk_part_effect_rows_mismatch")
        if int(row["part_count"]) != len(loadouts[profile]["parts"]): fail("punk_part_count_mismatch")
        if int(row["module_damage"]) != loadouts[profile]["module"]: fail("punk_module_damage_mismatch")
        if not close(row["STR"], float(row["cached_STR"])): fail("punk_cached_str_mismatch")
        frames, seconds, count = int(row["frames"]), float(row["game_seconds"]), int(row["native_damage_events"])
        if frames != 600 or abs(seconds - 10) > .05 or count <= 0: fail("punk_measurement_window_invalid", frames=frames, seconds=seconds, events=count)
        expected_per_hit = f32(f32(source_damage) + f32(float(row["cached_STR"]) / 10))
        if not close(row["event_damage"], expected_per_hit * count): fail("punk_native_damage_event_mismatch", logged=row["event_damage"], expected=expected_per_hit * count)
        if not close(row["event_dps"], float(row["event_damage"]) / seconds): fail("punk_event_dps_mismatch")
        checked.append(dict(case=case, profile=profile, STR=int(row["STR"]), events=count, frames=frames, seconds=seconds,
                            event_damage=float(row["event_damage"]), event_dps=float(row["event_dps"]), hp_damage=float(row["hp_damage"]),
                            hp_minus_event_damage=float(row["hp_damage"]) - float(row["event_damage"]), expected_damage_per_event=expected_per_hit))
    restored = progress.startswith("COMPLETE") and "status/parts_restored=True" in progress
    if progress.startswith("FAILED"): errors.append(dict(kind="punk_runner_failed", detail=progress))
    return dict(completed_cases=len(rows), expected_cases=4, progress=progress, runtime_restored=restored,
                source_damage=source_damage, source_interval=source_interval, checks=checked, errors=errors,
                note="EnemyController.Damagedイベント合計を主指標。高HPターゲットのHP減算はfloat丸め差があるため同一視しない。")


def human(report):
    lines = ["パーツ強化版・計測整合性の独立検証", "生成UTC: " + report["generated_at"], "",
             "完了CSV行のみ検証し、実行中のcaseは未完了として扱います。何度でも再実行できます。",
             "damage_per_hitはnative StatusManagerによる初期計算値のログです。全命中のHP差を個別に記録した検証ではありません。GearCraftは初期剣の値で、途中の斧切替を一つの値に平均していません。",
             "clearedと全撃破を区別し、逃走・自爆を数えます。mode=boss/maxSeconds=60は意図的観測打切りです。補充ありの処理時間を通常クリアタイムにしません。", ""]
    purchase = report["native_purchase"]
    lines.append(f"native購入検証: {purchase['recorded_profiles']}/{purchase['expected_profiles']}構成、件数/費用/効果不一致{len(purchase['errors'])}件。")
    lines.append("購入検証の費用列はパーツ分です。module20条件の追加費UpgradeCore12＋中級モジュールコア4は別途必要で、この購入ログだけではモジュール4個の作成操作まで確認したことにはなりません。")
    if purchase["derived_profiles"]:
        lines.append(f"追加module20の{len(purchase['derived_profiles'])}構成は購入済みbase構成と同じparts/配置/パーツ予算を確認。追加費UpgradeCore12＋中級モジュールコア4はパーツ予算とは別で、追加モジュール購入操作のnative確認ではありません。")
    if purchase["pending_profiles"]:
        lines.append("native購入未確認profile: " + ", ".join(purchase["pending_profiles"]))
    coefficients = report["native_damage_coefficients"]
    lines.append(f"nativeダメージ係数ログ: {coefficients['verified_weapon_count']}武器、基礎/ACC/flat/module不一致{len(coefficients['errors'])}件。")
    for issue in purchase["errors"] + coefficients["errors"]: lines.append("異常: " + json.dumps(issue, ensure_ascii=False))
    punk = report["punk_calibration"]
    lines.append(f"Punkのパーツ校正: {punk['completed_cases']}/{punk['expected_cases']}条件、不一致{len(punk['errors'])}件、runtime復元記録={punk['runtime_restored']}。")
    for issue in punk["errors"]: lines.append("異常: " + json.dumps(issue, ensure_ascii=False))
    for row in punk["checks"]:
        lines.append(f"  {row['case']}: nativeイベントDPS={row['event_dps']}、イベント数={row['events']}、HP減算との差={row['hp_minus_event_damage']}。")
    for batch in report["batches"]:
        lines += ["", f"[{batch['batch']}] 完了{batch['completed_cases']}/{batch['expected_cases']}件、異常{len(batch['errors'])}件、注意{len(batch['warnings'])}件。",
                  f"完了caseの設定効果・初期1ヒット値・shots/attack_events・敵収支・予定初期HPを照合。対照計画比較{len(batch['plan_comparisons'])}組、スポーン位置比較{batch['position_comparison_count']}件。",
                  f"実Y位置最大差={batch['max_spawn_y_spread']}、実出現時刻最大差={batch['max_spawn_actual_time_spread']}秒、予定からの最大遅れ={batch['max_schedule_lateness']}秒。",
                  "実行中sidecar: " + ", ".join(batch["active_sidecar_case_ids"])]
        for issue in batch["errors"]: lines.append("異常: " + json.dumps(issue, ensure_ascii=False))
        for issue in batch["warnings"]: lines.append("注意: " + json.dumps(issue, ensure_ascii=False))
        for replacement in batch["replacements"]:
            lines.append(f"正規採用: {replacement['case']} は {replacement['source']} の完了結果と全sidecarを優先。元記録を変更していません。")
    for history in report["superseded_history"]:
        lines.append("旧記録の履歴異常（正規結果から除外）: " + json.dumps(history, ensure_ascii=False))
    lines.append(f"正規バッチ横断のstage/seed出現列比較: {len(report['cross_batch_plans'])}組、不一致{sum(bool(group['mismatch']) for group in report['cross_batch_plans'])}組。")
    for group in report["cross_batch_plans"]:
        if group["mismatch"]: lines.append("異常: cross_batch_spawn_plan_mismatch " + json.dumps(group, ensure_ascii=False))
    lines += ["", "履歴の破棄や結果の差替えはこのスクリプトでは行いません。再測定・正式マージ後に再実行すると、その時点の正規CSVを評価します。",
              "",
              "初回asyncと報酬UI残留の影響監査：",
              "最初のroute_GearCraft_4原記録は予定21体へbootstrap由来の通常スポーン18体が重なったため不採用。route-fix完了結果があれば単独再測定の21/21体を優先し、元の異常は履歴として分離します。以後は通常wave完了処理が報酬選択のawaitで停止し、同じ余剰出現は検出されていません。",
              "報酬UIは選択しない限りTime.timeScaleや攻撃能力を変更せず、入力blockを参照する通常Player.Update/FixedUpdateはrunner中disabled。native敵AI・弾・近接判定にそのフラグ参照はありません。case2以降の通常戦闘結果は整合性検証を通った範囲で使用し、残留UI入りの画像は通常UXの根拠には使用しません。",
              "遅延したカード抽選がwave5の共有RNGを消費した可能性はありますが、lv1敵のAIは乱数を使わず、射撃RNGは分離し各caseで再seed。素材抽選差はあり得るものの、購入・BOOSTなしの固定ビルドの直接火力に結びつく経路は見つかりません。",
              "親担当のManager無効化はOnDisableによるpromiseのcancelです（未解決維持ではありません）。await後のstage++/StartNextStageへ進まないこととruntimeのIsStageActive=falseは親担当が確認済み。詳細はindependent-runner-review.txt、実介入はruntime-interventionsの記録を参照。",
              "",
              "速度・二重弾・精度の解釈：",
              "Gatlingの基礎間隔0.03秒×0.39168=約0.01175秒は、60gamefpsの1フレーム約0.01667秒より短く、1フレーム1攻撃の上限に達します。さらに速度だけを上げても攻撃回数は毎秒60回を超えません。Twinは1攻撃の弾数、Precisionは散布角を変えるので、速度のみの効果と分けて評価します。",
              "描画解像度は途中960×540から480×270へ変更されています。同じ16:9、captureDeltaTime=1/60と物理stepを維持し、botはワールド座標照準です。実時間描画性能や解像度別UXの比較には使いません。介入履歴はruntime-interventions.txt。",
              "再実行: python3 -B QA/WeaponUpgradeBalance20261015/verify_measurements.py"]
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--folder", type=Path, default=DEFAULT_FOLDER)
    parser.add_argument("--batches", nargs="+", default=["route", "probes", "fitted-final"])
    parser.add_argument("--stdout", action="store_true")
    args = parser.parse_args()
    notes = []
    loadouts = {row["profile"]: {"parts": [name for name in row["parts"].split(";") if name], "module": int(row["moduleDamage"])} for row in read_csv(args.folder / "loadouts.csv", notes)}
    parts, weapons, enemies = assets()
    batches = [verify(args.folder, label, parts, weapons, enemies, loadouts, ["route-fix"] if label == "route" else []) for label in args.batches]
    historical = []
    for batch in batches:
        if not batch["replacements"]:
            continue
        replaced_ids = {row["case"] for row in batch["replacements"]}
        raw = verify(args.folder, batch["batch"], parts, weapons, enemies, loadouts)
        historical.extend(dict(batch=batch["batch"], **issue) for issue in raw["errors"] if issue.get("case") in replaced_ids)
    cross_plan_groups = defaultdict(list)
    for batch in batches:
        for row in batch["case_spawn_plan_hashes"]:
            cross_plan_groups[(row["stage"], row["seed"])].append(dict(batch=batch["batch"], **row))
    cross_plans = [dict(stage=stage, seed=seed, mismatch=len({row["sha256"] for row in entries}) > 1, cases=entries)
                   for (stage, seed), entries in sorted(cross_plan_groups.items()) if len({row["batch"] for row in entries}) > 1]
    report = dict(generated_at=datetime.now(timezone.utc).isoformat(), read_notes=notes,
                  native_purchase=purchase_verification(args.folder, loadouts, parts, notes),
                  native_damage_coefficients=coefficient_verification(args.folder, weapons, notes),
                  punk_calibration=punk_verification(args.folder, parts, loadouts, notes),
                  batches=batches, superseded_history=historical, cross_batch_plans=cross_plans,
                  source_hashes={name: hashlib.sha256((args.folder / name).read_bytes()).hexdigest() for name in ["UpgradeCombatBenchmark.cs.txt", "NativeSurvivalRoute.cs.txt", "NativeSurvivalBootstrap.cs.txt"] if (args.folder / name).exists()})
    payload = json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    if args.stdout: print(payload, end="")
    else:
        (args.folder / "verification.json").write_text(payload, encoding="utf-8")
        (args.folder / "verification.txt").write_text(human(report), encoding="utf-8")
        print(human(report))


if __name__ == "__main__":
    main()
