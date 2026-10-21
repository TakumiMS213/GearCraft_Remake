#!/usr/bin/env python3
"""Summarize the planned native boss matrix without modifying measurement logs.

Only a completed, surviving native defeat contributes a kill time. Pending cases,
failure durations and invalid/incomplete artifacts remain separate. Slow coverage
uses observed non-stunned time, not the duration of the entire encounter.
"""
from __future__ import annotations

import argparse
import collections
import csv
import datetime
import json
import math
from pathlib import Path
import statistics


def number(value):
    try:
        value = float(value)
        return value if math.isfinite(value) else None
    except (TypeError, ValueError):
        return None


def rows(path):
    if not path.exists():
        return []
    with path.open(encoding="utf-8-sig", newline="") as stream:
        return list(csv.DictReader(stream))


def text(path):
    return path.read_text(encoding="utf-8-sig") if path.exists() else ""


def values(path):
    return dict(line.split("=", 1) for line in text(path).splitlines() if "=" in line)


def distribution(sequence):
    valid = [n for n in sequence if n is not None]
    return {"count": len(valid), "median": statistics.median(valid) if valid else None,
            "min": min(valid) if valid else None, "max": max(valid) if valid else None}


def read_case(folder, case, condition, progress):
    label = case["label"]
    result = values(folder / (label + "-result.txt"))
    boss_rows = rows(folder / (label + "-bosses.csv"))
    ended = [r for r in boss_rows if r.get("event") == "ended"]
    boss = ended[-1] if ended else None
    warnings = []
    outcome = "running" if label in progress and progress.startswith("RUNNING") else "pending"
    if result:
        if not all(k in result for k in ("result", "game_seconds", "end_hp", "end_gate")):
            outcome = "incomplete_artifacts"
            warnings.append("Result file is incomplete; excluded from completed trials.")
        elif boss is None:
            outcome = "incomplete_artifacts"
            warnings.append("No ended boss row; no kill/failure time inferred from whole-run duration.")
        elif result["result"] == "direct_boss_defeated":
            hp, gate = number(result["end_hp"]), number(result["end_gate"])
            remaining = number(boss.get("remaining_boss_hp"))
            if (boss.get("result") == "defeated" and remaining is not None and remaining <= 0
                    and hp is not None and hp > 0 and gate is not None and gate > 0):
                outcome = "defeated"
            else:
                outcome = "invalid"
                warnings.append("Defeat result lacks matching lethal boss HP and surviving player/gate.")
        elif result["result"] in ("player_dead", "gate_destroyed"):
            outcome = result["result"]
        elif "timeout" in result["result"]:
            outcome = "timeout"
        else:
            outcome = "invalid"
            warnings.append("Unrecognized or non-defeat terminal result: " + result["result"])
    elif boss is not None:
        outcome = "incomplete_artifacts"
        warnings.append("Ended boss row exists but final result file has not been written.")

    terminal = outcome in {"defeated", "player_dead", "gate_destroyed", "timeout"}
    elapsed = number(boss.get("boss_seconds")) if boss is not None and terminal else None
    if terminal and elapsed is None:
        outcome, terminal = "invalid", False
        warnings.append("Completed case has no finite boss elapsed time.")

    control_rows = rows(folder / (label + "-boss-control-totals.csv"))
    control = None
    if control_rows:
        # Direct mode should contain exactly one boss; never silently mix unrelated bosses.
        expected_name = boss.get("boss") if boss else (boss_rows[0].get("boss") if boss_rows else None)
        matching = [r for r in control_rows if r.get("boss") == expected_name]
        if len(matching) == 1:
            c = matching[0]
            keys = ["observed_seconds", "slowed_not_stunned_seconds",
                    "not_slowed_not_stunned_seconds", "stunned_seconds"]
            control = {k: number(c.get(k)) for k in keys}
            if any(v is None or v < 0 for v in control.values()):
                warnings.append("Invalid control total; excluded from control aggregation.")
                control = None
            else:
                nonstun = control["slowed_not_stunned_seconds"] + control["not_slowed_not_stunned_seconds"]
                control["non_stunned_seconds"] = nonstun
                control["slowed_nonstun_fraction"] = control["slowed_not_stunned_seconds"] / nonstun if nonstun > 0 else None
                control["slow_fraction_denominator"] = "slowed + not_slowed, excluding stunned time"
                if abs(control["observed_seconds"] - nonstun - control["stunned_seconds"]) > 0.1:
                    warnings.append("Control component sum differs from observed seconds by >0.1s.")
        else:
            warnings.append("Control totals do not identify exactly one matching boss.")
    elif terminal:
        warnings.append("Completed case has no control totals yet.")

    return {
        "label": label, "boss": condition["boss"], "weapon": case["weapon"],
        "normally_available": condition["normally_available"], "seed": int(case["seed"]),
        "condition": case, "outcome": outcome, "completed": terminal,
        "native_result": result.get("result"),
        "kill_seconds": elapsed if outcome == "defeated" else None,
        "failure_seconds": elapsed if outcome in {"player_dead", "gate_destroyed"} else None,
        "timeout_observed_seconds": elapsed if outcome == "timeout" else None,
        "boss_observed_seconds": elapsed,
        "start_boss_hp": number(boss.get("start_boss_hp")) if boss else None,
        "remaining_boss_hp": number(boss.get("remaining_boss_hp")) if boss else None,
        "player_hp": number(result.get("end_hp")) if terminal else None,
        "gate_hp": number(result.get("end_gate")) if terminal else None,
        "control": control if terminal else None,
        "warnings": warnings,
    }


