#!/usr/bin/env python3
"""Verify saved native survival logs without Unity or modifying original evidence.

Run again as native-plan.csv cases finish. Writes native-verification.json/txt.
Uses asset names from each bootstrap log and compares Unity instance references
within each run; a JsonUtility instanceID alone is not treated as an asset GUID.
"""
import argparse
import json
import re
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path

import verify_measurements as vm


STATUS_FIELDS = {"flat_damage": "bonusDamage", "cooldown_multiplier": "attackSpeedMult",
                 "double_shot": "hasBulletDouble", "spread_modifier": "spreadModifier",
                 "ricochet": "ricochetCount", "bullet_size": "bulletSizeMult",
                 "junk_collector": "junkCollectorMult", "magnet_range": "magnetRange"}
EVENT_FIELDS = {"flat_damage": "bonusDamage", "attack_speed": "attackSpeedMult",
                "double_shot": "hasBulletDouble", "spread_modifier": "spreadModifier",
                "ricochet": "ricochetCount", "bullet_size": "bulletSizeMult",
                "str": "STR", "acc": "ACC", "module_damage": "craftWeaponDamagebuff"}
MULTIPLY = {100: ("gearCraftAxeSizeMultiplier", .01), 110: ("steamCannonBulletSpeedMultiplier", .01),
            111: ("steamCannonExplosionRadiusMultiplier", .01), 122: ("steamThrowerBoostDamageMultiplier", 1),
            130: ("railCraftBulletSizeMultiplier", .01), 132: ("railCraftRicochetMultiplier", 1),
            140: ("steamGatlingBulletSpeedMultiplier", .01), 141: ("steamGatlingBossDamageMultiplier", .01),
            142: ("steamGatlingNormalDamageMultiplier", .01)}
BOOLEAN = {121: "steamThrowerNoBulletGravity", 123: "steamThrowerBoostBurnDrops", 131: "railCraftApplyOverheat", 143: "steamGatlingDownwardRecoil"}


def key_values(path):
    return dict(line.split("=", 1) for line in path.read_text().splitlines() if "=" in line)


def equal(actual, expected, tolerance=2e-5):
    if isinstance(expected, bool):
        return str(actual).lower() == str(expected).lower()
    return vm.close(actual, expected, tolerance)


def card_assets():
    cards = {}
    for path in (vm.ROOT / "Assets/GearCraft/ScriptableObject/BonusCard").glob("*.asset"):
        text = path.read_text(encoding="utf-8-sig")
        if "  effects:\n" not in text:
            continue
        cards[path.stem] = [(int(kind), vm.f32(value)) for kind, value in re.findall(r"  - type: (\d+)\n    value: ([^\n]+)", text.split("  effects:\n", 1)[1])]
    return cards


def apply_reward(expected, effects, event):
    hp, gate, hp_healed, gate_healed = float(event["hp"]), float(event["gate"]), 0., 0.
    for kind, value in effects:
        if kind == 0:
            after = max(0, min(100, hp + int(value)))
            hp_healed += max(0, after - hp); hp = after
        elif kind == 1:
            gate += value; gate_healed += max(0, value)
        elif kind == 2: expected["STR"] += int(value)
        elif kind == 3: expected["ACC"] += int(value)
        elif kind == 8: expected["magnetRange"] = vm.f32(expected["magnetRange"] + value)
        elif kind == 9 and expected["STR"] >= int(value):
            expected["STR"] -= int(value); expected["ACC"] += int(value)
        elif kind == 10 and expected["ACC"] >= int(value):
            expected["ACC"] -= int(value); expected["STR"] += int(value)
        elif kind == 101: expected["gearCraftSwordMoveSpeedBonus"] = vm.f32(expected["gearCraftSwordMoveSpeedBonus"] + value)
        elif kind == 102: expected["gearCraftTransformGearGain"] += max(0, int(value))
        elif kind == 112: expected["steamCannonGiantBulletChance"] = max(0, min(1, vm.f32(expected["steamCannonGiantBulletChance"] + value)))
        elif kind in (113, 120):
            field = "steamCannonDirectHitKnockback" if kind == 113 else "steamThrowerOverheatSlipDamage"
            expected[field] = vm.f32(expected[field] + max(0, value))
        elif kind in MULTIPLY:
            field, minimum = MULTIPLY[kind]; expected[field] = vm.f32(expected[field] * max(minimum, value))
        elif kind in BOOLEAN: expected[BOOLEAN[kind]] = True
    return hp_healed, gate_healed


