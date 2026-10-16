#!/usr/bin/env python3
"""Re-runnable, standard-library-only analysis of CombatBenchmark snapshots.

Usage:
  python3 QA/WeaponBalance20261015/analyze_results.py
  python3 QA/WeaponBalance20261015/analyze_results.py --batches route probes
  python3 QA/WeaponBalance20261015/analyze_results.py --stdout

Only complete newline-terminated CSV records are consumed. In-progress cases
are kept separate from committed results. Default output is analysis.json/txt
beside this script; --stdout writes JSON to stdout without creating files.
"""

import argparse
import csv
import io
import json
import math
import re
import statistics
from collections import Counter, defaultdict
from datetime import datetime, timezone
from decimal import Decimal, ROUND_HALF_UP
from pathlib import Path


LIMITATIONS = [
    "完全な通常プレイの通しテストではありません。各waveを初期位置・HP・敵状態へ戻した自動戦闘の連続比較です。",
    "プレイヤーと門のHPを毎フレーム補充しています。処理完了は人間が生存してクリアできる証拠にはなりません。被害を別記します。",
    "clearedはwave終了です。自爆・門到達等で敵が消えた場合も含むため、全撃破とは区別します。",
    "timeoutの時間は観測打切り時間です。window60_*は意図的な60秒DPS観測なので討伐失敗に数えません。未討伐ボスのboss_secondsを討伐時間として扱いません。",
    "移動・照準はbot、攻撃衝突・敵AIはnative実装です。実キー入力、素材回収、宿舎・クラフト・報酬選択、ED遷移の時間を含みません。",
    "commonはSTR=ACC=2*(day-1)、bonusDamage=3、攻撃速度倍率1。専用カード・二重弾・跳弾・Punkなし。moduleは工房武装+5を一つ追加、matureは工房武装+20と速度歯車4個相当の間隔倍率0.6561です。",
    "各wave合計/平均時間はスポーン待ちを含む自動戦闘の観測処理時間です。通常プレイのクリアタイムではありません。取得時期の異なる武器同士は全道中合計で順位付けせず、共通wave19-29を別比較します。",
    "撃破率は事前登録した予定スポーン敵（scheduled_*）を主指標にします。召喚込みtotalは初回観測前の瞬殺を取りこぼす可能性があり補助値です。",
    "正規v2の予定スポーンmaxhpは出現時の期待初期HP、召喚/単体ボス観測のmaxhpは初回観測HPです。asset期待HPと比較します。",
    "正規route/probesだけを集計します。observer-v1、pilot、照準補正前のRailCraft/SteamGatlingは除外し、route-provenance.csvで出典を保持します。",
]
INT_COLUMNS = {
    "stage", "day", "seed", "total_including_summons", "killed",
    "escaped_or_suicided", "remaining", "shots", "frames", "scheduled_spawned",
    "scheduled_count", "scheduled_killed", "scheduled_escaped", "scheduled_remaining",
}
FLOAT_COLUMNS = {
    "seconds", "damage", "player_damage", "gate_damage", "boss_seconds",
    "boss_remaining_hp", "average_game_dt",
}


def read_rows(path, issues):
    """Take one text snapshot and ignore a writer's incomplete last record."""
    if not path.exists():
        return []
    text = path.read_text(encoding="utf-8-sig")
    if text and not text.endswith("\n"):
        text = text.rsplit("\n", 1)[0] + "\n" if "\n" in text else ""
        issues.append({"file": path.name, "kind": "incomplete_last_line_ignored"})
    if not text.strip():
        return []
    output = []
    for number, row in enumerate(csv.DictReader(io.StringIO(text)), 2):
        if None in row or any(value is None for value in row.values()):
            issues.append({"file": path.name, "line": number, "kind": "malformed_csv_record"})
            continue
        output.append(row)
    return output


def results(path, issues):
    parsed = []
    for row in read_rows(path, issues):
        try:
            if not row.get("id") or not row.get("weapon"):
                raise ValueError("id/weapon missing")
            for key in INT_COLUMNS.intersection(row):
                row[key] = int(row[key])
            for key in FLOAT_COLUMNS.intersection(row):
                row[key] = float(row[key])
                if not math.isfinite(row[key]):
                    raise ValueError("non-finite " + key)
            parsed.append(row)
        except (ValueError, KeyError) as exc:
            issues.append({"file": path.name, "case": row.get("id"), "kind": "invalid_result", "detail": str(exc)})
    ids = Counter(row["id"] for row in parsed)
    for case, count in ids.items():
        if count > 1:
            issues.append({"file": path.name, "case": case, "kind": "duplicate_result_id", "count": count})
    return parsed


def case_plan(path):
    if not path.exists():
        return []
    output = []
    for row in csv.reader(line for line in path.read_text().splitlines() if line.strip() and not line.startswith("#")):
        if len(row) < 6:
            continue
        output.append({"id": row[0], "weapon": row[1], "stage": int(row[2]),
                       "profile": row[3], "seed": int(row[4]), "max_seconds": float(row[5]),
                       "mode": row[6] if len(row) > 6 else "wave"})
    return output


def key_for(row):
    return row["weapon"], row.get("profile", ""), row.get("seed", 0)


