from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import numpy as np
from math import pi,sin,cos
import random,struct,collections
work=Path(__file__).parent; root=work.parent/'Assets/Art/OldTownStreetStudy'
rng=random.Random(17); maps={}; batches=collections.defaultdict(list); group='Building'
def swatch(key):
    if key not in maps: maps[key]=Image.new('RGBA',(8,8),'#'+key)
def sheet(filename,keys):
    im=Image.open(work/filename).convert('RGB');dx,dy=im.width/3,im.height/3
    for i,k in enumerate(keys):
        x=(i%3)*dx;y=(i//3)*dy
        cell=im.crop((round(x)+4,round(y)+4,round(x+dx)-4,round(y+dy)-4))
        # Small global desaturation keeps newly painted surfaces with the indoor set.
        a=np.array(cell,dtype=float);gray=a.mean(2)[:,:,None];a=.90*a+.10*gray
        maps[k]=Image.fromarray(np.uint8(a)).convert('RGBA')
sheet('wall.png','plaster wall2 roof curtain glass blind AC canvas door'.split())
sheet('props.png','paving asphalt grass brick goods shopdoor redtin wood steel'.split())
for key,factor in [('plaster',(1.00,1.04,1.08)),('grass',(.80,.87,.86)),('paving',(1.03,1.03,1.04))]:
    maps[key]=Image.fromarray(np.uint8(np.clip(np.asarray(maps[key])[:,:,:3]*factor,0,255))).convert('RGBA')
def label(k,title,sub,back,fg,font=62):
    im=Image.new('RGBA',(640,150),back);d=ImageDraw.Draw(im)
    d.rectangle((6,6,633,143),outline='#93896e',width=2)
    d.text((320,51),title,font=ImageFont.truetype('C:/Windows/Fonts/msyhbd.ttc',font),fill=fg,anchor='mm')
    d.text((320,113),sub,font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',21),fill=fg,anchor='mm')
    for x in (13,627):
        for y in (14,136):d.ellipse((x-2,y-2,x+2,y+2),fill='#574f3f')
    maps[k]=im
label('shopSign','街 坊 电 脑','装 机   ·   配 件   ·   升 级','#4d655a','#ece0ba')
label('fixSign','配 件 维 修','旧 件 检 修   /   电 脑 维 护','#854f3c','#ebd8b1')
label('lane','新 安 里','18 号','#405d65','#e4dbbb',63)
label('small','营 业 中','欢迎光临','#8b6844','#f1dfbb',54)
label('board','电 脑 装 机','配件到货   可进店选购','#455248','#e2d2a9',53)
def painted_sign(k,w,h,bg,ink,lines):
    im=Image.new('RGB',(w,h),bg);d=ImageDraw.Draw(im)
    d.rectangle((4,4,w-5,h-5),outline='#a69878',width=2)
    for x,y,sz,words in lines:d.text((x,y),words,font=ImageFont.truetype('C:/Windows/Fonts/msyhbd.ttc',sz),anchor='mm',fill=ink)
    a=np.array(im,float);yy,xx=np.mgrid[:h,:w]
    patina=1+.016*np.sin(xx*.074+yy*.12)+.010*np.sin(xx*.022-yy*.16)-.07*(yy/h)**3
    maps[k]=Image.fromarray(np.uint8(np.clip(a*patina[:,:,None],0,255))).convert('RGBA')
painted_sign('shopSign',1280,106,'#50675c','#e9daba',[(530,50,67,'街 坊 电 脑'),(974,57,23,'装机 · 配件 · 升级')])
painted_sign('fixSign',780,103,'#7f4d39','#e8d6af',[(386,51,57,'配 件 维 修')])
painted_sign('board',184,220,'#4a574c','#e1d1a5',[(92,72,32,'装机'),(92,116,21,'配件到货'),(92,165,14,'欢迎进店')])
painted_sign('small',180,104,'#956b49','#e8d6a5',[(90,49,33,'营业中')])
leafim=Image.open(work/'leaves.png').convert('RGB')
for i in range(4):
    a=np.asarray(leafim.crop((i%2*leafim.width//2,i//2*leafim.height//2,(i%2+1)*leafim.width//2,(i//2+1)*leafim.height//2))).astype(float)
    alpha=np.clip((np.maximum(a[:,:,1]-a[:,:,0]*.70,a[:,:,1]-a[:,:,2]*.80)+8)/19,0,1)
    # Magenta separation depends on both red and blue; leaf yellow can have low blue.
    alpha=np.clip((a[:,:,1]-np.minimum(a[:,:,0],a[:,:,2])*.73+4)/22,0,1)
    a=a*.84+a.mean(2)[:,:,None]*.16;a*=np.array([.88,.92,.92])
    a[alpha<.5]=[82,89,51]
    out=Image.fromarray(np.uint8(np.dstack([np.clip(a,0,255),alpha*255])));bb=out.getchannel('A').point(lambda v:255 if v>190 else 0).getbbox()
    maps['leaf'+str(i)]=out.crop(bb)
# Coordinates X along the road, Y away from the road, Z up.
def face(v,m,n=None,uv=None,split=True):
    swatch(m);p=np.asarray(v,float)
    if n is None:n=np.cross(p[1]-p[0],p[2]-p[0])
    n=np.array(n,float);n/=max(np.linalg.norm(n),1e-8)
    u=np.array(uv if uv is not None else ([(0,1),(1,1),(1,0),(0,0)] if len(v)==4 else [(0,1),(1,1),(.5,0)]))
    if split and len(p)==4 and m in ('plaster','wall2','paving','brick','grass','roof','asphalt','goods','shopdoor','door','blind'):
        step=.44 if m in ('plaster','wall2','paving','goods') else .82
        nx=max(1,min(16,int(np.ceil(np.linalg.norm(p[1]-p[0])/step))));ny=max(1,min(16,int(np.ceil(np.linalg.norm(p[3]-p[0])/step))))
        if nx*ny>1:
            for iy in range(ny):
                for ix in range(nx):
                    ab=np.array([(ix/nx,iy/ny),((ix+1)/nx,iy/ny),((ix+1)/nx,(iy+1)/ny),(ix/nx,(iy+1)/ny)])
                    a,b=ab.T;weights=np.stack([(1-a)*(1-b),a*(1-b),a*b,(1-a)*b],axis=1)
                    face(weights@p,m,n,weights@u,False)
            return
    for j in range(1,len(p)-1):
        ii=[0,j,j+1];batches[group].append((p[ii],n,m,u[ii]))
def box(x,y,z,w,d,h,m,front=None,top=None):
    x0,x1=x-w/2,x+w/2;y0,y1=y-d/2,y+d/2;t=z+h
    v=[(x0,y0,z),(x1,y0,z),(x1,y1,z),(x0,y1,z),(x0,y0,t),(x1,y0,t),(x1,y1,t),(x0,y1,t)]
    for k,(order,n) in enumerate([((0,1,5,4),(0,-1,0)),((1,2,6,5),(1,0,0)),((2,3,7,6),(0,1,0)),((3,0,4,7),(-1,0,0)),((4,5,6,7),(0,0,1)),((3,2,1,0),(0,0,-1))]):
        face([v[i] for i in order],(front if k==0 else top if k==4 else None) or m,n)
def rod(a,b,r,m,r2=None,n=8):
    a=np.array(a,float);b=np.array(b,float);v=b-a;v/=np.linalg.norm(v);u=np.cross(v,[0,0,1] if abs(v[2])<.95 else [0,1,0]);u/=np.linalg.norm(u);w=np.cross(v,u)
    rad=np.array([cos(i*2*pi/n)*u+sin(i*2*pi/n)*w for i in range(n)])
    ra=a+r*rad;rb=b+(r if r2 is None else r2)*rad
    for i in range(n):
        j=(i+1)%n;face([ra[i],ra[j],rb[j],rb[i]],m,rad[i]+rad[j],[(i/n,1),(j/n if j else 1,1),(j/n if j else 1,0),(i/n,0)])
        face([rb[i],rb[j],b],m,v);face([ra[i],ra[j],a],m,-v)
def rotate_from(start,angle,dx,dy):
    aa=angle*pi/180;R=np.array([[cos(aa),-sin(aa),0],[sin(aa),cos(aa),0],[0,0,1]])
    ls=batches[group]
    for j in range(start,len(ls)):
        p,n,m,uv=ls[j];ls[j]=(p@R.T+[dx,dy,0],n@R.T,m,uv)
def rect(x,y,z,w,h,m):
    face([(x-w/2,y,z),(x+w/2,y,z),(x+w/2,y,z+h),(x-w/2,y,z+h)],m,(0,-1,0))
def horizon(x,y,z,w,d,m,uv=None):
    face([(x,y,z),(x+w,y,z),(x+w,y+d,z),(x,y+d,z)],m,(0,0,1),uv)
def beam(a,b,r,m):rod(a,b,r,m,n=6)
def cable(a,b,sag,r=.016,mat='554c41',k=10):
    a=np.array(a);b=np.array(b)
    def pt(t):return a+(b-a)*t-[0,0,sag*4*t*(1-t)]
    for i in range(k):rod(pt(i/k),pt((i+1)/k),r,mat,n=5)
# Main housing: bays contain real recessed openings, with independent glazing and rails.
W=8.7;D=5.8;H=10.5;front=0
def wall_face(length,side=False):
    global group
    bays=2 if side else 3;bw=length/bays
    for level in range(1,4):
        z=3.05+(level-1)*2.48
        for j in range(bays):
            x=-length/2+bw*(j+.5);ww=1.50 if side else 1.68;wh=1.40;low=.57
            m='wall2' if side else 'plaster'
            def section(l,r,bot,top):
                if r<=l:return
                uv=[((l-x+bw/2)/bw,1-bot/2.48),((r-x+bw/2)/bw,1-bot/2.48),((r-x+bw/2)/bw,1-top/2.48),((l-x+bw/2)/bw,1-top/2.48)]
                uv=[((1-u if (j+level)%2 else u)*.82+.065, .09+v*(.68+((j*2+level)%4)*.047)) for u,v in uv]
                face([(l,0,z+bot),(r,0,z+bot),(r,0,z+top),(l,0,z+top)],m,(0,-1,0),uv)
            section(x-bw/2,x-ww/2,0,2.48);section(x+ww/2,x+bw/2,0,2.48)
            section(x-ww/2,x+ww/2,0,low);section(x-ww/2,x+ww/2,low+wh,2.48)
            bot=z+low;mid=bot+wh
            box(x,.145,bot,ww,.07,wh,'645e51',front=('glass' if (j+level)%3 else 'curtain'))
            for s in (-1,1):box(x+s*(ww/2-.033),.038,bot,.068,.23,wh,'ac9e82')
            for zz in (bot,mid-.055):box(x,.037,zz,ww,.23,.057,'c1ad8c')
            box(x,.092,bot,.047,.075,wh,'ab9e84');box(x,.092,bot+wh*.72,ww,.077,.038,'ac9e86')
            box(x,-.085,bot-.083,ww+.22,.32,.096,'a28d6f');box(x,-.032,mid+.008,ww+.14,.19,.071,'bca686')
            if (level+j+(1 if side else 0))%3!=1:
                # Protruding iron window guards, warm charcoal with lit edges.
                for s in (-1,1):
                    for zz in (bot+.12,mid-.13):beam((x+s*(ww/2-.08),.015,zz),(x+s*(ww/2-.08),-.205,zz),.014,'65594b')
                for zz in (bot+.08,bot+.58,mid-.10):box(x,-.204,zz,ww-.08,.028,.025,'63594a')
                for k in range(7):box(x-ww*.45+k*ww*.15,-.205,bot+.08,.019,.022,wh-.16,'675d50')
            elif not side and level!=3:
                # Recessed drying rack projects far enough to cast a real shadow.
                for s in (-1,1):beam((x+s*.74,0,bot+.47),(x+s*.74,-.37,bot+.69),.016,'80715c')
                rod((x-.84,-.37,bot+.69),(x+.84,-.37,bot+.69),.016,'696151')
                for k,col in enumerate(('a7aca0','b39583')):
                    u=x-.51+k*.55
                    for segment in range(6):
                        t=segment/6;b=(segment+1)/6
                        def edge(t,high):
                            return (u+t*.37,-.39-.036*sin(3*pi*t)-(.013 if not high else 0),bot+(.68 if high else .08+.040*sin(pi*t*2+.3)))
                        face([edge(t,False),edge(b,False),edge(b,True),edge(t,True)],col)
                    for offset in (.04,.30):box(u+offset,-.391,bot+.64,.023,.028,.071,'9b8760')
            if (j+level)%3==1:
                cx=x+.52;cz=bot-.48
                box(cx,-.18,cz,.71,.39,.40,'ad9e80',front='AC')
                for dx in (-.23,.23):beam((cx+dx,0,cz-.16),(cx+dx,-.32,cz-.01),.018,'756c5b')
                cable((cx-.3,-.09,cz+.10),(cx-.65,-.022,cz+.21),.13,.018,'aba38b',5)
    # Only narrow floor divisions; no outline shader on plaster.
    for z in (3.0,5.53,8.01):
        box(0,-.006,z,length+.012,.11,.073,'ac9478')
group='UpperFront';wall_face(W)
# One shallow residential balcony gives the facade an inhabited silhouette.
group='Balcony';cx=2.9;zb=6.03
box(cx,-.36,zb,2.06,.78,.115,'aa987d')
for xx in (cx-.76,cx+.76):
    beam((xx,-.04,zb-.36),(xx,-.64,zb-.012),.031,'827969')
for zz in (zb+.21,zb+.87):
    rod((cx-.99,-.72,zz),(cx+.99,-.72,zz),.019,'675f51')
    for s in (-1,1):rod((cx+s*.99,-.72,zz),(cx+s*.99,-.045,zz),.018,'675f51')
for i in range(12):rod((cx-.98+i*.178,-.72,zb+.13),(cx-.98+i*.178,-.72,zb+.87),.013,'756d5e',n=6)
# A draped blanket turns over the front rail and shows both faces from the sidewalk.
for i in range(10):
    a=i/10;b=(i+1)/10
    def fabric(t,v):
        return (cx-.81+t*.96, -.757-.031*cos(t*6*pi)-v*.012,zb+.89-v*(.49+.031*sin(t*pi)))
    for j,c in [(0,'999f91'),(1,'929a8e'),(2,'a3ab9d'),(3,'86958b')]:
        a0,a1=[(0,.73),(.73,.78),(.78,.91),(.91,1)][j]
        face([fabric(a,a1),fabric(b,a1),fabric(b,a0),fabric(a,a0)],c)
box(cx-.32,-.712,zb+.876,.97,.12,.033,'9ca497')
group='GableWest';wall_face(D,True);rotate_from(0,-90,-W/2,D/2)
group='GableEast';wall_face(D,True);rotate_from(0,90,W/2,D/2)
group='BackWall';wall_face(W);rotate_from(0,180,0,D)
group='OldRoof'
box(0,D/2,H-.035,W+.24,D+.24,.14,'8e7964')
for i in range(3):
    for j in range(2):horizon(-W/2+i*W/3,j*D/2,H+.115,W/3,D/2,'roof')
for x in (-W/2,W/2):box(x,D/2,H+.09,.13,D+.1,.17,'ad9477');box(x,D/2,H+.25,.18,D+.20,.044,'b49e80')
for y in (0,D):box(0,y,H+.09,W+.06,.13,.17,'a58b70');box(0,y,H+.25,W+.22,.18,.044,'b49e80')
for x,y,h in [(1.98,3.9,1.30),(3.10,3.92,.92)]:
    box(x,y,H+.112,1.02,1.02,.12,'92806b');rod((x,y,H+.23),(x,y,H+.23+h),.43,'steel',n=28)
    for dz in (.09,h*.48,h-.06):rod((x,y,H+.23+dz),(x,y,H+.253+dz),.442,'79817b',n=28)
    rod((x,y,H+.23+h),(x,y,H+.32+h),.43,'858b81',.22,24)
    rod((x,y,H+.32+h),(x,y,H+.348+h),.112,'706f62',n=16)
# Ground-floor building and storefront inserts.
group='Shopfront'
box(0,D/2,0,W,D,3.05,'wall2')
box(0,-.02,.023,W+.03,.09,.35,'brick')
def awning(x,w,z,depth,tex):
    count=4 if tex=='canvas' else 3
    for j in range(count):
        x0=x-w/2+w*j/count;pw=w/count
        for k in range(4):
            u=k/4;v=(k+1)/4
            def rim(t):return z-.32-(.032*sin(pi*t) if tex=='canvas' else .0)
            face([(x0+u*pw,-depth,rim(u)),(x0+v*pw,-depth,rim(v)),(x0+v*pw,-.015,z),(x0+u*pw,-.015,z)],tex,(0,-.3,1),[(u,.92),(v,.92),(v,0),(u,0)])
            lip=.102 if tex=='canvas' else .07
            face([(x0+u*pw,-depth,rim(u)-lip),(x0+v*pw,-depth,rim(v)-lip),(x0+v*pw,-depth,rim(v)),(x0+u*pw,-depth,rim(u))],tex,(0,-1,0),[(u,1),(v,1),(v,.91),(u,.91)])
    for side in (-1,1):
        rod((x+side*(w/2-.10),-.03,z-.74),(x+side*(w/2-.10),-depth+.09,z-.33),.021,'6e6855')
        face([(x+side*w/2,-depth,z-.32),(x+side*w/2,-.012,z),(x+side*w/2,-.012,z-.32)],'806b55',(side,0,0))
def shopwindow(x,z,w,h,tex):
    box(x,-.083,z,w+.14,.20,h+.12,'6c6857',front=tex)
    for s in (-1,1):box(x+s*w/2,-.225,z,.074,.15,h+.08,'8a8069')
    box(x,-.225,z-.019,w+.17,.17,.075,'95836a');box(x,-.204,z+h,w+.15,.14,.055,'857762')
shopwindow(-2.90,.44,2.40,1.66,'goods');shopwindow(-.02,.14,1.51,2.03,'shopdoor');shopwindow(1.69,.49,1.52,1.64,'goods')
box(-.02,-.208,.17,.030,.038,1.98,'698173')
for xx in (-.13,.09):box(xx,-.24,1.03,.026,.045,.25,'ab9463')
for xx in (-2.90,1.69):box(xx,-.203,.49,.038,.062,1.61,'887d64')
box(-.72,-.134,2.46,6.82,.18,.51,'493f32',front='shopSign')
awning(-.75,6.9,2.47,1.08,'canvas')
box(-.03,-.37,.015,1.84,.72,.12,'ac9b82',top='paving')
box(3.38,-.045,.10,1.30,.15,2.20,'594c3d',front='door')
box(3.38,-.22,.08,1.55,.42,.14,'a9987d');box(3.38,-.18,2.35,1.53,.44,.12,'a18b70')
box(2.47,-.10,1.87,.41,.045,.30,'3c5761',front='lane')
rect(.56,-.202,1.10,.31,.19,'small')
# Lower gable details where the bike rests. Upper returns stay comparatively quiet.
start=len(batches[group])
shopwindow(-1.34,.97,1.25,.78,'curtain')
for k in range(6):box(-1.88+k*.217,-.215,.98,.017,.025,.78,'605a4b')
box(-1.34,-.217,1.28,1.23,.028,.02,'655e51')
box(1.45,-.07,.12,.95,.16,1.88,'847565',front='door')
box(1.45,-.19,.095,1.14,.39,.12,'a3947a')
for j in range(2):box(.02+j*.25,-.105,1.44,.21,.12,.33,'838775')
rod((.15,-.09,1.44),(.15,-.09,.37),.017,'9c9580')
rotate_from(start,-90,-W/2,D/2)
# Residential service pipe with elbows and contrasting warm fasteners.
for x in (-4.17,4.12):
    rod((x,-.115,.19),(x,-.115,H+.10),.043,'9c9981',n=10)
    for zz in (1.0,3.63,6.25,8.86):rod((x,-.115,zz),(x,-.115,zz+.045),.052,'777767',n=10)
group='RepairShop';x=6.58;y=.65;w=4.38;d=4.66
box(x,y+d/2,0,w,d,3.29,'plaster');box(x,y-.023,.012,w,.09,.40,'brick')
shopstart=len(batches[group])
shopwindow(0,.18,2.23,1.84,'blind');shopwindow(1.57,.10,.66,1.94,'door')
box(.29,-.134,2.54,3.42,.15,.45,'775443',front='fixSign')
awning(.24,3.75,2.48,1.02,'redtin')
rotate_from(shopstart,0,x-.37,y)
box(x,y+d/2,3.26,w+.16,d+.15,.16,'a18c70',top='roof')
for v in (y,y+d):box(x,v,3.40,w+.12,.13,.19,'b59c80')
for v in (x-w/2,x+w/2):box(v,y+d/2,3.40,.13,d,.19,'b59c80')
box(x+.82,y+3.1,3.43,.90,.75,.35,'989886',front='AC')
for i in range(2):horizon(4.35+i*2.22,-.03,.103,2.22,.81,'paving')
box(6.27,.40,.100,2.45,.54,.083,'a6957d',top='paving')
# Ground surfaces. The coherent meter scale is shared by both camera shots.
group='LaneAndPavement'
for i in range(-9,10):
    for j in range(-4,7):
        xx=i*3.06; yy=j*3.06
        horizon(xx,yy,-.055,3.061,3.061,'grass',[(0,0),(0,1),(1,1),(1,0)] if (i+j)%2 else None)
# Continue the matte behind the roof at the steep review angle without adding buildings.
for i in range(-9,10):
    for j in range(7,16):
        horizon(i*3.06,j*3.06,-.055,3.061,3.061,'grass',[(1,0),(0,0),(0,1),(1,1)] if (i+j)%2 else None)
# Distant ground only closes the camera background; no additional street blocks.
horizon(-100,-100,-.075,200,200,'727c55')
for i in range(-10,11):
    x=i*2.6
    horizon(x,-9.5,-.002,2.601,6.60,'asphalt',[(0,0),(0,1),(1,1),(1,0)] if i%2 else None)
    for j in range(2):horizon(x,-2.88+j*1.46,.102,2.601,1.461,'paving')
    horizon(x,-11.08,.10,2.601,1.57,'paving')
    for j in range(3):
        for y in (-2.93,-9.50):box(x+j*.867+.428,y,-.001,.851,.13,.114,'a99983')
    if i%2==0:box(x+.4,-6.21,.005,1.18,.085,.005,'bea062')
    for y in (-3.14,-9.27):box(x+1.30,y,.004,2.599,.037,.004,'c1b394')
# Short old alley turning beside the residential gable.
for x in (-3.83,6.13):
    box(x,-3.02,.109,.64,.18,.012,'6d6c5d')
    for i in range(8):box(x-.263+i*.075,-3.02,.121,.030,.12,.005,'424a43')
rod((3.75,-4.28,0),(3.75,-4.28,.013),.306,'6c6b60',n=32)
rod((3.75,-4.28,.013),(3.75,-4.28,.018),.271,'585b53',n=28)
for a in range(-2,3):
    box(3.75,-4.28+a*.069,.018,.42-.045*abs(a),.017,.004,'77786b')
for i in range(5):horizon(-5.90,i*1.39,.021,1.56,1.391,'paving')
for x,y in [(-12.0,-1.16),(-4.8,-11.16),(10.0,-1.56)]:
    horizon(x-.5,y-.48,.111,1.0,.96,'62563e')
    for s in (-1,1):
        box(x+s*.52,y,.103,.095,1.11,.075,'a7997d')
        box(x,y+s*.52,.103,.98,.092,.075,'a7997d')
group='Courtyard'
for j in range(5):
    x=-13.5+j*2.14;box(x,6.62,-.04,2.14,.21,1.02,'wall2')
    box(x,6.62,.96,2.18,.27,.073,'998770')
    if j%2==0:box(x-1.02,6.62,-.04,.24,.29,1.14,'brick')
group='UtilityLines'
for x in (-7.70,11.92):
    yy=-2.24;rod((x,yy,.105),(x+.04,yy,6.68),.133,'wood',.080,12)
    box(x,yy,.11,.39,.38,.16,'a7977f')
    for z in (5.95,6.49):
        box(x,yy,z,.14,1.06,.085,'7b6c54')
        for off in (-.42,0,.42):
            rod((x,yy+off,z+.07),(x,yy+off,z+.19),.041,'c5baa0',n=8)
            rod((x,yy+off,z+.15),(x,yy+off,z+.177),.058,'c4bea6',n=8)
    box(x-.045,yy-.15,2.13,.23,.17,.39,'899083')
for dy,dz in [(-.4,0),(0,.02),(.4,-.02)]:
    cable((-7.7,-2.24+dy,6.68+dz),(11.92,-2.24+dy,6.68+dz),.67,.014,'514c42',18)
cable((-7.7,-2.24,6.10),(-4.14,-.13,5.72),.45,.013,'5b5142',12)
# Storefront details and a step-through bicycle leaned against the gable.
group='Doorstep'
def pot(x,y,sz):
    rod((x,y,.11),(x,y,.11+.41*sz),.16*sz,'a1704e',.23*sz,14)
    rod((x,y,.11+.37*sz),(x,y,.11+.43*sz),.235*sz,'ad8059',n=14)
    rod((x,y,.11+.427*sz),(x,y,.11+.435*sz),.202*sz,'574d38',n=14)
for x,y,s in [(-1.13,-.49,.96),(2.62,-.38,.84),(8.32,.13,.72)]:pot(x,y,s)
# A-frame chalkboard with visible side struts.
box(1.82,-.83,.27,.70,.071,.79,'997f59',front='board')
for x in (1.45,2.19):
    rod((x,-.85,.105),(x,-.82,1.09),.030,'947955')
    rod((x,-.47,.105),(x,-.82,1.06),.026,'806b4d')
box(1.82,-.84,1.036,.77,.09,.055,'a18a66')
group='Bicycle'
def wheel(x,y,z):
    n=28
    for i in range(n):
        a,b=i*2*pi/n,(i+1)*2*pi/n
        p=(x+.325*cos(a),y,z+.325*sin(a));q=(x+.325*cos(b),y,z+.325*sin(b))
        beam(p,q,.027,'433f35');beam((x+.299*cos(a),y,z+.299*sin(a)),(x+.299*cos(b),y,z+.299*sin(b)),.011,'999786')
        if i%2==0:beam((x,y,z),(x+.296*cos(a),y,z+.296*sin(a)),.005,'928f7d')
for x in (-.53,.54):wheel(x,0,.45)
for a,b in [((-.53,0,.45),(-.21,0,.89)),((-.53,0,.45),(-.03,0,.45)),((-.21,0,.89),(-.03,0,.45)),((-.03,0,.45),(.39,0,.91)),((-.20,0,.85),(.36,0,.78)),((.54,0,.45),(.33,0,1.06))]:beam(a,b,.027,'4d685b')
box(-.22,0,.90,.25,.14,.055,'584a38');beam((.33,-.16,1.08),(.33,.17,1.08),.019,'8d8c7c')
box(-.51,0,.80,.50,.115,.026,'767463')
for i in range(6):beam((.45+i*.05,-.12,.77),(.43+i*.065,-.16,.96),.008,'776c55')
for y in (-.15,.15):beam((.42,y,.965),(.78,y,.965),.012,'82775e')
rotate_from(0,82,-4.65,1.38)
# Rounded lobes: curved, crossed cutout meshes with real depth, no camera-facing script.
def cluster(x,y,z,w,h,angle,k):
    a=angle*pi/180;side=np.array([cos(a),sin(a),0]);normal=np.array([sin(a),-cos(a),.26]);normal/=np.linalg.norm(normal)
    for i in range(4):
        for j in range(3):
            pts=[];uv=[]
            for u,v in [(i/4,j/3),((i+1)/4,j/3),((i+1)/4,(j+1)/3),(i/4,(j+1)/3)]:
                bulge=.16*w*(1-(u*2-1)**2)*(1-(v*2-1)**2)
                pts.append(np.array([x,y,z])+side*(u-.5)*w+normal*(bulge+(v-.5)*h*.21)+[0,0,(v-.5)*h]);uv.append((u,1-v))
            face(pts,'leaf'+str(k),normal,uv)
def tree(x,y,s,seed):
    global group
    R=random.Random(seed)
    group='TreeWood'
    def P(a,b,c):return (x+a*s,y+b*s,c*s)
    rod(P(0,0,.02),P(.035,-.012,1.48),.19*s,'wood',.143*s,12)
    for i in range(4):
        a=i*pi/2+.36
        rod(P(.033,0,.68),P(.36*cos(a),.36*sin(a),.032),.088*s,'wood',.037*s,9)
    group='TreeWood'
    limbs=[]
    for i in range(7):
        a=2*pi*i/7+.27;dz=R.uniform(-.22,.18)
        bend=(.65*cos(a),.65*sin(a),2.48+dz);end=(1.21*cos(a),1.21*sin(a),3.25+dz)
        rod(P(.035,-.012,1.41),P(*bend),.106*s,'wood',.075*s,9)
        rod(P(*bend),P(*end),.075*s,'wood',.034*s,8)
        limbs.append(end)
    group='Canopies'
    for i,(cx,cy,cz) in enumerate(limbs+[(0,0,3.90),(.20,.05,4.27)]):
        for da in (0,68,133):
            cluster(x+cx*s,y+cy*s,cz*s,2.10*s,1.45*s,da+i*29,(0 if i%3 else 2) if i<7 else 1)
for x,y,s,seed in [(-7.6,3.40,1.12,13),(10.0,-1.56,1.04,72),(-12.0,-1.16,1.06,28),(4.9,9.55,1.14,30),(-4.8,-11.16,.89,42)]:tree(x,y,s,seed)
for x,y,s,seed in [(-5.40,11.5,1.20,61),(9.1,13.6,1.28,50),(-13.5,12.4,.94,73)]:tree(x,y,s,seed)
group='Canopies'
for i,(x,y) in enumerate([(-5.68,5.44),(-7.21,5.89),(-8.1,5.97),(9.22,1.58),(10.04,1.88),(-6.78,2.14)]):
    for a in (12,78,145):cluster(x,y,.37,1.15,.78,a+i*17,2)
group='PottedLeaves'
for x,y,s in [(-1.13,-.49,.96),(2.62,-.38,.84),(8.32,.13,.72)]:
    for angle in (8,77,147):cluster(x,y,.11+.67*s,.69*s,.54*s,angle,3)
# Two texture atlases with padded UV islands, one opaque and one alpha-tested.
def export(name,groups,keys,W):
    pack={};atlas=Image.new('RGBA',(W,W),(108,105,82,255) if name=='Street' else (82,84,57,0))
    x=y=rh=0;p=6
    items=[]
    for k in keys:
        im=maps[k]
        target=(400,400) if k.startswith('leaf') else ((790,96) if k=='shopSign' else (560,105) if im.width>2.6*im.height else (250,250))
        if im.width<10:target=(8,8)
        im=im.resize(target,Image.Resampling.LANCZOS);items.append((k,im))
    for k,im in sorted(items,key=lambda item:-item[1].height):
        w,h=im.size
        if x+w+2*p>W:x=0;y+=rh;rh=0
        if y+h+2*p>W:raise ValueError('Texture overflow')
        atlas.paste(Image.fromarray(np.pad(np.asarray(im),((p,p),(p,p),(0,0)),mode='edge')),(x,y))
        pack[k]=x+p,y+p,w,h;x+=w+2*p;rh=max(rh,h+2*p)
    hh=2**int(np.ceil(np.log2(y+rh)));atlas=atlas.crop((0,0,W,hh))
    if name=='Street':atlas=atlas.convert('RGB')
    atlas.save(root/(name+'Albedo.png'))
    for g,ls in groups.items():
        vv=[];ii=[];lut={}
        for pts,n,k,uv in ls:
            ax,ay,w,h=pack[k];ids=[]
            for v,(u,t) in zip(pts,uv):
                vt=tuple(np.round([v[0],v[2],v[1],n[0],n[2],n[1],(ax+.5+(w-1)*u)/W,1-(ay+.5+(h-1)*t)/hh],6))
                if vt not in lut:lut[vt]=len(vv);vv.append(vt)
                ids.append(lut[vt])
            if np.dot(np.cross(pts[1]-pts[0],pts[2]-pts[0]),n)>0:ids.reverse()
            ii.extend(ids)
        with (work/(name+'-'+g+'.bin')).open('wb') as f:
            f.write(struct.pack('<ii',len(vv),len(ii)));f.write(np.asarray(vv,dtype='<f4').tobytes());f.write(np.asarray(ii,dtype='<i4').tobytes())
for name,flag,size in [('Street',False,2048),('Foliage',True,1024)]:
    groups={g:ls for g,ls in batches.items() if (g in ('Canopies','PottedLeaves'))==flag}
    export(name,groups,sorted({k for ls in groups.values() for _,_,k,_ in ls}),size)
    print(name,len(groups),sum(len(ls) for ls in groups.values()))

