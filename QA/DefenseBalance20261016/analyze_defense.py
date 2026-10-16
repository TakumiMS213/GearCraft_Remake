#!/usr/bin/env python3
"""Summarize native defense observations; never operates Unity or game state.

Run repeatedly while result files arrive:
  python3 QA/DefenseBalance20261016/analyze_defense.py
  python3 QA/DefenseBalance20261016/analyze_defense.py --folder /path/to/logs

Only derived summary.csv, tables.txt and attack-pauses.csv are replaced.
An explicit boss defeat AND a native day/ending transition are required for success.
"""
import argparse
import csv
import io
import json
import math
import statistics
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path


BOSSES = {7: (9, "IC-408"), 16: (18, "BB-413"), 28: (30, "AR-227"), 31: (31, "EK-9")}
FIELDS = "label primary weapon profile seed day start_stage boss_stage boss state measurement_valid invalid_reason result defense_success boss_reached boss_defeated boss_result boss_seconds boss_start_hp boss_remaining_hp boss_start_player_hp boss_end_player_hp boss_start_gate boss_end_gate boss_player_damage boss_gate_damage start_hp end_hp start_gate end_gate player_damage gate_damage player_healed gate_healed game_seconds completed_native_waves wave_rows wave_rows_completed wave_seconds rewards_selected shots attack_events initial_STR initial_ACC initial_flat_damage initial_module_damage initial_attack_speed initial_punk initial_repair initial_barrier initial_scrap_module part_count final_STR final_ACC final_flat_damage final_module_damage reactive_dashes reactive_intercept_targets reactive_boosts sample_count sample_max_gap attack_pause_count attack_pause_max_seconds boss_attack_pause_count boss_attack_pause_max_seconds attack_pause_reasons warnings".split()
FIELDS += "preboss_completed_wave_seconds boss_wave_before_spawn_seconds before_boss_elapsed wave_times boss_entry_weapon boss_entry_STR boss_entry_ACC boss_entry_flat_damage boss_entry_module_damage boss_entry_attack_speed boss_entry_double_shot boss_entry_spread_modifier initial_hit_damage".split()
FIELDS += "boss_sample_phases boss_sample_states boss_armor_samples boss_stagger_samples boss_crossfire_observed boss_combined_observed".split()
FIELDS += ["initial_gear"]
FIELDS += "min_player_y max_player_y physical_anomaly physical_anomaly_reason".split()
PAUSE_FIELDS = "label start_seconds end_seconds seconds stage attack_events boss_present start_suppression end_suppression endpoint_input_blocked endpoint_flow_active start_live end_live max_sample_gap sample_intervals".split()


def number(value, default=None):
    try:
        result = float(value)
        return result if math.isfinite(result) else default
    except (TypeError, ValueError):
        return default


def truth(value):
    return str(value).strip().lower() in ("1", "true", "yes")


def fmt(value, digits=3):
    value = number(value)
    if value is None:
        return "—"
    return f"{value:.{digits}f}".rstrip("0").rstrip(".")


def read_text(path):
    return path.read_text(encoding="utf-8-sig") if path.exists() else ""


def read_key_values(path):
    return dict(line.split("=", 1) for line in read_text(path).splitlines() if "=" in line)


def read_json(path, warnings):
    if not path.exists():
        return {}
    try:
        return json.loads(read_text(path))
    except (ValueError, OSError) as exc:
        warnings.append(f"{path.name}: JSON未完了/読取失敗 {type(exc).__name__}")
        return {}


def read_csv(path, warnings):
    if not path.exists():
        return []
    try:
        rows = list(csv.DictReader(io.StringIO(read_text(path))))
    except (csv.Error, OSError) as exc:
        warnings.append(f"{path.name}: CSV読取失敗 {type(exc).__name__}")
        return []
    complete = []
    for row in rows:
        if None in row or any(value is None for value in row.values()):
            warnings.append(f"{path.name}: 書込み途中/列数不一致行を除外")
        else:
            complete.append(row)
    return complete


def atomic_text(path, content):
    temporary = path.with_name(path.name + ".analysis-tmp")
    temporary.write_text(content, encoding="utf-8")
    temporary.replace(path)


def write_csv(path, fields, rows):
    buffer = io.StringIO(newline="")
    writer = csv.DictWriter(buffer, fieldnames=fields, extrasaction="ignore", lineterminator="\n")
    writer.writeheader()
    writer.writerows(rows)
    atomic_text(path, buffer.getvalue())