def aggregate(rows):
    total = sum(row["total_including_summons"] for row in rows)
    killed = sum(row["killed"] for row in rows)
    seconds = sum(row["seconds"] for row in rows)
    damage = sum(row["damage"] for row in rows)
    escaped = sum(row["escaped_or_suicided"] for row in rows)
    remaining = sum(row["remaining"] for row in rows)
    timeout_ids = [row["id"] for row in rows if row["result"] == "timeout" and not row["id"].startswith("window60_")]
    windows = [row for row in rows if row["id"].startswith("window60_")]
    has_scheduled = bool(rows) and all("scheduled_count" in r for r in rows)
    scheduled_total = sum(row["scheduled_count"] for row in rows) if has_scheduled else None
    scheduled_killed = sum(row["scheduled_killed"] for row in rows) if has_scheduled else None
    full_kills = [row["id"] for row in rows if row["total_including_summons"] > 0
                  and row["killed"] == row["total_including_summons"]
                  and row["remaining"] == 0 and row["escaped_or_suicided"] == 0]
    return {
        "case_count": len(rows), "stages": sorted({row["stage"] for row in rows}),
        "case_ids": [row["id"] for row in rows],
        "result_counts": dict(Counter(row["result"] for row in rows)),
        "observed_processing_seconds": round(seconds, 4),
        "mean_observed_seconds_per_case": round(seconds / len(rows), 4) if rows else None,
        "total_including_summons": total, "killed": killed,
        "kill_rate": killed / total if total else None,
        "kill_rate_is_supplementary_including_observed_summons": True,
        "primary_scheduled_count": scheduled_total,
        "primary_scheduled_killed": scheduled_killed,
        "primary_scheduled_kill_rate": scheduled_killed / scheduled_total if scheduled_total else None,
        "primary_scheduled_escaped": sum(r["scheduled_escaped"] for r in rows) if has_scheduled else None,
        "primary_scheduled_remaining": sum(r["scheduled_remaining"] for r in rows) if has_scheduled else None,
        "scheduled_full_annihilation_case_count": sum(r["scheduled_count"] > 0 and r["scheduled_count"] == r["scheduled_killed"] for r in rows) if has_scheduled else None,
        "escaped_or_suicided": escaped, "remaining_at_end": remaining,
        "full_annihilation_case_count": len(full_kills), "full_annihilation_case_ids": full_kills,
        "cleared_with_escape_case_ids": [r["id"] for r in rows if r["result"] == "cleared" and r["escaped_or_suicided"] > 0],
        "timeouts": timeout_ids, "timeout_count": len(timeout_ids),
        "planned_60_second_window_case_count": len(windows),
        "planned_window_timeout_ids": [r["id"] for r in windows if r["result"] == "timeout"],
        "player_damage": round(sum(r["player_damage"] for r in rows), 4),
        "gate_damage": round(sum(r["gate_damage"] for r in rows), 4),
        "damage_dealt": round(damage, 4), "shots": sum(r["shots"] for r in rows),
        "damage_per_observed_second": round(damage / seconds, 4) if seconds else None,
        "kills_per_observed_second": killed / seconds if seconds else None,
        "time_is_censored": bool(timeout_ids),
    }


