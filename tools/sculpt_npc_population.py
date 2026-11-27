"""全员手绘三维资源：继承三人母版的连续表面、耳手、PBM2 骨架与布面形变。"""
from pathlib import Path
import argparse,copy,json,pickle
import numpy as np
import sculpt_character_samples as s
b=s.b
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/Resources/Characters'
CACHE=ROOT/'work/npc-art/sculpt-cache';CACHE.mkdir(parents=True,exist_ok=True)

def cache(key,fn):
    path=CACHE/(key+'.pickle')
    if path.exists():return pickle.loads(path.read_bytes())
    result=fn();path.write_bytes(pickle.dumps(result));return result
def collect(fn):
    result={};writer=b.write_model
    try:
        b.write_model=lambda p,sections:result.update({p.stem.replace('PlayerB-',''):copy.deepcopy(sections)})
        fn()
    finally:b.write_model=writer
    return result
def append(target,sections,remap=None):
    for mat,family,mesh in copy.deepcopy(sections):target.append(((mat&192)|(remap or {}).get(mat&63,mat&63),family,mesh))
def primitive(target,mat,family,field,bounds,faces=650,step=.004):s.add(target,mat,family,field,bounds,step,faces)
_shirt_rings=b.PchipInterpolator([.660,.700,.850,1.016,1.074,1.130],
    [[0,.171,.095],[0,.173,.100],[0,.173,.106],[0,.165,.111],[-.002,.137,.099],[-.007,.052,.052]])
def shirt_front(x,y):
    """与身体相同的超椭圆截面，衣片与结构线贴合真实布面。"""
    z,rx,rz=np.moveaxis(_shirt_rings(np.clip(y,.660,1.130)),-1,0)
    return z+rz*np.maximum(0,1-(np.abs(x)/rx)**2.5)**.4

def disk(target,mat,family,at,radii,faces=180,lift=.006):
    if family=='torso' and at[2]>0:at=(at[0],at[1],float(shirt_front(at[0],at[1]))+lift)
    at=np.array(at);r=np.array(radii);bounds=tuple(zip(at-r-.008,at+r+.008))
    primitive(target,mat,family,lambda p:b.ellipsoid(p,at,r),bounds,faces,.0025)
def line(target,mat,family,points,r=.0025,lift=.007):
    if family=='torso':
        dense=[]
        for a,c in zip(points,points[1:]):
            for t in np.linspace(0,1,max(2,int(np.linalg.norm(np.array(c)-a)/.012)+1),endpoint=False):
                x,y,z=np.array(a)*(1-t)+np.array(c)*t
                dense.append((x,y,float(shirt_front(x,y))+lift if z>0 else z))
        x,y,z=points[-1];dense.append((x,y,float(shirt_front(x,y))+lift if z>0 else z));points=dense
    s.stroke(target,mat|s.NO_SHELL,family,points,r,220)

