#!/usr/bin/env python3
"""Build the QA-only continuous-Play defense runner from existing native bodies.

Does not operate Unity, edit production files or copy/replace session backups.
Run this script again after either source body changes. Existing case CSVs are
kept exactly; use --cases-output to prepare a differently named plan.
"""
import argparse
import csv
import hashlib
import io
import json
from datetime import datetime, timezone
from pathlib import Path


FOLDER = Path(__file__).resolve().parent
CASE_FIELDS = "label weapon start_stage profile seed STR ACC moduleDamage repair barrier punk scrap_module initial_hp initial_gate".split()


def digest(text):
    return hashlib.sha256(text.encode()).hexdigest()


def replace_once(text, old, new):
    if text.count(old) != 1:
        raise ValueError(f"Expected one transformation anchor, found {text.count(old)}: {old[:100]}")
    return text.replace(old, new, 1)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--route", default="ReactiveGateSurvivalRoute.cs.txt")
    parser.add_argument("--output", default="BatchDefense.cs.txt")
    parser.add_argument("--cases-output", default="batch-cases.csv")
    args = parser.parse_args()
    # All mutable artifacts stay in this QA folder. Never accept a path escape.
    for name in (args.route, args.output, args.cases_output):
        if Path(name).name != name:
            raise ValueError("Arguments must be filenames within the QA folder")
    bootstrap_path = FOLDER / "NativeSurvivalBootstrap.cs.txt"
    route_path = FOLDER / args.route
    bootstrap = bootstrap_path.read_text()
    route = route_path.read_text()  # Deliberately no fallback to the old bot.
    if "reactive_gate_v1" not in route:
        raise ValueError("Gate-aware route marker not found; review the requested source")
    cases_path = FOLDER / args.cases_output
    cases_created = not cases_path.exists()
    if cases_created:
        source = json.loads((FOLDER / "execution-cases.json").read_text())
        case_rows = []
        for case in source:
            row = {key: str(case.get(key, "")) for key in CASE_FIELDS}
            label = case["label"]
            row["label"] = "gate_" + (label[len("reactive_"):] if label.startswith("reactive_") else label)
            row["profile"] = case.get("execution_profile", case["profile"])
            case_rows.append(row)
        buffer = io.StringIO(newline="")
        writer = csv.DictWriter(buffer, fieldnames=CASE_FIELDS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(case_rows)
        # Exclusive creation preserves a plan another operator created meanwhile.
        with cases_path.open("x") as stream:
            stream.write(buffer.getvalue())
    case_text = cases_path.read_text()
    case_rows = list(csv.DictReader(io.StringIO(case_text)))
    if not case_rows or set(case_rows[0]) != set(CASE_FIELDS):
        raise ValueError("Invalid or empty batch case CSV schema")
    labels = [row["label"] for row in case_rows]
    if len(labels) != len(set(labels)):
        raise ValueError("Duplicate case labels")
    for row in case_rows:
        if any(any(character in value for character in '\n\r,"') for value in row.values()):
            raise ValueError("Case values must be simple unquoted CSV fields")
        if not row["label"].startswith("gate_") or any(c in row["label"] for c in "/\\"):
            raise ValueError("Gate cases require distinct safe gate_ labels")
    original_bootstrap, original_route = bootstrap, route
    bootstrap = replace_once(
        bootstrap,
        "UnityEditor.EditorApplication.CallbackFunction readyCheck=null;double requestedAt=UnityEditor.EditorApplication.timeSinceStartup;",
        "UnityEditor.EditorApplication.CallbackFunction readyCheck=null;double requestedAt=UnityEditor.EditorApplication.timeSinceStartup;\n"
        "bBootstrapCleanup=()=>{if(readyCheck!=null)UnityEditor.EditorApplication.update-=readyCheck;};")
    route = replace_once(route, "UnityEditor.EditorApplication.isPlaying=false;",
                         "Time.timeScale=0f; // Generated batch only: retain Play between cases.")
    route = replace_once(route, "System.Func<BonusCardDataSO,float> cardScore=data=>{",
                         "bRouteFinish=finish; // Generated cleanup hook; normal route semantics unchanged.\n"
                         "System.Func<BonusCardDataSO,float> cardScore=data=>{")
    if "UnityEditor.EditorApplication.update-=tick;" not in route:
        raise ValueError("Route callback cleanup disappeared")
    template = (FOLDER / "BatchDefense.template.cs.txt").read_text()
    replacements = {
        "@@BOOTSTRAP@@": bootstrap,
        "@@ROUTE@@": route,
        "@@BOOTSTRAP_SHA@@": digest(original_bootstrap),
        "@@ROUTE_SHA@@": digest(original_route),
        "@@ROUTE_FILE@@": args.route,
        "@@CASES_FILE@@": args.cases_output,
        "@@CASES_SHA@@": digest(case_text),
        "@@CSV_HEADER@@": ",".join(CASE_FIELDS),
    }
    for marker, value in replacements.items():
        if marker not in template:
            raise ValueError("Template marker missing: " + marker)
        template = template.replace(marker, value)
    if "@@" in template:
        raise ValueError("Unresolved template marker")
    output = FOLDER / args.output
    output.write_text(template)
    manifest = dict(generated_utc=datetime.now(timezone.utc).isoformat(), output=output.name,
                    output_sha256=digest(template), cases=cases_path.name, cases_sha256=digest(case_text),
                    case_count=len(case_rows), cases_created=cases_created,
                    source_sha256={bootstrap_path.name: digest(original_bootstrap), route_path.name: digest(original_route)},
                    transformations=["Bootstrap: expose readyCheck removal hook only",
                                     "Route: expose native finish callback only",
                                     "Route: replace one isPlaying=false with timeScale=0; all native cleanup retained"],
                    runtime_verification="Pending Unity compilation and execution by root")
    (FOLDER / "batch-build-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(f"Generated {output.name}: {len(template.encode())} bytes, {len(case_rows)} cases; originals unchanged")


if __name__ == "__main__":
    main()
