"""主角、面包师、歌手的三维样本。PBM2 保留绘制坐标，沿用正式骨架与换装入口。"""
from pathlib import Path
import argparse
import struct
import numpy as np
import sculpt_player_b as b
import sculpt_npc_b as npc
import sculpt_npc_wardrobe as w

OUT = Path(__file__).resolve().parents[1] / 'Assets/Resources/Characters'
HEAD = ((-.34,.34),(.79,1.57),(-.29,.19))
BODY = ((-.34,.34),(.38,1.24),(-.20,.20))
INK, HAIR_LIGHT, BOW = 12, 13, 14
NO_SHELL=128

def add(s, mat, family, field, bounds=BODY, step=.004, faces=1600):
    vertices,normals,indices=b.iso(field,bounds,step,faces)
    # 薄衣片的正面不能混入下缘/背面的法线，否则大片平面会出现三角形暗斑。
    epsilon=min(step*.25,.0007)
    exact=np.column_stack([field(vertices+axis)-field(vertices-axis) for axis in np.eye(3)*epsilon])
    exact/=np.maximum(np.linalg.norm(exact,axis=1)[:,None],1e-9)
    normals=exact if family in ('apron','torso','skirt','top') else normals
    if family=='torso' and mat in (b.TOP,b.CREAM):
        # 平衣摆底盖的法线不跨大三角面爬到衣服背面；保留沿布面方向的明暗。
        hem=(vertices[:,1]<.710)&(normals[:,1]<-.12)
        radial=np.column_stack((vertices[:,0]/.17**2,np.zeros(len(vertices)),vertices[:,2]/.10**2))
        radial[np.linalg.norm(radial,axis=1)<1e-6]=[0,-1,0]
        radial/=np.maximum(np.linalg.norm(radial,axis=1)[:,None],1e-9)
        normals[hem]=radial[hem]
    s.append((mat,family,(vertices,normals,indices)))

def stroke(s,mat,family,points,r=.0025,faces=320):
    pts=np.asarray(points);pad=r+.006
    bounds=tuple((float(pts[:,i].min()-pad),float(pts[:,i].max()+pad)) for i in range(3))
    add(s,mat,family,lambda p:b.curve(p,points,r),bounds,min(r, .0025),faces)

def patch(s,mat,family,points,z_at,depth=.004,roundness=.003,faces=650):
    pts=np.asarray(points);yy=np.linspace(pts[:,1].min(),pts[:,1].max(),50);zz=z_at(yy)
    bounds=((float(pts[:,0].min()-.012),float(pts[:,0].max()+.012)),
        (float(yy.min()-.012),float(yy.max()+.012)),(float(np.min(zz)-depth-.012),float(np.max(zz)+depth+.012)))
    def field(p):
        x,y,z=np.moveaxis(p,-1,0);inside=np.zeros_like(x,dtype=bool);distance=np.full_like(x,100.)
        for a,c in zip(pts,np.roll(pts,-1,axis=0)):
            dx,dy=c-a;t=np.clip(((x-a[0])*dx+(y-a[1])*dy)/(dx*dx+dy*dy),0,1)
            distance=np.minimum(distance,np.hypot(x-a[0]-t*dx,y-a[1]-t*dy))
            if abs(dy)>1e-9:inside^=((a[1]>y)!=(c[1]>y)) & (x<a[0]+(y-a[1])*dx/dy)
        signed=distance*np.where(inside,-1,1);d=np.abs(z-z_at(y))-depth
        return np.hypot(np.maximum(signed,0),np.maximum(d,0))+np.minimum(np.maximum(signed,d),0)-roundness
    add(s,mat,family,field,bounds,.0025,faces)

def curved_panel(surface,width,y0,y1,depth=.006,nx=32,ny=42):
    """均匀四边网格的薄衣片；正面与切边分离法线，避免简化器跨整片布产生暗三角。"""
    verts=[];normals=[];faces=[]
    ys=np.linspace(y0,y1,ny);us=np.linspace(-1,1,nx)
    for side in (1,-1):
        offset=len(verts)
        for y in ys:
            for u in us:
                x=u*width(y);h=.0001
                dx=(surface(x+h,y)-surface(x-h,y))/(2*h)
                dy=(surface(x,y+h)-surface(x,y-h))/(2*h)
                normal=np.array([-dx,-dy,1.])*side;normal/=np.linalg.norm(normal)
                verts.append([x,y,surface(x,y)+side*depth]);normals.append(normal)
        for iy in range(ny-1):
            for ix in range(nx-1):
                a=offset+iy*nx+ix;c=a+nx
                faces.extend([(a,a+1,c),(a+1,c+1,c)] if side>0 else [(a,c,a+1),(a+1,c,c+1)])
    rim=list(range(nx))+[i*nx+nx-1 for i in range(1,ny)]+list(range(ny*nx-2,(ny-1)*nx-1,-1))+[i*nx for i in range(ny-2,0,-1)]
    for a,c in zip(rim,rim[1:]+rim[:1]):
        p=np.array([verts[a],verts[c],verts[c+nx*ny],verts[a+nx*ny]])
        normal=-np.cross(p[1]-p[0],p[2]-p[0]);normal/=max(np.linalg.norm(normal),1e-9)
        offset=len(verts);verts.extend(p);normals.extend([normal]*4);faces.extend([(offset,offset+2,offset+1),(offset,offset+3,offset+2)])
    return np.asarray(verts),np.asarray(normals),np.asarray(faces,np.int32)