def attack_pauses(label, rows, boss_name, warnings):
    """Deduplicate per-enemy rows, then find non-incrementing sampled counters.

    Consecutive equal counters prove no recorded attacks between endpoints, but
    endpoint flow/input flags do not prove the entire interval was actionable.
    Legitimate cooldown, offscreen enemies and inter-wave waits are not bugs.
    """
    grouped = defaultdict(list)
    for row in rows:
        timestamp = number(row.get("game_seconds"))
        if timestamp is not None:
            grouped[timestamp].append(row)
    samples = []
    for timestamp, group in sorted(grouped.items()):
        row = dict(group[0])
        row["timestamp"] = timestamp
        row["boss_present"] = any(truth(item.get("is_boss")) and
                                  (not boss_name or item.get("enemy_name") == boss_name)
                                  for item in group)
        shared = ("attack_events", "stage", "hp", "gate", "flow_active", "input_blocked")
        if any(len({item.get(key, "") for item in group}) != 1 for key in shared):
            warnings.append(f"samples時刻{timestamp}: 敵別行の共通値が不一致")
        samples.append(row)
    max_gap = max((right["timestamp"] - left["timestamp"] for left, right in zip(samples, samples[1:])), default=0)
    if max_gap > 1.5:
        warnings.append(f"samples最大間隔{max_gap:.3f}秒（標準約1秒より疎）")
    intervals, active = [], None

    def close():
        nonlocal active
        if active and active["seconds"] >= 1.0 - 0.001:
            intervals.append(active)
        active = None

    for left, right in zip(samples, samples[1:]):
        before, after = number(left.get("attack_events")), number(right.get("attack_events"))
        delta = right["timestamp"] - left["timestamp"]
        if before is not None and after is not None and after < before:
            warnings.append(f"{right['timestamp']:.3f}秒: attack_eventsが減少")
        eligible = (before is not None and before == after and delta > 0 and
                    left.get("stage") == right.get("stage") and
                    truth(left.get("flow_active")) and truth(right.get("flow_active")) and
                    not truth(left.get("input_blocked")) and not truth(right.get("input_blocked")) and
                    number(left.get("live_enemy_count"), 0) > 0 and number(right.get("live_enemy_count"), 0) > 0)
        if not eligible:
            close()
            continue
        both_boss = left["boss_present"] and right["boss_present"]
        if active and (active["boss_present"] != both_boss or active["end_seconds"] != left["timestamp"]):
            close()
        if active is None:
            active = dict(label=label, start_seconds=left["timestamp"], end_seconds=left["timestamp"],
                          seconds=0, stage=left.get("stage", ""), attack_events=before,
                          boss_present=both_boss, start_suppression=left.get("attack_suppression", ""),
                          end_suppression="", endpoint_input_blocked=False, endpoint_flow_active=True,
                          start_live=left.get("live_enemy_count", ""), end_live="", max_sample_gap=0, sample_intervals=0)
        active["end_seconds"] = right["timestamp"]
        active["seconds"] = right["timestamp"] - active["start_seconds"]
        active["end_suppression"] = right.get("attack_suppression", "")
        active["end_live"] = right.get("live_enemy_count", "")
        active["max_sample_gap"] = max(active["max_sample_gap"], delta)
        active["sample_intervals"] += 1
    close()
    return samples, max_gap, intervals


