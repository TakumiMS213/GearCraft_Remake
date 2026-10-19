"""Read all serialized ParticleSystems under Assets and audit enabled velocity XYZ modes."""
from pathlib import Path
import json
import re
import subprocess

qa = Path(__file__).resolve().parent
found = subprocess.run(['rg', '-l', '^  VelocityModule:', 'Assets', '-g', '*.prefab', '-g', '*.unity', '-g', '*.asset'], capture_output=True, text=True)
if found.returncode not in (0, 1):
    raise SystemExit(found.stderr)
module = re.compile(r'(?ms)^  VelocityModule:\r?\n(.*?)(?=^  \S|\Z)')
doc = re.compile(r'(?ms)^--- !u!198 &(-?\d+)(?: stripped)?\r?\n(.*?)(?=^--- !u!|\Z)')
records, errors = [], []
total = disabled = 0
for name in sorted(found.stdout.splitlines()):
    text = Path(name).read_bytes().decode('utf-8')
    for particle in doc.finditer(text):
        total += 1
        velocity = module.search(particle.group(2))
        if velocity is None:
            errors.append({'path': name, 'particle_id': particle.group(1), 'error': 'VelocityModule missing'})
            continue
        body = velocity.group(1)
        if not re.search(r'^    enabled: 1\r?$', body, re.M):
            disabled += 1
            continue
        entry = {'path': name, 'particle_id': particle.group(1), 'line': text.count('\n', 0, particle.start(2) + velocity.start()) + 1, 'axes': {}}
        for axis in 'xyz':
            block = re.search(r'(?ms)^    ' + axis + r':\r?\n(.*?)(?=^    \S|\Z)', body)
            if not block:
                errors.append({'path': name, 'particle_id': particle.group(1), 'error': 'Axis missing: ' + axis})
                continue
            def value(field):
                match = re.search(r'^      ' + field + r': ([^\r\n]+)', block.group(1), re.M)
                if match is None:
                    raise ValueError(name + ' missing ' + axis + '.' + field)
                return match.group(1)
            entry['axes'][axis] = {'mode': int(value('minMaxState')), 'scalar': float(value('scalar')), 'minScalar': float(value('minScalar'))}
        records.append(entry)
mismatches = [row for row in records if len(row['axes']) != 3 or len({v['mode'] for v in row['axes'].values()}) != 1]
result = {'scope': 'All serialized !u!198 ParticleSystem blocks with VelocityModule under Assets (*.prefab, *.unity, *.asset); excludes runtime-generated systems and PackageCache',
          'files_with_velocity_modules': len(found.stdout.splitlines()), 'particle_systems': total, 'disabled_velocity_modules': disabled,
          'enabled_velocity_modules': len(records), 'mismatch_count': len(mismatches), 'parse_errors': errors, 'mismatches': mismatches, 'enabled_records': records}
(qa / 'particle-velocity-audit-after.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
print(json.dumps({key: value for key, value in result.items() if key not in ('enabled_records', 'scope')}, ensure_ascii=False, indent=2))
raise SystemExit(1 if errors or mismatches else 0)