def summarize(condition, cases):
    complete = [c for c in cases if c["completed"]]
    wins = [c for c in complete if c["outcome"] == "defeated"]
    controls = [c["control"] for c in complete if c["control"] is not None]
    slow = sum(c["slowed_not_stunned_seconds"] for c in controls)
    nonstun = sum(c["non_stunned_seconds"] for c in controls)
    return {
        **condition,
        "availability": "normal" if condition["normally_available"] else "hypothetical_locked",
        "planned_trials": len(cases), "completed_trials": len(complete),
        "successful_trials": len(wins),
        "success_fraction_completed": len(wins) / len(complete) if complete else None,
        "outcome_counts": dict(collections.Counter(c["outcome"] for c in cases)),
        "kill_seconds_successes_only": distribution(c["kill_seconds"] for c in wins),
        "player_hp_completed": distribution(c["player_hp"] for c in complete),
        "gate_hp_completed": distribution(c["gate_hp"] for c in complete),
        "remaining_boss_hp_completed": distribution(c["remaining_boss_hp"] for c in complete),
        "control_trials": len(controls), "slowed_not_stunned_seconds_total": slow,
        "non_stunned_seconds_total": nonstun,
        "slowed_nonstun_fraction": slow / nonstun if nonstun > 0 else None,
        "stunned_seconds": distribution(c["stunned_seconds"] for c in controls),
        "stunned_seconds_total": sum(c["stunned_seconds"] for c in controls),
        "trials": cases,
    }


