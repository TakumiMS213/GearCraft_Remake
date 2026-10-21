#!/usr/bin/env python3
"""Select a fresh case plan and update only its batch hash. No Unity operations."""
from pathlib import Path
import argparse
import csv
import hashlib
import io
import re

here=Path(__file__).resolve().parent
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument("--mode",choices=["direct","survival"],default="direct")
parser.add_argument("--pass-name",required=True)
parser.add_argument("--boss",choices=["IC","BB","AR","EK"])
parser.add_argument("--weapon",choices=["GearCraft","SteamCannon","SteamThrower","RailCraft","SteamGatling"])
parser.add_argument("--seeds",default="77")
args=parser.parse_args()
if not re.fullmatch(r"[A-Za-z0-9_]+",args.pass_name):
    raise SystemExit("pass-name must contain only letters/digits/underscore")
for file in here.glob("*progress.txt"):
    if file.read_text().lstrip().startswith(("RUNNING","LOADING")):
        raise SystemExit("A QA operation is still active: "+file.name)
seeds=[int(s) for s in args.seeds.split(",")]
stage={"IC":"7","BB":"16","AR":"28","EK":"31"}
rows=list(csv.DictReader((here/("all-"+args.mode+"-cases.csv")).open()))
fields=list(rows[0])
selected=[]
for template in rows:
    if args.boss and template["start_stage"]!=stage[args.boss]: continue
    if args.weapon and template["weapon"]!=args.weapon: continue
    for seed in seeds:
        row=dict(template)
        row["label"]=re.sub(r"_seed\d+_pass1$",f"_seed{seed}_{args.pass_name}",row["label"])
        row["seed"]=str(seed)
        if list(here.glob(row["label"]+"-*")):
            raise SystemExit("Existing results must not be overwritten: "+row["label"])
        selected.append(row)
if not selected:
    raise SystemExit("No valid cases selected")
buf=io.StringIO();writer=csv.DictWriter(buf,fieldnames=fields,lineterminator="\n")
writer.writeheader();writer.writerows(selected)
plan=here/(args.mode+"-cases.csv");batch=here/("Batch"+args.mode.title()+".cs.txt")
old_hash=hashlib.sha256(plan.read_bytes()).hexdigest()
new_hash=hashlib.sha256(buf.getvalue().encode()).hexdigest()
text=batch.read_text()
if text.count(old_hash)!=1:
    raise SystemExit("Current plan SHA does not match generated batch; review before mutation")
batch.write_text(text.replace(old_hash,new_hash,1));plan.write_text(buf.getvalue())
print(f"Prepared {len(selected)} {args.mode} cases for {args.pass_name}; no Unity execution.")
