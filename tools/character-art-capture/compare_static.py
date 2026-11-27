"""把已确定的概念与 Unity 原始静态截图等高并排；不修图、不拉伸、不计算伪相似度。"""
from pathlib import Path
import argparse
from PIL import Image, ImageDraw, ImageFont, ImageChops

parser=argparse.ArgumentParser();parser.add_argument('capture');args=parser.parse_args()
root=Path(__file__).resolve().parents[2];folder=Path(args.capture).resolve()
concept=Image.open(root/'docs/art-direction/concept/characters-3d/three-character-proportion-locked-v3.png').convert('RGB')
actual=Image.open(folder/'01-front.png').convert('RGB')
canvas=Image.new('RGB',(1800,970),'#e4e4e6');draw=ImageDraw.Draw(canvas)
font=lambda n:ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',n)
draw.text((25,15),'静态模型对照 · 概念 / Unity 实机',font=font(28),fill='#302923')
draw.text((25,57),'各角色等高，宽高比保持不变。模型一侧为引擎原始截图，未补画。',font=font(18),fill='#525252')
rects=[(201,99,505,896),(633,109,932,896),(1065,115,1391,896)]
for i,name in enumerate(['主角','面包师','歌手']):
    draw.text((i*600+300,111),name,font=font(23),fill='#302923',anchor='mm')
    section=actual.crop((i*actual.width//3,0,(i+1)*actual.width//3,actual.height))
    diff=ImageChops.difference(section,Image.new('RGB',section.size,actual.getpixel((0,0))))
    diff=diff.convert('L').point(lambda p:255 if p>18 else 0)
    rect=diff.getbbox();render=section.crop(rect)
    for j,crop in enumerate([concept.crop(rects[i]),render]):
        draw.text((i*600+j*300+150,147),'概念' if j==0 else 'Unity 静态',font=font(19),fill='#515151',anchor='mm')
        size=(round(crop.width*720/crop.height),720);crop=crop.resize(size,Image.Resampling.LANCZOS)
        canvas.paste(crop,(i*600+j*300+(300-crop.width)//2,180))
    for ratio in (0,.21,.50,.60,1):
        y=round(180+ratio*720)
        for x in range(i*600+8,(i+1)*600-8,14):draw.line((x,y,x+5,y),fill='#99a6ad')
draw.text((25,928),'辅助线：头顶 / 约下巴 / 约腰线 / 约手尖 / 足底。服装和个体差异需逐项看。',font=font(17),fill='#515151')
canvas.save(folder/'00-concept-vs-static.png')
