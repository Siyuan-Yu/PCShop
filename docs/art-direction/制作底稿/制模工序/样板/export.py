from pathlib import Path
import sys, collections, json, struct
work=Path(sys.argv[1]); srcdir=Path(sys.argv[2]); temp=work/'SourceData'
entry=srcdir/'棕夹克青年'/'young.py'
code=entry.read_text('utf8').split('pos=(-1.23')[0]
paint="""
    if cap:
        skin=(albedo[:,:,0]>120)&(albedo[:,:,1]>.52*albedo[:,:,0])&(albedo[:,:,2]<albedo[:,:,1])
        warm=(85,49,38) if key=='Crown' else (48,40,32)
        albedo[skin]=warm
    if key in ('face','Face'):
        frontweight=np.clip((a-.24)/.36,0,1)[:,:,None]
        sidepaint=np.zeros_like(albedo)+np.array([223,163,111])
        temple=np.abs(np.sin(th))-0.65
        sidepaint-=np.maximum(temple,0)[:,:,None]*np.array([21,21,17])
        # Skin is painted around the cheek; exclude dark strands from the side portrait.
        ear=lookup(1, np.clip(752+(D-754)*.49,744,783)-512,np.clip(T,199,259))
        useful=(ear[:,:,0]>145)&(ear[:,:,1]>85)
        e=np.exp(-((T-236)/23)**4)*np.clip((1-np.abs(a))/.55,0,1)*useful
        sidepaint=sidepaint*(1-e[:,:,None])+ear*e[:,:,None]
        albedo=front*frontweight+sidepaint*(1-frontweight)
    if 'Sleeve' in key or 'Hand' in key or key in ('CreamCollar','neck'):
        blend=np.clip((a-.38)/.37,0,1)[:,:,None]
        female=key[0].isupper()
        skin= 'Hand' in key or key in ('neck','CreamCollar')
        base=np.array(([227,173,130] if female else [218,166,111]) if skin else ([155,68,52] if female else [143,101,60]))
        paint=np.zeros_like(albedo)+base
        paint*= (1-.048*(1-a)-.025*np.cos(3*th+4*vv))[:,:,None]
        if 'Sleeve' in key:
            fold=np.exp(-((vv-.67-.045*np.sin(2.5*th))/.034)**2)
            cuff=np.exp(-((vv-.86)/.011)**2)
            paint*=(1-.065*fold-.17*cuff)[:,:,None]
        albedo=front*blend+paint*(1-blend)
    if key in ('hair','Crown','BobBack','LeftWaves','RightWaves'):
        female=key!='hair'
        t=vv
        ridge=(.5+.5*np.cos(th*5.5+2*np.sin(t*3.7)+t*4.7))
        ground=np.array([128,67,49] if female else [57,47,36])
        pigment=ground[None,None,:]*(.85+.19*ridge+.065*np.sin(2.5*th+t*4))[:,:,None]
        faceside=np.clip((a-.50)/.39,0,1)[:,:,None]
        if key in ('LeftWaves','RightWaves','BobBack'):faceside*=.20
        albedo=albedo*faceside+pigment*(1-faceside)
    if key in ('LeftWaves','RightWaves'):
        haircolor=np.array([109,60,44]); skin=(albedo[:,:,0]>151)&(albedo[:,:,1]>albedo[:,:,0]*.54)
        albedo[skin]=haircolor
"""
code=code[:code.index('    if cap:\n        # The hair mass')]+paint+code[code.index("    bank[key,'person']="):]
exec(compile(code.replace("folder=Path(__file__).resolve().parent","folder=entry.parent").replace("Path(__file__).resolve().parent.parent","entry.parent.parent"),str(entry),'exec'))
# Python source axes: X, shop depth, height. Unity scene centres on the room.
destination=work/'Assets/Art/OldTownShopStudy'
leaf=tiles['leaf'].astype(float)
tiles['leaf']=np.uint8(np.clip(leaf*.40+np.array([100,111,52])*.60,0,255))
def group(p,material):
    x,y,z=p.mean(axis=0);c,k=material
    if k=='paving' or y<-2.94 or x>4.13 or x<-4.13 or y>2.96:return 'Street'
    if z<.032:return 'Floor'
    if k=='plaster' or c in ('78816d','ac9169','baaa90','747c6b','ddd3b7','605d50'):return 'Walls'
    if y>2.14:
        i=min(range(4),key=lambda j:abs(x-(-2.96+j*1.56)));return f'Shelf{i+1}'
    if x>3.36:return 'Shelf5' if y>0 else 'Shelf6'
    if x<-2.73:return 'Checkout'
    if y<-1.95:return 'Entrance'
    return 'Display'