def card_score(effects, event, weapon, initial_gate):
    score, melee = 0., weapon == "GearCraft"
    for kind, value in effects:
        if kind == 0 and float(event["hp"]) <= 60 and value > 0: score += 10000 + min(value, 100 - float(event["hp"]))
        elif kind == 1 and float(event["gate"]) <= min(100, initial_gate * .5) and value > 0: score += 9000 + value
        elif (melee and kind == 2) or (not melee and kind == 3): score += 100 + value
        elif melee and kind == 10 and int(event["acc"]) >= int(value): score += 100 + value
        elif not melee and kind == 9 and int(event["str"]) >= int(value): score += 100 + value
        elif weapon == "GearCraft" and kind == 100: score += 60 + 10 * max(0, value - 1)
        elif weapon == "SteamCannon" and kind in (112, 111, 113): score += 60 + value
        elif weapon == "SteamThrower" and kind == 120: score += 60 + value
        elif weapon == "RailCraft" and kind in (130, 131): score += 60 + value
        elif weapon == "SteamGatling" and kind in (141, 142): score += 10 * (value - 1)
    return score


def review_case(folder, case, parts, weapons, loadouts, placements, cards, backup_count):
    label = case["label"]
    errors, warnings, pending = [], [], []
    def fail(kind, **details): errors.append(dict(kind=kind, **details))
    result_path = folder / (label + "-result.txt")
    events_path = folder / (label + "-events.csv")
    if not result_path.exists():
        return dict(label=label, state="running" if events_path.exists() else "not_started", errors=[], warnings=[])
    required = ["events.csv", "initial-status.json", "final-status.json", "bootstrap-config.txt"]
    missing = [suffix for suffix in required if not (folder / (label + "-" + suffix)).exists()]
    if missing:
        return dict(label=label, state="pending_artifacts", missing=missing, errors=[], warnings=[])
    try:
        result = key_values(result_path)
        initial = json.loads((folder / (label + "-initial-status.json")).read_text())
        final = json.loads((folder / (label + "-final-status.json")).read_text())
        bootstrap_path = folder / (label + "-bootstrap-config.txt")
        bootstrap_text = bootstrap_path.read_text()
        bootstrap = key_values(bootstrap_path)
        events = vm.read_csv(events_path, warnings)
        by_event = defaultdict(list)
        for row in events: by_event[row["event"]].append(row)
        if len(by_event["started"]) != 1 or len(by_event["finished"]) != 1:
            return dict(label=label, state="pending_event_completion", errors=[], warnings=warnings)
        first, last = by_event["started"][0], by_event["finished"][0]
        weapon_name, stage, profile = case["weapon"], int(case["stage"]), case["profile"]
        weapon_asset = "GearCraft_Sword" if weapon_name == "GearCraft" else weapon_name
        weapon = weapons[weapon_asset]
        build = loadouts[profile]
        ranged = weapon["weaponType"] == 1
        allowed_weapons = {"GearCraft_Sword", "GearCraft_Axe"} if weapon_name == "GearCraft" else {weapon_name}
        expected_effects, _ = vm.expected_effects(build["parts"], parts, ranged)
        expected = dict(initial)
        expected_attr = 2 * ((stage - 1) // 3)
        for field, value in dict(HP=100, GATE=100, STR=expected_attr, ACC=expected_attr, craftWeaponDamagebuff=build["module"]).items():
            if not equal(initial[field], value): fail("initial_stat_mismatch", field=field, actual=initial[field], expected=value)
        for field, status_field in STATUS_FIELDS.items():
            if not equal(initial[status_field], expected_effects[field]): fail("initial_part_effect_mismatch", field=status_field, actual=initial[status_field], expected=expected_effects[field])
        for field in ("module_barrier", "module_repair", "punkDrive"):
            if initial[field]: fail("unexpected_initial_auxiliary_equipment", field=field)
        wanted_config = ",".join([weapon_name, str(stage), profile, case["seed"]])
        if bootstrap_text.splitlines()[0] != wanted_config: fail("bootstrap_config_vs_plan_mismatch", expected=wanted_config)
        if Counter(name for name in bootstrap.get("parts", "").split(";") if name) != Counter(sorted(build["parts"])): fail("bootstrap_parts_mismatch")
        if not bootstrap.get("asset", "").endswith("/" + weapon_asset + ".asset"): fail("bootstrap_weapon_asset_mismatch")
        declared = [tuple(row.split(":")) for row in bootstrap.get("placements", "").split(";") if row]
        wanted_placements = [(profile, row["asset"], row["x"], row["y"], row["rotation"]) for row in placements[profile]]
        if declared != wanted_placements: fail("bootstrap_placement_manifest_mismatch")
        stored = initial["savedUpgradePartPlacements"]
        owned = initial["ownedUpgradeParts"]
        if len(stored) != len(wanted_placements) or len(owned) != len(build["parts"]):
            fail("initial_placement_or_inventory_count_mismatch")
        asset_ids, id_assets = {}, {}
        for index, (placement, wanted) in enumerate(zip(stored, wanted_placements)):
            name = wanted[1]
            if tuple(int(placement[field]) for field in ("gridX", "gridY", "rotation")) != tuple(map(int, wanted[2:])):
                fail("initial_placement_coordinates_mismatch", index=index)
            part_id = placement["partData"]["instanceID"]
            inventory_index = int(placement["inventoryIndex"])
            if part_id == 0 or inventory_index < 0 or inventory_index >= len(owned) or owned[inventory_index]["instanceID"] != part_id:
                fail("placement_inventory_reference_mismatch", index=index)
            if name in asset_ids and asset_ids[name] != part_id: fail("same_asset_different_reference", asset=name)
            if part_id in id_assets and id_assets[part_id] != name: fail("different_assets_share_reference", instanceID=part_id)
            asset_ids[name] = part_id; id_assets[part_id] = name
        if final["savedUpgradePartPlacements"] != stored: fail("placements_changed_during_native_test")
        if initial["currentWeapon"] not in initial["ownedWeapons"]: fail("initial_weapon_not_owned")
        acc = vm.f32(initial["ACC"] * weapon["accDamageMultiplier"]) if ranged else initial["STR"]
        flat_coef = weapon["flatDamageMultiplier"] if weapon["flatDamageMultiplier"] >= 0 else weapon["accDamageMultiplier"]
        initial_hit = vm.f32(weapon["baseDamage"] + vm.f32(vm.f32(acc + vm.f32(initial["bonusDamage"] * flat_coef)) + initial["craftWeaponDamagebuff"]))
        if "initial_hit_damage" in bootstrap and not equal(bootstrap["initial_hit_damage"], initial_hit): fail("bootstrap_hit_damage_mismatch", actual=bootstrap["initial_hit_damage"], expected=initial_hit)

        reward_summaries, reward_hp_healed, reward_gate_healed = [], 0., 0.
        invariant_failures = Counter()
        previous_time = -1.
        previous_counters = {field: 0 for field in ("shots", "attack_events", "spawned", "killed", "escaped", "player_damage", "gate_damage")}
        for row in events:
            if float(row["game_seconds"]) < previous_time: fail("nonmonotonic_event_time")
            previous_time = float(row["game_seconds"])
            for field in previous_counters:
                value = float(row[field])
                if value < previous_counters[field]: invariant_failures["nonmonotonic_" + field] += 1
                previous_counters[field] = value
            for field, status_field in EVENT_FIELDS.items():
                if not equal(row[field], expected[status_field], .0011 if field == "flat_damage" else 2e-5): invariant_failures["event_stat_" + field] += 1
            if row["weapon"] not in allowed_weapons: invariant_failures["unexpected_weapon"] += 1
            multiplier = 2 if ranged and expected["hasBulletDouble"] and not weapon["useLineHitbox"] else 1
            if int(row["shots"]) != int(row["attack_events"]) * multiplier: invariant_failures["shots_attack_events"] += 1
            if row["event"] == "reward_selected":
                card = row["details"].split(" | ", 1)[0]
                if card not in cards:
                    fail("unknown_selected_card", card=card)
                    continue
                offer_text = row["details"].split(" | offers: ", 1)
                offers = re.findall(r"([^;=]+)=(-?[0-9.]+);", offer_text[1]) if len(offer_text) == 2 else []
                scored = []
                for offered_name, logged_score in offers:
                    offered_name = offered_name.strip()
                    if offered_name not in cards:
                        fail("unknown_offered_card", card=offered_name); continue
                    score = card_score(cards[offered_name], row, weapon_name, initial["GATE"])
                    if not equal(logged_score, score, .006): fail("reward_score_mismatch", card=offered_name, logged=logged_score, expected=score)
                    scored.append((offered_name, score))
                if not scored or max(scored, key=lambda item: item[1])[0] != card:
                    fail("reward_selection_vs_policy_mismatch", selected=card, scores=scored)
                hp, gate = apply_reward(expected, cards[card], row)
                reward_hp_healed += hp; reward_gate_healed += gate
                reward_summaries.append(dict(card=card, at_seconds=float(row["game_seconds"]), hp_before=float(row["hp"]), hp_restored=hp, gate_restored=gate, effects=cards[card], offer_scores=scored))
                if any(kind == 5 for kind, _ in cards[card]):
                    warnings.append(dict(kind="weapon_grant_may_recalculate_parts", card=card, note="各eventの実効果維持を別途照合。"))
        for kind, count in invariant_failures.items(): fail(kind, event_count=count)
        final_fields = set(STATUS_FIELDS.values()) | {"STR", "ACC", "craftWeaponDamagebuff", "gearCraftSwordMoveSpeedBonus", "gearCraftTransformGearGain", "steamCannonGiantBulletChance", "steamCannonDirectHitKnockback", "steamThrowerOverheatSlipDamage"} | {field for field, _ in MULTIPLY.values()} | set(BOOLEAN.values())
        for field in final_fields:
            if not equal(final[field], expected[field]): fail("final_effect_vs_rewards_mismatch", field=field, actual=final[field], expected=expected[field])
        if last["details"] != result["result"]: fail("finish_reason_mismatch")
        for field in ("shots", "attack_events", "player_damage", "gate_damage", "game_seconds"):
            if not equal(last[field], float(result[field]), .002): fail("finish_summary_mismatch", field=field)
        for field, event in [("completed_native_waves", "native_wave_completed"), ("rewards_selected", "reward_selected")]:
            if int(result[field]) != len(by_event[event]): fail("finish_event_count_mismatch", field=field)
        for field, event in [("spawned", "enemy_spawned"), ("killed", "enemy_killed"), ("escaped", "enemy_escaped_or_suicided")]:
            if int(last[field]) != len(by_event[event]): fail("enemy_event_count_mismatch", field=field)
        for status_field, event_field, prefix in [("HP", "hp", "player"), ("GATE", "gate", "gate")]:
            predicted = initial[status_field] - float(result[prefix + "_damage"]) + float(result[prefix + "_healed"])
            if not equal(final[status_field], predicted, .025): fail("durability_accounting_mismatch", field=status_field, actual=final[status_field], expected=predicted)
            if not equal(final[status_field], float(last[event_field]), .015): fail("final_event_durability_mismatch", field=status_field)
        if not equal(result["player_healed"], reward_hp_healed, .025): fail("player_healing_vs_reward_mismatch", logged=result["player_healed"], expected=reward_hp_healed)
        if not equal(result["gate_healed"], reward_gate_healed, .025): fail("gate_healing_vs_reward_mismatch", logged=result["gate_healed"], expected=reward_gate_healed)
        if int(result["start_stage"]) != stage or int(first["stage"]) != stage: fail("start_stage_mismatch")
        if int(result["last_stage"]) != int(last["stage"]): fail("last_stage_mismatch")
        completed_stages = [int(row["stage"]) for row in by_event["native_wave_completed"]]
        if completed_stages != list(range(stage, stage + len(completed_stages))): fail("nonconsecutive_completed_waves", stages=completed_stages)
        reason = result["result"]
        if reason == "native_day_cleared_and_dorm_reached":
            if last["scene"] != "CraftSpace" or len(completed_stages) != 3 or len(reward_summaries) != 2 or not by_event["native_day_report_confirmed"] or int(last["stage"]) != stage + 3:
                fail("native_day_return_requirements_mismatch")
        elif reason == "player_dead" and final["HP"] > 0: fail("player_dead_with_positive_hp")
        elif reason == "gate_destroyed" and final["GATE"] > 0: fail("gate_destroyed_with_positive_gate")
        boss_names = {"IC-408", "BB-413", "AR-227", "EK-9"}
        boss_events = []
        for row in by_event["enemy_spawned"]:
            name = row["details"].split(" hp=", 1)[0]
            if name not in boss_names: continue
            kills = [entry for entry in by_event["enemy_killed"] if entry["details"] == name and float(entry["game_seconds"]) >= float(row["game_seconds"])]
            end = kills[0] if kills else last
            boss_events.append(dict(name=name, appeared=float(row["game_seconds"]), defeated=bool(kills), battle_seconds=float(end["game_seconds"])-float(row["game_seconds"]), spawn_details=row["details"]))
        if reason.startswith("ending_reached_"):
            ending = int(reason.rsplit("_", 1)[1])
            if last["scene"] != "Ending" or not any(row["name"] == "EK-9" and row["defeated"] for row in boss_events) or 31 not in completed_stages:
                fail("ending_without_final_boss_completion")
            expected_ending = 3 if final["killAllEnemies"] else 2
            if ending != expected_ending: fail("ending_branch_mismatch", expected=expected_ending, observed=ending)
        restore_path = folder / (label + "-restore-verified.txt")
        restore = restore_path.read_text().strip() if restore_path.exists() else ""
        match = re.search(r"Verified (\d+) backup keys\. EditorPlaying=(True|False)", restore)
        if not match or match.group(2) != "False": pending.append("edit_mode_restore_verification")
        elif int(match.group(1)) != backup_count: fail("restored_key_count_mismatch", logged=int(match.group(1)), expected=backup_count)
        error_path = folder / (label + "-errors.txt")
        if error_path.exists() and error_path.read_text().strip(): fail("runner_error_file", file=error_path.name)
        return dict(label=label, state="completed_with_errors" if errors else "completed_pending_restore" if pending else "verified_complete", plan=case,
                    result=reason, initial_hp=initial["HP"], initial_gate=initial["GATE"], final_hp=final["HP"], final_gate=final["GATE"],
                    player_damage=float(result["player_damage"]), gate_damage=float(result["gate_damage"]), player_healed=float(result["player_healed"]), gate_healed=float(result["gate_healed"]),
                    completed_stages=completed_stages, rewards=reward_summaries, initial_hit_damage=initial_hit,
                    shots=int(result["shots"]), attack_events=int(result["attack_events"]), observed_spawned=int(last["spawned"]), observed_killed=int(last["killed"]), observed_escaped=int(last["escaped"]),
                    boss_battles=boss_events, initial_kill_all_enemies=initial["killAllEnemies"], initial_boss_kills=initial["bossKillCount"],
                    restore_record=restore, pending=pending, errors=errors, warnings=warnings)
    except (KeyError, ValueError, TypeError, IndexError) as exc:
        return dict(label=label, state="invalid_or_incomplete_data", errors=[dict(kind="parse_or_schema_error", detail=str(exc))], warnings=warnings)


def human(report):
    lines = ["パーツ装着版・非補充native生存ログの独立検証", "生成UTC: " + report["generated_at"], "",
             f"予定{report['expected_cases']}件。復元まで確認済み{report['verified_complete_cases']}件、状態内訳: {report['states']}。",
             "初期武器・パーツ・モジュールは指定ビルドを付与した状態です。購入可能性の別検証があっても、通常入手からEDまで一つのセーブを通した実走ではありません。",
             "game_secondsには開始演出、報酬、日末回収、画面遷移の実時間待ち換算が混じるため通常クリアタイムにはしません。ボス出現→撃破/終了のイベント差と到達可否を使います。",
             "botは回避・ダッシュ・BOOST・敵弾の意図的迎撃を行わず、報酬選択も最適化されていません。武器単体の人間の生存可否・総合順位とは区別します。",
             "JsonUtilityのpartDataはinstanceIDです。bootstrapのasset名/配置とケース内参照対応を照合し、instanceIDそのものをasset GUIDとはみなしません。",
             "restore-verifiedはrunnerが8キーを読み戻して確認しEditorPlaying=Falseで保存した記録です。現在のEditor/PrefsをこのPythonで直接確認したものではありません。", ""]
    for issue in report["errors"]: lines.append("全体異常: " + json.dumps(issue, ensure_ascii=False))
    for row in report["cases"]:
        lines.append(f"{row['label']}: {row['state']}")
        if "result" in row:
            lines.append(f"  {row['result']}、完了wave={row['completed_stages']}、HP/門={row['final_hp']}/{row['final_gate']}、被害={row['player_damage']}/{row['gate_damage']}、回復={row['player_healed']}/{row['gate_healed']}。")
            lines.append(f"  shots/attack_events={row['shots']}/{row['attack_events']}、観測敵={row['observed_spawned']}、撃破={row['observed_killed']}、逃走等={row['observed_escaped']}。")
            for boss in row["boss_battles"]:
                lines.append(f"  {boss['name']}: {'撃破' if boss['defeated'] else '未討伐で終了'}、出現から{boss['battle_seconds']:.3f}秒。")
            if int(row["plan"]["stage"]) == 31:
                lines.append(f"  wave31開始時killAllEnemies={row['initial_kill_all_enemies']}、既撃破ボス={row['initial_boss_kills']}。ED到達時も最終wave→ED接続の証拠であり、全行程の真ED条件獲得ではありません。")
        for issue in row["errors"]: lines.append("  異常: " + json.dumps(issue, ensure_ascii=False))
        for issue in row["warnings"]: lines.append("  注意: " + json.dumps(issue, ensure_ascii=False))
    lines += ["", "Gearのbot変形は通常ChangeGearCraftを通らず変形時歯車報酬を再現しません。BOOST/後続購入なしの直接火力試験と素材収支を分けます。",
              "観測敵ログは初回観測前の瞬殺を取りこぼす余地があり、全敵撃破保証には使いません。native非補充は同編成固定のroute/probesとは役割が異なります。",
              "再実行: python3 -B QA/WeaponUpgradeBalance20261015/verify_native_survival.py"]
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--folder", type=Path, default=Path(__file__).resolve().parent)
    parser.add_argument("--stdout", action="store_true")
    args = parser.parse_args()
    notes = []
    plan = vm.read_csv(args.folder / "native-plan.csv", notes)
    parts, weapons, _ = vm.assets()
    loadouts = {row["profile"]: dict(parts=[name for name in row["parts"].split(";") if name], module=int(row["moduleDamage"])) for row in vm.read_csv(args.folder / "loadouts.csv", notes)}
    placements = defaultdict(list)
    for row in vm.read_csv(args.folder / "loadout-placements.csv", notes): placements[row["profile"]].append(row)
    backup_count = sum(bool(line.strip()) for line in (args.folder / "playerprefs-before.txt").read_text().splitlines())
    cards = card_assets()
    cases = [review_case(args.folder, row, parts, weapons, loadouts, placements, cards, backup_count) for row in plan]
    report = dict(generated_at=datetime.now(timezone.utc).isoformat(), expected_cases=len(plan), verified_complete_cases=sum(row["state"] == "verified_complete" for row in cases),
                  states=dict(Counter(row["state"] for row in cases)), expected_restore_keys=backup_count, read_notes=notes, cases=cases,
                  errors=[] if backup_count == 8 else [dict(kind="backup_does_not_contain_expected_eight_keys", actual=backup_count)])
    payload = json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    if args.stdout: print(payload, end="")
    else:
        (args.folder / "native-verification.json").write_text(payload, encoding="utf-8")
        (args.folder / "native-verification.txt").write_text(human(report), encoding="utf-8")
        print(human(report))


if __name__ == "__main__":
    main()
