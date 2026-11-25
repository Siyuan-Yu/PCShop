from pathlib import Path
import sys, math, random
import numpy as np
from PIL import Image,ImageDraw,ImageFont,ImageFilter
out=Path(__file__).parent
orig=(out/'base.py').read_text('utf8').split('room(); lady(')[0].replace('out = Path(sys.argv[1])','')
orig=orig.replace("uv=np.clip((k@u*127).astype(int),0,127)\n            col=texture(c,kind)[uv[:,1],uv[:,0]]","tex=texture(c,kind); uv=np.clip(k@u,0,1); col=tex[(uv[:,1]*(tex.shape[0]-1)).astype(int),(uv[:,0]*(tex.shape[1]-1)).astype(int)]")
orig=orig.replace("canvas[y,x]=np.clip(col*shading[:,None]+(1-illum[:,None])*np.array([-3,-1,3]),0,255)","""if kind=='person':
            shading=.84+.16*nl+.045*illum
        canvas[y,x]=np.clip(col*shading[:,None]+(1-illum[:,None])*np.array([-5,-1,6]),0,255)""")
orig=orig.replace("return Image.fromarray(canvas)","""# Depth-based contact darkening on the far side of real geometry boundaries.
    valid=depth>-9000
    occ=np.zeros_like(depth); edge=np.zeros_like(depth)
    for dx,dy in ((1,0),(-1,0),(0,1),(0,-1)):
        for jump,weight in ((2,.055),(5,.04),(10,.019)):
            dz=np.roll(depth,(dy*jump,dx*jump),axis=(0,1))-depth
            occ+=weight*((dz>.040*width/11)&(dz<.7*width/11))
            if jump==2: edge+=.038*(dz>.14*width/11)
    canvas=np.clip(canvas*(1-(occ+edge)*valid)[:,:,None],0,255).astype('uint8')
    return Image.fromarray(canvas)""")
exec(orig)
rawtex=texture
atlas=Image.open(out/'surfaces.png').convert('RGB')
tiles={}
for i,name in enumerate(('plaster','wood','enamel','tile','paving','road','grass','cabinet','awning')):
    a,b=i%3,i//3; d=atlas.width/3
    tiles[name]=np.asarray(atlas.crop((round(a*d+3),round(b*d+3),round((a+1)*d-3),round((b+1)*d-3))))
labels=['显卡','处理器','存储','散热','主板','电源']
def texture(base,kind):
    if (base,kind) in bank:return bank[base,kind]
    if kind in tiles:
        arr=tiles[kind].astype(float)
        # Preserve local painted variation while adjusting the base value of selected props.
        if kind in ('wood','enamel'): arr*=np.clip(rgb(base)/arr.mean((0,1)),.7,1.6)*.82+.18
        if kind=='tile': arr=np.clip(arr*.9+26,0,255)
        bank[base,kind]=np.clip(arr,0,255).astype('uint8')
    elif kind.startswith('label') or kind in ('display','street','small'):
        label=labels[int(kind[-1])] if kind.startswith('label') else {'display':'整 机 展 示','street':'配 件 维 修','small':'营业中'}[kind]
        im=Image.new('RGB',(256,64),'#d6c094' if kind.startswith('label') else '#43564e')
        d=ImageDraw.Draw(im);d.rectangle((3,3,252,60),outline='#a09072',width=2)
        d.text((127,31),label,font=imgfont(37),anchor='mm',fill='#493c2e' if kind.startswith('label') else '#e6d8b7')
        bank[base,kind]=np.asarray(im)
    else:return rawtex(base,kind)
    return bank[base,kind]
