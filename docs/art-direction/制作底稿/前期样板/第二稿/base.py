from pathlib import Path
import math, random, sys
import numpy as np
from PIL import Image, ImageDraw, ImageFont

out = Path(sys.argv[1])
rng = random.Random(62)
mesh, bank = [], {}
def rgb(c):
    return np.array(bytes.fromhex(c) and list(bytes.fromhex(c)), dtype=float)
def texture(base, kind):
    key = (base,kind)
    if key in bank: return bank[key]
    n=128
    basec=rgb(base)
    img=Image.new('RGB',(n,n),tuple(basec.astype(int)))
    dr=ImageDraw.Draw(img)
    def tint(s): return tuple(np.clip(basec+s,0,255).astype(int))
    if kind in ('plaster','paving','road','wood','grass','tile','mat'):
        for i in range(160 if kind!='wood' else 85):
            x,y=rng.randrange(n),rng.randrange(n)
            w,h=rng.randrange(2,15),rng.randrange(1,7)
            if kind=='wood': w,h=rng.randrange(12,65),rng.choice([1,1,2])
            dr.polygon([(x,y+1),(x+w//3,y),(x+w,y+1),(x+w-2,y+h),(x+2,y+h)],fill=tint(rng.choice([-12,-7,-4,3,6,9])))
        if kind=='plaster':
            for x,y in [(16,100),(75,58)]:
                dr.line([(x,y),(x+2,y+5),(x,y+9),(x+3,y+14)], fill=tint(-20))
    if kind in ('gpu','board','case','box'):
        dr.rectangle((3,4,124,123),outline=tint(-15),width=3)
        if kind in ('gpu','board','box'):
            dr.rectangle((8,90,119,115), fill=tint(-26))
            dr.rectangle((15,96,44,100),fill=(211,197,160))
            dr.rectangle((15,103,33,106),fill=(171,164,142))
            dr.polygon([(85,7),(118,7),(118,32),(109,32)],fill=(153,98,65))
        if kind in ('gpu','case'):
            for cx,cy in ([(42,53),(84,53)] if kind=='gpu' else [(64,46),(64,88)]):
                dr.ellipse((cx-19,cy-19,cx+19,cy+19),fill=(54,53,48),outline=(127,121,102),width=2)
                for j in range(7):
                    a=2*math.pi*j/7
                    dr.polygon([(cx,cy),(cx+int(15*math.cos(a)),cy+int(15*math.sin(a))),(cx+int(12*math.cos(a+.7)),cy+int(12*math.sin(a+.7)))],fill=(104,102,91))
            if kind=='case': dr.line((12,17,39,17),fill=(176,175,159),width=2)
        if kind=='board':
            dr.rectangle((26,26,96,79),fill=(74,84,70))
            dr.rectangle((43,35,60,52),outline=(176,167,134),width=3)
            for y in (60,66,72): dr.line((35,y,83,y),fill=(168,159,128),width=2)
    bank[key]=np.array(img)
    return bank[key]

def face(pts,mat,center,uv=None):
    p=np.array(pts,dtype=float)
    normal=np.cross(p[1]-p[0],p[2]-p[0]); normal/=max(1e-10,np.linalg.norm(normal))
    if np.dot(normal,p.mean(axis=0)-center)<0: normal=-normal
    if uv is None: uv=[(0,1),(1,1),(1,0),(0,0)] if len(p)==4 else [(0,1),(1,1),(.5,0)]
    for j in range(1,len(p)-1):
        idx=[0,j,j+1]; mesh.append((p[idx],normal,mat,np.array(uv)[idx]))
def box(x,y,z,w,d,h,c,kind='',front=None):
    # Z denotes bottom. Each side has its own UV rectangle.
    a,b=x-w/2,x+w/2; e,f=y-d/2,y+d/2; t=z+h; mid=(x,y,z+h/2)
    q=[(a,e,z),(b,e,z),(b,f,z),(a,f,z),(a,e,t),(b,e,t),(b,f,t),(a,f,t)]
    for i,idx in enumerate([(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7),(3,2,1,0)]):
        face([q[k] for k in idx],(c,front if i==0 and front else kind),mid)
def loft(x,y,rings,c,n=8):
    # height, x radius, y radius, optional offsets per ring.
    rs=[]
    for ring in rings:
        z,rx,ry,*off=ring; dx,dy=off or (0,0)
        rs.append(np.array([(x+dx+rx*math.cos((i+.5)*2*math.pi/n),y+dy+ry*math.sin((i+.5)*2*math.pi/n),z) for i in range(n)]))
    center=np.mean(np.vstack(rs),axis=0)
    for bot,top in zip(rs,rs[1:]):
        for i in range(n):
            j=(i+1)%n
            face([bot[i],bot[j],top[j],top[i]],(c,''),center)
    for row in (rs[0],rs[-1]):
        for i in range(n): face([row.mean(axis=0),row[i],row[(i+1)%n]],(c,''),center)
def rotate_from(start,angle,x=0,y=0,z=0):
    a=math.radians(angle)
    rot=np.array([[math.cos(a),-math.sin(a),0],[math.sin(a),math.cos(a),0],[0,0,1]])
    for i in range(start,len(mesh)):
        p,n,m,u=mesh[i]; mesh[i]=(p@rot.T+[x,y,z],n@rot.T,m,u)
def limb(a,b,rx,ry,c,n=7):
    x,y,z=a; X,Y,Z=b
    loft(x,y,[(z,rx,ry),(Z,rx*.81,ry*.84,X-x,Y-y)],c,n)
def lady(x,y,angle=0,man=False):
    start=len(mesh); skin='c29b79' if man else 'd0a382'; red='756044' if man else 'a04f41'; hair='42362c' if man else '543b32'
    for side in (-1,1):
        k=side*.108
        loft(k,-.032,[(.012,.083,.142),(.036,.085,.143),(.071,.078,.135),(.10,.067,.093,0,.018)],'493e35',8)
        limb((k,.01,.09),(k,.01,.65),.077,.083,'47525a' if man else '9f8067')
    if not man: loft(0,0,[(.26,.266,.166),(.31,.268,.166),(.82,.184,.127)],'675345',12)
    loft(0,0,[(.69,.22,.148),(.76,.225,.15),(1.02,.195,.141),(1.15,.244,.137),(1.19,.163,.102)],red,10)
    loft(0,0,[(1.16,.077,.071),(1.28,.077,.074)],skin)
    for side in (-1,1):
        # Arms fall naturally and lean slightly forward at the wrist.
        limb((side*.222,-.002,1.13),(side*.254,-.018,.903),.086,.096,red)
        limb((side*.254,-.018,.93),(side*.222,-.095,.745),.073,.083,red)
        loft(side*.22,-.095,[(.64,.04,.038),(.675,.049,.042),(.753,.044,.04)],skin)
        pts=[(0,-.145,1.095),(side*.115,-.137,1.16),(side*.084,-.14,1.195),(0,-.138,1.17)]
        face(pts,('ddd0b0',''),(0,0,1.13))
    if not man:
        for s in (-1,1):
            face([(s*.069,-.132,.704),(s*.121,-.129,1.10),(s*.071,-.146,1.157),(s*.017,-.157,.72)],('94483f',''),(0,0,.86))
        face([(-.041,-.162,.715),(.04,-.162,.715),(.063,-.161,1.136),(-.064,-.161,1.136)],('d0bfa0',''),(0,0,.9))
        for z in (.79,.92,1.03): box(.061,-.155,z,.013,.008,.014,'b39972')
        face([(-.022,-.166,1.09),(0,-.168,1.055),(.023,-.166,1.09),(0,-.166,1.111)],('799b83',''),(0,0,1.08))
    loft(0,0,[(1.21,.107,.106),(1.27,.169,.139),(1.40,.201,.17),(1.535,.199,.171),(1.605,.147,.134)],skin,10)
    for s in (-1,1):
        box(s*.074,-.157,1.402,.026,.011,.016,'4a3931')
        if not man: box(s*.178,-.041,1.347,.018,.016,.02,'dfcbb0')
    loft(0,-.159,[(1.357,.029,.017),(1.386,.021,.029),(1.415,.012,.012)],'c19675',5)
    box(0,-.147,1.318,.047,.009,.009,'8c6553')
    loft(0,.008,[(1.496,.207,.178),(1.556,.216,.18),(1.638,.17,.152),(1.669,.095,.09)],hair,10)
    loft(0,.097,[(1.31 if man else 1.244,.123,.073),(1.388,.19,.106),(1.513,.201,.098),(1.55,.16,.085)],hair,8)
    for s in (-1,1):
        loft(s*.166,-.008,[(1.373 if man else 1.291,.041,.049,-s*.018,.014),(1.373,.069,.099,s*.008,0),(1.445,.053,.097),(1.518,.063,.121),(1.55,.04,.079)],hair,7)
    loft(-.065,-.092,[(1.445,.045,.046,-.034,0),(1.506,.114,.095),(1.567,.133,.083),(1.606,.094,.062)],hair,7)
    rotate_from(start,angle,x,y)
    return len(mesh)-start

def unit(x):
    x=np.array(x,float);return x/np.linalg.norm(x)
def basis(direction):
    v=unit(direction); h=unit(np.cross([0,0,1],v));return np.array([h,np.cross(v,h),v])
def projection(b,aim,width,w,h):
    def project(p):
        t=(p-aim)@b.T
        return np.stack([w/2+t[...,0]*w/width,h/2-t[...,1]*w/width,t[...,2]],axis=-1)
    return project
def samples(q,w,h):
    x0,y0=np.maximum(np.floor(q[:,:2].min(axis=0)).astype(int),0); x1,y1=np.minimum(np.ceil(q[:,:2].max(axis=0)).astype(int),[w-1,h-1])
    if x0>x1 or y0>y1:return
    a,b,c=q[:,:2]; d=np.cross(np.r_[b-a,0],np.r_[c-a,0])[2]
    if abs(d)<1e-7:return
    yy,xx=np.mgrid[y0:y1+1,x0:x1+1]; X=xx+.5-a[0]; Y=yy+.5-a[1]
    v=(X*(c[1]-a[1])-Y*(c[0]-a[0]))/d; t=((b[0]-a[0])*Y-(b[1]-a[1])*X)/d
    ok=(v>=-.00001)&(t>=-.00001)&(t+v<=1.00001)
    return yy[ok],xx[ok],np.array([1-v[ok]-t[ok],v[ok],t[ok]]).T
def render(w,h,center,direction,width,bg,light_area):
    center=np.array(center); sun=unit([-5,1.8,6] if light_area>4 else [-4,-5,6])
    pj=projection(basis(direction),center,width,w,h)
    S=1200; lp=projection(basis(sun),center,light_area,S,S); shadow=np.full((S,S),-1e5)
    for p,n,m,u in mesh:
        q=lp(p); s=samples(q,S,S)
        if s is None:continue
        y,x,k=s; shadow[y,x]=np.maximum(shadow[y,x],k@q[:,2])
    canvas=np.empty((h,w,3),np.uint8);canvas[:]=rgb(bg); depth=np.full((h,w),-1e5)
    for p,n,(c,kind),u in mesh:
        q=pj(p);s=samples(q,w,h)
        if s is None: continue
        y,x,k=s; z=k@q[:,2]; good=z>=depth[y,x]; y,x,k,z=y[good],x[good],k[good],z[good]
        if not len(z):continue
        depth[y,x]=z
        l=k@lp(p); ix=np.clip(l[:,0].astype(int),1,S-2); iy=np.clip(l[:,1].astype(int),1,S-2)
        illum=np.zeros(len(x))
        for dx,dy in [(0,0),(-1,0),(1,0),(0,1),(0,-1)]: illum+=(shadow[iy+dy,ix+dx]<l[:,2]+.009+light_area/S*1.4)*.2
        nl=max(0,n@sun); ambient=.68+.06*n[2]; shading=ambient+(.45*illum+.055)*nl
        if kind:
            uv=np.clip((k@u*127).astype(int),0,127)
            col=texture(c,kind)[uv[:,1],uv[:,0]]
        else: col=rgb(c)
        canvas[y,x]=np.clip(col*shading[:,None]+(1-illum[:,None])*np.array([-3,-1,3]),0,255)
    return Image.fromarray(canvas)

def room():
    box(0,0,-.18,8.2,5.6,.18,'88806e')
    for i in range(10):
        for j in range(7):
            box(-3.6+i*.8,-2.4+j*.8,-.02,.796,.796,.026,rng.choice(['c9bea6','c8bba2','c4b8a1']),'tile')
    for x,y,w,d in [(0,2.82,8.22,.16),(4.06,0,.16,5.8)]:
        box(x,y,0,w,d,2.35,'c3b69e','plaster')
        box(x,y,0,w+.016,d+.019,.65,'78816d')
        box(x,y,.65,w+.025,d+.025,.028,'ac9169')
        box(x,y,2.33,w+.04,d+.04,.06,'baaa90')
    for x,w in [(-1.72,4.66),(3.08,2.08)]:
        box(x,-2.85,.015,w,.125,.41,'747c6b')
    box(-4.075,0,.01,.14,5.65,.40,'747c6b')
    box(1.34,-2.62,.009,1.22,.53,.015,'925f48','mat')
    box(0,-.1,.01,2.95,1.62,.016,'88937a','mat')
    def shelf(x,y,ang,num):
        st=len(mesh)
        box(0,.19,.08,1.22,.075,1.66,'66695b')
        for a in (-.62,.62): box(a,0,.06,.066,.49,1.74,'576458')
        for lev in (.13,.62,1.12,1.74): box(0,0,lev,1.28,.52,.06,'9d8464','wood')
        for k in range(2):
            for j in range(2 if (num+k)%3 else 1):
                box(-.31+j*.53,.02,.19+k*.5,.43,.27,.29,rng.choice(['676b5c','867861','8c795d','6e7370']),'','board' if (j+num)%3==0 else 'gpu')
        box(0,-.273,1.745,.49,.015,.084,'ac966e')
        for at in (-.14,-.08,-.02,.05,.114):
            box(at,-.284,1.766,.031,.008,.025,'6c6657')
        rotate_from(st,ang,x,y)
    for i in range(4):shelf(-2.96+i*1.56,2.48,0,i)
    dial=[(.04+.176*math.cos(i*math.pi/12),2.711,2.037+.176*math.sin(i*math.pi/12)) for i in range(24)]
    for i in range(24):face([(.04,2.711,2.037),dial[i],dial[(i+1)%24]],('ddd3b7',''),(.04,2.8,2.037))
    box(.04,2.702,2.031,.012,.008,.108,'605d50')
    box(.075,2.700,2.029,.074,.01,.012,'605d50')
    for i in range(2):shelf(3.7,.88-1.65*i,-90,i+4)
    box(-.05,-.08,.04,2.35,.82,.75,'948064','wood')
    box(-.05,-.08,.77,2.42,.89,.075,'cbbca0','wood')
    box(.0,-.065,.845,.31,.44,.56,'454842','','case')
    box(.56,-.37,.849,.22,.109,.009,'e5d7b7')
    box(-3.30,-.93,.02,.78,2.64,.82,'768173')
    box(-3.30,-.93,.84,.86,2.72,.076,'c8b998','wood')
    box(-3.32,-.83,.918,.25,.19,.032,'55554b')
    box(-3.32,-.83,.946,.07,.077,.14,'59594f')
    box(-3.32,-.83,1.05,.47,.075,.32,'4a4e48')
    box(-3.32,-.871,1.08,.408,.014,.242,'606b66')
    box(-3.30,-1.35,.92,.29,.21,.095,'52554b')
    for x in (.51,2.15):
        loft(x,-2.52,[(.015,.144,.144),(.29,.21,.21)],'a07450')
        loft(x,-2.52,[(.25,.112,.112),(.36,.217,.21),(.51,.235,.22),(.66,.14,.13),(.69,.06,.06)],'718050',9)
    box(0,-3.7,-.19,13.5,1.65,.17,'ae9e84','paving')
    for i in range(17):box(-6.6+i*.82,-3.7,-.018,.015,1.65,.004,'8f8473')
    box(0,-4.55,-.17,13.5,.17,.13,'aa9981')
    box(0,-6.0,-.215,13.5,2.85,.038,'595855','road')
    for x in range(-6,7,2):box(x,-6.22,-.172,.78,.072,.006,'bfaa73')
    box(6,0,-.12,3.75,5.64,2.52,'b9a88f','plaster')
    box(6,-2.84,.06,2.73,.07,1.68,'777767')
    for k in range(10):box(6,-2.885,.12+k*.157,2.66,.02,.019,'68695d')
    box(6,-3.005,1.84,3.07,.53,.10,'a16b50')
    box(-5.4,0,-.19,2.39,5.59,.13,'838664','grass')

def imgfont(size):
    return ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',size)
def sheet_header(im,title,subtitle):
    d=ImageDraw.Draw(im);d.text((42,24),title,font=imgfont(30),fill='#3e4038')
    d.text((44,72),subtitle,font=imgfont(16),fill='#656455')
    d.line((43,108,im.width-44,108),fill='#cbc2af',width=1)

room(); lady(1.25,-1.73,-19); lady(-1.76,1.91,155,True)
world=render(1500,844,[0,-.26,.80],[-8,-12,9.6],13.75,'d2c8b1',21)
world.save(out/'01-电脑店正常镜头.png')
mesh.clear(); tri=lady(0,0)
board=Image.new('RGB',(1456,790),'#e7e2d6');sheet_header(board,'房东 · 场景小人','同一网格，三个正交视角。右侧取自店内画面，按原图像素显示。')
for i,cam in enumerate([(0,-10,1),(-4,-8,2),(5,7,2)]):
    frame=render(590,980,[0,0,.81],cam,1.18,'e7e2d6',3)
    frame=frame.resize((295,490),Image.Resampling.LANCZOS);board.paste(frame,(42+i*309,142))
    ImageDraw.Draw(board).text((137+i*309,653),['正面','斜前方','背面'][i],font=imgfont(18),fill='#555348')
boxcam=projection(basis([-8,-12,9.6]),np.array([0,-.26,.80]),13.75,1500,844)
sx,sy=boxcam(np.array([1.25,-1.73,.84]))[:2].astype(int)
crop=world.crop((sx-185,sy-160,sx+185,sy+160))
board.paste(crop,(1030,245));ImageDraw.Draw(board).text((1040,598),'店内镜头 · 原尺寸局部',font=imgfont(18),fill='#555348')
board.save(out/'02-房东网格与店内大小.png')
print('Character triangles:',tri)