def car():
    # Compact boxy saloon, glasshouse inset from the beltline.
    start=len(mesh); x,y=0,0
    def body(rings,c):
        rr=[]
        for z,l,w,dx in rings:
            rr.append(np.array([(dx+l*i,w*j,z) for i,j in [(-1,-.70),(-.90,-1),(.90,-1),(1,-.70),(1,.70),(.90,1),(-.90,1),(-1,.70)]]))
        for a,b in zip(rr,rr[1:]):
            for i in range(8):face([a[i],a[(i+1)%8],b[(i+1)%8],b[i]],(c,''),(0,0,.33))
        a=rr[-1]
        for i in range(8):face([a[i],a[(i+1)%8],a.mean(0)],(c,''),(0,0,0))
    body([(.19,1.57,.59,0),(.30,1.67,.65,0),(.58,1.69,.65,0),(.70,1.59,.615,0)],'687b65')
    body([(.70,1.01,.55,.08),(1.06,.68,.46,.12),(1.10,.64,.45,.12)],'4b504b')
    box(.10,0,1.08,1.24,.90,.037,'71816a')
    for s in (-1,1):
        side=s*.552
        face([(-.87,side,.72),(-.03,side,.72),(-.03,s*.47,1.045),(-.61,s*.47,1.04)],('606765',''),(0,0,.83))
        face([(.04,side,.72),(.98,side,.72),(.72,s*.47,1.04),(.04,s*.47,1.045)],('586361',''),(0,0,.83))
        box(.15,s*.649,.38,.024,.025,.25,'4a554a')
        box(.05,s*.663,.36,2.70,.017,.028,'586050')
    face([(-.92,-.48,.72),(-.92,.48,.72),(-.57,.41,1.038),(-.57,-.41,1.038)],('737c76',''),(0,0,.83))
    box(-1.63,0,.31,.08,1.14,.052,'8f9488')
    box(-1.684,0,.43,.034,.29,.085,'b2a789')
    for side in (-1,1):
        for i in (-1,1):
            st=len(mesh);loft(0,0,[(0,.266,.266),(.038,.278,.278),(.13,.269,.269)],'373733',18)
            loft(0,0,[(.132,.142,.142),(.15,.132,.132)],'938c73',14)
            for m in range(st,len(mesh)):
                co,n,ma,u=mesh[m];rot=np.array([[1,0,0],[0,0,side],[0,-side,0]])
                mesh[m]=(co@rot.T+[i*1.05,side*.57,.292],n@rot.T,ma,u)
        box(.06,side*.519,.72,.045,.063,.33,'515646')
        box(.22,side*.657,.54,.15,.022,.024,'474b42')
        for i,c in [(-1,'d8caaa'),(1,'aa6653')]:box(i*1.63,side*.396,.43,.045,.27,.097,c)
    rotate_from(start,0,3.28,-5.42)
