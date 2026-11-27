"""绘制等高比例对照图。只排版原图，不修饰、重画或非等比拉伸角色。"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root=Path(__file__).resolve().parents[2]
folder=root/'docs/art-direction/concept/characters-3d'
original=Image.open(folder/'user-approved-body-proportions.png').convert('RGB')
concept=Image.open(folder/'three-character-proportion-locked-v3.png').convert('RGB')
canvas=Image.new('RGB',(1440,680),'#e4e4e6');draw=ImageDraw.Draw(canvas)
def font(size):return ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',size)
draw.text((24,10),'身体比例对照 · 原图与新概念等高显示，未拉伸宽高比',font=font(23),fill='#332b27')
original_rects=[(55,57,176,391),(375,68,511,391),(710,76,829,391)]
concept_rects=[(201,99,505,896),(633,109,932,896),(1065,115,1391,896)]
for i,name in enumerate(['主角','面包师','歌手']):
    draw.text((240+i*480,60),name,font=font(22),fill='#332b27',anchor='mm')
    for j,(image,rect) in enumerate(((original,original_rects[i]),(concept,concept_rects[i]))):
        draw.text((i*480+j*240+120,95),'用户原图' if j==0 else '修正概念',font=font(16),fill='#444444',anchor='mm')
        crop=image.crop(rect);scale=510/crop.height
        crop=crop.resize((round(crop.width*scale),510),Image.Resampling.LANCZOS)
        canvas.paste(crop,(i*480+j*240+(240-crop.width)//2,120))
    for ratio in (0,.21,.51,.60,1):
        y=round(120+510*ratio)
        for x in range(i*480+8,(i+1)*480-8,11):
            draw.line((x,y,x+4,y),fill='#8d9fac',width=1)
draw.text((24,646),'横线仅辅助观察头部、腰和胯的位置，不代表服装分割线或自动相似度评分。',font=font(15),fill='#51565d')
canvas.save(folder/'three-character-proportion-comparison.png')