def panel_patch(out,mat,family,points,z_at,depth=.004,roundness=.003,faces=650,lift=None):
    """细分并贴合躯干的薄布片；正反面和切边独立法线，蒙皮不跨长三角。"""
    xy=np.asarray(points,dtype=float)
    if np.sum(xy[:,0]*np.roll(xy[:,1],-1)-xy[:,1]*np.roll(xy[:,0],-1))<0:xy=xy[::-1]
    poly=list(range(len(xy)));tri=[]
    def cross(a,c,d):return (c[0]-a[0])*(d[1]-a[1])-(c[1]-a[1])*(d[0]-a[0])
    while len(poly)>3:
        for j in range(len(poly)):
            a,c,d=poly[j-1],poly[j],poly[(j+1)%len(poly)]
            if cross(xy[a],xy[c],xy[d])<=1e-12:continue
            inside=False
            for k in poly:
                if k in (a,c,d):continue
                if min(cross(xy[a],xy[c],xy[k]),cross(xy[c],xy[d],xy[k]),cross(xy[d],xy[a],xy[k]))>=-1e-12:inside=True;break
            if not inside:tri.append((a,c,d));poly.pop(j);break
        else:raise ValueError('衣片轮廓不能三角化')
    tri.append(tuple(poly))
    # 只有四角的衣片会横穿弧形躯干，并跨越腰部蒙皮过渡；必须先细分再投影。
    for _ in range(5):
        longest=max(np.linalg.norm(xy[a]-xy[c]) for f in tri for a,c in zip(f,(*f[1:],f[0])))
        if longest<.022:break
        pts=list(xy);edges={};refined=[]
        def midpoint(a,c):
            edge=tuple(sorted((a,c)))
            if edge not in edges:edges[edge]=len(pts);pts.append((xy[a]+xy[c])*.5)
            return edges[edge]
        for a,c,d in tri:
            ac,cd,da=midpoint(a,c),midpoint(c,d),midpoint(d,a)
            refined.extend([(a,ac,da),(ac,c,cd),(da,cd,d),(ac,cd,da)])
        xy=np.asarray(pts);tri=refined
    def surface(x,y):
        # 衬衫、领片、领结使用明确的叠层间距，避免同面闪烁。
        return shirt_front(x,y)+(lift if lift is not None else .014 if mat&63==b.TIE else .004)
    verts=[];normals=[];indices=[];count=len(xy)
    for side in (1,-1):
        offset=len(verts)
        for x,y in xy:
            h=.0001;z=float(surface(x,y));dx=(surface(x+h,y)-surface(x-h,y))/(2*h);dy=(surface(x,y+h)-surface(x,y-h))/(2*h)
            n=np.array([-dx,-dy,1.])*side;n/=np.linalg.norm(n);verts.append([x,y,z+side*min(depth,.0015)]);normals.append(n)
        indices.extend([(a+offset,c+offset,d+offset) if side>0 else (d+offset,c+offset,a+offset) for a,c,d in tri])
    boundary={}
    for face in tri:
        for a,c in zip(face,(*face[1:],face[0])):
            key=tuple(sorted((a,c)))
            if key in boundary:boundary[key]=None
            else:boundary[key]=(a,c)
    for edge in boundary.values():
        if edge is None:continue
        a,c=edge;offset=len(verts);p=np.array([verts[a],verts[c],verts[c+count],verts[a+count]])
        n=np.cross(p[1]-p[0],p[2]-p[0]);n/=max(np.linalg.norm(n),1e-9)
        verts.extend(p);normals.extend([n]*4);indices.extend([(offset,offset+1,offset+2),(offset,offset+2,offset+3)])
    out.append((mat,family,(np.asarray(verts),np.asarray(normals),np.asarray(indices,np.int32))))

def cap(kind):
    # 从连续曲面控制前发际、后脑及两侧；没有朝相机的一张头发卡片。
    mesh=copy.deepcopy(s.sculpted_cap('baker' if kind in ('crop','senior','receding','officer','worker','cap') else 'singer' if kind in ('swept','bun','sidepony','ponytail','braid','shortfemale','long','waves','curly') else 'player'))
    v,n,f=mesh
    if kind in ('fringe','longfringe','bob'):
        front=np.clip((v[:,2]+.015)/.095,0,1);low=np.clip((1.46-v[:,1])/.10,0,1)
        v[:,1]-=.016*front*low
    if kind in ('tousled','curlymale','curly'):
        above=np.clip((v[:,1]-1.39)/.10,0,1);a=np.arctan2(v[:,0],v[:,2]+.025)
        crown=np.clip(np.hypot(v[:,0],v[:,2]+.026)/.075,0,1)**2
        v[:,1]+=.012*np.sin(a*7+.4)**2*above*crown;v[:,0]+= .006*np.sin(a*5)*above*crown
    if kind=='receding':
        front=np.clip((v[:,2]+.04)/.11,0,1);v[:,1]+=.022*front*np.clip((1.48-v[:,1])/.06,0,1)
    # 变形后重新计算共享顶点法线；发帽底缘保留独立顶点。
    normal=np.zeros_like(v);c=v[f];fn=np.cross(c[:,1]-c[:,0],c[:,2]-c[:,0])
    for j in range(3):np.add.at(normal,f[:,j],fn)
    normal/=np.maximum(np.linalg.norm(normal,axis=1)[:,None],1e-9)
    return v,normal,f