def pleated_skirt_mesh():
    radii=b.PchipInterpolator([.376,.393,.540,.645,.697],[[.230,.156],[.231,.157],[.196,.139],[.150,.110],[.146,.104]],extrapolate=True)
    def point(y,a):
        rx,rz=radii(y);pleat=1+.08*np.cos(a*10)*np.clip((.72-y)/.24,0,1)
        radius=1/np.sqrt((np.sin(a)/rx)**2+(np.cos(a)/rz)**2)/pleat
        return np.array([radius*np.sin(a),y,radius*np.cos(a)])
    verts=[];normals=[];faces=[];rings=42;segments=80
    for y in np.linspace(.376,.697,rings):
        for a in np.arange(segments)*2*np.pi/segments:
            normal=np.cross(point(y,a+.001)-point(y,a-.001),point(y+.0001,a)-point(y-.0001,a));normal/=np.linalg.norm(normal)
            verts.append(point(y,a));normals.append(normal)
    for row in range(rings-1):
        for col in range(segments):
            a=row*segments+col;c=row*segments+(col+1)%segments
            faces.extend([(a,c,a+segments),(c,c+segments,a+segments)])
    # 裙子是有内衬的薄布壳，不是底部封死的圆台。底盖会在走路/坐下时变成一大块深色面。
    count=len(verts)
    for p,n in zip(list(verts),list(normals)):
        p=p.copy();radial=np.array([p[0],0,p[2]]);radial/=np.linalg.norm(radial)
        verts.append(p-radial*.004);normals.append(-n)
    faces.extend([(c+count,b+count,a+count) for a,b,c in list(faces)])
    for end in (0,rings-1):
        for i in range(segments):
            a=end*segments+i;c=end*segments+(i+1)%segments
            offset=len(verts);verts.extend([verts[a],verts[c],verts[c+count],verts[a+count]])
            normals.extend([[0,-1 if end==0 else 1,0]]*4)
            faces.extend([(offset,offset+1,offset+2),(offset,offset+2,offset+3)] if end==0 else
                [(offset,offset+2,offset+1),(offset,offset+3,offset+2)])
    return np.asarray(verts),np.asarray(normals),np.asarray(faces,np.int32)

def cloth_strap(points,width=.018,depth=.004):
    """有实际厚度的连续扁布带，沿肩部转向；不再把围裙肩带做成圆绳。"""
    points=np.asarray(points);dist=np.r_[0,np.cumsum(np.linalg.norm(np.diff(points,axis=0),axis=1))]
    path=b.PchipInterpolator(dist,points,axis=0);ts=np.linspace(0,dist[-1],max(8,round(dist[-1]/.012)))
    verts=[];normals=[];faces=[];rows=len(ts)
    for side in (1,-1):
        offset=len(verts)
        for t in ts:
            center=path(t);tangent=path.derivative()(t);normal=np.cross([1,0,0],tangent);normal/=np.linalg.norm(normal)
            for u in (-1,1):verts.append(center+[u*width,0,0]+side*depth*normal);normals.append(side*normal)
        for row in range(rows-1):
            a=offset+row*2;c=a+2
            faces.extend([(a,a+1,c),(a+1,c+1,c)] if side>0 else [(a,c,a+1),(a+1,c,c+1)])
    rim=[0,1]+[i*2+1 for i in range(1,rows)]+[rows*2-2]+[i*2 for i in range(rows-2,0,-1)]
    for a,c in zip(rim,rim[1:]+rim[:1]):
        p=np.array([verts[a],verts[c],verts[c+rows*2],verts[a+rows*2]])
        normal=-np.cross(p[1]-p[0],p[2]-p[0]);normal/=max(np.linalg.norm(normal),1e-9)
        offset=len(verts);verts.extend(p);normals.extend([normal]*4);faces.extend([(offset,offset+2,offset+1),(offset,offset+3,offset+2)])
    return np.asarray(verts),np.asarray(normals),np.asarray(faces,np.int32)

def face(p):
    skull=b.loft(p,[(1.212,0,.005,.028,.030),(1.231,0,.003,.068,.067),
        (1.268,0,0,.105,.092),(1.316,0,-.007,.117,.106),
        (1.371,0,-.012,.112,.105),(1.426,0,-.019,.089,.084),
        (1.466,0,-.026,.042,.042),(1.476,0,-.026,.009,.009)])
    # 耳朵与脸是一张连续表面，贴近颅骨；不用侧脸投影贴图。
    ear=b.ellipsoid(b.mirror_x(p),(.116,1.310,.004),(.020,.024,.019))
    nose=b.ellipsoid(p,(0,1.304,.100),(.014,.012,.014))
    return b.mix_surface(b.mix_surface(skull,ear,.012),nose,.014)

