"""归档测试证据，不复制玩家存档、临时工程或庞大的 Unity 日志，不进行上传。"""
import argparse
import shutil
import json
from collections import defaultdict
from pathlib import Path
from summarize import collect, main as summarize_main
import sys


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', action='append', required=True)
    parser.add_argument('--visual')
    parser.add_argument('--out', required=True)
    args = parser.parse_args()
    out = Path(args.out).resolve()
    runs = out / 'runs'
    runs.mkdir(parents=True, exist_ok=True)
    _, history = collect(args.root)
    recovered = defaultdict(list)
    for attempts in history.values():
        for case in attempts:
            recovered[Path(case['artifact'])].append(case)
    for root in args.root:
        for summary in Path(root).glob('*/summary.json'):
            source = summary.parent
            target = runs / source.name
            target.mkdir(exist_ok=True)
            for filename in ['summary.json', 'results.xml', 'failures.json', 'cases.jsonl', 'source-manifest.json']:
                if (source / filename).exists():
                    shutil.copy2(source / filename, target / filename)
            for progress in source.glob('*-progress.txt'):
                shutil.copy2(progress, target / progress.name)
            for pattern in ('random-events-*.txt', '开局预算-*.tsv', 'simulation-performance.tsv', 'core-timings.tsv', 'hotspots.txt'):
                for evidence in source.glob(pattern):
                    shutil.copy2(evidence, target / evidence.name)
            log = source / 'unity.log'
            if log.exists():
                timings = [line for line in log.read_text(encoding='utf-8-sig', errors='replace').splitlines()
                           if line.startswith('RECOVERY_COMPARE ')]
                if timings:
                    (target / '睡眠恢复耗时.txt').write_text('\n'.join(timings)+'\n', encoding='utf-8')
            if not (source / 'results.xml').exists():
                # 原始日志很大；将日志中恢复的完成记录及最后一个超时用例归档，避免丢失中断证据。
                (target / 'cases.jsonl').write_text(''.join(json.dumps(case, ensure_ascii=False) + '\n'
                    for case in recovered[source]), encoding='utf-8')
            (target / '原始目录.txt').write_text(str(source.resolve()) + '\n', encoding='utf-8')
    if args.visual:
        visual = out / '实机界面'
        visual.mkdir(exist_ok=True)
        for source in Path(args.visual).iterdir():
            if source.is_file() and source.suffix in ('.png', '.txt', '.json'):
                shutil.copy2(source, visual / source.name)
    sys.argv = ['summarize.py', '--root', str(runs), '--out', str(out)]
    # 汇总工具默认读取 root/*/summary.json，与归档目录结构一致。
    summarize_main()


if __name__ == '__main__':
    main()