def head(kind):
    base=cache('approved-head-v1',lambda:s.head_parts('player'))
    parts=copy.deepcopy([p for p in base if p[0]!=b.HAIR])
    style=kind.split('_')[0]
    if style in ('bob','fringe','curly','waves','long','longfringe','shortfemale','bun','sidepony','ponytail','braid'):
        for mat,_,(v,n,_) in parts:
            v[:,0]*=.96;n[:,0]/=.96;n/=np.maximum(np.linalg.norm(n,axis=1)[:,None],1e-9)
    parts.append((b.EYE if style in ('worker','officer','cap') else b.HAIR,'head',cap(style)))
    long=style in ('long','longfringe','waves')
    bob=style in ('bob','fringe','curly','shortfemale','curlymale')
    if long or bob:
        y0=.94 if long else 1.176 if style in ('curly','bob','fringe') else 1.225
        def curtain(p):
            x,y,z=np.moveaxis(p,-1,0)
            bulge=(.006*np.cos(y*60+x*14)) if style in ('curly','waves','curlymale') else 0
            rx=np.interp(y,[y0,1.27,1.37,1.44],[.116 if long else .106,.134,.126,.085])+bulge
            rz=np.interp(y,[y0,1.27,1.44],[.082,.113,.090])
            radial=np.sqrt((x/rx)**2+((z+.035)/rz)**2)
            shell=np.abs(radial-1)*.10-.018
            # 前开口沿真实颊面收束，不挡住眼睛，不做独立突出的侧脸面片。
            edge=np.interp(y,[y0,1.28,1.41],[.002,.034,.014])
            hem=y0+.012*np.cos(x*34) if long or style=='curly' else y0+.005*np.cos(x*25)
            return np.maximum.reduce([shell,hem-y,y-1.438,z-edge])
        primitive(parts,b.HAIR|s.NO_SHELL,'head',curtain,((-.18,.18),(y0-.025,1.46),(-.19,.07)),2700,.0038)
    if style in ('bun','sidepony','ponytail','braid'):
        if style=='bun':
            disk(parts,b.HAIR,'head',(0,1.280,-.155),(.068,.060,.055),950)
        else:
            side=.125 if style=='sidepony' else 0
            ytop=1.36 if style=='ponytail' else 1.33 if style=='sidepony' else 1.26
            def tail(p):
                if style=='sidepony':
                    return b.loft(p,[(.93,.13,-.155,.014,.021),(.98,.14,-.170,.035,.032),(1.08,.15,-.178,.045,.041),(1.20,.14,-.154,.047,.045),(1.28,.13,-.103,.047,.043),(1.33,.10,-.055,.037,.038)])
                wave=.012*np.sin(p[...,1]*42) if style=='braid' else .010*np.sin(p[...,1]*17)
                q=p.copy();q[...,0]-=side+wave
                return b.loft(q,[(.93,0,-.155,.014,.021),(.98,0,-.170,.035,.032),(1.08,0,-.178,.045,.041),(1.20,0,-.168,.044,.045),(ytop,0,-.131,.034,.039)])
            primitive(parts,b.HAIR,'head',tail,((-.09,.22),(.91,ytop+.03),(-.24,-.07)),1600,.004)
            disk(parts,b.BROW,'head',(.125,1.278,-.106) if style=='sidepony' else (side,ytop-.013,-.153),(.039,.014,.043),220)
    if 'glasses' in kind or style=='spectacles':
        primitive(parts,b.BROW|s.NO_SHELL,'head',s.npc.glasses,((-.15,.15),(1.29,1.38),(-.005,.16)),1200,.0023)
    if 'ribbon' in kind or 'headband' in kind:
        line(parts,s.BOW,'head',[(-.124,1.36,-.033),(-.100,1.46,-.033),(0,1.508,-.033),(.10,1.46,-.033),(.126,1.36,-.033)],.006)
        if 'ribbon' in kind:
            for sign in (-1,1):disk(parts,s.BOW,'head',(-.104+sign*.023,1.445,.015),(.029,.018,.012),220)
    if 'headphones' in kind:
        line(parts,b.BROW,'head',[(-.138,1.31,-.014),(-.142,1.43,-.014),(0,1.516,-.014),(.142,1.43,-.014),(.138,1.31,-.014)],.012)
        for sign in (-1,1):disk(parts,b.BROW,'head',(sign*.139,1.319,-.007),(.024,.040,.035),450)
    if style in ('officer','worker','cap'):
        disk(parts,b.HAIR,'head',(0,1.435,-.025),(.143,.092,.133),1400)
        disk(parts,b.HAIR,'head',(0,1.418,.086),(.117,.013,.078),650)
    return parts