def front(x,y):
    ray=np.linspace(.06,.18,420)
    values=face(np.stack([np.full_like(ray,x),np.full_like(ray,y),ray],axis=-1))
    return float(ray[np.flatnonzero(values<0)[-1]])

def face_line(p,xy,r):
    return b.curve(p,[(x,y,front(x,y)+.0006) for x,y in xy],r)

def sculpted_cap(kind):
    """连续梳理的发帽：用闭合曲面控制发际线，不再叠一串椭球形成硬帽檐。"""
    rings,segments=32,112
    phi=np.arange(segments)*2*np.pi/segments
    facing=np.clip((np.sin(phi)+.30)/.60,0,1)
    facing=facing*facing*(3-2*facing)
    rx=.136 if kind!='singer' else .1275
    cy,ry=1.357,.143
    back=1.234+.045*np.abs(np.cos(phi))**3
    bottom=back+(1.393-back)*facing
    if kind=='baker':
        for _ in range(8):
            theta=np.arccos(np.clip((bottom-cy)/ry,-.99,.99));xx=rx*np.sin(theta)*np.cos(phi)
            fringe=1.448-.047*(np.abs(xx)/.13)**2+.005*np.cos(xx*84)
            bottom=back+(fringe-back)*facing
    elif kind=='player':bottom+=.005*np.cos(phi*9+.4)*facing
    else:
        # 以真实表面 X 求侧分，而不是用圆周角近似，防止误成正中分。
        for _ in range(8):
            theta=np.arccos(np.clip((bottom-cy)/ry,-.99,.99))
            xx=rx*np.sin(theta)*np.cos(phi)
            front=np.interp(xx,[-.13,-.085,-.015,.045,.070,.13],[1.345,1.363,1.393,1.423,1.407,1.398])
            bottom=1.235+(front-1.235)*facing
    theta_max=np.arccos(np.clip((bottom-cy)/ry,-.99,.99))
    verts=[]
    for row in range(rings):
        theta=theta_max*(.0001+(1-.0001)*row/(rings-1))
        ripple=np.ones_like(phi)
        verts.extend(np.column_stack((rx*np.sin(theta)*np.cos(phi)*ripple,
            cy+ry*np.cos(theta)*ripple,-.026+.121*np.sin(theta)*np.sin(phi)*ripple)))
    verts=np.asarray(verts,dtype=np.float64);faces=[]
    if kind=='player':verts[:,0]*=1-.06*np.clip((1.43-verts[:,1])/.05,0,1)
    if kind=='baker':
        angle=np.arctan2(verts[:,0],verts[:,1]-cy)
        wave=.005*np.sin(angle*9)**2*np.clip((verts[:,1]-1.36)/.06,0,1)*np.exp(-((verts[:,2]+.026)/.080)**2)
        verts[:,0]+=wave*np.sin(angle);verts[:,1]+=wave*np.cos(angle)
    for row in range(rings-1):
        for col in range(segments):
            a=row*segments+col;c=row*segments+(col+1)%segments
            faces.extend([(a,a+segments,c),(c,a+segments,c+segments)])
    faces=np.asarray(faces,dtype=np.int32)
    corners=verts[faces];fn=np.cross(corners[:,1]-corners[:,0],corners[:,2]-corners[:,0])
    if np.sum(fn*(corners.mean(1)-[0,1.35,-.026]))<0:faces=faces[:,(0,2,1)];fn=-fn
    normals=np.zeros_like(verts)
    for j in range(3):np.add.at(normals,faces[:,j],fn)
    normals/=np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-9)
    # 底缘封到头内部，边缘分开法线，避免发际线被平均成硬壳边。
    start=len(verts);rim=verts[-segments:].copy();center=np.array([[0,1.28,-.025]])
    verts=np.concatenate([verts,rim,center]);normals=np.concatenate([normals,np.tile([0,-1,0],(segments+1,1))])
    caps=np.asarray([(start+i,start+(i+1)%segments,start+segments) for i in range(segments)],np.int32)
    return verts,normals,np.concatenate([faces,caps])

