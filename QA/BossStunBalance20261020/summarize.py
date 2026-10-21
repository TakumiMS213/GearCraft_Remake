#!/usr/bin/env python3
"""Summarize recorded runs only; never extrapolates time from partial HP damage."""
from pathlib import Path
import csv
import json

here=Path(__file__).resolve().parent
summary=[]
for mode in ("direct","survival"):
    for case in csv.DictReader((here/(mode+"-cases.csv")).open()):
        label=case["label"]
        result_path=here/(label+"-result.txt")
        if not result_path.exists():
            continue
        result=dict(line.split("=",1) for line in result_path.read_text().splitlines() if "=" in line)
        boss_path=here/(label+"-bosses.csv")
        bosses=list(csv.DictReader(boss_path.open())) if boss_path.exists() else []
        ended=[r for r in bosses if r["event"]=="ended"]
        boss=ended[-1] if ended else None
        stuns_path=here/(label+"-stun.csv")
        stuns=list(csv.DictReader(stuns_path.open())) if stuns_path.exists() else []
        starts=[r for r in stuns if r["event"]=="started"]
        ends=[r for r in stuns if r["event"]=="ended"]
        thresholds=[r for r in stuns if r["event"]=="threshold_damage"]
        particle_path=here/(label+"-particles.csv")
        particles=list(csv.DictReader(particle_path.open())) if particle_path.exists() else []
        defeated=bool(boss and boss["result"]=="defeated")
        # A successful kill and a surviving defense are separate facts.
        alive=float(result.get("end_hp","0"))>0 and float(result.get("end_gate","0"))>0
        summary.append(dict(label=label,mode=mode,weapon=case["weapon"],
            result=result["result"],boss=boss["boss"] if boss else None,
            boss_result=boss["result"] if boss else None,
            killed=defeated,survived=alive,
            kill_seconds=float(boss["boss_seconds"]) if defeated else None,
            failure_or_timeout_seconds=float(boss["boss_seconds"]) if boss and not defeated else None,
            meets_60_seconds=defeated and float(boss["boss_seconds"])>=60,
            start_boss_hp=float(boss["start_boss_hp"]) if boss else None,
            remaining_boss_hp=float(boss["remaining_boss_hp"]) if boss else None,
            end_hp=float(result.get("end_hp","0")),end_gate=float(result.get("end_gate","0")),
            stun_count=max([int(r["count"]) for r in stuns],default=0),
            threshold_damage_hp=[float(r["hp"]) for r in thresholds],
            completed_stun_durations=[float(r["stunned_seconds"]) for r in ends],
            max_stunned_body_drift=max([float(r["max_body_drift"]) for r in stuns],default=0),
            max_visual_vibration=max([float(r["max_visual_drift"]) for r in stuns],default=0),
            observed_explosion_components=len(particles),
            max_explosion_position_error=max([float(r["distance_to_last_hit"]) for r in particles],default=None)))
(here/"summary.json").write_text(json.dumps(summary,indent=2,ensure_ascii=False)+"\n")
for r in summary:
    t=r["kill_seconds"]
    print(f'{r["label"]}: {r["boss_result"]} '+(f'{t:.3f}s' if t is not None else f'failure/timeout={r["failure_or_timeout_seconds"]}')+f' HP={r["end_hp"]} gate={r["end_gate"]} stuns={r["stun_count"]}')
print(f"Recorded {len(summary)} cases. Pending cases omitted; no synthetic kill times.")