def expected_enemy_hp(name, stage, bases):
    base = bases.get(name, {}).get("base_hp") if bases else None
    scaling_stage = ((stage - 1) // 3) * 3 + 1
    return round(base * (1 + scaling_stage / 10), 6) if base is not None else None


def boss_record(row, bases=None):
    defeated = bool(row["boss_name"]) and row["boss_remaining_hp"] <= 0 and row["boss_seconds"] >= 0
    return {"id": row["id"], "weapon": row["weapon"], "profile": row["profile"],
            "seed": row["seed"], "stage": row["stage"], "boss_name": row["boss_name"],
            "expected_initial_hp": expected_enemy_hp(row["boss_name"], row["stage"], bases),
            "result": row["result"], "boss_defeated": defeated,
            "purpose": "planned_60_second_dps_window" if row["id"].startswith("window60_") else "defeat_attempt",
            "boss_defeat_seconds": row["boss_seconds"] if defeated else None,
            "boss_observed_seconds": row["boss_seconds"] if row["boss_seconds"] >= 0 else None,
            "boss_time_censored": not defeated, "boss_remaining_hp": row["boss_remaining_hp"],
            "wave_observed_seconds": row["seconds"], "player_damage": row["player_damage"],
            "gate_damage": row["gate_damage"], "damage_scope": "entire_wave_including_preboss_enemies",
            "escaped_or_suicided": row["escaped_or_suicided"]}


def enemy_bases(project_root):
    bases = {}
    for path in (project_root / "Assets/GearCraft/ScriptableObject/EnemyData").glob("*.asset"):
        text = path.read_text()
        name = re.search(r"^  enemyName: (.+)$", text, re.M)
        hp = re.search(r"^  baseHP: ([\d.eE+-]+)$", text, re.M)
        if name and hp:
            bases[name[1].strip().strip('"')] = {"base_hp": float(hp[1]), "source": str(path.relative_to(project_root))}
    return bases


def spawn_integrity(folder, label, rows, planned, issues, bases):
    completed = {row["id"]: row for row in rows}
    metadata = {row["id"]: row for row in planned}
    metadata.update(completed)
    plans = defaultdict(list)
    scheduled = defaultdict(list)
    observed = defaultdict(list)
    for row in read_rows(folder / (label + "-spawn-plan.csv"), issues):
        try:
            row.update(stage=int(row["stage"]), seed=int(row["seed"]), index=int(row["index"]), scheduled_elapsed=float(row["scheduled_elapsed"]))
            plans[row["case"]].append(row)
        except (ValueError, KeyError):
            issues.append({"kind": "invalid_spawn_plan", "row": row})
    for row in read_rows(folder / (label + "-scheduled-spawns.csv"), issues):
        try:
            row.update(index=int(row["index"]), scheduled_elapsed=float(row["scheduled_elapsed"]), actual_elapsed=float(row["actual_elapsed"]), x=float(row["x"]), y=float(row["y"]))
            scheduled[row["case"]].append(row)
        except (ValueError, KeyError):
            issues.append({"kind": "invalid_scheduled_spawn", "row": row})
    for row in read_rows(folder / (label + "-spawns.csv"), issues):
        try:
            row.update(elapsed=float(row["elapsed"]), maxhp=float(row["maxhp"]), x=float(row["x"]), y=float(row["y"]))
            observed[row["case"]].append(row)
        except (ValueError, KeyError):
            issues.append({"kind": "invalid_observed_spawn", "row": row})

    plan_groups = defaultdict(list)
    for case, plan in plans.items():
        if case not in completed or not plan:
            continue
        plan.sort(key=lambda r: r["index"])
        plan_groups[(plan[0]["stage"], plan[0]["seed"])].append(case)
        if [r["index"] for r in plan] != list(range(len(plan))):
            issues.append({"kind": "noncontiguous_spawn_plan_indices", "case": case})
    compared_groups, mismatches = [], []
    for (stage, seed), cases in sorted(plan_groups.items()):
        if len(cases) < 2:
            continue
        baseline = cases[0]
        signature = lambda case: [(r["index"], r["name"], round(r["scheduled_elapsed"], 6), r["last"]) for r in plans[case]]
        reference = signature(baseline)
        changed = [case for case in cases[1:] if signature(case) != reference]
        group = {"stage": stage, "seed": seed, "cases": cases, "baseline": baseline,
                 "same_sequence_and_schedule": not changed, "mismatched_cases": changed,
                 "planned_counts": {case: len(plans[case]) for case in cases}}
        compared_groups.append(group)
        if changed:
            mismatches.append(group)

    position_groups, lateness = defaultdict(list), []
    counts = []
    observed_hp_groups = defaultdict(list)
    initial_hp_mismatches = []
    for case, result in completed.items():
        plan_by_index = {r["index"]: r for r in plans[case]}
        for entry in scheduled[case]:
            expected = plan_by_index.get(entry["index"])
            if expected is None or (expected["name"], expected["last"]) != (entry["name"], entry["last"]) or abs(expected["scheduled_elapsed"] - entry["scheduled_elapsed"]) > 0.000002:
                issues.append({"kind": "scheduled_spawn_does_not_match_plan", "case": case, "index": entry["index"]})
            late = entry["actual_elapsed"] - entry["scheduled_elapsed"]
            lateness.append(late)
            if late < -0.0002:
                issues.append({"kind": "spawn_before_planned_time", "case": case, "index": entry["index"], "seconds": late})
            position_groups[(result["stage"], result["seed"], entry["index"], entry["name"])].append(dict(entry, weapon=result["weapon"]))
        initial_sum = sum(r["maxhp"] for r in observed[case])
        counts.append({"case": case, "planned": len(plans[case]), "scheduled": len(scheduled[case]),
                       "observed_including_summons": len(observed[case]), "result_total": result["total_including_summons"],
                       "first_observed_hp_sum": initial_sum})
        if "scheduled_count" in result and result["scheduled_count"] != len(scheduled[case]):
            issues.append({"kind": "scheduled_result_count_mismatch", "case": case,
                           "scheduled_log": len(scheduled[case]), "result_scheduled_count": result["scheduled_count"]})
        if observed[case] and len(observed[case]) != result["total_including_summons"]:
            issues.append({"kind": "observed_spawn_count_mismatch", "case": case, "logged": len(observed[case]), "result": result["total_including_summons"]})
        if plans[case] and result["result"] == "cleared" and len(scheduled[case]) != len(plans[case]):
            issues.append({"kind": "cleared_before_all_planned_spawns", "case": case, "planned": len(plans[case]), "scheduled": len(scheduled[case])})
        if observed[case] and result["damage"] > initial_sum + max(0.1, initial_sum * 0.00001):
            issues.append({"kind": "damage_exceeds_logged_initial_hp", "case": case, "damage": result["damage"], "initial_hp_sum": initial_sum})
        scaling_stage = ((result["stage"] - 1) // 3) * 3 + 1
        for entry in observed[case]:
            base = bases.get(entry["name"])
            expected_hp = base["base_hp"] * (1 + scaling_stage / 10) if base else None
            observed_hp_groups[(result["stage"], entry["name"], expected_hp)].append(entry["maxhp"])
            if expected_hp is not None and abs(entry["maxhp"] - expected_hp) > max(0.001, expected_hp * 0.00001):
                initial_hp_mismatches.append({"case": case, "name": entry["name"], "elapsed": entry["elapsed"],
                                             "first_observed_hp": entry["maxhp"], "expected_initial_hp": expected_hp})
    spreads = []
    for (stage, seed, index, name), entries in position_groups.items():
        if len({e["weapon"] for e in entries}) < 2:
            continue
        spreads.append({"stage": stage, "seed": seed, "index": index, "name": name,
                        "cases": [e["case"] for e in entries],
                        "y_spread": round(max(e["y"] for e in entries) - min(e["y"] for e in entries), 6),
                        "x_spread": round(max(e["x"] for e in entries) - min(e["x"] for e in entries), 6),
                        "actual_elapsed_spread": max(e["actual_elapsed"] for e in entries) - min(e["actual_elapsed"] for e in entries)})
    spreads.sort(key=lambda e: e["y_spread"], reverse=True)
    hp_summary = [{"stage": stage, "name": name, "expected_initial_hp": expected, "count": len(values),
                   "first_observed_hp_min": min(values), "first_observed_hp_max": max(values)}
                  for (stage, name, expected), values in sorted(observed_hp_groups.items())]
    return {"completed_case_count": len(completed), "pending_plan_case_ids": sorted(set(plans) - set(completed)),
            "plan_comparison_groups": compared_groups, "plan_mismatch_group_count": len(mismatches),
            "plan_mismatches": mismatches, "cross_weapon_spawn_positions_compared": len(spreads),
            "max_cross_weapon_spawn_y_spread": max((r["y_spread"] for r in spreads), default=None),
            "max_cross_weapon_spawn_x_spread": max((r["x_spread"] for r in spreads), default=None),
            "max_cross_weapon_actual_elapsed_spread": max((r["actual_elapsed_spread"] for r in spreads), default=None),
            "worst_spawn_y_spreads": spreads[:20],
            "min_spawn_schedule_lateness": min(lateness, default=None), "max_spawn_schedule_lateness": max(lateness, default=None),
            "case_spawn_counts": counts, "initial_hp_summary": hp_summary,
            "initial_hp_mismatch_count": len(initial_hp_mismatches), "initial_hp_mismatches": initial_hp_mismatches,
            "hp_log_note": "予定スポーンは事前登録した期待初期HP、召喚/単体ボスは初回観測HP。後者は初回観測前被弾・瞬殺の可能性を含む。"}


def analyze_batch(folder, label, bases):
    issues = []
    path = folder / (label + "-results.csv")
    rows = results(path, issues)
    planned = case_plan(folder / (label + "-cases.csv"))
    ids = {row["id"] for row in rows}
    grouped, planned_groups = defaultdict(list), defaultdict(list)
    for row in rows:
        grouped[key_for(row)].append(row)
        total = row["killed"] + row["escaped_or_suicided"] + row["remaining"]
        if total != row["total_including_summons"]:
            issues.append({"kind": "enemy_accounting_mismatch", "case": row["id"], "sum": total, "total": row["total_including_summons"]})
        if "scheduled_count" in row:
            scheduled_sum = row["scheduled_killed"] + row["scheduled_escaped"] + row["scheduled_remaining"]
            if scheduled_sum != row["scheduled_count"]:
                issues.append({"kind": "scheduled_enemy_accounting_mismatch", "case": row["id"],
                               "sum": scheduled_sum, "scheduled_count": row["scheduled_count"]})
        if any(row[k] < 0 for k in ("seconds", "damage", "player_damage", "gate_damage", "killed", "remaining", "escaped_or_suicided")):
            issues.append({"kind": "unexpected_negative_measurement", "case": row["id"]})
        if row["result"] == "cleared" and row["remaining"] != 0:
            issues.append({"kind": "cleared_with_remaining_enemies", "case": row["id"]})
        mismatch = abs(row["seconds"] - row["frames"] * row["average_game_dt"])
        if mismatch > 0.06 + row["frames"] * 0.0000006:
            issues.append({"kind": "frame_time_mismatch", "case": row["id"], "absolute_seconds": mismatch})
    for row in planned:
        planned_groups[key_for(row)].append(row)
    groups = []
    for key in sorted(set(grouped) | set(planned_groups)):
        entries = sorted(grouped[key], key=lambda row: (row["stage"], row["id"]))
        expected = planned_groups[key]
        route = aggregate(entries)
        expected_stages = sorted({r["stage"] for r in expected})
        minimum = min(expected_stages or route["stages"]) if expected_stages or route["stages"] else None
        is_route = label == "route"
        missing = [r["id"] for r in expected if r["id"] not in ids]
        route.update(expected_case_count=len(expected) if expected else None, missing_case_ids=missing,
                     acquisition_start_wave=minimum if is_route else None,
                     planned_through_wave=max(expected_stages) if expected_stages else None,
                     all_planned_cases_recorded=not missing if expected else None)
        finished_route = is_route and bool(expected) and not missing and 31 in expected_stages and not route["timeouts"]
        route["complete_route_observed_processing_seconds"] = route["observed_processing_seconds"] if finished_route else None
        common = aggregate([r for r in entries if 19 <= r["stage"] <= 29 and not r["boss_name"]])
        common["missing_stages_19_29"] = sorted(set(range(19, 30)) - set(common["stages"]))
        common["all_11_stages_recorded"] = not common["missing_stages_19_29"]
        common["comparable_full_window_seconds"] = common["observed_processing_seconds"] if common["all_11_stages_recorded"] and not common["timeouts"] else None
        groups.append({"weapon": key[0], "profile": key[1], "seed": key[2],
                       "route_or_cases": route, "common_normal_waves_19_29": common,
                       "bosses": [boss_record(r, bases) for r in entries if r["boss_name"]],
                       "cases": entries})
    integrity = spawn_integrity(folder, label, rows, planned, issues, bases)
    provenance = read_rows(folder / (label + "-provenance.csv"), issues)
    return {"label": label, "source_exists": path.exists(), "source": str(path),
            "recorded_cases": len(rows), "expected_cases": len(planned) if planned else None,
            "missing_case_ids": [r["id"] for r in planned if r["id"] not in ids],
            "overall": aggregate(rows), "groups": groups, "spawn_integrity": integrity,
            "provenance": provenance, "data_issues": issues}


def cross_batch_comparisons(batches, bases):
    """Combine route seed77 with probes seed78/79 without treating windows as failures."""
    rows = [dict(row, batch=batch["label"]) for batch in batches
            for group in batch["groups"] for row in group["cases"]]
    wave_groups = defaultdict(list)
    lookup = {}
    windows, mature = [], []
    for row in rows:
        if row["id"].startswith("window60_"):
            record = boss_record(row, bases)
            expected = record["expected_initial_hp"]
            removed = expected - row["boss_remaining_hp"] if expected is not None else None
            record["boss_hp_removed"] = removed
            record["observed_boss_dps"] = removed / row["seconds"] if removed is not None and row["seconds"] > 0 else None
            record["shots"] = row["shots"]
            windows.append(record)
        elif row["profile"] == "mature":
            record = boss_record(row, bases)
            record["craft_weapon_flat_bonus"] = 20
            record["attack_interval_multiplier"] = 0.6561
            mature.append(record)
        elif not row["boss_name"] and row["stage"] in (10, 19, 28):
            wave_groups[(row["weapon"], row["stage"], row["profile"])].append(row)
            lookup[(row["weapon"], row["stage"], row["profile"], row["seed"])] = row
    repetitions = []
    for (weapon, stage, profile), entries in sorted(wave_groups.items()):
        seconds = [r["seconds"] for r in entries]
        record = aggregate(entries)
        record.update(weapon=weapon, stage=stage, profile=profile,
                      seeds=sorted(r["seed"] for r in entries), expected_seeds=[77, 78, 79],
                      all_three_seeds_recorded={77, 78, 79}.issubset({r["seed"] for r in entries}),
                      observed_seconds_min=min(seconds), observed_seconds_max=max(seconds),
                      observed_seconds_mean=statistics.mean(seconds),
                      observed_seconds_sample_stdev=statistics.stdev(seconds) if len(seconds) > 1 else None)
        repetitions.append(record)
    module_pairs = []
    for (weapon, stage, profile, seed), common in sorted(lookup.items()):
        if profile != "common" or stage != 19:
            continue
        module = lookup.get((weapon, stage, "module", seed))
        if module is None:
            continue
        common_kill_rate = common["scheduled_killed"] / common["scheduled_count"] if common.get("scheduled_count") else None
        module_kill_rate = module["scheduled_killed"] / module["scheduled_count"] if module.get("scheduled_count") else None
        module_pairs.append({"weapon": weapon, "stage": stage, "seed": seed,
                             "common_case": common["id"], "module_case": module["id"],
                             "common_seconds": common["seconds"], "module_seconds": module["seconds"],
                             "time_saved_seconds": common["seconds"] - module["seconds"],
                             "module_to_common_time_ratio": module["seconds"] / common["seconds"] if common["seconds"] else None,
                             "common_scheduled_kill_rate": common_kill_rate, "module_scheduled_kill_rate": module_kill_rate,
                             "common_gate_damage": common["gate_damage"], "module_gate_damage": module["gate_damage"],
                             "any_timeout": common["result"] == "timeout" or module["result"] == "timeout"})
    window_lookup = {(r["weapon"], r["profile"]): r for r in windows}
    window_ratios = []
    for weapon in sorted({r["weapon"] for r in windows}):
        common, module = window_lookup.get((weapon, "common")), window_lookup.get((weapon, "module"))
        if common is not None and module is not None:
            a, b = common["observed_boss_dps"], module["observed_boss_dps"]
            window_ratios.append({"weapon": weapon, "common_dps": a, "module_dps": b,
                                  "module_to_common_dps_ratio": b / a if a else None})
    return {"three_seed_wave_comparisons": repetitions, "paired_module_wave19": module_pairs,
            "intentional_60_second_boss_windows": windows, "module_boss_dps_ratios": window_ratios,
            "mature_ek_defeat_attempts": mature}


def compact_tables(batches, comparisons):
    """Small, presentation-ready rows; no synthetic human completion estimate."""
    route = next((b for b in batches if b["label"] == "route"), None)
    whole_route, common_rows, boss_rows = [], [], []
    for group in route["groups"] if route else []:
        summary = group["route_or_cases"]
        common = group["common_normal_waves_19_29"]
        whole_route.append({"weapon": group["weapon"], "profile": group["profile"],
                            "from_wave": summary["acquisition_start_wave"], "to_wave": 31,
                            "recorded_cases": summary["case_count"], "expected_cases": summary["expected_case_count"],
                            "observed_processing_seconds_including_spawn_wait": summary["observed_processing_seconds"],
                            "all_recorded": summary["all_planned_cases_recorded"],
                            "timeout_count": summary["timeout_count"],
                            "scheduled_kill_rate": summary["primary_scheduled_kill_rate"],
                            "scheduled_escaped": summary["primary_scheduled_escaped"],
                            "player_damage": summary["player_damage"], "gate_damage": summary["gate_damage"]})
        common_rows.append({"weapon": group["weapon"], "profile": group["profile"],
                            "recorded_waves": common["case_count"], "all_11_recorded": common["all_11_stages_recorded"],
                            "total_processing_seconds_including_spawn_wait": common["observed_processing_seconds"],
                            "mean_processing_seconds_including_spawn_wait": common["mean_observed_seconds_per_case"],
                            "scheduled_killed": common["primary_scheduled_killed"],
                            "scheduled_count": common["primary_scheduled_count"],
                            "scheduled_kill_rate": common["primary_scheduled_kill_rate"],
                            "scheduled_escaped": common["primary_scheduled_escaped"],
                            "timeout_count": common["timeout_count"],
                            "player_damage": common["player_damage"], "gate_damage": common["gate_damage"]})
        boss_rows.extend(group["bosses"])
    common_rows.sort(key=lambda r: r["mean_processing_seconds_including_spawn_wait"] or float("inf"))
    wave19 = {(r["weapon"], r["profile"]): r for r in comparisons["three_seed_wave_comparisons"] if r["stage"] == 19}
    ranks = {}
    for profile in ("common", "module"):
        candidates = [r for (weapon, p), r in wave19.items() if p == profile]
        if len(candidates) == 5 and all(r["all_three_seeds_recorded"] and not r["timeouts"] for r in candidates):
            for rank, record in enumerate(sorted(candidates, key=lambda r: r["observed_seconds_mean"]), 1):
                ranks[(record["weapon"], profile)] = rank
    module_rows = []
    for weapon in sorted({key[0] for key in wave19}):
        common, module = wave19.get((weapon, "common")), wave19.get((weapon, "module"))
        common_rank, module_rank = ranks.get((weapon, "common")), ranks.get((weapon, "module"))
        module_rows.append({"weapon": weapon,
                            "common_samples": common["case_count"] if common else 0,
                            "module_samples": module["case_count"] if module else 0,
                            "common_mean_seconds": common["observed_seconds_mean"] if common else None,
                            "module_mean_seconds": module["observed_seconds_mean"] if module else None,
                            "common_time_rank": common_rank, "module_time_rank": module_rank,
                            "rank_improvement": common_rank - module_rank if common_rank and module_rank else None,
                            "common_scheduled_kill_rate": common["primary_scheduled_kill_rate"] if common else None,
                            "module_scheduled_kill_rate": module["primary_scheduled_kill_rate"] if module else None,
                            "common_gate_damage": common["gate_damage"] if common else None,
                            "module_gate_damage": module["gate_damage"] if module else None})
    return {
        "time_label": "bot自動戦闘の処理時間（出現待ち込み、HP補充あり）。通常クリアタイムではない。",
        "rank_label": "wave19・同じ3seedの平均処理時間順位。撃破率と門被害も併記し総合性能順位とは呼ばない。全5武器×3seedが揃うまで順位は出さない。",
        "route": whole_route, "normal_waves_19_29": common_rows, "route_bosses": boss_rows,
        "wave19_module_rank_change": module_rows,
        "ek_60_second_dps": comparisons["intentional_60_second_boss_windows"],
        "ek_module_dps_ratios": comparisons["module_boss_dps_ratios"],
        "ek_mature": comparisons["mature_ek_defeat_attempts"],
    }


def number(value, decimals=2):
    if value is None:
        return "未計測"
    # Decimal input text keeps display ties independent of binary float storage.
    rounded = Decimal(str(value)).quantize(Decimal(1).scaleb(-decimals), rounding=ROUND_HALF_UP)
    return f"{rounded:.{decimals}f}"


def human_tables(report):
    data = report["compact_tables"]
    percent = lambda value: "未計測" if value is None else f"{value * 100:.1f}%"
    lines = ["工房武装・計測要約表", "", data["time_label"],
             "撃破は予定スポーン敵のHPが0以下になった数。HP残存で消滅した逃走・自爆を区別。", ""]
    def table(title, headers, entries):
        lines.extend([title, "", "| " + " | ".join(headers) + " |", "| " + " | ".join("---" for _ in headers) + " |"])
        lines.extend("| " + " | ".join(str(cell) for cell in row) + " |" for row in entries)
        lines.append("")
    table("取得想定waveから31までの観測合計（開始waveが違うため直接順位付けしない）",
          ["武器", "開始wave", "記録/予定", "観測合計秒", "timeout", "予定撃破率", "逃走等", "門被害"],
          [[r["weapon"], r["from_wave"], f"{r['recorded_cases']}/{r['expected_cases']}",
            number(r["observed_processing_seconds_including_spawn_wait"]), r["timeout_count"],
            percent(r["scheduled_kill_rate"]), r["scheduled_escaped"], number(r["gate_damage"])] for r in data["route"]])
    table("後半の共通通常wave19-29（11wave、出現待ち込み）",
          ["武器", "記録wave数", "合計秒", "平均秒/wave", "予定撃破", "逃走等", "player被害", "門被害"],
          [[r["weapon"], r["recorded_waves"], number(r["total_processing_seconds_including_spawn_wait"]),
            number(r["mean_processing_seconds_including_spawn_wait"]), f"{r['scheduled_killed']}/{r['scheduled_count']}",
            r["scheduled_escaped"], number(r["player_damage"]), number(r["gate_damage"])] for r in data["normal_waves_19_29"]])
    table("ボス出現から討伐まで（道中の出現待ちを除く）",
          ["武器", "ボス/wave", "初期HP", "討伐秒", "未討伐時の観測秒/残HP", "wave全体player被害", "wave全体門被害"],
          [[r["weapon"], f"{r['boss_name']}/{r['stage']}", number(r["expected_initial_hp"], 0),
            number(r["boss_defeat_seconds"]) if r["boss_defeated"] else "未討伐",
            "―" if r["boss_defeated"] else f"{number(r['boss_observed_seconds'])} / {number(r['boss_remaining_hp'])}",
            number(r["player_damage"]), number(r["gate_damage"])] for r in data["route_bosses"]])
    lines.extend([data["rank_label"], ""])
    table("wave19・+5モジュール前後（平均処理時間、3seed）",
          ["武器", "common件数", "+5件数", "common平均秒/順位", "+5平均秒/順位", "common撃破率", "+5撃破率"],
          [[r["weapon"], r["common_samples"], r["module_samples"],
            f"{number(r['common_mean_seconds'])} / {r['common_time_rank'] or '未確定'}",
            f"{number(r['module_mean_seconds'])} / {r['module_time_rank'] or '未確定'}",
            percent(r["common_scheduled_kill_rate"]), percent(r["module_scheduled_kill_rate"])] for r in data["wave19_module_rank_change"]])
    table("EK単体・60秒の意図的観測（timeoutを討伐失敗に数えない）",
          ["武器", "条件", "HP減少", "観測DPS", "player被害", "門被害"],
          [[r["weapon"], r["profile"], number(r["boss_hp_removed"]), number(r["observed_boss_dps"]),
            number(r["player_damage"]), number(r["gate_damage"])] for r in data["ek_60_second_dps"]])
    table("成熟ビルドのEK討伐（工房武装+20、攻撃間隔×0.6561）",
          ["武器", "初期HP", "討伐秒", "未討伐時の観測秒/残HP", "player被害", "門被害"],
          [[r["weapon"], number(r["expected_initial_hp"], 0), number(r["boss_defeat_seconds"]) if r["boss_defeated"] else "未討伐",
            "―" if r["boss_defeated"] else f"{number(r['boss_observed_seconds'])} / {number(r['boss_remaining_hp'])}",
            number(r["player_damage"]), number(r["gate_damage"])] for r in data["ek_mature"]])
    return "\n".join(lines) + "\n"


def human_text(report):
    lines = ["工房武装・自動戦闘計測の集計", "生成日時: " + report["generated_at"], ""]
    lines.extend("注意: " + item for item in LIMITATIONS)
    for batch in report["batches"]:
        lines += ["", f"[{batch['label']}] 記録済み {batch['recorded_cases']} / 予定 {batch['expected_cases'] if batch['expected_cases'] is not None else '不明'} case"]
        for group in batch["groups"]:
            route, common = group["route_or_cases"], group["common_normal_waves_19_29"]
            rate = number(route["primary_scheduled_kill_rate"] * 100 if route["primary_scheduled_kill_rate"] is not None else None)
            lines += [f"{group['weapon']} / {group['profile']} / seed {group['seed']}",
                      f"  観測wave={route['stages']}、取得想定wave={route['acquisition_start_wave']}、処理時間合計={number(route['observed_processing_seconds'])}秒（未記録={len(route['missing_case_ids'])}、timeout={route['timeout_count']}）。",
                      f"  主指標・予定スポーン撃破={route['primary_scheduled_killed']}/{route['primary_scheduled_count']}（{rate}%）、逃走・自爆={route['primary_scheduled_escaped']}、残存={route['primary_scheduled_remaining']}。",
                      f"  補助・観測召喚込み撃破={route['killed']}/{route['total_including_summons']}、逃走・自爆={route['escaped_or_suicided']}、残存={route['remaining_at_end']}。clearedのうち逃走等あり={len(route['cleared_with_escape_case_ids'])}件。",
                      f"  プレイヤー被害={number(route['player_damage'])}、門被害={number(route['gate_damage'])}（HP補充条件）。",
                      f"  共通通常wave19-29: 記録={common['case_count']}件、合計観測={number(common['observed_processing_seconds'])}秒、全11wave比較用時間={number(common['comparable_full_window_seconds'])}秒、未記録wave={common['missing_stages_19_29']}。",
                      f"  共通通常区間・予定敵: 撃破={common['primary_scheduled_killed']}/{common['primary_scheduled_count']}、逃走・自爆={common['primary_scheduled_escaped']}、player被害={number(common['player_damage'])}、gate被害={number(common['gate_damage'])}。"]
            for boss in group["bosses"]:
                timing = (f"意図的60秒観測、討伐成否を評価しない、残HP {number(boss['boss_remaining_hp'])}" if boss["purpose"] == "planned_60_second_dps_window" else
                          f"討伐 {number(boss['boss_defeat_seconds'])}秒" if boss["boss_defeated"]
                          else f"未討伐・観測 {number(boss['boss_observed_seconds'])}秒、残HP {number(boss['boss_remaining_hp'])}")
                lines.append(f"  ボスwave{boss['stage']} {boss['boss_name']}: {timing}、wave結果={boss['result']}、player被害={number(boss['player_damage'])}、gate被害={number(boss['gate_damage'])}。")
        integrity = batch["spawn_integrity"]
        lines += ["  データ整合性:",
                  f"    同stage/seedの出現計画比較={len(integrity['plan_comparison_groups'])}組、不一致={integrity['plan_mismatch_group_count']}組。",
                  f"    武器間スポーン位置比較={integrity['cross_weapon_spawn_positions_compared']}件、最大Y差={number(integrity['max_cross_weapon_spawn_y_spread'], 6)}、最大X差={number(integrity['max_cross_weapon_spawn_x_spread'], 6)}。",
                  f"    実スポーン時刻の武器間最大差={number(integrity['max_cross_weapon_actual_elapsed_spread'], 6)}秒、計画からの最大遅延={number(integrity['max_spawn_schedule_lateness'], 6)}秒。",
                  f"    初回観測HPとasset期待HPの不一致={integrity['initial_hp_mismatch_count']}件。他の記録異常/部分行通知={len(batch['data_issues'])}件。"]
        for issue in batch["data_issues"][:20]:
            lines.append("    " + json.dumps(issue, ensure_ascii=False))
    compare = report["comparisons"]
    lines += ["", "[正規route/probesを統合した補助比較]"]
    for record in compare["three_seed_wave_comparisons"]:
        lines.append(f"  {record['weapon']} wave{record['stage']} {record['profile']} seeds={record['seeds']}: 平均観測{number(record['observed_seconds_mean'])}秒、範囲{number(record['observed_seconds_min'])}～{number(record['observed_seconds_max'])}秒、予定撃破={record['primary_scheduled_killed']}/{record['primary_scheduled_count']}、timeout={record['timeout_count']}。")
    for record in compare["paired_module_wave19"]:
        lines.append(f"  +5比較 {record['weapon']} wave19 seed{record['seed']}: common {number(record['common_seconds'])}秒 → module {number(record['module_seconds'])}秒、時間比={number(record['module_to_common_time_ratio'], 3)}、timeout混在={record['any_timeout']}。")
    for record in compare["intentional_60_second_boss_windows"]:
        lines.append(f"  EK60秒観測 {record['weapon']} {record['profile']}: HP減少={number(record['boss_hp_removed'])}、観測DPS={number(record['observed_boss_dps'])}。意図的打切りであり討伐失敗ではない。")
    for record in compare["mature_ek_defeat_attempts"]:
        timing = f"討伐{number(record['boss_defeat_seconds'])}秒" if record["boss_defeated"] else f"未討伐・観測{number(record['boss_observed_seconds'])}秒、残HP{number(record['boss_remaining_hp'])}"
        lines.append(f"  mature EK {record['weapon']}: {timing}、player被害={number(record['player_damage'])}、gate被害={number(record['gate_damage'])}。")
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--folder", type=Path, default=Path(__file__).resolve().parent)
    parser.add_argument("--batches", nargs="+", default=["route", "probes"])
    parser.add_argument("--output-prefix", default="analysis")
    parser.add_argument("--stdout", action="store_true", help="Print JSON only; do not write result files")
    args = parser.parse_args()
    folder = args.folder.resolve()
    project_root = Path(__file__).resolve().parents[2]
    bases = enemy_bases(project_root)
    batches = [analyze_batch(folder, label, bases) for label in args.batches]
    report = {"generated_at": datetime.now(timezone.utc).isoformat(), "limitations": LIMITATIONS,
              "runner_source": str(folder / "CombatBenchmark.cs.txt"), "batches": batches,
              "comparisons": cross_batch_comparisons(batches, bases)}
    report["compact_tables"] = compact_tables(batches, report["comparisons"])
    payload = json.dumps(report, ensure_ascii=False, indent=2, allow_nan=False) + "\n"
    if args.stdout:
        print(payload, end="")
    else:
        prefix = folder / args.output_prefix
        prefix.with_suffix(".json").write_text(payload, encoding="utf-8")
        prefix.with_suffix(".txt").write_text(human_text(report), encoding="utf-8")
        table_path = prefix.parent / (prefix.name + "-tables.txt")
        table_path.write_text(human_tables(report), encoding="utf-8")
        print(f"Wrote {prefix.with_suffix('.json')}, {prefix.with_suffix('.txt')}, and {table_path}")


if __name__ == "__main__":
    main()
