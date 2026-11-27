"""读取原始图测量等高轮廓。仅辅助找比例偏差，不代表整体美术相似度评分。"""
from pathlib import Path
import argparse,json,sys
sys.stdout.reconfigure(encoding='utf-8')
from PIL import Image
import numpy as np

p=argparse.ArgumentParser();p.add_argument('capture');a=p.parse_args()
root=Path(__file__).resolve().parents[2];folder=Path(a.capture).resolve()
ref=Image.open(root/'docs/art-direction/concept/characters-3d/three-character-proportion-locked-v3.png').convert('RGB')
actual=Image.open(folder/'01-front.png').convert('RGB')
rects=[(201,99,505,896),(633,109,932,896),(1065,115,1391,896)]
def silhouette(rgb):
    rgb=rgb.astype(float)
    return (rgb.max(2)-rgb.min(2)>18)|(rgb.max(2)<180)
def normalize(im):
    yy,xx=np.where(silhouette(np.array(im)))
    im=im.crop((xx.min(),yy.min(),xx.max()+1,yy.max()+1));scale=720/im.height
    im=im.resize((round(im.width*scale),720),Image.Resampling.LANCZOS)
    rgb=np.zeros((720,420,3),np.uint8)+228;offset=(420-im.width)//2;rgb[:,offset:offset+im.width]=im
    return silhouette(rgb)
report={'说明':'统一到720像素高度，只平移与等比缩放。IoU仅衡量正面轮廓重合，不评价材质、衣褶、侧背面或动作。','人物':{}}
panels=[]
for i,name in enumerate(['主角','面包师','歌手']):
    r=normalize(ref.crop(rects[i]));m=normalize(actual.crop((i*640,0,(i+1)*640,1080)))
    overlay=np.full((720,420,3),244,np.uint8)
    overlay[r&m]=[150,150,150];overlay[r&~m]=[220,70,60];overlay[m&~r]=[50,130,220]
    panels.append(overlay)
    widths={}
    for fraction in [.10,.20,.28,.35,.45,.51,.58,.70,.85,.97]:
        row=round(fraction*719);rx=np.flatnonzero(r[row]);mx=np.flatnonzero(m[row])
        widths[str(fraction)]={'概念宽度':int(rx[-1]-rx[0]),'模型宽度':int(mx[-1]-mx[0])}
    report['人物'][name]={'正面轮廓IoU':round(float((r&m).sum()/(r|m).sum()),5),'高度比例对应轮廓宽度':widths}
(folder/'轮廓测量.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
Image.fromarray(np.concatenate(panels,axis=1)).save(folder/'轮廓差异.png')
for name,stats in report['人物'].items():print(name,stats['正面轮廓IoU'])
