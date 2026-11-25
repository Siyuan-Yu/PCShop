"""将 TownMatchCamera 的复拍整理成对照板和地点索引。依赖 Pillow。"""
import argparse
import csv
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFont, ImageStat


DISTRICTS = [("02", "社区", "community"), ("03", "公园", "park"), ("04", "花园", "garden")]
VIEWS = ["街景 A", "街景 B", "鸟瞰 A", "鸟瞰 B"]
PAPER, INK = "#f2f1eb", "#25382f"


def read(path):
    with Image.open(path) as im:
        return im.convert("RGB")


def quadrants(path):
    image = read(path)
    w, h = image.size
    return [image.crop((x*w//2, y*h//2, (x+1)*w//2, (y+1)*h//2))
            for y in range(2) for x in range(2)]


def frames(folder, tag):
    return [read(folder / f"{prefix}{tag}{angle}.png")
            for prefix in ("", "wide") for angle in ("A", "B")]


def rgb_score(a, b):
    size = (128, 72)
    diff = ImageChops.difference(a.resize(size, Image.Resampling.BOX), b.resize(size, Image.Resampling.BOX))
    return 100 * (1 - sum(ImageStat.Stat(diff).mean) / (3 * 255))


def sheet(path, title, rows, columns, width, font):
    pad = 16
    height = width * 9 // 16
    large = ImageFont.truetype(str(font), 32)
    small = ImageFont.truetype(str(font), 19 if width >= 520 else 16)
    image = Image.new("RGB", (pad + columns * (width + pad), 78 + len(rows)*(height+51)), PAPER)
    draw = ImageDraw.Draw(image)
    draw.text((pad, 13), title, fill=INK, font=large)
    draw.line((pad, 59, image.width-pad, 59), fill="#aab9ae")
    for row, cells in enumerate(rows):
        for col, (label, frame) in enumerate(cells):
            x, y = pad + col*(width+pad), 74 + row*(height+51)
            draw.text((x, y), label, font=small, fill=INK)
            image.paste(frame.resize((width, height), Image.Resampling.LANCZOS), (x, y+31))
    image.save(path, quality=91, subsampling=0, optimize=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("capture", type=Path)
    parser.add_argument("concepts", type=Path)
    parser.add_argument("output", type=Path)
    parser.add_argument("--font", type=Path, default=Path("C:/Windows/Fonts/msyh.ttc"))
    parser.add_argument("--baseline", type=Path)
    parser.add_argument("--occluded", type=Path, help="修正前的社区默认镜头原图")
    args = parser.parse_args()
    out, src = args.output, args.capture
    out.mkdir(parents=True, exist_ok=True)
    overview, race, entrance, times = [], [], [], []
    scores = []
    race_ref = quadrants(args.concepts / "四阶段赛场.png")
    with (src / "地点索引.tsv").open(encoding="utf-8-sig") as handle:
        manifest = list(csv.reader(handle, delimiter="\t"))
    index = ["地区\t地点 ID\t地点名\t模型类型\t图版\t行\t列"]
    for stage, (tag, name, key) in enumerate(DISTRICTS, start=1):
        ref = quadrants(next(args.concepts.glob(tag+"-*.png")))
        current = frames(src, tag)
        before = frames(args.baseline, tag) if args.baseline else None
        rows = []
        for i, view in enumerate(VIEWS):
            rows.append([(view+" · 已确认概念", ref[i]), (view+" · Unity 实机", current[i])])
            scores.append([name, view, f"{rgb_score(ref[i],current[i]):.1f}",
                           f"{rgb_score(ref[i],before[i]):.1f}" if before else ""])
        sheet(out/f"{tag}-{name}概念与实机.jpg", name+"　概念与实机", rows, 2, 880, args.font)
        overview.append([(name+" · 街景", current[0]), (name+" · 正常游玩镜头", read(src/f"play{tag}.png"))])
        times.append([(name+" · 10:30", current[0]), (name+" · 18:10", read(src/f"{tag}Dusk.png")),
                      (name+" · 21:30", read(src/f"{tag}Night.png"))])
        prefix=f"race_{stage}"
        race.append([(name+" · 概念看台", race_ref[stage]), (name+" · 实机看台", read(src/f"{prefix}Front.png")),
                     (name+" · 实机全场", read(src/f"{prefix}B.png"))])
        entrance.append([(name+" · 接待入口", read(src/f"{prefix}C.png")),
                         (name+" · 临水车库", read(src/f"sites/{tag}-garage_{stage}.png")),
                         (name+" · 赛道弯道", read(src/f"{prefix}D.png"))])
        sites = [r for r in manifest if r[0] == key]
        filename = f"{tag}-{name}地点索引.jpg"
        cells = []
        for j, (_, site_id, title, model, file) in enumerate(sites):
            cells.append((f"{j+1:02d}　{title}", read(src/file)))
            index.append(f"{name}\t{site_id}\t{title}\t{model}\t{filename}\t{j//5+1}\t{j%5+1}")
        sheet(out/filename, f"{name}　{len(sites)} 处地点", [cells[i:i+5] for i in range(0,len(cells),5)], 5, 300, args.font)
    sheet(out/"三区街景实机.jpg", "社区、公园、花园　正常游玩距离", overview, 2, 680, args.font)
    sheet(out/"三区昼夜实机.jpg", "同一地点　日间、傍晚与夜间", times, 3, 550, args.font)
    sheet(out/"赛场看台与全景.jpg", "三个赛场　看台与赛道全貌", race, 3, 580, args.font)
    sheet(out/"赛场入口与弯道.jpg", "三个赛场　接待、车库与弯道", entrance, 3, 550, args.font)
    if args.occluded:
        wheel_views = [
            [("裁缝铺 · 修正前", read(args.occluded)), ("裁缝铺 · 修正后", read(src/"play02.png"))],
            [("社区游乐场 · 售票入口", read(src/"02WheelGate.png")), ("社区游乐场 · 全景", read(src/"02Wheel.png"))],
            [("花园游乐场 · 售票入口", read(src/"04WheelGate.png")), ("花园游乐场 · 全景", read(src/"04Wheel.png"))],
        ]
        sheet(out/"摩天轮与街景视线.jpg", "街道视线与摩天轮地标", wheel_views, 2, 680, args.font)
    (out/"地点索引.tsv").write_text("\n".join(index)+"\n", encoding="utf-8")
    for label in ("社区","公园","花园"):
        rows = [r for r in scores if r[0]==label]
        print(label, *rows, "平均", round(sum(float(row[2]) for row in rows)/len(rows),1))


if __name__ == "__main__":
    main()