def common_body():
    out=[];s.skin(out)
    primitive(out,b.TOP,'torso',s.shirt_body,s.BODY,2600)
    primitive(out,b.TOP,'sleeve',lambda p:s.sleeves(p,True),s.BODY,2400)
    return out
def collar(out,bow=False,mat=None,lift=.009):
    material=b.CREAM if mat is None else mat
    for sign in (-1,1):panel_patch(out,material|s.NO_SHELL,'torso',[(sign*x,y) for x,y in ((.015,1.131),(.049,1.127),(.076,1.071),(.040,1.055),(.009,1.091))],lambda y:np.interp(y,[1.039,1.085,1.131],[.137,.127,.075]),.004,.003,500,lift=lift)
    if bow:
        for sign in (-1,1):panel_patch(out,b.TIE|s.NO_SHELL,'torso',[(sign*x,y) for x,y in ((.006,1.080),(.050,1.102),(.059,1.061),(.008,1.067))],lambda y:np.zeros_like(y)+.137,.004,.003,350,lift=lift+.006)
        disk(out,b.TIE,'torso',(0,1.077,.142),(.012,.014,.006),180,lift=lift+.014)
        for sign in (-1,1):panel_patch(out,b.TIE|s.NO_SHELL,'torso',[(sign*x,y) for x,y in ((.004,1.069),(.017,1.070),(.034,.995),(.009,1.007))],lambda y:np.zeros_like(y)+.131,.003,.002,250,lift=lift+.006)
def buttons(out,mat=b.TIE):
    for y in (.77,.85,.93,1.01):disk(out,mat|s.NO_SHELL,'torso',(0,y,.113),(.0065,.0065,.003),100)
def badge(out):
    line(out,b.TIE,'torso',[(-.035,1.11,.086),(-.05,1.02,.125),(0,.905,.125),(.05,1.02,.125),(.035,1.11,.086)],.003)
    panel_patch(out,b.CREAM|s.NO_SHELL,'torso',[(-.019,.864),(.019,.864),(.019,.913),(-.019,.913)],lambda y:np.zeros_like(y)+.136,.002)
    line(out,b.TIE,'torso',[(-.012,.890,.143),(.012,.890,.143)],.003)
def scarf(out):
    for sign in (-1,1):panel_patch(out,b.TIE|s.NO_SHELL,'torso',[(sign*x,y) for x,y in ((.015,1.125),(.050,1.115),(.083,1.01),(.046,1.017),(.008,1.086))],lambda y:np.interp(y,[1.01,1.08,1.13],[.144,.124,.070]),.004,.003,450,lift=.024)
    line(out,b.CREAM,'torso',[(-.036,1.11,.095),(-.043,1.09,.125),(-.062,1.035,.150)],.002,lift=.028)
