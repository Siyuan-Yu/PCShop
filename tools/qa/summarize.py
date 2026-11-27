"""汇总 NUnit XML、超时进度和历次结果；保留失败历史，不用重跑掩盖偶发失败。"""
import argparse
import csv
import json
import re
import xml.etree.ElementTree as ET
from collections import Counter, defaultdict
from pathlib import Path


def collect(roots):
    runs, history = [], defaultdict(list)
    for path in sorted(p for root in roots for p in Path(root).glob('*/summary.json')):
        run = json.loads(path.read_text(encoding='utf-8-sig'))
        # 同一证据根目录还可包含资产预生成、画面巡检；它们不是 NUnit 批次。
        if 'tag' not in run or 'started' not in run:
            continue
        run['artifact'] = str(path.parent)
        cases = []
        xml = path.parent / 'results.xml'
        if xml.exists():
            for case in ET.parse(xml).getroot().iter('test-case'):
                cases.append(dict(name=case.get('fullname'), result=case.get('result'),
                                  seconds=float(case.get('duration', 0)),
                                  message=case.findtext('failure/message', ''), stack=case.findtext('failure/stack-trace', '')))
        elif (path.parent / 'cases.jsonl').exists():
            for line in (path.parent / 'cases.jsonl').read_text(encoding='utf-8-sig').splitlines():
                if line.strip():
                    cases.append(json.loads(line))
        log = (path.parent / 'unity.log').read_text(encoding='utf-8-sig', errors='replace') if (path.parent / 'unity.log').exists() else ''
        if not xml.exists() and not cases:
            for name, result in re.findall(r'^REGRESSION DONE (.+) ((?:Passed|Failed|Skipped|Ignored|Inconclusive)(?::[^\s]+)?)\s*$', log, re.M):
                cases.append(dict(name=name, result=result, seconds=None, message='仅有进度日志；原始异常见 unity.log', stack=''))
        for case in cases:
            # NUnit 进度回调使用 Failed:Error 等子状态，XML 则写 Failed；统一统计，保留原值。
            case.setdefault('rawResult', case['result'])
            case['result'] = case['result'].split(':', 1)[0]
        if run.get('timeout'):
            started = re.findall(r'^REGRESSION CASE (.+)$', log, re.M)
            finished = {case['name'] for case in cases}
            if started and started[-1].strip() not in finished:
                cases.append(dict(name=started[-1].strip(), result='Timeout', seconds=None,
                                  message='整个分批进程超时，此用例未返回；不能据此断定该用例单独卡死', stack=''))
        run['observedCases'] = len(cases)
        run['observedCounts'] = dict(Counter(case['result'] for case in cases))
        runs.append(run)
        for case in cases:
            case.update(tag=run['tag'], started=run['started'], artifact=str(path.parent))
            history[case['name']].append(case)
    runs.sort(key=lambda run: run['started'])
    for entries in history.values():
        entries.sort(key=lambda case: case['started'])
    return runs, history


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', action='append', required=True)
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    out = Path(args.out); out.mkdir(parents=True, exist_ok=True)
    runs, history = collect(args.root)
    latest = [entries[-1] for entries in history.values()]
    counts = dict(Counter(case['result'] for case in latest))
    report = dict(runs=runs, latestCounts=counts, history=history)
    (out / '测试历史.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
    with (out / '待处理用例.csv').open('w', encoding='utf-8-sig', newline='') as stream:
        writer=csv.writer(stream);writer.writerow(['用例','最新状态','轮次','失败说明','首次结果','记录数','证据目录','复核分类'])
        for case in sorted(latest, key=lambda c: c['name']):
            if case['result'] != 'Passed':
                entries=history[case['name']]
                writer.writerow([case['name'],case['result'],case['tag'],case.get('message'),entries[0]['result'],len(entries),case['artifact'],'待逐项复核'])
    lines=['# 多轮自动化测试原始汇总','',
           '该表保留全部尝试，最新结果并不抹去先前失败。逻辑断言失败不等于已确认的产品 bug；超时也不等于零用例执行。','',
           '## 各轮记录','', '|轮次|完整结果|XML 用例数|通过|失败|耗时（秒）|进度中已观察用例|', '|---|---|---:|---:|---:|---:|---:|']
    for run in runs:
        lines.append(f"|{run['tag']}|{run['result']}|{run['total']}|{run['passed']}|{run['failed']}|{run['seconds']}|{run['observedCases']}|")
    lines.extend(['','## 按用例最新一次结果','',str(counts),'',
                  '各用例的最新结果可能来自不同轮次、不同源代码快照，不等于在最终版本上全量同时通过。实机截图另行记录。', '',
                  '## 已失败后通过的用例',''])
    for name, entries in sorted(history.items()):
        if entries[-1]['result']=='Passed' and any(case['result'] not in ('Passed','Skipped') for case in entries[:-1]):
            lines.append('- '+name+'：'+' → '.join(case['result'] for case in entries))
    (out / '自动汇总.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
    print(json.dumps(dict(runs=len(runs), uniqueCases=len(history), latestCounts=counts),ensure_ascii=False))


if __name__=='__main__': main()