# Recover cross-sections and bake wrapped diffuse colour from aligned orthographic source paintings.
sheet=np.asarray(Image.open(out/'turn.png').convert('RGB'))
views=[]
for idx in range(3):
    im=sheet[:,idx*sheet.shape[1]//3:(idx+1)*sheet.shape[1]//3]
    c=im.astype(int); mask=~((c[:,:,1]>c[:,:,0]+40)&(c[:,:,1]>c[:,:,2]+30))
    yy,xx=np.where(mask); im=im[yy.min():yy.max()+1,xx.min():xx.max()+1].copy()
    mask=mask[yy.min():yy.max()+1,xx.min():xx.max()+1]
    # Extend border albedo into antialiased outline for texture sampling.
    for y in range(len(im)):
        ids=np.flatnonzero(mask[y]); 
        if len(ids)>2:
            ids=ids[1:-1]; pos=np.arange(im.shape[1]); ii=np.searchsorted(ids,pos).clip(0,len(ids)-1); jj=np.maximum(ii-1,0)
            nearest=np.where(abs(ids[jj]-pos)<abs(ids[ii]-pos),ids[jj],ids[ii])
            im[y]=im[y,nearest]
    views.append((im,mask))
def span(view,t,leg=0):
    im,m=views[view]; row=int(np.clip(t,0,1)*(len(im)-1)); ids=np.where(m[row])[0]
    if leg and view!=1: 
        right=(leg==1)^(view==2); ids=ids[ids>im.shape[1]/2] if right else ids[ids<im.shape[1]/2]
    if not len(ids):return im.shape[1]/2,1
    return (ids[0]+ids[-1])/2,max(.6,(ids[-1]-ids[0])/2)
def person(x,y,angle=0):
    st=len(mesh)
    for lo,hi,leg in [(0,.828,0),(.819,1,-1),(.819,1,1)]:
        H,W=420,384; clr=np.empty((H,W,3),np.uint8)
        theta=(np.arange(W)/(W-1)*2-1)*np.pi
        U=np.sin(theta); V=-np.cos(theta)
        for ri,t in enumerate(np.linspace(lo,hi,H)):
            colors=[]
            for v,coord in enumerate((U,V,-U)):
                im,ms=views[v]; mid,half=span(v,t,leg)
                ix=np.clip((mid+half*coord).round().astype(int),0,im.shape[1]-1)
                colors.append(im[int(t*(len(im)-1)),ix].astype(float))
            # Keep facial landmarks single across the frontal arc; switch onto profile near the ear.
            front=np.clip(((-V)-.08)/.33,0,1);back=np.clip((V-.06)/.4,0,1)
            clr[ri]=np.clip(colors[0]*front[:,None]+colors[2]*back[:,None]+colors[1]*(1-front-back)[:,None],0,255)
        key='lady'+str(leg);bank[key,'person']=clr
        rings=[]; steps=65 if not leg else 18; count=44 if not leg else 20
        for t in np.linspace(lo,hi,steps):
            fx,rx=span(0,t,leg); sy,ry=span(1,t,leg)
            sf=1.72/(len(views[0][0])-1)
            cx=(fx-views[0][0].shape[1]/2)*sf; cy=(sy-views[1][0].shape[1]/2)*sf
            th=np.linspace(-np.pi,np.pi,count+1); sn=np.sin(th); co=np.cos(th)
            X=cx+rx*sf*sn; Y=cy-ry*sf*co
            # Nose and folded hands: a shallow front relief inside the side silhouette.
            if .08<t<.18:
                bump=.018*np.exp(-((t-.12)/.033)**2)
                Y+=bump*np.maximum(0,co)*(1-np.exp(-(sn/.19)**2))
            rings.append(np.column_stack((X,Y,np.full(len(X),1.72*(1-t)+.008))))
        for j,(a,b) in enumerate(zip(rings,rings[1:])):
            for i in range(count):
                uv=[(i/count,j/(steps-1)),((i+1)/count,j/(steps-1)),((i+1)/count,(j+1)/(steps-1)),(i/count,(j+1)/(steps-1))]
                face([a[i],a[i+1],b[i+1],b[i]],(key,'person'),(a[:,0].mean(),a[:,1].mean(),(a[0,2]+b[0,2])/2),uv)
        for row,z in [(rings[0],0),(rings[-1],1)]:
            for i in range(count):face([row[i],row[i+1],row.mean(0)],(key,'person'),row.mean(0)+[0,0,(-.1 if z==0 else .1)],[(i/count,z),((i+1)/count,z),(.5,z)])
    rotate_from(st,angle,x,y)
    return len(mesh)-st
oldbox=box
def box(x,y,z,w,d,h,c,kind='',front=None):
    if kind=='tile':
        # Individual ceramic tiles retain unique tonal variation at their true scale.
        idx=(round((x+3.6)/.8)+round((y+2.4)/.8)*3)%16
        key='ceramic'+str(idx)
        if (c,key) not in bank:
            tile=tiles['tile']; dim=tile.shape[0]//4; a,b=idx%4,idx//4
            bank[c,key]=np.clip(tile[b*dim:(b+1)*dim,a*dim:(a+1)*dim]*.96+17,0,255).astype('uint8')
        kind=key
    if c in ('576458','768173','747c6b','78816d','66695b'):kind='enamel'
    if c=='a16b50':kind='awning'
    if c=='948064':front='cabinet'
    if c=='6c6657':return
    if kind=='paving' and w>8:kind=''
    oldbox(x,y,z,w,d,h,c,kind,front)
def paint_quad(pts,kind,c='8d8067'):
    p=np.array(pts);face(pts,(c,kind),p.mean(0)+[0,.1,-.1])
def foliage(x,y,z,sx,sy,sz,c,seed,n=10):
    rnd=random.Random(seed); levels=[(-.8,.42),(-.45,.84),(.02,1),(.49,.83),(.86,.51),(1,.16)]
    patches=[]
    for k,(dz,rd) in enumerate(levels):
        ring=[]
        for j in range(n):
            a=j*2*math.pi/n+.19*(k%2); rad=rd*rnd.uniform(.9,1.09)
            ring.append([x+sx*rad*math.cos(a),y+sy*rad*math.sin(a),z+sz*(dz+rnd.uniform(-.052,.052))])
        patches.append(ring)
    for k,(a,b) in enumerate(zip(patches,patches[1:])):
        for j in range(n):
            vv=int(5*k+rnd.randrange(-11,9)); base=np.clip(rgb(c)+[vv,vv*.87,vv*.46],0,255).astype(int)
            h=bytes(base.astype('uint8')).hex();face([a[j],a[(j+1)%n],b[(j+1)%n],b[j]],(h,''),(x,y,z))
def environment():
    room()
    for i,(x,y,ang) in enumerate([(-2.96+j*1.56,2.48,0) for j in range(4)]+[(3.7,.88-1.65*j,-90) for j in range(2)]):
        st=len(mesh)
        for lev in (.13,.62,1.12):
            box(0,-.273,lev,1.27,.025,.051,'a18b6c','wood')
            for col in (-.43,.14):
                box(col,-.287,lev+.004,.11,.006,.024,'e4d3ae')
        box(0,-.279,1.71,.47,.018,.111,'ac966e','',f'label{i}')
        for j in range(1+(i%3)):
            box(-.39+j*.29,.07,1.18,.27,.22,.30+(.12 if i%2 else 0),['87948b','b39368','84695e'][j%3],'','gpu' if i%2 else 'board')
        if i in (1,3):
            box(.28,.06,1.8,.48,.29,.235,'b8996f','wood')
            box(.28,.06,2.033,.07,.295,.009,'d0b58b')
        rotate_from(st,ang,x,y)
    # Cabinet frontage, countertop edge, counter ledger and keyboard.
    for x in (-.63,.55):
        box(x,-.495,.13,1.12,.018,.52,'795d41','','cabinet')
    box(-.05,-.553,.635,1.0,.021,.128,'53584b','','display')
    box(-3.2,-1.01,.92,.34,.13,.017,'504b40');box(-3.36,-1.68,.926,.24,.30,.025,'bfa67b','')
    for i in range(5):box(-3.35,-1.77+.032*i,.953,.153,.007,.002,'8a8170')
    # Expand the streetscape behind the cutaway and lay sidewalk in short quads.
    box(-1,2,-.28,24,19,.075,'7a8254','grass')
    for i in range(-6,5):
        paint_quad([(i*2,-4.52,-.015),(i*2+2,-4.52,-.015),(i*2+2,-2.90,-.015),(i*2,-2.90,-.015)],'paving')
    for i in range(-4,5):
        paint_quad([(i*3,-7.45,-.17),(i*3+3,-7.45,-.17),(i*3+3,-4.66,-.17),(i*3,-4.66,-.17)],'road')
    for i in range(-6,8,2):box(i,-6.22,-.167,.80,.075,.003,'bd9b59')
    for i,(x,y) in enumerate([(-5.2,4.3),(-2.1,5.6),(1.2,6.2),(5.9,5.0),(-7.2,.7)]):
        loft(x,y,[(-.21,.17,.16),(1.7,.12,.10),(2.8,.065,.065,.15,-.13)],'665138',8)
        for j,(ox,oy,oz,r) in enumerate([(-.68,.05,2.30,.92),(.62,.09,2.46,1.03),(.02,-.47,2.82,1.05),(-.24,.30,3.21,.96),(.17,.0,3.66,.68)]):
            foliage(x+ox,y+oy,oz,r,r*.9,r*.75,'616c3c',100*i+j)
    for j,x in enumerate((.51,2.15)):
        for i,(a,b,h,s) in enumerate([(-.1,.03,.45,.16),(.1,.04,.49,.16),(.0,-.09,.57,.17),(.01,.035,.67,.13)]):
            foliage(x+a,-2.52+b,h,s,s,.13,'6d7845',90+j*9+i)
        loft(x,-2.52,[(.24,.217,.217),(.285,.219,.219),(.292,.20,.20)],'a87853',12)
    box(6,-2.86,2.03,2.26,.07,.27,'536653','','street')
    for x in (-3.34,3.45):
        box(x,-4.37,-.045,.095,.094,2.64,'584e40');box(x,-4.54,2.55,.095,.42,.085,'534a3c');box(x,-4.77,2.49,.19,.39,.085,'6c6450')
        box(x,-4.77,2.475,.158,.33,.016,'eacb91')
environment()
tri=person(1.21,-1.73,-12)
world=render(1800,1012,[.05,-.30,.92],[-8,-12,9.1],14.6,'bcbaa1',24)
world.save(out/'01-街边电脑店.png')
mesh.clear();person(0,0)
board=Image.new('RGB',(1480,795),'#e5dfd2')
d=ImageDraw.Draw(board);d.text((48,29),'房东',font=imgfont(34),fill='#423d35')
d.text((134,46),'角色静态网格   /   三向贴图',font=imgfont(17),fill='#726858')
d.line((48,96,1432,96),fill='#c2b8a6')
for i,cam in enumerate([(0,-8,.65),(-4.8,-7,1.6),(3.8,7,1.3)]):
    fr=render(402,685,[0,0,.868],cam,1.12,'e5dfd2',3)
    board.paste(fr.resize((310,528),Image.Resampling.LANCZOS),(34+i*306,135))
    d.text((157+i*306,692),['正面','斜前方','背面'][i],font=imgfont(17),fill='#64594b')
proj=projection(basis([-8,-12,9.1]),np.array([.05,-.30,.92]),14.6,1800,1012)
xx,yy=proj(np.array([1.21,-1.73,.86]))[:2].astype(int)
board.paste(world.crop((xx-214,yy-175,xx+214,yy+175)),(996,223))
d.text((1000,603),'店内镜头',font=imgfont(20),fill='#534a3e')
d.text((1000,641),'与左侧为同一网格，按场景原图像素裁切。',font=imgfont(15),fill='#716858')
board.save(out/'02-房东三视角.png')
print('triangles',tri)