def pocket(out,side=-1,mat=b.TOP,y=.81):
    panel_patch(out,mat|s.NO_SHELL,'torso',[(side*x,yy) for x,yy in ((.065,y),(.150,y),(.146,y-.070),(.075,y-.072))],lambda yy:np.zeros_like(yy)+.130,.003,.003,350)
    line(out,s.INK,'torso',[(side*.064,y,.138),(side*.15,y,.138)],.002)

def coat_hem():
    # 敞开的连续外衣，不用三张互不相接的平片围着腿。
    rows,cols=30,70;verts=[];normals=[];faces=[]
    for side in (1,-1):
        offset=len(verts)
        for y in np.linspace(.43,.71,rows):
            t=(y-.43)/.28;rx=.211-.038*t;rz=.161-.055*t
            for a in np.linspace(.22,2*np.pi-.22,cols):
                n=np.array([np.sin(a)/rx,.11,np.cos(a)/rz]);n/=np.linalg.norm(n)
                p=np.array([rx*np.sin(a),y,rz*np.cos(a)])
                verts.append(p-(.004 if side<0 else 0)*n);normals.append(n*side)
        for row in range(rows-1):
            for col in range(cols-1):
                a=offset+row*cols+col;c=a+cols
                faces.extend([(a,a+1,c),(a+1,c+1,c)] if side>0 else [(a,c,a+1),(a+1,c,c+1)])
    return np.asarray(verts),np.asarray(normals),np.asarray(faces,np.int32)
def hood(out):
    def shape(p):
        shell=b.loft(p,[(1.02,0,-.064,.099,.075),(1.10,0,-.077,.105,.082),(1.18,0,-.075,.081,.067),(1.205,0,-.066,.039,.036)])
        inside=b.ellipsoid(p,(0,1.179,-.060),(.060,.052,.062));return np.maximum(shell,-inside)
    primitive(out,b.TRIM,'torso',shape,((-.13,.13),(1.,1.22),(-.18,.02)),1100,.0038)
    for sign in (-1,1):line(out,b.CREAM,'torso',[(sign*.055,1.103,.063),(sign*.071,1.04,.125),(sign*.063,1.0,.13)],.003)

def fitted_jacket(out,jacket):
    # 保留母版躯干和袖型；泛用款的贴袋、领片重新沿实际布面生成。
    append(out,[jacket[i] for i in (0,1,17)])
    panel_patch(out,b.CREAM|s.NO_SHELL,'torso',[(-.055,.66),(.055,.66),(.057,1.105),(.025,1.086),(-.025,1.086),(-.057,1.105)],lambda y:np.zeros_like(y),lift=.001)
    for sign in (-1,1):
        points=[(sign*x,y) for x,y in ((.045,1.126),(.077,1.108),(.118,1.037),(.083,1.050),(.057,1.062),(.045,1.092))]
        panel_patch(out,b.TOP|s.NO_SHELL,'torso',points,lambda y:np.zeros_like(y),lift=.009)
        line(out,s.INK,'torso',[(x,y,.12) for x,y in points[:-1]],.002,lift=.013)
        pocket(out,sign,b.TOP,.80)
        line(out,s.INK,'torso',[(sign*.050,.665,.109),(sign*.049,.85,.118),(sign*.052,1.038,.129)],.002)

