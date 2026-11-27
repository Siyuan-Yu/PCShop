"""按用例去重比较基线与复测，超时批次也读取已保存进度。"""
import argparse
import csv
import hashlib
import json
from collections import Counter, defaultdict
from pathlib import Path
from summarize import collect


def latest(history):
    return {name: attempts[-1] for name, attempts in history.items()}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--baseline', required=True)
    parser.add_argument('--regression', required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    _, before_history = collect([args.baseline])
    runs, after_history = collect([args.regression])
    before, after = latest(before_history), latest(after_history)
    counts = dict(Counter(case['result'] for case in after.values()))
    groups = defaultdict(Counter)
    for name, case in after.items():
        groups[name.split('.')[2]][case['result']] += 1
    regressions = [name for name in after if before.get(name, {}).get('result') == 'Passed' and after[name]['result'] != 'Passed']
    recovered = [name for name in after if before.get(name, {}).get('result') in ('Failed', 'Timeout') and after[name]['result'] == 'Passed']
    fingerprints, production_fingerprints, test_fingerprints = {}, {}, {}
    for run in runs:
        manifest = Path(run['artifact']) / 'source-manifest.json'
        if manifest.exists():
            data = json.loads(manifest.read_text(encoding='utf-8-sig'))
            code = [(row['path'], row['sha256']) for row in data if row['path'].startswith(('Assets/Scripts/', 'Assets/Tests/'))]
            fingerprints[run['tag']] = hashlib.sha256(json.dumps(sorted(code)).encode()).hexdigest()
            for prefix, target in [('Assets/Scripts/', production_fingerprints), ('Assets/Tests/', test_fingerprints)]:
                target[run['tag']] = hashlib.sha256(json.dumps(sorted(row for row in code if row[0].startswith(prefix))).encode()).hexdigest()
    out = Path(args.out); out.mkdir(parents=True, exist_ok=True)
    report = dict(uniqueCases=len(after), counts=counts, recovered=recovered, previouslyPassedNowNotPassed=regressions,
                  codeFingerprints=fingerprints, productionFingerprints=production_fingerprints, testFingerprints=test_fingerprints,
                  groups=groups, runTags=[run['tag'] for run in runs])
    (out / '回归对照.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    with (out / '最终回归用例.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['用例', '基线状态', '本轮状态', '本轮批次', '失败说明'])
        for name, case in sorted(after.items()):
            writer.writerow([name, before.get(name, {}).get('result', '未观察到'), case['result'], case['tag'], case.get('message', '')])
    lines = ['# 全项目回归对照', '', f"已观察 {len(after)} 个不同用例：{counts}。重复补测按最后一次结果计数，原始历史仍保留。", '',
             f"生产脚本指纹种类：{len(set(production_fingerprints.values()))}；测试源码指纹种类：{len(set(test_fingerprints.values()))}（已记录 {len(fingerprints)} 批）。不包含美术二进制资源，也不代表发行包验证。", '',
             '|测试组|通过|失败|超时|其他|', '|---|---:|---:|---:|---:|']
    for group, states in sorted(groups.items()):
        lines.append(f"|{group}|{states['Passed']}|{states['Failed']}|{states['Timeout']}|{sum(count for state,count in states.items() if state not in ('Passed','Failed','Timeout'))}|")
    lines.extend(['', '## 基线通过而本轮未通过', ''])
    lines.extend('- '+name for name in sorted(regressions))
    if not regressions: lines.append('当前已完成结果中未观察到此类变化；未执行和被过滤的用例不在此结论内。')
    lines.extend(['', '## 基线失败后本轮通过', ''])
    lines.extend('- '+name for name in sorted(recovered))
    (out / '回归对照.md').write_text('\n'.join(lines)+'\n', encoding='utf-8')
    print(json.dumps(dict(uniqueCases=len(after), counts=counts, recovered=len(recovered), regressions=len(regressions), codeFingerprints=len(set(fingerprints.values()))), ensure_ascii=False))


if __name__ == '__main__':
    main()
