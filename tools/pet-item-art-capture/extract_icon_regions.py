"""读取图集的 alpha，写入独立 UV 范围；不改动图片像素。

AI 图集的实际留白不严格等分。先找相邻图标间的透明谷，再记录每格的
透明包围盒。运行时保持原宽高比，不再从下一行带进一截图案。
"""
import json
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'Assets/Resources/ItemPortraits'


def cuts(projection, count):
    n = len(projection)
    result = [0]
    for i in range(1, count):
        target = n * i / count
        lo, hi = int(target - n / count * .30), int(target + n / count * .30)
        # 三像素宽的透明缝可保证双线性采样不碰到旁边的图案。
        occupancy = np.convolve(projection, np.ones(3), 'same')
        window = occupancy[lo:hi]
        candidates = np.flatnonzero(window == window.min()) + lo
        runs = np.split(candidates, np.flatnonzero(np.diff(candidates) != 1) + 1)
        centers = [(run[0] + run[-1]) // 2 for run in runs if len(run)]
        result.append(min(centers, key=lambda c: abs(c - target)))
    return result + [n]


def main():
    path = ART / 'catalog.json'
    catalog = json.loads(path.read_text(encoding='utf-8-sig'))
    report = ['图集\t格\t物品\t像素框\t边界占用']
    for sheet in catalog['sheets']:
        image = np.asarray(Image.open(ART / (sheet['texture'] + '.png')).convert('RGBA'))
        height, width = image.shape[:2]
        alpha = image[:, :, 3] > 32
        count = 3 if sheet['texture'] == 'supplement' else 5
        ys = cuts(alpha.sum(axis=1), count)
        rough_xs = cuts(alpha.sum(axis=0), count)
        regions = []
        for index, item in enumerate(sheet['items']):
            row, col = divmod(index, count)
            # 每列的细长瓶口、吊灯等可能高过其他列，边界按本列透明谷修正。
            local_ys = cuts(alpha[:, rough_xs[col]:rough_xs[col+1]].sum(axis=1), count)
            y0, y1 = local_ys[row:row+2]
            xs = cuts(alpha[y0:y1].sum(axis=0), count)
            x0, x1 = xs[col:col+2]
            mask = alpha[y0:y1, x0:x1]
            points = np.argwhere(mask)
            if len(points):
                top, left = points.min(axis=0)
                bottom, right = points.max(axis=0) + 1
                xx0, yy0 = max(x0, x0 + left - 2), max(y0, y0 + top - 2)
                xx1, yy1 = min(x1, x0 + right + 2), min(y1, y0 + bottom + 2)
            else:
                xx0, yy0, xx1, yy1 = x0, y0, x1, y1
            boundary = int(mask[0].sum() + mask[-1].sum() + mask[:, 0].sum() + mask[:, -1].sum())
            regions.append(dict(x=round(xx0 / width, 8), y=round(1 - yy1 / height, 8),
                                width=round((xx1 - xx0) / width, 8), height=round((yy1 - yy0) / height, 8)))
            report.append(f'{sheet["texture"]}\t{index}\t{item}\t{xx0},{yy0},{xx1},{yy1}\t{boundary}')
            if item and boundary:
                print('需查看边界：', sheet['texture'], item, boundary)
        sheet['regions'] = regions
        # 两个储物入口实际都是纸箱，统一使用纸箱轮廓，避免把店铺纸箱画成塑料周转筐。
        if 'shop_box' in sheet['items'] and 'box' in sheet['items']:
            sheet['regions'][sheet['items'].index('shop_box')] = dict(sheet['regions'][sheet['items'].index('box')])
            report.append(f'{sheet["texture"]}\t{sheet["items"].index("shop_box")}\tshop_box\t共用 box 的范围（覆盖本格原始测量）\t0')
    path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    (Path(__file__).parent / '图标裁切范围.tsv').write_text('\n'.join(report) + '\n', encoding='utf-8')
    print('已记录', sum(bool(x) for s in catalog['sheets'] for x in s['items']), '个图标；未修改任何图片像素。')


if __name__ == '__main__':
    main()