def head_parts(kind):
    s=[]
    add(s,b.SKIN,'head',face,HEAD,.0027,5800)
    if kind=='singer':
        s.append((b.HAIR,'head',sculpted_cap(kind)))
        # 贴脸侧发从太阳穴连到颌侧，遮住耳根，不能只剩两根悬空马尾。
        def cheek_hair(p):
            return b.loft(b.mirror_x(p),[(1.205,.067,.003,.009,.014),(1.235,.096,.010,.025,.035),
                (1.300,.125,.006,.023,.047),(1.352,.124,-.009,.022,.049),(1.398,.111,-.018,.020,.046)])
        add(s,b.HAIR|NO_SHELL,'head',cheek_hair,((-.17,.17),(1.19,1.42),(-.09,.08)),.0025,2200)
        for sign in (-1,1):
            def tail(p):
                q=p.copy();q[...,0]*=sign
                return b.loft(q,[(.790,.226,-.100,.008,.012),(.820,.238,-.100,.035,.029),
                    (.880,.255,-.116,.060,.042),(.955,.224,-.137,.059,.045),
                    (1.035,.214,-.128,.051,.046),(1.110,.225,-.116,.055,.049),
                    (1.200,.193,-.108,.043,.050),(1.285,.183,-.080,.045,.052),
                    (1.355,.165,-.015,.058,.045),(1.405,.148,.014,.022,.023)])
            family='tailL' if sign<0 else 'tailR'
            add(s,b.HAIR,family,tail,HEAD,.0035,3400)
        def bow(p):
            q=b.mirror_x(p)
            q[...,2]+=.055+(q[...,0]-.14)*.6
            left=b.front_patch(q,[(.143,1.416),(.178,1.448),(.205,1.407),(.176,1.407)],lambda y:np.zeros_like(y)+.074,.008,.008)
            right=b.front_patch(q,[(.139,1.419),(.116,1.433),(.095,1.410),(.120,1.393)],lambda y:np.zeros_like(y)+.081,.008,.008)
            tail=b.front_patch(q,[(.137,1.423),(.159,1.415),(.158,1.331),(.138,1.349)],lambda y:np.zeros_like(y)+.086,.006,.004)
            return b.mix_surface(b.mix_surface(left,right,.005),b.mix_surface(tail,b.ellipsoid(q,(.139,1.422,.096),(.015,.015,.012)),.004),.004)
        add(s,BOW,'head',bow,((-.23,.23),(1.31,1.49),(-.06,.08)),.002,2200)
    else:
        s.append((b.HAIR,'head',sculpted_cap(kind)))
    for sign in (-1,1):
        x,y=sign*(.054 if kind=='singer' else .050),1.330
        add(s,b.EYE,'head',lambda p:b.ellipsoid(p,(x,y,front(x,y)+.0015),(.014 if kind=='singer' else .0125,.014,.0045)),w.FACE,.0018,240)
        dy=0 if kind=='singer' else .006
        # 内眉略抬高抵消额头的前后曲率，35° 游戏俯视镜头里仍保持温和的弧眉。
        add(s,b.BROW,'head',lambda p:face_line(p,[(sign*.028,1.367+dy),(sign*.050,1.367+dy),(sign*.072,1.356+dy)],.0041),w.FACE,.0022,280)
    add(s,b.LIP,'head',lambda p:face_line(p,[(float(x),1.259+.006*(x/.028)**2) for x in np.linspace(-.028,.028,25)],.0032),w.FACE,.002,280)
    add(s,b.LIP,'head',lambda p:face_line(p,[(float(x),1.291+.004*(x/.010)**2) for x in np.linspace(-.010,.010,15)],.0020),w.FACE,.0018,180)
    if kind=='singer':
        for material,_,mesh in s:
            if (material & 127) not in (b.HAIR,BOW):
                mesh[0][:,0]*=.95;mesh[0][:,1]-=.010 if material==b.SKIN else .015
                mesh[1][:,0]/=.95;mesh[1][:]/=np.maximum(np.linalg.norm(mesh[1],axis=1)[:,None],1e-9)
            elif material==BOW:
                mesh[0][:,0]*=.985;mesh[1][:,0]/=.985
                mesh[1][:]/=np.maximum(np.linalg.norm(mesh[1],axis=1)[:,None],1e-9)
    if kind=='baker':
        for material,_,mesh in s:
            if (material & 127) not in (b.HAIR,BOW):
                mesh[0][:,0]*=.97;mesh[1][:,0]/=.97
                mesh[1][:]/=np.maximum(np.linalg.norm(mesh[1],axis=1)[:,None],1e-9)
    return s

def hands(p):
    q=b.mirror_x(p)
    q[...,1]=np.where(q[...,1]<.640,.640+(q[...,1]-.640)/.85,q[...,1])
    q[...,0]=.250+(q[...,0]-.250)/.96
    palm=b.sculpt(q,[((.250,.626,.027),(.028,.041,.023),5),
        ((.249,.602,.031),(.026,.025,.022)),
        ((.214,.629,.045),(.012,.024,.014),-20)],.009)
    for x,y in ((.229,.587),(.242,.580),(.255,.582),(.267,.590)):
        palm=b.mix_surface(palm,b.ellipsoid(q,(x,y,.032),(.008,.017,.016)),.003)
    wrist=b.loft(q,[(.639,.247,.022,.024,.022),(.691,.240,.018,.029,.026)])
    return b.mix_surface(palm,wrist,.010)

def skin(s, arms=True):
    add(s,b.SKIN,'top',lambda p:b.ellipsoid(p,(0,1.151,-.002),(.043,.103,.045)),faces=450)
    if arms:
        add(s,b.SKIN,'arm',lambda p:np.maximum(hands(p),p[...,1]-.700),
            ((-.315,.315),(.535,.72),(-.096,.102)),.0038,1500)

def shirt_body(p):
    return b.loft(p,[(.660,0,0,.171,.095),(.700,0,0,.173,.100),(.850,0,0,.173,.106),
        (1.016,0,0,.165,.111),(1.074,0,-.002,.137,.099),(1.130,0,-.007,.052,.052)],2.5)