def prepare_room():
    interior()
    groups=collections.defaultdict(list)
    for p,n,m,u in mesh:
        name=group(p,m);c,k=m; x,y,z=p.mean(0)
        if name.startswith('Shelf') and k in ('board','gpu','cpu','ssd','power'):
            k=['gpu','cpu','ssd','shipping','board','power'][int(name[-1])-1]
        if k=='' and rgb(c)[1]>rgb(c)[0] and c not in ('665138',):
            if z>1.65 and (y>2.98 or x<-5.5):k='leaf';name='Street'
            elif z<.99 and y<-1.95 and -.2<x<2.7:k='leaf';name='Entrance'
        if c=='b8996f':k='shipping'
        if k=='plaster' and n[2]>.8:k='paving'
        if k=='leaf':c='6f793c'
        groups[name].append((p.copy(),n,(c,k),u))
    mesh.clear()
    # Match FurnitureCatalog.DefaultShopFixtures(Small) centres and the 8 x 6 RoomSpec.
    shifts=dict(Checkout=(.20,1.23),Display=(-.05,.18),Shelf1=(.86,.26),Shelf2=(1.033,.26),Shelf3=(1.207,.26),Shelf4=(1.38,.26),Shelf5=(.06,-.13),Shelf6=(.06,-1.23),Entrance=(-.34,-.15))
    for name,items in groups.items():
        for i,(p,n,m,u) in enumerate(items):
            if name in shifts:p[:,:2]+=shifts[name]
            if name in ('Floor','Walls','Street'):p[:,1]*=6/5.60
            if name=='Walls':
                # Shift the posts and low wall opening to x=5m from the left corner.
                if p[:,1].max()<-2.98:
                    if p[:,0].mean()<.8:p[:,0]= -4+(p[:,0]+4)*(4.27/4.61)
                    else:p[:,0]=4-(4-p[:,0])*(2.27/1.95)
                elif m[1]=='plaster' or m[0]=='baaa90':p[:,2]*=1.14
            items[i]=(p,n,m,u)
    # Timber assembly bench along left wall, adjacent to checkout.
    box(-3.55,2.1,.025,.68,1.12,.77,'777b66','enamel')
    box(-3.55,2.1,.785,.72,1.16,.068,'a58f70','wood')
    box(-3.52,2.22,.855,.26,.43,.40,'50514b','','case')
    groups['AssemblyBench']=list(mesh);mesh.clear()
    # Drawers, corner mouldings and recesses separate the worktop from the painted cabinet.
    for z in (.11,.39,.69):
        box(-3.514,.32,z,.025,2.00,.018,'5d6558')
    for y in (-.42,.28,.98):
        box(-3.516,y,.40,.031,.019,.55,'566054')
        box(-3.526,y+.15,.57,.029,.14,.018,'aaa28d')
    box(-3.10,.30,.828,.87,2.72,.027,'846e53','wood')
    groups['Checkout']+=list(mesh);mesh.clear()
    # Visible pavement and lawn use repeated metre-size patches at all distances.
    for gx in range(-6,7):
        for gy in range(0,6):
            xx=gx*2.;yy=gy*2.
            if -4.2<xx<8.1 and yy<3.2:continue
            paint_quad([(xx,yy,-.152),(xx+2,yy,-.152),(xx+2,yy+2,-.152),(xx,yy+2,-.152)],'grass')
    for j in range(-6,6):
        xx=j*2.8
        for yy in (-10.1,-12.85):
            paint_quad([(xx,yy,-.20),(xx+2.8,yy,-.20),(xx+2.8,yy+2.8,-.20),(xx,yy+2.8,-.20)],'road')
    groups['Street']+=list(mesh);mesh.clear()
    # Slim painted timber trim gives silhouettes and references the bevel on the concept counter.
    for xx in (-1.26,1.07):
        box(xx,.10,.062,.035,.85,.72,'806749','wood')
    box(-.10,-.329,.095,2.35,.04,.065,'66543f','wood')
    groups['Display']+=list(mesh);mesh.clear()
    # Shopfront threshold, distance visible grime, and a short service conduit by the bench.
    box(1.0,-3.16,-.034,1.48,.40,.052,'978872','paving')
    box(-3.86,2.88,.78,.025,.036,1.05,'94826a')
    box(-3.86,2.82,1.79,.173,.095,.23,'788073','enamel')
    groups['Walls']+=list(mesh);mesh.clear()
    car();groups['ParkedCar']=list(mesh);mesh.clear()
    return groups
def add_man():
    actor()
    # Tapered locks interrupt the round outline, particularly from the store camera.
    for x,y,z,dx,dy,h in [(-.09,.012,1.67,-.068,.003,.020),(.015,.024,1.698,.049,.012,.015),(.107,.05,1.644,.050,.012,.012),(-.171,.02,1.58,-.036,-.006,.006)]:
        loft(x,y,[(z-.040,.062,.050),(z,.044,.031),(z+h,.014,.010,dx*.87,dy),(z+h-.002,.003,.004,dx,dy)],'3d332a',8)
    groups=collections.defaultdict(list)
    for tri in mesh:groups['hair' if tri[2][0]=='3d332a' else tri[2][0]].append(tri)
    mesh.clear();return groups