def top(cut):
    jacket=cache('approved-jacket-v1',lambda:collect(s.jacket))['PaintedJacket']
    baker=cache('approved-baker-v1',lambda:collect(s.baker))['NpcTop-chef']
    out=[]
    if cut in ('jacket','workjacket','workbadge','blazer','blazerbow','blazerscarf','floralblazer','labcoat','formal'):
        s.skin(out);fitted_jacket(out,jacket)
        if cut in ('blazer','blazerbow','blazerscarf','floralblazer','labcoat','formal'):
            collar(out,cut=='blazerbow',lift=.016)
            # 西装扣与内搭露出的 V 领，而非另套一个旧的人体网格。
            disk(out,b.TIE|s.NO_SHELL,'torso',(.043,.842,.125),(.009,.009,.005),180)
        if cut=='workbadge':badge(out)
        if cut=='workjacket':pocket(out,-1,b.TOP,.99)
        if cut=='blazerscarf':scarf(out)
        if cut=='formal':panel_patch(out,b.TIE|s.NO_SHELL,'torso',[(-.011,1.09),(.011,1.09),(.020,.89),(0,.87),(-.020,.89)],lambda y:np.zeros_like(y)+.140,.003,.002,500)
        if cut=='labcoat':
            out.append((b.TOP|s.NO_SHELL,'skirt',coat_hem()))
            pocket(out,-1,b.TOP,.97)
            for sign in (-1,1):line(out,b.BROW,'torso',[(sign*.044,1.117,.079),(sign*.079,1.05,.138),(sign*.071,.965,.147),(0,.940,.149)],.0035,lift=.024)
            disk(out,b.TIE,'torso',(0,.934,.15),(.017,.017,.005),250)
            badge(out)
        return out
    if cut in ('apron','apronbow','waistapron'):
        append(out,cache('common-body-v1',common_body));collar(out,cut=='apronbow')
        # 围裙与肩带直接使用已验收面包师的连续衣片。
        for mat,family,mesh in copy.deepcopy(baker):
            if mat&63 not in (b.TRIM,b.CREAM,s.INK):continue
            if family=='sleeve':continue
            if mesh[0][:,1].min()>1.02:continue
            if cut=='waistapron' and mesh[0][:,1].max()>.81:continue
            if family=='apron':mesh[0][:,2]+=.022*np.clip((.91-mesh[0][:,1])/.17,0,1)
            out.append((mat,family,mesh))
        if cut=='waistapron':
            mesh=s.curved_panel(lambda x,y:.15-.12*(x/.24)**2,lambda y:.196,.414,.765);out.append((b.CREAM,'apron',mesh))
        return out
    append(out,cache('common-body-v1',common_body))
    if cut=='landlady':
        panel_patch(out,b.CREAM|s.NO_SHELL,'torso',[(-.033,.67),(.033,.67),(.037,1.08),(-.037,1.08)],lambda y:np.interp(y,[.67,.86,1.08],[.127,.137,.129]),.003,.002)
        for sign in (-1,1):
            panel_patch(out,b.CREAM|s.NO_SHELL,'torso',[(sign*x,y) for x,y in ((.013,1.129),(.058,1.120),(.093,1.078),(.087,1.051),(.071,1.043),(.045,1.047),(.017,1.085))],lambda y:np.interp(y,[1.04,1.08,1.13],[.143,.129,.073]),.004,.003,lift=.009)
        line(out,b.CREAM,'torso',[(-.031,1.11,.102),(-.032,1.07,.141),(0,1.028,.146),(.032,1.07,.141),(.031,1.11,.102)],.002)
        disk(out,b.TIE|s.NO_SHELL,'torso',(0,1.018,.151),(.012,.022,.005),280)
        for y in (.77,.85,.93):disk(out,b.CREAM|s.NO_SHELL,'torso',(.043,y,.130),(.008,.008,.004),140)
        return out
    if cut in ('shirt','polo','vest','vestbow','vestscarf','vestbadge','utilityvest','knitvest','formal','cardigan','floral','flowerblouse','bowblouse'):
        collar(out,cut in ('vestbow','cardigan','floral','flowerblouse','bowblouse'))
        buttons(out)
    if cut in ('hoodie','windbreaker'):hood(out)
    if cut in ('hoodie','windbreaker','track','patrol'):
        line(out,b.CREAM,'torso',[(0,.67,.125),(0,.90,.133),(0,1.08,.124)],.003)
        disk(out,b.TIE,'torso',(0,1.035,.117),(.005,.015,.004),120)
    if cut in ('windbreaker','track','patrol'):
        panel_patch(out,(b.TIE if cut=='windbreaker' else b.CREAM)|s.NO_SHELL,'torso',[(-.15,1.013),(.15,1.013),(.152,.972),(-.152,.972)],lambda y:np.zeros_like(y)+.132,.003,.003,700)
        for sign in (-1,1):line(out,b.TIE if cut=='windbreaker' else b.CREAM,'sleeve',[(sign*.16,1.065,.061),(sign*.200,.96,.070),(sign*.238,.80,.067),(sign*.244,.71,.064)],.005)
        pocket(out,-1,b.TOP,.83);pocket(out,1,b.TOP,.83)
    if cut=='patrol':badge(out)
    if cut in ('cardigan','floral','floralblazer'):
        # 开衫中间的内搭，保留全宽暖色衣袖。
        panel_patch(out,b.CREAM|s.NO_SHELL,'torso',[(-.033,.67),(.033,.67),(.037,1.08),(-.037,1.08)],lambda y:np.interp(y,[.67,.86,1.08],[.127,.137,.129]),.003,.002,900)
        for y in (.77,.85,.93,1.01):disk(out,b.TIE|s.NO_SHELL,'torso',(.042,y,.121),(.006,.006,.004),120)
    if cut in ('vest','vestbow','vestscarf','vestbadge','utilityvest','knitvest','formal'):
        # 袖子改为内搭色；背心拥有实际的袖窿和 V 形开口。
        out=[((b.CREAM if family=='sleeve' and mat==b.TOP else mat),family,mesh) for mat,family,mesh in out]
        def opening(p):
            shell=s.shirt_body(p)
            radius=np.interp(p[...,1],[.95,1.115],[.006,.070])
            return np.maximum(shell,-np.maximum(np.abs(p[...,0])-radius,.03-p[...,2]))
        out=[p for p in out if not(p[0]==b.TOP and p[1]=='torso')]
        primitive(out,b.TOP,'torso',opening,s.BODY,2400)
        panel_patch(out,b.CREAM|s.NO_SHELL,'torso',[(-.065,1.12),(.065,1.12),(.004,.94),(-.004,.94)],lambda y:np.interp(y,[.94,1.12],[.116,.070]),.005,.003,500)
        if cut=='utilityvest':
            for y in (.80,.98):pocket(out,-1,b.TOP,y);pocket(out,1,b.TOP,y)
        if cut=='formal':
            # 有袖西装：袖子采用外套主色；中间领带在浅色衬衫上。
            out=[((b.TOP if family=='sleeve' and mat==b.CREAM else mat),family,mesh) for mat,family,mesh in out]
            panel_patch(out,b.TIE|s.NO_SHELL,'torso',[(-.011,1.09),(.011,1.09),(.020,.89),(0,.87),(-.020,.89)],lambda y:np.zeros_like(y)+.13,.003,.002,500)
        if cut=='vestscarf':scarf(out)
        if cut=='vestbadge':badge(out)
    if cut=='polo':
        # 短袖以下保留母版的完整手与前臂。
        out=[p for p in out if not(p[1]=='sleeve' or p[0]==b.SKIN and p[1]=='arm')]
        primitive(out,b.TOP,'sleeve',lambda p:np.maximum(s.sleeves(p,True),.925-p[...,1]),s.BODY,1500)
        primitive(out,b.SKIN,'arm',lambda p:np.maximum(b.mix_surface(np.maximum(b.bare_arms(p),.655-p[...,1]),s.hands(p),.01),p[...,1]-.953),((-.315,.315),(.535,.98),(-.096,.102)),2200)
    return out