def sleeves(p,fuller=False):
    return b.loft(b.mirror_x(p),[(.660,.244,.010,.035,.035),(.700,.240,.010,.042,.042),
        (.820,.235 if fuller else .228,.010,.052,.047),(.970,.199 if fuller else .195,.008,.052,.050),
        (1.055,.167,.002,.056,.050),(1.102,.132,-.002,.034,.033)])

def shoes(s):
    bounds= ((-.22,.22),(-.006,.17),(-.15,.21))
    def upper(p):
        q=b.mirror_x(p);q[...,0]=.104+(q[...,0]-.104)/1.08
        return b.shoes(q)
    add(s,b.SHOE,'shoe',upper,bounds,.0035,1900)
    add(s,b.SOLE,'shoe',lambda p:b.loft(b.mirror_x(p),[
        (.002,.104,.041,.059,.093),(.009,.104,.041,.066,.102),(.023,.104,.041,.067,.103)],2.7),bounds,.0035,850)

def trousers(s):
    def shape(p):
        legs=b.loft(b.mirror_x(p),[(.076,.104,0,.053,.052),(.120,.104,0,.055,.055),
            (.300,.105,0,.060,.061),(.460,.104,0,.076,.075),(.565,.094,-.004,.083,.088),
            (.650,.078,-.008,.077,.084),(.674,.075,-.008,.073,.082)],2.6)
        return b.mix_surface(legs,b.rounded_box(p,(0,.618,-.009),(.300,.105,.170),.040),.014)
    add(s,b.PANTS,'pants',shape,w.LEGS,.0035,4200)

def jacket():
    s=[]
    def jacket_body(p):
        shell=shirt_body(p)
        opening=np.maximum(np.abs(p[...,0])-np.interp(p[...,1],[.66,.88,1.12],[.049,.048,.053]),.057-p[...,2])
        return np.maximum(shell,-opening)
    add(s,b.TOP,'torso',jacket_body,faces=2800)
    add(s,b.TOP,'sleeve',lambda p:sleeves(p,True),faces=2600)
    # 圆领棉衫与大翻领，和概念里的 V 字皮肤开口区分开。
    add(s,b.CREAM,'torso',lambda p:np.maximum(npc.shirt(p),p[...,1]-(1.085+np.minimum(.028,(np.abs(p[...,0])/.055)**2*.025))),faces=1500)
    for sign in (-1,1):
        points=[(sign*x,y) for x,y in ((.045,1.126),(.077,1.108),(.118,1.037),(.083,1.050),(.057,1.062),(.045,1.092))]
        patch(s,b.TOP|NO_SHELL,'torso',points,lambda y:np.interp(y,[1.025,1.06,1.126],[.126,.122,.064]),.006,.004,1000)
        stroke(s,INK,'torso',[(sign*.047,1.125,.076),(sign*.078,1.107,.096),(sign*.116,1.039,.139),
            (sign*.083,1.052,.139),(sign*.057,1.062,.136)],.0030,420)
        patch(s,b.TOP|NO_SHELL,'torso',[(sign*x,y) for x,y in ((.062,.798),(.154,.804),(.151,.738),(.146,.722),(.066,.722))],lambda y:np.zeros_like(y)+.107,.004,.005,800)
        stroke(s,INK,'torso',[(sign*.153,.799,.118),(sign*.150,.738,.118),(sign*.145,.725,.118),(sign*.067,.725,.118),(sign*.064,.795,.118)],.0032,450)
        stroke(s,INK,'torso',[(sign*.064,.798,.118),(sign*.153,.803,.118)],.0018,180)
        stroke(s,INK,'torso',[(sign*.050,.665,.109),(sign*.049,.85,.118),(sign*.052,1.038,.129)],.0030,400)
    stroke(s,INK,'torso',[(-.050,.664,.108),(-.100,.664,.104),(-.166,.664,.075)],.002,220)
    stroke(s,INK,'torso',[(.050,.664,.108),(.100,.664,.104),(.166,.664,.075)],.002,220)
    add(s,b.TRIM,'sleeve',lambda p:b.loft(b.mirror_x(p),[(.660,.244,.010,.036,.037),(.684,.242,.010,.044,.044),(.700,.240,.010,.044,.044)]),faces=650)
    b.write_model(OUT/'PlayerB-PaintedJacket.bytes',s)
    s=[];trousers(s);b.write_model(OUT/'PlayerB-PaintedTrousers.bytes',s)