def analyze_case(folder, label, case, primary=None):
    warnings = []
    path = lambda suffix: folder / (label + suffix)
    result = read_key_values(path("-result.txt"))
    initial = read_json(path("-initial-status.json"), warnings)
    initial_materials = read_json(path("-initial-materials.json"), warnings)
    final = read_json(path("-final-status.json"), warnings)
    bosses = read_csv(path("-bosses.csv"), warnings)
    waves = read_csv(path("-waves.csv"), warnings)
    events = read_csv(path("-events.csv"), warnings)
    diagnostic = read_csv(path("-samples.csv"), warnings)
    bootstrap = read_text(path("-bootstrap-config.txt")).splitlines()
    bootstrap_first = next(csv.reader([bootstrap[0]]), []) if bootstrap else []
    start = number(result.get("start_stage"), number(case.get("start_stage")))
    if start is None and len(bootstrap_first) >= 2:
        start = number(bootstrap_first[1])
    if start is None and events:
        start = number(events[0].get("stage"))
    inferred_stage, inferred_boss = BOSSES.get(start, ("", ""))
    boss_stage = case.get("boss_stage", inferred_stage)
    boss_name = case.get("boss", inferred_boss)
    matched_bosses = [row for row in bosses if (not boss_name or row.get("boss") == boss_name) and
                      (not boss_stage or number(row.get("stage")) == number(boss_stage))]
    if not boss_name and bosses:
        matched_bosses = bosses
        boss_name = bosses[-1].get("boss", "")
        boss_stage = bosses[-1].get("stage", "")
    spawned = next((row for row in matched_bosses if row.get("event") == "spawned"), None)
    ended = next((row for row in reversed(matched_bosses) if row.get("event") == "ended"), None)
    observed = ended or (matched_bosses[-1] if matched_bosses else {})
    defeated = bool(ended and ended.get("result") == "defeated")
    reached = bool(matched_bosses)
    reason = result.get("result", "")
    complete = bool(reason and "game_seconds" in result and "last_stage" in result)
    transitioned = reason.startswith("native_day_cleared") or reason.startswith("ending_reached_")
    success = complete and defeated and transitioned
    if not complete:
        state = "実行中/結果未完了"
    elif success:
        state = "防衛成功"
    elif transitioned:
        state = "遷移済み・ボス撃破未確認"
        warnings.append("native成功遷移だがbosses.csvのdefeated証拠なし")
    elif not reached and path("-bosses.csv").exists():
        state = "防衛失敗・ボス未到達"
    elif not reached:
        state = "結果終了・ボス到達不明"
    elif defeated:
        state = "ボス撃破・防衛完了未確認"
    else:
        state = "防衛失敗・ボス未撃破"
    if reached and not ended and complete:
        warnings.append("結果終了後のboss ended行がない")
    if not path("-bosses.csv").exists():
        warnings.append("bosses.csvなし：ボス未到達とは断定不可")
    if not path("-samples.csv").exists():
        warnings.append("samples.csvなし：攻撃停止判定不可")
    if not path("-initial-status.json").exists():
        warnings.append("initial-status.jsonなし：初期装備未確認")
    error_log = read_text(path("-errors.txt")).strip()
    if error_log:
        warnings.append("runner errors.txtあり")
    if defeated and number(observed.get("remaining_boss_hp"), 0) > 0:
        warnings.append("defeatedだがボス残HPが正数")
    if success and (number(result.get("end_hp"), 1) <= 0 or number(result.get("end_gate"), 1) <= 0):
        warnings.append("成功遷移だが本人/門耐久が0以下")
    event_counts = Counter(row.get("event") for row in events)
    boss_entry = next((event for event in events if event.get("event") == "enemy_spawned" and
                       event.get("details", "").startswith(str(boss_name) + " hp=")), {})
    boss_wave = next((wave for wave in waves if number(wave.get("stage")) == number(boss_stage)), {})
    before_boss_elapsed = number((spawned or {}).get("spawn_game_seconds"))
    boss_wave_start = number(boss_wave.get("start_game_seconds"))
    bootstrap_values = read_key_values(path("-bootstrap-config.txt"))
    samples, max_gap, pauses = attack_pauses(label, diagnostic, boss_name, warnings)
    boss_pauses = [pause for pause in pauses if pause["boss_present"]]
    invalid_reason = ""
    if complete and (error_log or reason.startswith(("failed_", "unexpected_scene_", "no_game_frames_", "batch_stopped_"))):
        invalid_reason = "ランナー実行エラー/環境中断"
    normal_samples = [sample for sample in samples if not sample.get("attack_suppression", "").startswith("finished:")]
    player_y = [number(sample.get("player_y")) for sample in samples]
    player_y = [value for value in player_y if value is not None]
    physical_anomaly = bool(player_y and min(player_y) < -20)
    physical_anomaly_reason = ""
    if physical_anomaly:
        physical_anomaly_reason = f"player_y最小{min(player_y):.6f} < -20（異常落下）"
        warnings.append(physical_anomaly_reason + "。原因未確定。記録の有効性とは分離し、通常DPS/所要時間の統計から除外")
        if complete and not success:
            state = "異常落下を伴う防衛失敗"
        elif success:
            state = "異常落下を伴う防衛成功"
    boss_diagnostic = {sample.get("game_seconds"): sample for sample in diagnostic
                       if sample.get("enemy_name") == boss_name and truth(sample.get("is_boss"))}
    boss_states = Counter(sample.get("ai_state", "") for sample in boss_diagnostic.values())
    boss_phases = sorted({sample.get("ai_phase", "") for sample in boss_diagnostic.values()} - {""})
    if (complete and len(normal_samples) >= 3 and number(result.get("attack_events"), -1) == 0
            and all(sample.get("attack_suppression") == "ending_loading" for sample in normal_samples)):
        invalid_reason = "開始から全samplesがending_loading・攻撃0（前caseのED遷移フラグ残留）"
    if invalid_reason:
        success = False
        state = "無効試験・再計測必要"
        warnings.append(invalid_reason + "。武器の防衛失敗へ算入しない")
    weapon = case.get("weapon", bootstrap_first[0] if bootstrap_first else "")
    if not weapon and events:
        weapon = events[0].get("weapon", "")
    if weapon.startswith("GearCraft"):
        weapon = "GearCraft"
    profile = case.get("execution_profile", case.get("profile", bootstrap_first[2] if len(bootstrap_first) >= 3 else ""))
    row = {key: result.get(key, "") for key in FIELDS}
    row.update(label=label, primary=bool(case) if primary is None else primary, weapon=weapon, profile=profile,
               seed=case.get("seed", bootstrap_first[3] if len(bootstrap_first) >= 4 else ""),
               day=case.get("day", int((start-1)//3+1) if start is not None else ""),
               start_stage=int(start) if start is not None else "", boss_stage=boss_stage, boss=boss_name,
               state=state, measurement_valid=complete and not invalid_reason, invalid_reason=invalid_reason,
               result=reason, defense_success=success, boss_reached=reached, boss_defeated=defeated,
               boss_result=observed.get("result", "not_reached" if complete and path("-bosses.csv").exists() else "unknown"),
               boss_seconds=observed.get("boss_seconds", ""), boss_start_hp=observed.get("start_boss_hp", ""),
               boss_remaining_hp=observed.get("remaining_boss_hp", ""),
               boss_start_player_hp=observed.get("start_hp", ""), boss_end_player_hp=observed.get("end_hp", ""),
               boss_start_gate=observed.get("start_gate", ""), boss_end_gate=observed.get("end_gate", ""),
               boss_player_damage=observed.get("player_damage", ""), boss_gate_damage=observed.get("gate_damage", ""),
               wave_rows=len(waves), wave_rows_completed=sum(wave.get("result") == "completed" for wave in waves),
               wave_seconds=sum(number(wave.get("wave_seconds"), 0) for wave in waves),
               initial_STR=initial.get("STR", ""), initial_ACC=initial.get("ACC", ""),
               initial_flat_damage=initial.get("bonusDamage", ""), initial_module_damage=initial.get("craftWeaponDamagebuff", ""),
               initial_attack_speed=initial.get("attackSpeedMult", ""), initial_punk=initial.get("punkDrive", ""),
               initial_repair=initial.get("module_repair", ""), initial_barrier=initial.get("module_barrier", ""),
               initial_scrap_module=initial.get("module_scrap", ""), part_count=len(initial.get("savedUpgradePartPlacements", [])),
               final_STR=final.get("STR", ""), final_ACC=final.get("ACC", ""),
               final_flat_damage=final.get("bonusDamage", ""), final_module_damage=final.get("craftWeaponDamagebuff", ""),
               reactive_dashes=event_counts["reactive_dash"], reactive_boosts=event_counts["reactive_boost"],
               reactive_intercept_targets=sum(event.get("event") == "reactive_intercept_target" and
                                              event.get("details") != "return_to_enemy" for event in events),
               sample_count=len(samples), sample_max_gap=max_gap, attack_pause_count=len(pauses),
               attack_pause_max_seconds=max((pause["seconds"] for pause in pauses), default=0),
               boss_attack_pause_count=len(boss_pauses),
               boss_attack_pause_max_seconds=max((pause["seconds"] for pause in boss_pauses), default=0),
               attack_pause_reasons="; ".join(sorted({pause["start_suppression"] + " → " + pause["end_suppression"] for pause in pauses})))
    row.update(preboss_completed_wave_seconds=sum(number(wave.get("wave_seconds"), 0) for wave in waves
                                                  if wave.get("result") == "completed" and number(wave.get("stage"), 999) < number(boss_stage, 0)),
               boss_wave_before_spawn_seconds=before_boss_elapsed-boss_wave_start if before_boss_elapsed is not None and boss_wave_start is not None else "",
               before_boss_elapsed=before_boss_elapsed if before_boss_elapsed is not None else "",
               wave_times=";".join(f"{wave.get('stage')}:{wave.get('result')}:{fmt(wave.get('wave_seconds'))}" for wave in waves),
               boss_entry_weapon=boss_entry.get("weapon", ""), boss_entry_STR=boss_entry.get("str", ""),
               boss_entry_ACC=boss_entry.get("acc", ""), boss_entry_flat_damage=boss_entry.get("flat_damage", ""),
               boss_entry_module_damage=boss_entry.get("module_damage", ""), boss_entry_attack_speed=boss_entry.get("attack_speed", ""),
               boss_entry_double_shot=boss_entry.get("double_shot", ""), boss_entry_spread_modifier=boss_entry.get("spread_modifier", ""),
               initial_hit_damage=bootstrap_values.get("initial_hit_damage", ""))
    row["initial_gear"] = initial_materials.get("gear", "")
    row.update(min_player_y=min(player_y) if player_y else "", max_player_y=max(player_y) if player_y else "",
               physical_anomaly=physical_anomaly, physical_anomaly_reason=physical_anomaly_reason)
    row.update(boss_sample_phases=";".join(boss_phases),
               boss_sample_states=";".join(f"{state}:{count}" for state, count in sorted(boss_states.items())),
               boss_armor_samples=boss_states["ArmorTransition"], boss_stagger_samples=boss_states["Staggered"],
               boss_crossfire_observed=boss_states["CrossfireBurst"] > 0,
               boss_combined_observed=boss_states["CombinedVolley"] > 0)
    if case and initial:
        for source, expected in (("STR", "STR"), ("ACC", "ACC"), ("craftWeaponDamagebuff", "moduleDamage"),
                                 ("HP", "initial_hp"), ("GATE", "initial_gate")):
            if expected in case and number(initial.get(source)) != number(case[expected]):
                warnings.append(f"初期{source}={initial.get(source)} != 計画{case[expected]}")
        for source, expected in (("punkDrive", "punk"), ("module_repair", "repair"), ("module_barrier", "barrier")):
            if expected in case and truth(initial.get(source)) != truth(case[expected]):
                warnings.append(f"初期{source} != 計画{expected}")
    row["warnings"] = "; ".join(dict.fromkeys(warnings))
    return row, pauses


def render(rows, cases):
    primary = [row for row in rows if row["primary"]]
    complete = [row for row in primary if row["result"] and row["state"] != "実行中/結果未完了"]
    valid = [row for row in complete if row["measurement_valid"]]
    invalid = [row for row in complete if not row["measurement_valid"]]
    success = [row for row in complete if row["defense_success"]]
    pending = [case["label"] for case in cases if case.get("label") not in {row["label"] for row in complete}]
    lines = ["防衛バランス試験・ログ集計", "生成UTC: " + datetime.now(timezone.utc).isoformat(),
             f"主試験結果 {len(complete)}/{len(cases)}、有効 {len(valid)}・無効 {len(invalid)}、防衛成功 {len(success)}/{len(valid)}有効試験。追加/対照ログ {len(rows)-len(primary)}件。",
             "成功条件：bosses.csvで対象ボスのdefeatedを確認し、native_day_clearedまたはending_reachedまで遷移。",
             "ボス秒は出現から撃破/終了まで。失敗例の秒数は撃破タイムではない。ボス未到達は明記し、欠測値を0に置換しない。", ""]
    for primary_group, title in ((True, "主試験"), (False, "追加・対照試験（主試験件数へ混入しない）")):
        selected = [row for row in rows if row["primary"] == primary_group]
        if not selected:
            continue
        lines.extend([title, "ボス | 武器 | 判定 | ボス秒 | ボス残HP | 本人HP 終了 | 門 終了 | ボス中無攻撃最大秒"])
        for row in selected:
            boss_seconds = fmt(row["boss_seconds"]) if row["boss_reached"] else "未到達" if row["boss_result"] == "not_reached" else "不明"
            lines.append(" | ".join((str(row["boss"]), row["weapon"], row["state"], boss_seconds,
                                     fmt(row["boss_remaining_hp"]), fmt(row["end_hp"]), fmt(row["end_gate"]),
                                     fmt(row["boss_attack_pause_max_seconds"]) if row["sample_count"] else "未記録")))
        lines.append("")
    lines.append("詳細（初期条件と終了理由）")
    for row in rows:
        lines.extend([row["label"],
                      f"  profile={row['profile']}、初期STR/ACC={row['initial_STR']}/{row['initial_ACC']}、PartsFlat={row['initial_flat_damage']}、Module={row['initial_module_damage']}、間隔倍率={row['initial_attack_speed']}、Punk/Repair/Barrier={row['initial_punk']}/{row['initial_repair']}/{row['initial_barrier']}",
                      f"  result={row['result'] or '未記録'}、日全体{fmt(row['game_seconds'])}秒、native完了wave={row['completed_native_waves']}、報酬={row['rewards_selected']}、ダッシュ={row['reactive_dashes']}、迎撃対象選択={row['reactive_intercept_targets']}、BOOST={row['reactive_boosts']}",
                      f"  ボス開始/終了 本人HP={fmt(row['boss_start_player_hp'])}/{fmt(row['boss_end_player_hp'])}、門={fmt(row['boss_start_gate'])}/{fmt(row['boss_end_gate'])}、ボス初期/残HP={fmt(row['boss_start_hp'])}/{fmt(row['boss_remaining_hp'])}",
                      f"  wave別秒={row['wave_times'] or '未記録'}。ボス前完了wave合計={fmt(row['preboss_completed_wave_seconds'])}秒、ボスwave内前座={fmt(row['boss_wave_before_spawn_seconds'])}秒、出現までの全経過={fmt(row['before_boss_elapsed'])}秒。",
                      f"  ボス出現時装備={row['boss_entry_weapon']}、STR/ACC={row['boss_entry_STR']}/{row['boss_entry_ACC']}、PartsFlat={row['boss_entry_flat_damage']}、Module={row['boss_entry_module_damage']}、間隔倍率={row['boss_entry_attack_speed']}、二連={row['boss_entry_double_shot']}、拡散補正={row['boss_entry_spread_modifier']}。",
                      f"  約1秒samples={row['sample_count']}時刻、最大間隔{fmt(row['sample_max_gap'])}秒。攻撃数不増(敵生存/両端操作可、1秒以上)={row['attack_pause_count']}区間、最大{fmt(row['attack_pause_max_seconds'])}秒、うち対象ボス生存{row['boss_attack_pause_count']}区間。理由={row['attack_pause_reasons'] or '該当なし'}"])
        if row["warnings"]:
            lines.append("  要確認: " + row["warnings"])
        if row["boss"] == "EK-9":
            lines.append(f"  EK状態観測: phases={row['boss_sample_phases']}、ArmorTransition={row['boss_armor_samples']}sample、Staggered={row['boss_stagger_samples']}sample、CrossfireBurst={row['boss_crossfire_observed']}、CombinedVolley={row['boss_combined_observed']}。約1秒sampleの観測数であり厳密な持続秒/発射数ではない。")
    if pending:
        lines.extend(["", "未完了/未実行の主試験："] + ["  " + label for label in pending])
    if invalid:
        lines.extend(["", "再計測が必要な無効試験："] + ["  " + row["label"] + ": " + row["invalid_reason"] for row in invalid])
    lines.extend(["", "解釈上の範囲：",
                  "・取得済みの代表パーツ/強化から、当日の通常耐久・native進行で防衛する試験。全キャンペーン連続走破、実プレイヤー平均、成功率の統計推定ではない。",
                  "・初期値はinitial-status.jsonを優先。Punk/モジュールの有無は実データを表示し、計画と不一致なら警告する。",
                  "・攻撃不増はattack_events累計が同じsample間。敵別の同時刻行は重複除去。対象ボスが両端で生存し、flow_activeかつinput非blockの区間を別集計する。",
                  "・約1秒の観測なので区間内部のUI/クールダウン/照準判断や命中を全て保証しない。正当な待機も含むため無攻撃を直ちにボット不具合/火力不足と断定しない。詳細はattack-pauses.csv。",
                  "・被害/回復はframe間のnet変化であり、同一frameに生じる回復と被害を分離できない。迎撃対象選択回数は迎撃成功数ではない。",
                  "・player_y < -20を観測した試験は異常落下を別分類する。記録自体の有効性と原因の確定は別で、通常の火力/所要時間の統計から除外する。",
                  "・この集計はセーブ/Editor復元を実行・認定しない。復元記録はrootの最終確認を参照。", ""])
    return "\n".join(lines)


MULTI_FIELDS = "group boss weapon profile planned_seeds planned_count result_count valid_count pending_count invalid_count defense_success_count defense_failed_count boss_defeated_count boss_unreached_count player_dead_count gate_destroyed_count censored_count completed_seeds pending_seeds".split()
MULTI_FIELDS += "regular_valid_count abnormal_count abnormal_failure_count abnormal_success_count regular_success_count min_player_y".split()
for _metric in ("defeated_boss_seconds", "failure_boss_seconds", "observed_boss_end_seconds", "wave_seconds", "total_record_seconds", "boss_end_hp", "boss_end_gate", "boss_remaining_hp"):
    MULTI_FIELDS += [_metric + suffix for suffix in ("_n", "_min", "_median", "_max")]
MULTI_FIELDS += "boss_attack_pause_max_seconds initial_gear_observed boost_count_observed per_seed_outcomes labels".split()


def summarize_groups(rows, plans):
    measured = {row["label"]: row for row in rows}
    grouped = {}
    for plan in plans:
        label = plan["label"]
        group = ("grounded_posture" if "_grounded_" in label else
                 "ic_speed_sensitivity" if "sensitivity" in label else
                 "standard_3seeds" if int(plan["start_stage"]) in (28, 31) else "primary_only")
        # A group must have identical planned build and initial durability/stat flags.
        key = (group,) + tuple(str(plan.get(field, "")) for field in
                              ("weapon", "start_stage", "profile", "STR", "ACC", "moduleDamage", "repair", "barrier", "punk", "scrap_module", "initial_hp", "initial_gate"))
        grouped.setdefault(key, []).append(plan)
    summaries = []
    for key, members in grouped.items():
        members.sort(key=lambda plan: int(plan["seed"]))
        observations = [measured[plan["label"]] for plan in members if plan["label"] in measured]
        complete = [row for row in observations if row.get("result") and row.get("state") != "実行中/結果未完了"]
        valid = [row for row in complete if row.get("measurement_valid")]
        regular = [row for row in valid if not row.get("physical_anomaly")]
        abnormal = [row for row in valid if row.get("physical_anomaly")]
        invalid = [row for row in complete if not row.get("measurement_valid")]
        completed_labels = {row["label"] for row in complete}
        pending = [plan for plan in members if plan["label"] not in completed_labels]
        defeated = [row for row in regular if row.get("boss_defeated")]
        failures = [row for row in regular if row.get("result") in ("player_dead", "gate_destroyed")]
        boss_stage, boss = BOSSES.get(int(members[0]["start_stage"]), ("", ""))
        summary = dict(group=key[0], boss=boss, weapon=members[0]["weapon"], profile=members[0]["profile"],
                       planned_seeds=";".join(plan["seed"] for plan in members), planned_count=len(members), result_count=len(complete),
                       valid_count=len(valid), pending_count=len(pending), invalid_count=len(invalid),
                       regular_valid_count=len(regular), abnormal_count=len(abnormal),
                       abnormal_failure_count=sum(row.get("result") in ("player_dead", "gate_destroyed") for row in abnormal),
                       abnormal_success_count=sum(bool(row.get("defense_success")) for row in abnormal),
                       regular_success_count=sum(bool(row.get("defense_success")) for row in regular),
                       min_player_y=min((number(row.get("min_player_y")) for row in valid if number(row.get("min_player_y")) is not None), default=""),
                       defense_success_count=sum(bool(row.get("defense_success")) for row in valid), defense_failed_count=len(failures),
                       boss_defeated_count=sum(bool(row.get("boss_defeated")) for row in valid), boss_unreached_count=sum(not row.get("boss_reached") for row in valid),
                       player_dead_count=sum(row.get("result") == "player_dead" for row in valid),
                       gate_destroyed_count=sum(row.get("result") == "gate_destroyed" for row in valid),
                       censored_count=sum("timeout" in row.get("result", "") for row in valid),
                       completed_seeds=";".join(str(row.get("seed", "")) for row in complete),
                       pending_seeds=";".join(plan["seed"] for plan in pending), labels=";".join(plan["label"] for plan in members))
        metrics = {
            "defeated_boss_seconds": (defeated, "boss_seconds"),
            "failure_boss_seconds": ([row for row in failures if row.get("boss_reached") and not row.get("boss_defeated")], "boss_seconds"),
            "observed_boss_end_seconds": ([row for row in regular if row.get("boss_reached")], "boss_seconds"),
            "wave_seconds": (regular, "wave_seconds"), "total_record_seconds": (regular, "game_seconds"),
            "boss_end_hp": (regular, "boss_end_player_hp"), "boss_end_gate": (regular, "boss_end_gate"),
            "boss_remaining_hp": (regular, "boss_remaining_hp"),
        }
        for metric, (source, field) in metrics.items():
            values = [number(row.get(field)) for row in source]
            values = [value for value in values if value is not None]
            summary[metric + "_n"] = len(values)
            summary[metric + "_min"] = min(values) if values else ""
            summary[metric + "_median"] = statistics.median(values) if values else ""
            summary[metric + "_max"] = max(values) if values else ""
        summary["boss_attack_pause_max_seconds"] = max((number(row.get("boss_attack_pause_max_seconds"), 0) for row in valid), default=0)
        summary["initial_gear_observed"] = ";".join(sorted({str(row.get("initial_gear", "")) for row in valid}))
        summary["boost_count_observed"] = ";".join(sorted({str(row.get("reactive_boosts", "")) for row in valid}))
        outcomes = []
        for plan in members:
            row = measured.get(plan["label"])
            if row is None or not row.get("result"):
                outcomes.append(plan["seed"] + ":未完了")
            else:
                outcomes.append(f"{plan['seed']}:{row['state']}:{row['result']}:boss={fmt(row.get('boss_seconds'))}s:remaining={fmt(row.get('boss_remaining_hp'))}:HP={fmt(row.get('boss_end_player_hp'))}:GATE={fmt(row.get('boss_end_gate'))}:minY={fmt(row.get('min_player_y'))}")
        summary["per_seed_outcomes"] = " | ".join(outcomes)
        summaries.append(summary)
    return summaries


def render_multiseed(summaries):
    lines = ["防衛試験・seed反復集計", "生成UTC: " + datetime.now(timezone.utc).isoformat(),
             "標準AR/EKはseed77/101/202の3件。取得済み代表装備・有限bot条件の少数反復であり、人間の成功率や平均装備の推定ではない。",
             "撃破時間は撃破例だけの中央値/範囲を明示し、失敗の秒数・本人/門/ボス残HPもseed別に併記。未完了・無効を成功/失敗へ算入しない。",
             "player_y < -20の異常落下は記録として保持し、通常失敗とは別計数。全ての時間/耐久の中央値・範囲から除外し、seed別の生値を表示する。原因は別途調査し、通常DPS不足とは判断しない。",
             "grounded姿勢とIC速度/持込Gear8/BOOST6は別枠。標準3seedへ混ぜない。", ""]
    names = {"primary_only": "IC/BB 主seed77のみ", "standard_3seeds": "標準AR/EK 3seed", "grounded_posture": "EK Gear 自発jump抑止の姿勢比較", "ic_speed_sensitivity": "IC速度・持込資源の感度試験"}
    for group in names:
        selected = [row for row in summaries if row["group"] == group]
        if not selected:
            continue
        lines.extend([names[group], "ボス | 武器 | 結果/予定 | 通常成功/通常失敗/異常落下失敗/打切 | 無効/未完了 | 通常撃破秒 中央値[最小–最大] | 通常失敗まで秒 中央値[最小–最大]"])
        for row in selected:
            stats = lambda metric: (f"{fmt(row[metric+'_median'])}[{fmt(row[metric+'_min'])}–{fmt(row[metric+'_max'])}] n={row[metric+'_n']}" if row[metric+'_n'] else "— n=0")
            lines.append(f"{row['boss']} | {row['weapon']} | {row['result_count']}/{row['planned_count']} | {row['regular_success_count']}/{row['defense_failed_count']}/{row['abnormal_failure_count']}/{row['censored_count']} | {row['invalid_count']}/{row['pending_count']} | {stats('defeated_boss_seconds')} | {stats('failure_boss_seconds')}")
            if row["abnormal_success_count"]:
                lines.append(f"  異常落下を伴う成功{row['abnormal_success_count']}件（通常成功と統計は分離）")
            lines.append("  " + row["per_seed_outcomes"])
            lines.append(f"  profile={row['profile']}、初期Gear実測={row['initial_gear_observed'] or '未記録'}、BOOST実使用={row['boost_count_observed'] or '未記録'}、戦闘wave合計秒={stats('wave_seconds')}、ボス中攻撃不増最大={fmt(row['boss_attack_pause_max_seconds'])}秒")
        lines.append("")
    lines.extend(["observed_boss_end_secondsは撃破と失敗の終了時刻を含むため、全例撃破と確認できない限りTTKとは呼ばない。",
                  "区間総game_secondsは日末回収/報告/遷移を含む。captureDeltaTime固定中のunscaled待機は端末速度に依存してゲーム秒換算が増えるため、通常プレイの実所要時間とは扱わない。",
                  "反復間はseedでドロップ/報酬/敵挙動も変わり、武器間の完全な出現/報酬一致を保証しない。", ""])
    return "\n".join(lines)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--folder", type=Path, default=Path(__file__).resolve().parent)
    args = parser.parse_args()
    folder = args.folder.resolve()
    warnings = []
    # Gate-aware cases supersede preliminary reactive cases as the primary set.
    # Earlier observations remain visible in the separate comparison group.
    cases = (read_csv(folder / "batch-cases.csv", warnings) if (folder / "batch-cases.csv").exists()
             else read_json(folder / "execution-cases.json", warnings) or [])
    if not isinstance(cases, list):
        raise ValueError("execution-cases.json must be a case list")
    by_label = {case["label"]: case for case in cases if case.get("label")}
    all_plans = dict(by_label)
    for extra_file in ("repeat-cases.csv", "sensitivity-cases.csv"):
        for case in read_csv(folder / extra_file, warnings):
            if case.get("label"):
                all_plans[case["label"]] = case
    labels = {path.name[:-len("-result.txt")] for path in folder.glob("*-result.txt")}
    labels.update(path.name[:-len("-initial-status.json")] for path in folder.glob("*-initial-status.json"))
    order = {label: index for index, label in enumerate(by_label)}
    rows, pauses = [], []
    for label in sorted(labels, key=lambda item: (item not in order, order.get(item, 0), item)):
        row, intervals = analyze_case(folder, label, all_plans.get(label, {}), primary=label in by_label)
        rows.append(row)
        pauses.extend(intervals)
    write_csv(folder / "summary.csv", FIELDS, rows)
    write_csv(folder / "attack-pauses.csv", PAUSE_FIELDS, pauses)
    report = render(rows, cases)
    if warnings:
        report += "\n集計設定の読取警告: " + "; ".join(warnings) + "\n"
    atomic_text(folder / "tables.txt", report)
    multiseed = summarize_groups(rows, list(all_plans.values()))
    write_csv(folder / "multiseed-summary.csv", MULTI_FIELDS, multiseed)
    atomic_text(folder / "multiseed-summary.txt", render_multiseed(multiseed))
    print(f"cases={len(rows)}; primary_success={sum(row['primary'] and row['defense_success'] for row in rows)}; planned={len(cases)}; attack_pauses={len(pauses)}")


if __name__ == "__main__":
    main()
