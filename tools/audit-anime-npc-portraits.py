"""Audit generated portrait files/provenance without editing image pixels."""
import hashlib
import json
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[1]
manifest = json.loads((ROOT / 'docs/art/npc-anime-portraits.json').read_text(encoding='utf-8'))
records, errors, hashes, guids = [], [], set(), set()
for p in manifest['portraits']:
    asset = ROOT / p['asset']
    if p['status'] != 'complete' or not asset.is_file():
        errors.append(p['id'] + ': missing or incomplete')
        continue
    blob = asset.read_bytes()
    width, height = struct.unpack('>II', blob[16:24])
    digest = hashlib.sha256(blob).hexdigest()
    meta = Path(str(asset) + '.meta').read_text(encoding='utf-8')
    guid = re.search(r'(?m)^guid: ([a-f0-9]+)$', meta).group(1)
    if blob[:8] != b'\x89PNG\r\n\x1a\n' or not .73 < width/height < .77:
        errors.append(p['id'] + ': bad PNG or aspect')
    if digest != p['sha256'] or digest in hashes or guid in guids:
        errors.append(p['id'] + ': hash/GUID mismatch or duplication')
    source = Path(p['sourceFile'])
    if not source.is_file() or hashlib.sha256(source.read_bytes()).hexdigest() != digest:
        errors.append(p['id'] + ': source differs')
    hashes.add(digest)
    guids.add(guid)
    records.append(dict(id=p['id'], age=p['age'], width=width, height=height, sha256=digest, guid=guid))
roster = ROOT / 'docs/previews/anime-npc-portraits/runtime-roster.tsv'
if roster.exists():
    people = [line.split('\t') for line in roster.read_text(encoding='utf-8-sig').splitlines() if line]
    actual = {row[0]: row for row in people}
    expected = {p['id']: p for p in manifest['portraits'] if p['id'] != 'default'}
    if actual.keys() != expected.keys():
        errors.append('runtime and art roster IDs differ')
    for key in actual.keys() & expected.keys():
        if int(actual[key][2]) != expected[key]['age'] or actual[key][1] != expected[key]['name']:
            errors.append(key + ': runtime name/age differs')
if len(records) != 125:
    errors.append('expected 125 portrait assets')
report = dict(total=len(manifest['portraits']), audited=len(records), uniqueHashes=len(hashes),
              uniqueGuids=len(guids), errors=errors, records=records)
out = ROOT / 'docs/validation/anime-npc-portraits-source-audit.json'
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print(json.dumps({k:v for k,v in report.items() if k != 'records'}, ensure_ascii=False))
raise SystemExit(bool(errors))