def baker():
    b.write_model(OUT/'PlayerB-NpcHead-baker.bytes',head_parts('baker'))
    s=[];skin(s)
    add(s,b.TOP,'sleeve',lambda p:sleeves(p/np.array([.98,1,1])),faces=2600)
    # 立领环由连续壳体构成，前中间留小缺口。
    def collar(p):
        q=p-[0,1.115,-.006]
        radius=np.sqrt((q[...,0]/.055)**2+(q[...,2]/.055)**2)
        front=np.clip((p[...,2]+.020)/.075,0,1)
        top=1.143-.022*(1-np.clip(np.abs(p[...,0])/.055,0,1))*front
        return np.maximum.reduce([np.abs(radius-1)*.055-.005,1.093-p[...,1],p[...,1]-top,
            np.minimum(.002-np.abs(p[...,0]),p[...,2]-.02)])
    def chef_surface(p):return b.mix_surface(np.maximum(shirt_body(p),p[...,1]-1.105),collar(p),.005)
    add(s,b.TOP,'torso',chef_surface,step=.003,faces=3600)
    def chef_front(x,y):
        ray=np.linspace(.020,.180,420);probe=np.stack([np.full_like(ray,x),np.full_like(ray,y),ray],axis=-1)
        hits=np.flatnonzero(chef_surface(probe)<0)
        return float(ray[hits[-1]]+.0015)
    line=[]
    for x in np.linspace(-.083,.083,32):
        y=1.074+.024*(x/.085)**2+.006*x/.085;line.append((float(x),float(y),chef_front(x,y)))
    stroke(s,b.TRIM|NO_SHELL,'torso',line,.0028,400)
    stroke(s,INK,'torso',[(0,float(y),chef_front(0,y)) for y in np.linspace(1.104,1.074,15)],.0022,300)
    # 围裙从胸口连续到膝上，后面留裤子并有腰带和结。
    # 胸片、腰带、裙片各自有宽度，裙片横向弧面包住大腿。
    s.append((b.TRIM,'apron',curved_panel(
        lambda x,y:np.interp(y,[.407,.67,.755,.920],[.160,.155,.151,.148])-.12*(x/.24)**2+.004*np.cos(x*25)*np.clip((.75-y)/.35,0,1),
        lambda y:np.interp(y,[.407,.755,.920],[.196,.174,.111])+.007,.414,.914)))
    for sign in (-1,1):
        for path in [[(.097,.929,.140),(.098,.954,.137)],[(.099,.969,.134),(.102,1.015,.127),(.100,1.075,.108),
            (.072,1.119,.019),(.075,1.080,-.086),(.082,1.030,-.119),(.088,.900,-.115),(.093,.762,-.114)]]:
            s.append((b.TRIM,'top',cloth_strap([(sign*x,y,z) for x,y,z in path])))
    add(s,b.TRIM,'torso',lambda p:b.loft(p,[(.735,0,.024,.178,.144),(.772,0,.024,.178,.144)],2.5),faces=850)
    def backbow(p):
        return b.sculpt(p,[((0,.757,-.124),(.017,.018,.014)),
            ((-.027,.762,-.123),(.030,.018,.012),25),((.027,.762,-.123),(.030,.018,.012),-25)],.004)
    add(s,b.TRIM,'top',backbow,step=.003,faces=550)
    for points in [[(.027,.774),(.059,.767),(.031,.705),(-.008,.732)],[(.047,.767),(.077,.775),(.112,.730),(.087,.708)]]:
        points=[(-.054+(-x+.054)*1.25,.758+(y-.750)*1.15) for x,y in points]
        patch(s,b.TRIM|NO_SHELL,'torso',points,lambda y:np.zeros_like(y)+.178,.006,.004,550)
        stroke(s,INK,'torso',[(x,y,.189) for x,y in points+[points[0]]],.0027,350)
    patch(s,b.TRIM|NO_SHELL,'apron',[(-.038,.751),(-.074,.751),(-.078,.620),(-.030,.620)],lambda y:np.interp(y,[.620,.751],[.160,.180]),.005,.003,550)
    stroke(s,INK,'apron',[(-.038,.738,.188),(-.030,.621,.171),(-.078,.621,.171),(-.074,.738,.188)],.0032,430)
    for y in (.736,.773):
        stroke(s,INK,'torso',[(float(x),y,float(.026+.144*(1-(abs(x)/.178)**2.5)**(1/2.5))) for x in np.linspace(-.170,.170,22)],.0027,500)
    def buckle(p):
        q=b.mirror_x(p)
        outside=b.rounded_box(q,(.097,.970,.145),(.041,.044,.014),.012)
        hole=b.rounded_box(q,(.097,.972,.145),(.026,.028,.035),.008)
        return np.maximum(outside,-hole)
    add(s,b.CREAM,'torso',buckle,step=.002,faces=550)
    add(s,b.TRIM,'torso',lambda p:b.curve(b.mirror_x(p),[(.009,.751,-.117),(.035,.663,-.116),(.030,.636,-.122)],.010),step=.003,faces=550)
    # 双排扣与大贴袋在正常游玩距离仍保留简单可读的大色块。
    for y in (1.052,.967):
        add(s,b.TRIM|NO_SHELL,'top',lambda p:b.ellipsoid(b.mirror_x(p),(.060,y,.123),(.0145,.0145,.006)),step=.002,faces=320)
    add(s,b.TOP,'sleeve',lambda p:b.loft(b.mirror_x(p),[(.666,.244,.010,.040,.041),(.706,.240,.010,.046,.047)]),faces=650)
    b.write_model(OUT/'PlayerB-NpcTop-chef.bytes',s)
    s=[];trousers(s);shoes(s);b.write_model(OUT/'PlayerB-NpcBottom-chef.bytes',s)