def add_woman():
    # Reuse the projection mapping, with separate silhouette sections for the hair, skirt and limbs.
    global pieces
    source=code[code.index('ink=np.asarray'):code.index('actor_parts={}')].replace("folder/'三向原画.png'","temp/'landlady.png'")
    exec(source,globals())
    # Crown and hair should retain her chestnut local colour.
    surface('Cardigan',[(293,270,329,717,779),(308,245,362,695,797),(333,220,382,678,804),(368,218,388,664,812),(420,218,388,658,817),(476,218,390,657,823),(537,215,393,655,826),(571,212,395,656,822)])
    surface('CreamCollar',[(269,278,329,720,770),(291,277,332,723,773),(315,271,335,724,773)])
    surface('Skirt',[(509,221,385,663,816),(553,213,391,658,820),(615,207,397,654,824),(713,196,410,651,834),(811,186,423,644,847),(837,202,410,651,836)])
    for s in (-1,1):
        def side(rows):
            return rows if s<0 else [(t,608-r,608-l,n,f) for t,l,r,n,f in rows]
        prefix='L' if s<0 else 'R'
        surface(prefix+'Leg',side([(820,227,281,718,786),(854,231,281,721,782),(884,234,277,724,781),(913,228,277,721,782)]))
        surface(prefix+'Shoe',side([(895,228,277,718,777),(906,215,281,692,786),(926,198,285,665,791),(945,199,284,662,793),(954,206,278,670,786)]))
        surface(prefix+'Sleeve',side([(325,223,241,730,780),(345,203,239,726,786),(384,190,230,719,791),(447,172,222,716,792),(510,159,217,714,793),(538,162,216,718,790),(559,164,211,722,786)]))
        surface(prefix+'Hand',side([(557,170,207,727,778),(576,164,210,720,776),(603,163,212,716,774),(623,173,204,724,767),(634,183,196,737,755)]))
    surface('Face',[(102,247,357,669,812),(147,231,377,651,824),(191,219,388,648,825),(219,217,389,645,824),(239,228,379,641,813),(259,241,368,650,806),(277,261,350,662,787),(291,289,328,678,765)],groove=True)
    surface('Crown',[(73,287,318,701,753),(81,257,347,676,803),(98,226,380,652,829),(123,206,392,626,849),(153,187,409,617,865),(179,171,424,613,877),(202,168,430,623,888),(214,176,427,638,880)],cap=True)
    surface('BobBack',[(123,221,380,795,845),(163,191,411,776,872),(203,181,425,776,883),(234,181,424,783,876),(260,196,408,780,858),(280,223,390,776,835),(293,262,352,774,808)])
    for s in (-1,1):
        surface(('Left' if s<0 else 'Right')+'Waves',side([(143,193,237,699,835),(171,174,246,714,855),(197,171,246,731,868),(224,167,243,739,868),(246,177,246,748,859),(266,187,252,754,849),(282,211,262,759,832),(291,232,257,770,804)]))
    g=collections.defaultdict(list)
    for tr in mesh:g[tr[2][0]].append(tr)
    mesh.clear();return g
def pack(name,groups):
    mats=sorted({t[2] for g in groups.values() for t in g})
    patches=[]
    for c,k in mats:
        if k:
            arr=texture(c,k);size=(300,300) if k=='person' else (160,160)
            if k.startswith('label') or k in ('display','street'):size=(196,52)
            img=Image.fromarray(arr).resize(size,Image.Resampling.LANCZOS)
            if k=='person':
                # Remove residual chroma key spill at the edges of the input sheet.
                arr=np.array(img);v=arr.astype(int); mask=(v[:,:,1]>v[:,:,0]*1.15+4)&(v[:,:,1]>v[:,:,2]*1.1+3)
                arr[mask]=[71,51,35] if name=='Landlady' else [52,44,36]
                img=Image.fromarray(arr)
        else:img=Image.new('RGB',(8,8),tuple(rgb(c).astype(int)))
        patches.append(((c,k),img))
    patches.sort(key=lambda pr:-pr[1].height)
    S=2048;x=y=row=0;loc={};im=Image.new('RGB',(S,S),'#6e6254'); pad=7
    for m,img in patches:
        w,h=img.size
        if x+w+2*pad>S:x=0;y+=row;row=0
        if y+h+2*pad>S:raise RuntimeError('atlas overflow')
        im.paste(Image.fromarray(np.pad(np.asarray(img),((pad,pad),(pad,pad),(0,0)),mode='edge')),(x,y))
        loc[m]=(x+pad,y+pad,w,h);x+=w+2*pad;row=max(row,h+2*pad)
    H=2**math.ceil(math.log2(y+row));im=im.crop((0,0,S,H));im.save(destination/(name+'Albedo.png'))
    for part,grouped in groups.items():
        data=[];indices=[];lut={};ns={}
        for p,n,m,u in grouped:
            if m[1]=='person':
                for pt in p:
                    key=tuple(np.round(pt,5));ns[key]=ns.get(key,np.zeros(3))+n
        for p,n,m,u in grouped:
            a,b,w,h=loc[m];tr=[]
            for pt,coord in zip(p,u):
                normal=ns.get(tuple(np.round(pt,5)),n) if m[1]=='person' else n
                normal=normal/max(np.linalg.norm(normal),.0001)
                vert=tuple(np.round([pt[0],pt[2],pt[1],normal[0],normal[2],normal[1],(a+.5+coord[0]*(w-1))/S,1-(b+.5+coord[1]*(h-1))/H],6))
                if vert not in lut:lut[vert]=len(data);data.append(vert)
                tr.append(lut[vert])
            # X Z Y has opposite handedness to the mesh source.
            if np.dot(np.cross(p[1]-p[0],p[2]-p[0]),n)>0:tr.reverse()
            indices.extend(tr)
        with (temp/(name+'-'+part+'.bin')).open('wb') as f:
            f.write(struct.pack('<ii',len(data),len(indices)));f.write(np.array(data,dtype='<f4').tobytes());f.write(np.array(indices,dtype='<i4').tobytes())
    print(name,len(groups),sum(len(g) for g in groups.values()),flush=True)
pack('Shop',prepare_room());pack('JacketYouth',add_man());pack('Landlady',add_woman())

