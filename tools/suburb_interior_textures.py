from pathlib import Path
from PIL import Image
import numpy as np,re,uuid
project=Path(__file__).resolve().parents[1]
src=project/'docs/art-direction/室内硬装实机/城郊硬装材质图集.png'
im=Image.open(src).convert('RGB');s=im.width//2
out=project/'Assets/Resources/OldTown/Surfaces'
for i,name in enumerate(['suburbPlaster','suburbEnamel','suburbShopTile','suburbCement']):
    x=(i%2)*s;y=(i//2)*s
    base=im.crop((x,y,x+s,y+s)).resize((600,600),Image.Resampling.LANCZOS)
    if i>=2:
        tile=Image.new('RGB',(1200,1200))
        clean=Image.new('RGB',(600,600))
        # Rotate unbroken pieces amongst the more worn bays. Preserve mortar scale at every join.
        cells=[(0,0),(1,0),(3,1),(1,3),(0,5),(3,4),(5,2),(3,0)]
        for tz in range(6):
            for tx in range(6):
                sx,sz=cells[(tx*5+tz*3)%len(cells)]
                cell=base.crop((sx*100,sz*100,(sx+1)*100,(sz+1)*100))
                clean.paste(cell.rotate(90*((tx*3+tz)%4)),(tx*100,tz*100))
        for j,variant in enumerate([base,clean if i==2 else base.rotate(180),clean.rotate(270) if i==2 else base.rotate(90),base.rotate(270)]):
            tile.paste(variant,(j%2*600,j//2*600))
        base=tile.resize((1024,1024),Image.Resampling.LANCZOS)
    else:base=base.resize((768,768),Image.Resampling.LANCZOS)
    base.save(out/(name+'.png'),optimize=True)
    a=np.asarray(base.convert('L')).astype('float32')/255
    dx=(np.roll(a,-1,axis=1)-np.roll(a,1,axis=1))*(1.5 if i>=2 else .86)
    dy=(np.roll(a,-1,axis=0)-np.roll(a,1,axis=0))*(1.5 if i>=2 else .86)
    n=np.stack([-dx,dy,np.ones_like(a)],axis=-1);n/=np.linalg.norm(n,axis=-1)[:,:,None]
    Image.fromarray(np.uint8((.5*n+.5)*255)).save(out/(name+'Relief.png'),optimize=True)
    for suffix,templ in [('','poolLight'),('Relief','ivoryPlasterRelief')]:
        m=out/(name+suffix+'.png.meta')
        if not m.exists():m.write_text(re.sub('guid: [0-9a-f]{32}','guid: '+uuid.uuid4().hex,(out/(templ+'.png.meta')).read_text(encoding='utf8'),count=1).replace('nPOTScale: 1','nPOTScale: 0'),encoding='utf8')


for name,amount,tint in [('suburbPlaster',.23,[.975,1,1.028]),('suburbCement',.26,[.981,1,1.038]),('suburbShopTile',.13,[.995,1,1.025]),('suburbEnamel',.07,[1,1,1])]:
 p=out/(name+'.png'); a=np.asarray(Image.open(p).convert('RGB')).astype(float)
 grey=(a@np.array([.2126,.7152,.0722]))[:,:,None]
 a=((1-amount)*a+amount*grey)*np.array(tint)
 if name=='suburbEnamel':a=a*.96+9
 Image.fromarray(np.uint8(np.clip(a,0,255))).save(p,optimize=True)