def singer():
    b.write_model(OUT/'PlayerB-NpcHead-twintail.bytes',head_parts('singer'))
    s=[];skin(s)
    def blouse(p):
        q=p.copy()
        ridge=np.exp(-((np.abs(p[...,0])-(.130-(p[...,1]-.72)*.40))/.014)**2-((p[...,1]-.75)/.067)**2)
        q[...,2]-=.009*ridge*np.clip((p[...,2]-.04)/.04,0,1)
        return b.loft(q,[(.661,0,0,.146,.095),(.741,0,0,.151,.097),(.865,0,0,.150,.105),
            (1.016,0,0,.155,.106),(1.070,0,-.003,.133,.094),(1.102,0,-.005,.084,.065),
            (1.115,0,-.005,.054,.050),(1.128,0,-.005,.048,.046)],2.4)
    def puff_sleeves(p):
        return b.loft(b.mirror_x(p),[(.662,.244,.010,.034,.036),(.700,.226,.010,.055,.048),
            (.787,.215,.010,.059,.052),(.913,.195,.010,.053,.050),(1.027,.181,.003,.060,.058),(1.086,.143,-.002,.045,.044)])
    add(s,b.TOP,'torso',blouse,faces=2600)
    add(s,b.TOP,'sleeve',lambda p:np.maximum(puff_sleeves(p),.700-p[...,1]),faces=2600)
    add(s,b.TRIM,'torso',lambda p:b.loft(p,[(1.115,0,-.005,.058,.055),(1.140,0,-.005,.054,.051)]),
        ((-.07,.07),(1.11,1.17),(-.07,.065)),.0025,550)
    add(s,b.TRIM,'sleeve',lambda p:b.loft(b.mirror_x(p),[(.660,.244,.010,.037,.039),(.705,.226,.010,.057,.052)]),faces=700)
    add(s,b.TRIM,'torso',lambda p:b.ellipsoid(p,(0,1.077,.132),(.014,.014,.010)),step=.0025,faces=350)
    add(s,b.TRIM,'torso',lambda p:b.front_patch(b.mirror_x(p),[(.005,1.070),(.019,1.070),(.046,.991),(.017,.996)],
        lambda y:.119,.004,.003),step=.0025,faces=650)
    add(s,b.TRIM,'torso',lambda p:b.front_patch(b.mirror_x(p),[(.077,1.087),(.096,1.078),(.130,.979),(.115,.975)],
        lambda y:np.interp(y,[.975,1.087],[.119,.077]),.005,.003),step=.003,faces=550)
    for sign in (-1,1):
        patch(s,b.TRIM,'torso',[(sign*x,y) for x,y in ((.006,1.077),(.070,1.092),(.067,1.051),(.009,1.062))],
            lambda y:np.zeros_like(y)+.122,.005,.003,550)
        for x,y in ((.111,1.068),(.121,1.033),(.129,.998)):
            patch(s,b.TRIM,'torso',[(sign*a,c) for a,c in ((x-.012,y+.021),(x+.016,y+.021),(x+.023,y+.006),(x+.017,y-.018),(x-.010,y-.016))],
                lambda yy:np.interp(yy,[.975,1.09],[.125,.083])+.008*np.cos((yy-y)*np.pi/.040),.006,.004,650)
        stroke(s,INK,'torso',[(sign*.138,.718,.082),(sign*.119,.752,.102),(sign*.130,.770,.095)],.0025,320)
    for y in (.954,.893,.833,.773):
        add(s,b.TRIM|NO_SHELL,'torso',lambda p:b.ellipsoid(p,(0,y,.116),(.009,.009,.004)),step=.002,faces=160)
    for _,_,mesh in s:
        t=np.clip((mesh[0][:,1]-.95)/.17,0,1)
        mesh[0][:,1]-=.012*t*t*(3-2*t)
        mesh[1][:,1]/=1-.012*6*t*(1-t)/.17
        mesh[1][:]/=np.maximum(np.linalg.norm(mesh[1],axis=1)[:,None],1e-9)
    b.write_model(OUT/'PlayerB-NpcTop-singer.bytes',s)
    s=[]
    def skirt(p):
        angle=np.arctan2(p[...,0],p[...,2])
        q=p.copy()
        pleat=1+.08*np.cos(angle*10)*np.clip((.72-p[...,1])/.24,0,1)
        q[...,0]*=pleat;q[...,2]*=pleat
        return b.loft(q,[(.376,0,0,.230,.156),(.393,0,0,.231,.157),(.540,0,0,.196,.139),
            (.645,0,0,.150,.110),(.697,0,0,.146,.104)],2.0)
    s.append((b.PANTS,'skirt',pleated_skirt_mesh()))
    for side in (-1,1):
        for part in (.31,.74):
            path=[]
            for yy in np.linspace(.382,.682,26):
                xx=side*part*np.interp(yy,[.376,.54,.682],[.230,.196,.143])
                ray=np.linspace(.01,.19,240);probe=np.stack([np.full_like(ray,xx),np.full_like(ray,yy),ray],axis=-1)
                hit=np.flatnonzero(skirt(probe)<0)
                if len(hit):path.append((xx,yy,ray[hit[-1]]+.001))
            stroke(s,INK,'skirt',path,.0022,400)
    add(s,b.TRIM,'torso',lambda p:b.loft(p,[(.665,0,0,.153,.107),(.702,0,0,.152,.107)],2.4),faces=500)
    add(s,b.PANTS,'calf',lambda p:b.loft(b.mirror_x(p),[(.065,.104,0,.043,.040),(.140,.104,0,.047,.045),
        (.290,.104,0,.054,.051),(.420,.104,0,.058,.057)]),w.LEGS,.0035,2300)
    shoes(s)
    b.write_model(OUT/'PlayerB-NpcBottom-singer.bytes',s)