def fmt(value):
    return "-" if value is None else f"{value:.2f}"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--folder", type=Path, default=Path(__file__).resolve().parent)
    args = parser.parse_args()
    folder = args.folder.resolve()
    plan = rows(folder / "direct-cases.csv")
    conditions = json.loads((folder / "matrix-conditions.json").read_text())
    mapping = {(int(c["stage"]), c["weapon"]): c for c in conditions}
    if len(mapping) != len(conditions) or len({r["label"] for r in plan}) != len(plan):
        raise ValueError("Duplicate conditions or case labels")
    progress = text(folder / "batch-progress.txt").strip()
    trials = []
    for case in plan:
        key = (int(case["start_stage"]), case["weapon"])
        condition = mapping[key]
        if any(str(condition[k]) != case[p] for k, p in
               [("profile", "profile"), ("STR", "STR"), ("ACC", "ACC"), ("module_damage", "moduleDamage")]):
            raise ValueError("Plan differs from matrix condition: " + case["label"])
        trials.append(read_case(folder, case, condition, progress))
    groups = [summarize(c, [t for t in trials if t["boss"] == c["boss"] and t["weapon"] == c["weapon"]]) for c in conditions]
    output = {
        "generated_at_utc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "batch_progress": progress,
        "planned_trials": len(trials), "completed_trials": sum(t["completed"] for t in trials),
        "successful_trials": sum(t["outcome"] == "defeated" for t in trials),
        "normal_conditions": [g for g in groups if g["normally_available"]],
        "hypothetical_locked_conditions": [g for g in groups if not g["normally_available"]],
        "notes": [
            "Native direct-boss automated input comparison, not a human campaign success-rate estimate.",
            "Kill median/range includes surviving defeats only; death/gate-loss durations are separate.",
            "Pending/incomplete/invalid artifacts are not counted as gameplay failures.",
            "Slow fraction is summed slowed non-stunned time / summed observed non-stunned time.",
            "Slow coverage is observed state uptime, not a causal estimate of time saved or actual movement reduction.",
            "Hypothetical locked weapons are supplied before their normal recipe unlock and kept separate.",
        ],
    }
    (folder / "results.json").write_text(json.dumps(output, ensure_ascii=False, indent=2, allow_nan=False) + "\n")
    columns = ["boss", "weapon", "availability", "profile", "planned_trials", "completed_trials", "successful_trials",
               "kill_median_seconds", "kill_min_seconds", "kill_max_seconds", "player_dead", "gate_destroyed", "timeout",
               "pending_or_running", "invalid_or_incomplete", "player_hp_min", "player_hp_max", "gate_hp_min", "gate_hp_max",
               "remaining_boss_hp_min", "remaining_boss_hp_max", "control_trials", "slowed_nonstun_fraction",
               "slowed_not_stunned_seconds_total", "non_stunned_seconds_total", "stunned_seconds_total",
               "stunned_seconds_median", "stunned_seconds_min", "stunned_seconds_max", "trial_results_json"]
    with (folder / "matrix-summary.csv").open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=columns); writer.writeheader()
        for g in groups:
            n = g["outcome_counts"]
            row = {k: g[k] for k in columns if k in g}
            row.update({"kill_" + k + "_seconds": g["kill_seconds_successes_only"][k] for k in ("median", "min", "max")})
            row.update({k: n.get(k, 0) for k in ("player_dead", "gate_destroyed", "timeout")})
            row.update({"pending_or_running": n.get("pending", 0) + n.get("running", 0),
                        "invalid_or_incomplete": n.get("invalid", 0) + n.get("incomplete_artifacts", 0)})
            for target, source in [("player_hp", "player_hp_completed"), ("gate_hp", "gate_hp_completed"),
                                   ("remaining_boss_hp", "remaining_boss_hp_completed"), ("stunned_seconds", "stunned_seconds")]:
                for key in ("median", "min", "max") if target == "stunned_seconds" else ("min", "max"):
                    row[target + "_" + key] = g[source][key]
            row["trial_results_json"] = json.dumps([{k: c[k] for k in ("seed", "outcome", "kill_seconds", "failure_seconds", "remaining_boss_hp", "player_hp", "gate_hp")} for c in g["trials"]], ensure_ascii=False)
            writer.writerow(row)
    print(f"{output['completed_trials']}/{len(trials)} completed; {output['successful_trials']} surviving defeats; {progress}")
    for normal, title in [(True, "NORMAL"), (False, "HYPOTHETICAL LOCKED")]:
        print(title + " | win/completed/planned | kill median [min,max] s | slow non-stun % | stun median s")
        for g in groups:
            if g["normally_available"] != normal: continue
            d = g["kill_seconds_successes_only"]; fraction = g["slowed_nonstun_fraction"]
            print(f"{g['boss']:5} {g['weapon']:13} {g['successful_trials']}/{g['completed_trials']}/{g['planned_trials']} "
                  f"{fmt(d['median'])} [{fmt(d['min'])},{fmt(d['max'])}] "
                  f"slow={fmt(fraction * 100 if fraction is not None else None)}% stun={fmt(g['stunned_seconds']['median'])} "
                  f"{g['outcome_counts']}")


if __name__ == "__main__":
    main()