def bottom(kind):
    if kind=='pants':return cache('approved-bottom-v1',lambda:collect(s.baker))['NpcBottom-chef']
    out=[]
    if kind in ('skirt','longskirt','pencilskirt'):
        mesh=s.pleated_skirt_mesh()
        if kind=='pencilskirt':
            v,n,f=mesh;t=np.clip((.70-v[:,1])/.324,0,1);sx=1-.26*t;sz=1-.20*t
            v[:,0]*=sx;v[:,2]*=sz;n[:,0]/=sx;n[:,2]/=sz;n/=np.maximum(np.linalg.norm(n,axis=1)[:,None],1e-9)
        if kind=='longskirt':
            v,n,f=mesh;t=np.clip((.70-v[:,1])/.324,0,1);v[:,1]-=.16*t;v[:,0]*=1-.035*t
        # 泛用上衣与歌手专用短上衣的腰口不同；普通裙腰藏入衣摆，不能顶穿背心。
        v,n,f=mesh;t=np.clip((v[:,1]-.54)/.157,0,1);v[:,1]-=.034*t
        n[:,1]/=1-.034/.157*(t>0);n/=np.maximum(np.linalg.norm(n,axis=1)[:,None],1e-9)
        out.append((b.PANTS|s.NO_SHELL,'skirt',mesh))
        primitive(out,b.SKIN|s.NO_SHELL,'pants',lambda p:b.loft(b.mirror_x(p),[(.065,.104,0,.039,.038),(.14,.104,0,.044,.043),(.29,.104,0,.053,.05),(.47,.096,0,.053,.057),(.63,.078,0,.055,.062),(.67,.072,0,.054,.061)]),s.w.LEGS,2600)
    else:
        def shorts(p):return np.maximum(s.w.b.top_field(p,'short'),0) # 未调用；短裤从正式裤腿裁切。
        parts=copy.deepcopy(cache('approved-bottom-v1',lambda:collect(s.baker))['NpcBottom-chef'])
        for mat,family,mesh in parts:
            if family!='pants':continue
            # 截短原裤腿并保留厚实裤口，膝盖以下为真实皮肤。
            v,n,f=mesh;v[:,1]=np.maximum(v[:,1],.39);out.append((mat,family,(v,n,f)))
        primitive(out,b.SKIN,'calf',lambda p:b.loft(b.mirror_x(p),[(.065,.104,0,.042,.039),(.14,.104,0,.047,.044),(.29,.104,0,.054,.05),(.43,.104,0,.060,.058)]),s.w.LEGS,2200)
    s.shoes(out);return out

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--kind',choices=['heads','tops','bottoms','all'],default='all');parser.add_argument('--names',nargs='*');a=parser.parse_args()
    data=json.loads((OUT/'NpcArtCatalog.json').read_text(encoding='utf-8'))['profiles']
    for group,key,fn,prefix in [('heads','head',head,'NpcHead-'),('tops','cut',top,'NpcTop-'),('bottoms','bottom',bottom,'NpcBottom-')]:
        if a.kind not in ('all',group):continue
        values=sorted({p[key] for p in data if p[key].startswith('painted_')})
        # 所有无名顾客也仅取本批重新制作的结构。
        if group=='heads':values=sorted(set(values)|{'painted_spectacles','painted_bob','painted_tousled','painted_swept','painted_crop','painted_senior','painted_bun'})
        for value in values:
            name=value.removeprefix('painted_')
            if a.names and name not in a.names:continue
            target=OUT/('PlayerB-'+prefix+value+'.bytes')
            staging=CACHE/(target.name+'.staging')
            s.write_sample(staging,fn(name))
            # 先写完整模型再替换，避免 Unity 自动导入期间读取半个文件。
            staging.replace(target)
if __name__=='__main__':main()
