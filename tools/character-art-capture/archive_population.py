"""归档实际引擎帧、逐人立绘对照及检阅页。只排版和编码，不补画模型截图。"""
from pathlib import Path
import argparse,json,shutil,csv,html
from PIL import Image,ImageDraw,ImageFont
p=argparse.ArgumentParser();p.add_argument('source');p.add_argument('destination');a=p.parse_args()
root=Path(__file__).resolve().parents[2];src=Path(a.source);dst=Path(a.destination);dst.mkdir(parents=True,exist_ok=True)
font=lambda size:ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',size)
names={r['Id']:r['姓名'] for r in csv.DictReader((src/'人口清单.tsv').open(encoding='utf-8-sig'),delimiter='\t')}
names.update({'service_officer':'警务人员','service_repair':'维修工','CommunityRoofWorker':'屋顶维修工','CommunityLostChild':'走失儿童','tailor_garden_1':'现场裁缝甲','tailor_garden_2':'现场裁缝乙','legacy:Coach':'足球练习教练','legacy:PhoneClerk':'旧枚举·手机店员','legacy:Classmate':'旧枚举·程序员','legacy:Florist':'旧枚举·花店人物'})
blocks=[];strips=[]
for pagefile in sorted(src.glob('*-名单.txt')):
    page=pagefile.name.split('-')[0];ids=pagefile.read_text(encoding='utf-8-sig').splitlines();n=len(ids)
    for suffix in ('正面','游玩角度','侧面','背面','坐姿'):
        shutil.copy2(src/f'{page}-{suffix}.png',dst/f'{page}-{suffix}.png')
    frames=[]
    for frame in sorted(src.glob(f'{page}-走路-*.png')):
        im=Image.open(frame);im.thumbnail((1200,675));frames.append(im.copy())
    if frames:frames[0].save(dst/f'{page}-走路.gif',save_all=True,append_images=frames[1:],duration=round(1000/24*(24/len(frames))),loop=0,optimize=True)
    front=Image.open(src/f'{page}-正面.png')
    board=Image.new('RGB',(n*360,900),'#efefed');d=ImageDraw.Draw(board)
    for i,ident in enumerate(ids):
        x=i*360
        d.text((x+16,12),names.get(ident,ident),font=font(22),fill='#2a3334')
        d.text((x+16,46),ident,font=font(12),fill='#54605d')
        portrait=root/'Assets/Resources/PortraitsAnime'/f'{ident}.png'
        if ':' not in ident and portrait.exists():
            im=Image.open(portrait).convert('RGB');im.thumbnail((270,300));board.paste(im,(x+(360-im.width)//2,78))
        else:d.text((x+24,180),'临时角色／共享标准',font=font(18),fill='#737772')
        center=1200+(i-(n-1)*.5)*.84*(1350/2.64)
        im=front.crop((int(center-202),230,int(center+202),1150));im.thumbnail((330,470));board.paste(im,(x+(360-im.width)//2,402))
        d.text((x+75,872),'上：原立绘  下：Unity 实机',font=font(14),fill='#515b55')
    board.save(dst/f'{page}-立绘与模型.jpg',quality=94)
    strip=Image.new('RGB',(1200,520),'#efefed');im=front.crop((0,245,2400,1155));im.thumbnail((1200,455));strip.paste(im,(0,0));ds=ImageDraw.Draw(strip)
    for i,ident in enumerate(ids):
        center=600+(i-(n-1)*.5)*.84*(675/2.64);ds.text((int(center-85),460),names.get(ident,ident),font=font(18),fill='#2c3838');ds.text((int(center-100),488),ident.removeprefix('life_leisure_staff_').removeprefix('resident_'),font=font(11),fill='#55625c')
    strips.append(strip)
    blocks.append(f'<section><h2>第 {int(page)} 组：'+html.escape('、'.join(names.get(s,s) for s in ids))+f'</h2><img src="{page}-立绘与模型.jpg"><div class="views">'+''.join(f'<a href="{page}-{kind}.png"><img loading="lazy" src="{page}-{kind}.png">{kind}</a>' for kind in ('正面','游玩角度','侧面','背面','坐姿'))+f'<a href="{page}-走路.gif"><img loading="lazy" src="{page}-走路.gif">行走序列</a></div></section>')
    shutil.copy2(pagefile,dst/pagefile.name)
for start in range(0,len(strips),4):
    rows=strips[start:start+4];im=Image.new('RGB',(1200,520*len(rows)),'white')
    for i,row in enumerate(rows):im.paste(row,(0,520*i))
    im.save(dst/f'总览-{start//4+1:02}.jpg',quality=94)
for name in ('网格记录.tsv','人口清单.tsv','角色列表.txt','复拍来源.json'):
    if(src/name).exists():shutil.copy2(src/name,dst/name)
extras=[]
for file in src.glob('*.png'):
    if file.name[:2].isdigit() or file.name.startswith('工作-'):continue
    shutil.copy2(file,dst/file.name);extras.append(f'<a href="{file.name}"><img loading="lazy" src="{file.name}">{file.stem}</a>')
work=[]
for file in sorted(src.glob('工作-*.png')):
    im=Image.open(file);im.thumbnail((1200,675));work.append(im.copy())
if work:
    work[0].save(dst/'工作动作.gif',save_all=True,append_images=work[1:],duration=42,loop=0,optimize=True)
    extras.append('<a href="工作动作.gif"><img src="工作动作.gif">正式装机工作动作</a>')
if extras:blocks.append('<h2>实际环境与动作衔接</h2><div class="views">'+''.join(extras)+'</div>')
(dst/'index.html').write_text('<!doctype html><html lang="zh"><meta charset="utf-8"><title>全体 NPC 实机检阅</title><style>body{background:#f0f1ed;color:#263735;font:16px sans-serif;max-width:1500px;margin:30px auto;padding:24px}h1{font-size:28px}section{margin:45px 0}img{max-width:100%;height:auto}.views{display:grid;grid-template-columns:repeat(3,1fr);gap:16px}a{color:inherit;text-decoration:none}p{line-height:1.7}</style><h1>'+html.escape(dst.name)+'</h1><p>正式人物入口生成的真实 Unity 三维模型。上方原立绘只核对身份、发型和服装；身体比例沿用已确认的三人母版。点击小图查看原始截图。未补画渲染帧。</p>'+''.join(blocks)+'</html>',encoding='utf-8')
print(dst,len(strips),'组')
