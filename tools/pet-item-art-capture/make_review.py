"""归档 Unity 原帧，拼对照页与动作动图；不修饰模型截图或重绘商品图标。"""
from pathlib import Path
from PIL import Image, ImageOps, ImageDraw, ImageFont
import shutil
import json
import sys
import html

ROOT = Path(__file__).resolve().parents[2]
SOURCE = Path(sys.argv[1]) if len(sys.argv) > 1 else Path('D:/tmp/pcshop-pet-item-final')
OUT = ROOT / 'docs/art-direction/implemented/pets-items-20261127'
OUT.mkdir(parents=True, exist_ok=True)
for path in SOURCE.iterdir():
    if path.suffix.lower() in ('.png', '.tsv', '.txt'):
        shutil.copy2(path, OUT / path.name)

font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 24)


def board(names, output, columns, width):
    cell_h = int(width * 9 / 16) + 38
    canvas = Image.new('RGB', (columns * width, ((len(names)+columns-1)//columns) * cell_h), '#EFECE6')
    draw = ImageDraw.Draw(canvas)
    for i, name in enumerate(names):
        x, y = (i % columns) * width, (i // columns) * cell_h
        image = Image.open(OUT / (name + '.png')).convert('RGB')
        image.thumbnail((width, cell_h - 38), Image.Resampling.LANCZOS)
        canvas.paste(image, (x + (width-image.width)//2, y+38))
        draw.text((x+10, y+4), name, font=font, fill='#3A3733')
    canvas.save(OUT / output, quality=94)


for stem, total, duration in [('事件猫狗-走路',24,83), ('捡球',12,225), ('逗猫',12,188)]:
    frames = [Image.open(OUT / f'{stem}-{i:02}.png').convert('RGB').resize((960,540),Image.Resampling.LANCZOS) for i in range(total)]
    frames[0].save(OUT / (stem+'.gif'), save_all=True, append_images=frames[1:], duration=duration, loop=0)
    board([f'{stem}-{i:02}' for i in range(total)], stem+'-逐帧.jpg', 4 if total==12 else 6, 480 if total==12 else 320)

board([f'{animal}-{angle}' for animal in ('狗','猫') for angle in ('正面','游玩角度','侧面','背面')], '静态八机位.jpg', 2, 960)

compare = Image.new('RGB',(1920,1160),'#EFECE6')
draw = ImageDraw.Draw(compare)
for row, (animal, reference) in enumerate([('狗','pet-dog-buildable.jpg'),('猫','pet-cat-buildable.jpg')]):
    for col, (path, label) in enumerate([(ROOT/'docs/art-direction/concept/animals'/reference,'既定可建版概念'),(OUT/f'{animal}-概念同向近景.png','Unity 实机 · 同向近景')]):
        picture = Image.open(path).convert('RGB')
        if col == 0:
            picture = picture.crop((0,0,340,350 if animal == '狗' else 366))
        # 只为等高并排裁切背景；原帧另存，保持颜色、形体与阴影不变。
        mask = picture.convert('HSV').getchannel('S').point(lambda value: 255 if value > 45 else 0)
        box = mask.getbbox()
        if box:
            pad = 6 if col == 0 else 12
            picture = picture.crop((max(0,box[0]-pad),max(0,box[1]-pad),min(picture.width,box[2]+pad),min(picture.height,box[3]+pad)))
        picture = ImageOps.contain(picture,(880,500),Image.Resampling.LANCZOS)
        x,y=col*960,row*580
        compare.paste(picture,(x+(960-picture.width)//2,y+40+(540-picture.height)//2))
        draw.text((x+18,y+5),animal+' · '+label,font=font,fill='#3A3733')
compare.save(OUT/'概念与模型对照.jpg',quality=95)

catalog = json.loads((ROOT/'Assets/Resources/ItemPortraits/catalog.json').read_text(encoding='utf-8-sig'))
icon_names = ['图标-'+sheet['texture'] for sheet in catalog['sheets']]
groups = {
    '概念与静态': ['概念与模型对照','狗-概念同向近景','猫-概念同向近景','静态八机位','事件猫狗-实际入口','事件猫狗-点头摆尾端点'],
    '实际场景': ['宠物店-正常距离','宠物店-聚焦寄养区','宠物店-主人送来寄养','陪伴小狗-领养回家'],
    '动作': ['事件猫狗-走路','捡球','逗猫','事件猫狗-走路-逐帧','捡球-逐帧','逗猫-逐帧'],
    '实际界面': ['实际界面-背包与后仓','实际界面-电脑配件采购','实际界面-城市进货'],
    '全量商品图标': icon_names,
}
sections=[]
for title,names in groups.items():
    figures=[]
    for name in names:
        file=next(OUT/(name+suffix) for suffix in ('.gif','.jpg','.png') if (OUT/(name+suffix)).exists())
        escaped=html.escape(file.name,quote=True)
        figures.append(f'<figure><figcaption>{html.escape(name)}</figcaption><a href="{escaped}" target="_blank"><img src="{escaped}" loading="lazy"></a></figure>')
    sections.append(f'<section id="{title}"><h2>{title}</h2><div class="grid">'+''.join(figures)+'</div></section>')
(OUT/'index.html').write_text('''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>猫狗与商品图标 · 实机验收</title><style>
body{margin:0;background:#EFECE6;color:#3A3733;font:16px/1.6 "Microsoft YaHei",sans-serif}header{padding:28px 4vw;background:#3A3733;color:#fff}main{padding:16px 3vw}h1{margin:0 0 8px;font-size:28px}h2{margin:20px 0 12px;font-size:23px}nav{position:sticky;top:0;padding:12px 3vw;background:#E2DED5;z-index:2;display:flex;gap:22px;flex-wrap:wrap}a{color:inherit}section{scroll-margin-top:76px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(520px,1fr));gap:18px}figure{margin:0;background:#f8f5ef;border:1px solid #c6bdae;border-radius:6px;overflow:hidden}figcaption{padding:10px 14px;font-weight:600}img{width:100%;height:auto;display:block}p{max-width:1100px}small{color:#ded9cf}@media(max-width:700px){.grid{grid-template-columns:1fr}}
</style><header><h1>猫狗模型与商品图标 · 实机验收</h1><div>10 种动物外观 · 256 个物品键 · 11 套透明图集</div><small>模型和动作均由 Unity 2022.3.6f1 渲染；概念原图单独标明。动图由原帧缩放合成，点击可看原尺寸图。</small></header><nav>'''+''.join(f'<a href="#{title}">{title}</a>' for title in groups)+'''<a href="商品映射核对.tsv">目录命中记录</a><a href="动物网格记录.tsv">网格记录</a></nav><main><p>验收关注轮廓、比例、毛色、部件接缝和游玩距离的辨识度。既定概念并非精确同机位工程图，不把视觉判断写成未经测量的相似度百分比。商品页每项按 88 / 52 / 34 像素调用游戏实际图标组件。</p>'''+''.join(sections)+'</main></html>',encoding='utf-8')
print(OUT)