# 只为这三个增补资源扩展权重，不改共享基础雕刻器的其他输出。
original_weights=b.weights
original_map=b.proportion_map
def weights(v,family):
    if family=='torso':
        wgt=np.zeros((len(v),18),np.float32);t=np.clip((v[:,1]-.68)/.16,0,1)
        wgt[:,0]=1-t;wgt[:,1]=t;return wgt
    if family=='sleeve':
        return np.pad(original_weights(v,'arm'),((0,0),(0,3)))
    if family.startswith('tail'):
        wgt=np.zeros((len(v),18),np.float32);wgt[:,15 if family=='tailL' else 16]=1;return wgt
    if family in ('apron','skirt'):
        # 连续围裙/裙摆避免左右腿权重在跨步时把中缝撕开。
        wgt=np.zeros((len(v),18),np.float32)
        hem=np.clip((.72-v[:,1])/.25,0,1)
        if family=='skirt':
            front=np.clip((v[:,2]+.035)/.11,0,1)
            hem*=front*front*(3-2*front)
        wgt[:,17]=hem;wgt[:,0]=1-hem
        if family=='apron':
            t=np.clip((v[:,1]-.68)/.16,0,1);wgt[:,0]=(1-hem)*(1-t);wgt[:,1]=t
        return wgt
    return np.pad(original_weights(v,family),((0,0),(0,3)))
def mapping(v,family):
    if family.startswith('tail'):
        q=v.copy();q[:,1]-=.044
        return original_map(q,'head')
    if family=='torso':
        q=original_map(v,'top');q[:,1]=original_map(v,'pants')[:,1];return q
    if family=='sleeve':
        q=original_map(v,'arm');q[:,1]+=.024*np.clip((1.08-v[:,1])/.42,0,1);return q
    return original_map(v,'pants' if family in ('apron','skirt') else family)
b.weights=weights;b.proportion_map=mapping

def write_sample(path,sections):
    """PBM2 仅用于这三个样本，额外保留原雕刻坐标供随蒙皮移动的布料色块使用。"""
    with path.open('wb') as stream:
        stream.write(b'PBM2'+struct.pack('<I',len(sections)))
        for material,family,(vertices,normals,faces) in sections:
            source=vertices.copy();source_normals=normals.copy();vertices=vertices.copy()
            if family=='head':vertices[:,1]-=.044
            influence=weights(vertices,family)
            positions,shading=b.proportion_mesh(vertices,normals,family)
            bones=np.argsort(-influence,axis=1)[:,:4];values=np.take_along_axis(influence,bones,axis=1);values/=values.sum(1)[:,None]
            vertex=np.empty(len(vertices),dtype=[('p','<f4',3),('n','<f4',3),('bones','u1',4),('w','<f4',4),('source','<f4',3),('sourceN','<f4',3)])
            vertex['p'],vertex['n'],vertex['bones'],vertex['w']=positions,shading,bones,values
            vertex['source'],vertex['sourceN']=source,source_normals
            if len(vertices)>65535:raise ValueError('样本子网格超出 16 位索引。')
            # 位 6 标记连续衣摆，供坐姿布面形变使用；位 7 仍表示已有手工结构线。
            if family in ('apron','skirt'):material|=64
            stream.write(struct.pack('<BII',material,len(vertices),faces.size));stream.write(vertex.tobytes());stream.write(faces.astype('<u2').tobytes())
    print(path.name,sum(len(section[2][0]) for section in sections),'vertices')
b.write_model=write_sample

def player():
    for short in (False,True):
        s=head_parts('player');skin(s,False)
        add(s,b.SKIN,'top',lambda p:b.front_patch(p,[(-.047,1.113),(.047,1.113),(0,1.056)],
            lambda y:.094-.45*np.maximum(0,y-1.035),.007,.002),step=.003,faces=400)
        limit=.956 if short else .700
        def arms(p):
            if not short:return np.maximum(hands(p),p[...,1]-limit)
            upper=np.maximum(b.bare_arms(p),.655-p[...,1])
            return np.maximum(b.mix_surface(upper,hands(p),.01),p[...,1]-limit)
        add(s,b.SKIN,'arm',arms,
            ((-.315,.315),(.535,limit+.02),(-.096,.102)),.0038,2200 if short else 1500)
        shoes(s)
        b.write_model(OUT/('PlayerB-PaintedCoreShort.bytes' if short else 'PlayerB-PaintedCore.bytes'),s)
    jacket()

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('characters',nargs='*',choices=['player','baker','singer'],default=['player','baker','singer']);args=parser.parse_args()
    for name in args.characters:globals()[name]()
