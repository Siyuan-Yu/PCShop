"""广回归快照加修复后的定向覆盖；不能让后运行的旧镜像覆盖新代码的验证。"""
import argparse
import csv
import hashlib
import json
from collections import Counter
from pathlib import Path
from summarize import collect

p = argparse.ArgumentParser()
p.add_argument('--wide', required=True)
p.add_argument('--repair', required=True)
p.add_argument('--extra-root', action='append', default=[])
p.add_argument('--baseline-csv', required=True)
p.add_argument('--out', required=True)
args = p.parse_args()
wide_runs, wide = collect([args.wide])
repair_runs, repairs = collect([args.repair] + args.extra_root)
accepted = {'story-venues-v2', 'critical-followup', 'focused-final', 'events-and-opening-audit',
            'final-regression', 'events-seed6', 'final-followup', 'quiet-performance',
            'performance-regression', 'pricing-followup'}
current = {name: rows[-1] for name, rows in wide.items()}
for name, rows in repairs.items():
    verified = [row for row in rows if row['tag'] in accepted]
    if verified:
        current[name] = verified[-1]
with open(args.baseline_csv, encoding='utf-8-sig', newline='') as stream:
    baseline = {row['用例']: row for row in csv.DictReader(stream)}
hashes = {}
artifact_hashes = {}
for run in wide_runs + repair_runs:
    file = Path(run['artifact']) / 'source-manifest.json'
    if file.exists():
        rows = json.loads(file.read_text(encoding='utf-8-sig'))
        scripts = sorted((row['path'], row['sha256']) for row in rows if row['path'].startswith('Assets/Scripts/'))
        hashes[run['tag']] = hashlib.sha256(json.dumps(scripts).encode()).hexdigest()
        artifact_hashes[run['artifact']] = hashes[run['tag']]
out = Path(args.out)
out.mkdir(parents=True, exist_ok=True)
recovered = [n for n, r in current.items() if baseline.get(n, {}).get('本轮状态') in ('Failed', 'Timeout') and r['result'] == 'Passed']
regressed = [n for n, r in current.items() if baseline.get(n, {}).get('本轮状态') == 'Passed' and r['result'] != 'Passed']
report = dict(counts=dict(Counter(r['result'] for r in current.values())), uniqueCases=len(current),
              recovered=recovered, previouslyPassedNowNotPassed=regressed,
              baselineNamesNotObserved=sorted(set(baseline)-set(current)),
              productionFingerprints=hashes,
              limitation='广回归镜像冻结于本轮早期；较新源码的定向结果覆盖同名用例。这是分层证据，不是最终源码全量同时通过。')
(out/'修复分层汇总.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
with (out/'修复后用例.csv').open('w', encoding='utf-8-sig', newline='') as stream:
    writer = csv.writer(stream)
    writer.writerow(['用例', '上一轮状态', '本轮观察状态', '证据轮次', '生产源码指纹', '失败说明', '堆栈', '证据目录'])
    for name, row in sorted(current.items()):
        writer.writerow([name, baseline.get(name, {}).get('本轮状态', '新增或改名'), row['result'], row['tag'],
                         artifact_hashes.get(row['artifact']), row.get('message', ''), row.get('stack', ''),
                         'runs/' + Path(row['artifact']).name])
print(json.dumps(dict(uniqueCases=len(current), counts=report['counts'], recovered=len(recovered),
                     regressions=len(regressed), baselineNamesNotObserved=len(report['baselineNamesNotObserved'])), ensure_ascii=False))
