"""将 Unity 原始帧组成动作联系表与 GIF。仅等比缩放，不补画或修饰画面。"""
from pathlib import Path
import argparse
from PIL import Image, ImageDraw, ImageFont

parser=argparse.ArgumentParser();parser.add_argument('capture');args=parser.parse_args()
folder=Path(args.capture).resolve()
font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
for prefix,title,indices in [('walk','走路正面',[4,16,28,40]),('walk-side','走路侧面',[4,16,28,40]),('work','装机 / 烹饪 / 交谈',[0,7,15,23])]:
    files=sorted(folder.glob(prefix+'-*.png'))
    files=[f for f in files if f.stem[len(prefix)+1:].isdigit()]
    if not files:continue
    canvas=Image.new('RGB',(1600,960),'#e4e4e6');draw=ImageDraw.Draw(canvas)
    for i,index in enumerate(indices):
        frame=Image.open(files[index]).convert('RGB').resize((800,450),Image.Resampling.LANCZOS)
        x=(i%2)*800;y=(i//2)*480;canvas.paste(frame,(x,y+30))
        draw.text((x+12,y+3),f'{title} · 第 {index+1} 帧',font=font,fill='#302923')
    canvas.save(folder/(prefix+'-contact.png'))
    frames=[Image.open(f).convert('RGB').resize((1200,675),Image.Resampling.LANCZOS) for f in files]
    # 同一调色板避免逐帧量化引起颜色跳变；帧序和实际播放速度保留。
    sample=Image.new('RGB',(1200,675*len(frames)))
    for i,frame in enumerate(frames):sample.paste(frame,(0,675*i))
    palette=sample.quantize(colors=256)
    frames=[frame.quantize(palette=palette,dither=Image.Dither.NONE) for frame in frames]
    frames[0].save(folder/(prefix+'.gif'),save_all=True,append_images=frames[1:],duration=42,loop=0,optimize=False)

# 连续 48 帧同时看，用于发现只在某一跨步相位出现的穿插；仍保留原始帧。
for name,x in [('baker',750),('singer',1280)]:
    for prefix in ['walk','walk-side']:
        sheet=Image.new('RGB',(1280,2112),'#e4e4e6');draw=ImageDraw.Draw(sheet)
        for i in range(48):
            frame=Image.open(folder/f'{prefix}-{i:02}.png').crop((x,140,x+430,1020)).resize((160,328))
            xx=i%8*160;yy=i//8*352;sheet.paste(frame,(xx,yy+24));draw.text((xx+5,yy+5),str(i),fill='black')
        sheet.save(folder/f'fullcycle-{name}-{prefix}.png')
