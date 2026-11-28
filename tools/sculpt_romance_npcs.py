"""私服人物的离线雕刻。与母版共用 PBM2、手、表情曲面与关节绑定。"""
import numpy as np
from scipy.interpolate import PchipInterpolator

import sculpt_character_samples as s

b = s.b
TAU = 2 * np.pi
NO = s.NO_SHELL
IVORY = 15


def rings(points, centers):
    """以环中心校正绕序，闭合两端，按面积加权求平滑法线。"""
    rows, cols, _ = points.shape
    i, j = np.indices((rows - 1, cols))
    a = i * cols + j
    c = i * cols + (j + 1) % cols
    faces = np.concatenate((np.stack((a,c,a+cols),-1).reshape(-1,3),
                            np.stack((c,c+cols,a+cols),-1).reshape(-1,3)))
    verts = points.reshape(-1,3)
    corners = verts[faces]
    outward = corners.mean(1) - centers[(faces // cols).min(1)]
    if np.sum(np.cross(corners[:,1]-corners[:,0], corners[:,2]-corners[:,0]) * outward) < 0:
        faces = faces[:, ::-1]
    # 端盖与外侧共享位置，各用自己的法线。
    normals = mesh_normals(verts, faces)
    end_vertices, end_normals, end_faces = [], [], []
    for row, other in ((0, 1), (-1, -2)):
        direction = centers[row] - centers[other]
        direction /= max(np.linalg.norm(direction), 1e-9)
        idx = len(verts) + len(end_vertices)
        end_vertices.extend([centers[row], *points[row]])
        end_normals.extend([direction] * (cols+1))
        for k in range(cols):
            q = (k+1) % cols
            winding = np.dot(np.cross(points[row,k]-centers[row],points[row,q]-centers[row]), direction)
            end_faces.append((idx,idx+1+k,idx+1+q) if winding >= 0 else (idx,idx+1+q,idx+1+k))
    return (np.vstack((verts,end_vertices)), np.vstack((normals,end_normals)),
            np.vstack((faces,end_faces)).astype(np.int32))


def mesh_normals(v, f):
    triangles = v[f]
    fn = np.cross(triangles[:,1]-triangles[:,0], triangles[:,2]-triangles[:,0])
    n = np.zeros_like(v)
    for k in range(3):
        np.add.at(n, f[:,k], fn)
    return n / np.maximum(np.linalg.norm(n,axis=1,keepdims=True),1e-10)


def mirror_pieces(pieces):
    # 原稿按观者左右对照。镜像须同时翻法线与面序，蒙皮在最终位置重新计算。
    for _,_,(v,n,f) in pieces:
        v[:,0]*=-1
        n[:,0]*=-1
        f[:]=f[:,::-1].copy()
    return pieces


def tube(out, mat, family, points, widths, depths, guide=(0,0,1)):
    """沿发束、布带或饰条铺设收尖的椭圆截面，背面有真实厚度。"""
    path = PchipInterpolator(np.linspace(0,1,len(points)),points)
    t = np.linspace(0,1,21)
    center = path(t)
    tangent = np.gradient(center,axis=0)
    tangent /= np.maximum(np.linalg.norm(tangent,axis=1,keepdims=True),1e-9)
    across = np.cross(tangent,guide)
    across /= np.maximum(np.linalg.norm(across,axis=1,keepdims=True),1e-9)
    deep = np.cross(across,tangent)
    angles = np.arange(12)*TAU/12
    w = np.interp(t,np.linspace(0,1,len(widths)),widths)
    d = np.interp(t,np.linspace(0,1,len(depths)),depths)
    vertices = (center[:,None,:] + (w[:,None]*np.cos(angles))[...,None]*across[:,None,:]
                + (d[:,None]*np.sin(angles))[...,None]*deep[:,None,:])
    out.append((mat|NO,family,rings(vertices,center)))


# 底发型、额发分缝、发梢长度、眼睛纵向比例、下颌宽度、微笑幅度。
HEADS = {
    'moto': ('tousled',-.045,.070,.90,.98,.8), 'street': ('tousled',.026,.060,1.04,1.,1.4),
    'student': ('bob',-.024,.060,.80,.97,.65), 'badminton': ('sidepony_glasses',.027,.074,1.05,.99,1.2),
    'ivy': ('spectacles',-.058,.052,.90,.96,.80), 'bonnet': ('curly',.058,.069,1.07,.96,.95),
    'ribbon': ('waves',.055,.080,.91,.96,.82), 'wolf': ('longfringe',-.060,.087,.80,.95,.55),
    'printbun': ('swept',.036,.074,1.09,1.02,1.), 'vinyl': ('waves',.073,.081,.91,1.01,1.20),
    'orchid': ('swept',-.012,.068,1.02,.98,.88), 'map': ('swept',.039,.065,.94,.96,1.05),
    'plush': ('swept',-.036,.077,1.12,1.04,1.6), 'runner': ('crop',.015,.035,.98,1.06,1.5),
    'racer': ('shortfemale',.080,.076,.94,1.,1.65), 'streamer': ('tousled',.014,.074,.89,.96,.85),
    'critic': ('waves',.059,.076,.91,.96,.85), 'kaiju': ('tousled',-.027,.049,1.02,1.06,1.5),
    'camera': ('bob',.075,.082,.85,.95,.86), 'pixel': ('curlymale_glasses',-.028,.071,1.06,1.02,1.25),
    'golfer': ('curlymale',.009,.068,1.10,1.02,1.45), 'driver': ('ponytail',.055,.069,.88,.96,.9),
    'twist': ('swept',.048,.067,.94,.97,1.06)
}


def coiffure(out, name, part, fringe):
    """从偏分发旋连续铺到后脑和发梢，刘海、自由下摆及内衬共用弧长参数。"""
    a = np.arange(80)*TAU/80
    angle = (a + np.pi) % TAU - np.pi
    front = np.cos(a)
    rear = np.clip((.53-front)/.61,0,1)
    rear = rear*rear*(3-2*rear)
    bob = {'student':1.201,'camera':1.204,'bonnet':1.177,'racer':1.223}
    long = {'critic':.993,'ribbon':.97,'vinyl':1.091,'wolf':1.115}
    curly = name in ('golfer','orchid','pixel','bonnet','vinyl','street','kaiju')
    tied = name in ('twist','plush','printbun','driver','badminton')
    short = name not in bob and name not in long
    waves = 7 if curly else 5
    sweep = -.66 if part<0 else .64
    if name=='racer': sweep=-.85
    yaw = np.arcsin(np.clip(part/.135,-.75,.75))
    peak = 1.442 if tied or name in ('map','runner','driver') else 1.415
    hairline = peak-.046*abs(np.sin(angle-yaw))-.013*(1-np.cos(waves*(angle-yaw)))**.85
    hairline += (.070-fringe)*.8
    if name=='runner':
        hairline=1.445-.026*np.sin(angle)**2+.004*np.cos(17*angle)
    elif name in ('racer','camera','driver'):
        hairline=1.408+.037*np.sin(angle+.26)-.012*np.cos(3*angle+.6)
    if name in ('moto','pixel','golfer','street','streamer','kaiju'):
        hairline -= .012*np.cos(angle*7+.8)
    hem = long.get(name,bob.get(name,1.251 if tied else 1.247))
    tip = hairline*(1-rear)+(hem + .012*np.cos(5*a+.4)+.009*np.sin(3*a))*rear
    if name in ('camera','racer'):
        tip += .086*rear*np.maximum(0,np.sin(a))
    if name=='wolf':
        tip += .095*rear*abs(np.sin(a))**2
    # 每列沿外壳跟随发丝梳向；过耳后的长发再向颈、肩收拢。
    tgrid=np.linspace(.001,1,33)
    points=[]
    for t in tgrid:
        sphere_y=np.maximum(1.316,tip)
        th=np.arccos(np.clip((sphere_y-1.36)/.145,-1,.999))
        split=1-.23*np.clip((sphere_y-tip)/.085,0,1)
        u=np.minimum(1,t/split)
        phase=angle+sweep*(1-u)**1.5
        theta=th*(t if short else u)
        if short:
            theta=np.arccos(np.clip((np.maximum(1.238,tip)-1.36)/.145,-.99,.99))*t
        ripple=(.004+.003*curly)*np.sin(phase*waves+u*1.8)**2*np.sin(theta)**1.5
        x=(.135+ripple)*np.sin(theta)*np.sin(phase) + part*.64*(1-u)**2
        y=1.36+(.139 if name=='runner' else .145)*np.cos(theta)
        z=-.025+(.124+ripple)*np.sin(theta)*np.cos(phase)
        if not short:
            drop=np.clip((t-split)/np.maximum(1-split,.0001),0,1)
            y-=drop*np.maximum(0,sphere_y-tip)
            x*=1+drop*((.10 if curly else -.04)*rear+.063*np.cos(4*a+drop*2)*rear)
            z-=drop*rear*(.037 if name in long else .010)
            z+=drop*.009*rear*np.cos(5*a+drop)
        points.append(np.stack((x,y,z),-1))
    shell=np.array(points)
    # 面朝头部的薄内壁随外缘返回，使肩部与脸侧的发梢从背面也完整。
    inner=shell[[-1,-3,-6,-10,-18,0]].copy()
    inner[:,:,0]*=.91
    inner[:,:,2]=-.025+(inner[:,:,2]+.025)*.91
    inner[:,:,1]-=.002
    grid=np.concatenate((shell,inner))
    rr,cc=np.indices((len(grid)-1,len(a)))
    i=rr*len(a)+cc;j=rr*len(a)+(cc+1)%len(a)
    faces=np.concatenate((np.stack((i,i+len(a),j),-1).reshape(-1,3),
                          np.stack((j,i+len(a),j+len(a)),-1).reshape(-1,3)))
    v=grid.reshape(-1,3)
    corners=v[faces[:(len(tgrid)-1)*len(a)]]
    if np.sum(np.cross(corners[:,1]-corners[:,0],corners[:,2]-corners[:,0])*(corners.mean(1)-[0,1.36,-.025]))<0:
        faces=faces[:,::-1]
    out.append((b.HAIR,'head',(v,mesh_normals(v,faces),faces)))
    # 体积发绺顺外壳走；末端尖收，不从头顶垂下相同的一排叶片。
    for i,phi in enumerate((-.98,-.58,-.27,.31,.64,1.03,1.61,2.03,2.62,3.22,3.79,4.29,4.72)):
        if name=='runner':
            break
        branch=1 if np.sin(phi-yaw)>0 else -1
        path=[]
        for t in (.19,.38,.57,.77,.91,.986):
            local=phi-.39*branch*(1-t)**1.3+.07*np.sin(t*4+i)
            row=int(t*(len(tgrid)-1))
            k=int((local%TAU)/TAU*len(a))%len(a)
            pt=shell[row,k].copy()
            radial=np.array([np.sin(local),.32,np.cos(local)])
            pt+=radial*(.003+.002*np.sin(t*np.pi))
            if t>.98:
                pt[0]+=.010*branch
                pt[1]-=.010 if (i%3!=0 or tied) else .019
            path.append(pt)
        # 宽窄相间，亮发束贴在暗底上，与额前的分缝相呼应。
        guide=(np.sin(phi),.18,np.cos(phi))
        width=.013 if i%3==0 else .021
        tube(out,s.HAIR_LIGHT if i in (1,4,8) else b.HAIR,'head',path,
             [.002,width,width*.86,width*.75,.012,.0008],[.001,.005,.006,.006,.004,.001],guide)
    # 外轮廓上的发梢要离开发帽，才能从游戏的斜俯视角读出卷、翘和侧扫。
    tousled=name in ('moto','kaiju','street','streamer','ivy')
    if tousled or curly or name=='racer':
        for side in (-1,1):
            for j,(ax,ay,az) in enumerate(((.025,1.485,.027),(.077,1.466,.042),(.101,1.431,.029))):
                if name=='racer' and side==1 and j>0:
                    continue
                if name in ('bonnet','vinyl') and j==0:
                    continue
                lift=.012 if tousled else -.006
                reach=(.071,.060,.044)[j] if tousled or name=='racer' else (.081,.056,.045)[j]
                offset=.008*side*np.sin(j*3+part*22)
                pts=[(side*(ax-.016),ay-.003,az-.017),(side*(ax+.008),ay+.012,az+.017),
                     (side*(ax+reach*.66),ay+.009+lift,az+.020),
                     (side*(ax+reach),ay-.009+lift+offset,az+.001)]
                tube(out,b.HAIR,'head',pts,[.006,.018,.011,.0008],[.004,.009,.007,.0008])
        if name not in ('bonnet','vinyl'):
            for side in (-1,1):
                shift=.018 if name=='racer' and side==1 else 0
                tube(out,b.HAIR,'head',[(side*.022,1.478,.064),(side*.064,1.457,.084),
                     (side*.082,1.418+shift,.097),(side*(.070 if curly else .104),1.382+shift,.088)],
                     [.009,.025,.016,.001],[.004,.014,.010,.001])
    if name=='runner':
        for phi in np.linspace(-2.8,2.8,17):
            sx,cz=np.sin(phi),np.cos(phi)
            tube(out,b.HAIR,'head',[(.078*sx,1.463,-.025+.074*cz),
                 (.080*sx,1.479,-.025+.077*cz),(.087*sx-.010,1.500+.005*cz,-.025+.081*cz)],
                 [.016,.012,.0008],[.009,.009,.0008],(sx,.15,cz))


def open_smile(out,wide):
    extent=.029*wide
    def band(mat,x,base,top,lift):
        points=np.array([(xx,yy,s.front(xx,yy)+lift) for row in (base,top) for xx,yy in zip(x,row)])
        idx=np.arange(len(x)-1)
        faces=np.vstack((np.stack((idx,idx+1,idx+len(x)),-1),np.stack((idx+1,idx+1+len(x),idx+len(x)),-1)))
        out.append((mat|NO,'head',(points,mesh_normals(points,faces),faces)))
    x=np.linspace(-extent,extent,23)
    upper=1.266+.006*(x/extent)**2
    band(b.BROW,x,upper-(.010+.004*wide)*(1-(x/extent)**2),upper,.003)
    if wide>=1.4:
        x=x[2:-2]
        upper=1.265+.006*(x/extent)**2
        band(IVORY,x,upper-.006*(1-(x/extent)**2),upper,.0045)


def head(p, name):
    base, part, length, eyes, jaw, smile = HEADS[name]
    out = [piece for piece in p.head(base) if not (
        (piece[0] & 63) == b.HAIR and
        (base not in ('ponytail','sidepony_glasses') or piece[2][0][:,1].max()>1.39))]
    if smile>=1.2:
        out=[piece for piece in out if not (piece[0]==b.LIP and piece[2][0][:,1].mean()<1.28)]
    for mat, family, (v,n,f) in out:
        if family != 'head':
            continue
        paint = mat & 63
        if paint in (b.SKIN,b.LIP,b.EYE,b.BROW):
            v[:,0] *= 1 + (jaw-1)*np.clip((1.335-v[:,1])/.11,0,1)
            if paint == b.EYE:
                v[:,1] = 1.330 + (v[:,1]-1.330)*eyes
            if paint == b.BROW and np.ptp(v[:,0])<.08 and v[:,1].min()>1.349:
                # 体育生的平浓眉、赛车手的单侧眉峰和小圆眼的弧眉都贴着额头曲率。
                before=v[:,1].copy()
                t=(abs(v[:,0])-.050)/.025
                if name in ('runner','kaiju','moto','wolf','camera'):
                    v[:,1]+=.007*t
                if name in ('racer','driver','twist','vinyl','critic'):
                    v[:,1]+=.008*np.maximum(0,1-abs(t-.35))*(1 if v[:,0].mean()>0 else .3)
                if name in ('plush','golfer','street','pixel'):
                    v[:,1]+=.005*(1-t*t)
                bx,by=v[:,0].mean(),before.mean()
                slope=(s.front(bx,by+.003)-s.front(bx,by-.003))/.006
                v[:,2]+=(v[:,1]-before)*slope
            if paint == b.LIP and v[:,1].mean() < 1.28:
                v[:,1] += (smile-1)*.012*(np.abs(v[:,0])/.032)**1.8
            n[:] = mesh_normals(v,f)
        # 背发按实际头像截短或削去一侧，和侧面轮廓一起调整。
        if paint == b.HAIR and v[:,1].min() < 1.18:
            if name == 'vinyl':
                v[:,1] += .17*np.clip((1.31-v[:,1])/.37,0,1)
            if name == 'wolf':
                v[:,0] *= 1-.40*np.clip((1.30-v[:,1])/.33,0,1)
            if name in ('racer','camera'):
                v[:,1] += .056*np.clip((v[:,0]+.02)/.12,0,1)*np.clip((1.36-v[:,1])/.16,0,1)
            n[:] = mesh_normals(v,f)
    if smile>=1.2:
        open_smile(out,smile)
    coiffure(out, name, part, length)
    if name in ('twist','plush','printbun'):
        x,y,z,rx,ry,rz = {'twist':(.012,1.437,-.117,.075,.072,.063),
            'plush':(0,1.479,-.088,.068,.059,.057),'printbun':(.091,1.285,-.113,.050,.049,.047)}[name]
        p.disk(out,b.HAIR,'head',(x,y,z),(rx,ry,rz),850)
        for side in (-1,1):
            tube(out,b.HAIR,'head',[(side*.088,1.412,.047),(side*.122,1.35,.035),
                (side*.115,1.286,.047),(side*.099,1.247,.042)],[.009,.014,.009,.001],[.005,.008,.006,.001])
        if name=='printbun':
            for side in (-1,1):
                p.disk(out,b.TIE,'head',(x+side*.019,y-.012,z-.035),(.023,.014,.012))
            tube(out,b.TIE,'head',[(x,y-.011,z-.036),(x+.016,y-.039,z-.044),
                (x+.017,y-.074,z-.035)],[.007,.014,.006],[.004]*3)
        elif name=='twist':
            tube(out,b.SHOE,'head',[(-.056,1.401,-.148),(.011,1.438,-.174),(.091,1.466,-.151)],
                [.003,.006,.006],[.004]*3)
            for sign in (-1,1):
                p.disk(out,b.CREAM,'head',(sign*.126,1.279,.012),(.007,.014,.007))
        else:
            for sign in (-1,1):
                for i in range(6):
                    p.disk(out,b.TIE if i%2 else b.HAIR,'head',(sign*(.122+.003*np.cos(i)),1.311-i*.012,.022),(.009,.011,.008))
            p.disk(out,b.CREAM,'head',(-.108,1.406,.052),(.019,.009,.006))
    if name in ('moto','camera','wolf','racer'):
        sign = -1 if name=='wolf' else 1
        length = .13 if name=='wolf' else .077
        tube(out,b.TIE if name!='racer' else b.HAIR,'head',[(sign*.106,1.418,.026),
            (sign*.129,1.367,.018),(sign*.133,1.325,-.008),(sign*.118,1.325-length,-.052)],
            [.003,.012,.014,.002],[.003,.006,.007,.002])
        if name=='racer':
            for i in range(7):
                p.disk(out,b.HAIR,'head',(sign*(.116+.003*np.sin(i)),1.428-i*.014,.012-i*.008),(.010,.014,.012))
    if name in ('racer','vinyl','student'):
        for sign in (-1,1):
            if name=='racer':
                ring=np.linspace(0,TAU,17)
                tube(out,IVORY,'head',np.column_stack((sign*(.126+.008*np.sin(ring)),
                     1.281+.014*np.cos(ring),.021+.004*np.sin(ring))),[.0025]*2,[.0025]*2)
            else:
                p.disk(out,b.CREAM,'head',(sign*.127,1.286,.014),(.009,.013,.005))
    if name=='ribbon':
        for sign in (-1,1):
            p.disk(out,b.TIE,'head',(.122,1.353+sign*.018,-.012),(.026,.022,.015))
            tube(out,b.TIE,'head',[(.123,1.353,-.016),(.140+sign*.012,1.309,-.02),
                  (.133+sign*.013,1.253,-.031)],[.006,.012,.009],[.003,.004,.003])
    if name in ('critic','ribbon','vinyl','bonnet','badminton'):
        # 长卷落在一侧肩前，正面保留胸口衣领，后片仍独立包住后脑。
        side=-1 if name in ('ribbon','vinyl','badminton') else 1
        tip={'critic':1.003,'ribbon':.986,'vinyl':1.124,'bonnet':1.172,'badminton':1.056}[name]
        if name=='badminton':
            side=1
        for strand in range(2):
            t=np.linspace(0,1,10)
            x=side*(.104+.024*np.sin(t*np.pi)+(.017+.010*t)*np.sin(t*11+strand*1.2)-.012*strand)
            y=1.390+(tip+.024*strand-1.390)*t
            z=.022+.104*t+.010*np.cos(t*12+strand)
            tube(out,b.HAIR,'head',np.column_stack((x,y,z)),[.014,.025,.029,.019,.001],
                 [.012,.019,.019,.014,.001])
    if name=='bonnet':
        a=np.linspace(-1.33,1.33,13)
        path=np.stack((.138*np.sin(a),1.358+.148*np.cos(a),-.032+0*a),axis=1)
        tube(out,b.TIE,'head',path,[.016,.019,.016],[.011]*3)
        for x,y,z in path:
            p.disk(out,b.CREAM|NO,'head',(x,y+.006,z+.011),(.016,.012,.009))
    return mirror_pieces(out) if name in ('racer','twist') else out

# 款式定义两条袖子露肤的高度以及实际衣领，左右独立以支持斜肩和半褪赛车服。
# 1.12 = 整条手臂露出，.66 = 长袖，.93 = 短袖。
CUTS = {
    'moto': ('zip',.66,.66), 'openhood': ('zip',.66,.66), 'sailor': ('open',.68,.68),
    'court': ('tank',1.04,1.04), 'ivy': ('shirt',.87,.87), 'clockwork': ('bib',.90,.90),
    'cape': ('open',.70,.70), 'cassette': ('zip',.71,.71), 'pinafore': ('square',.72,.72),
    'bowling': ('shirt',.935,.935), 'orchid': ('shirt',.87,.87), 'map': ('open',.67,.67),
    'rabbit': ('zip',1.12,1.12), 'runner': ('tank',1.12,1.12), 'racer': ('tank',1.0,1.12),
    'streamer': ('zip',.66,.66), 'critic': ('open',.70,.70), 'denim': ('open',1.12,1.12),
    'camera': ('slant',1.085,.972), 'pixel': ('zip',.66,.66), 'golf': ('polo',.93,.93),
    'driver': ('open',.67,.67), 'rose': ('slant',1.12,1.12),
}


def emit_bands(out,family,mesh,paints):
    v,n,f=mesh
    # 每一色沿同一张曲面分界，避免在露肤、领口和衣袖接缝处叠壳重影。
    for paint in np.unique(paints):
        section=f[paints==paint]
        used,inds=np.unique(section.ravel(),return_inverse=True)
        out.append((int(paint),family,(v[used],n[used],inds.reshape(-1,3))))


def torso(p,out,name,cut):
    a=np.arange(80)*TAU/80
    front=np.maximum(0,np.cos(a))
    side=np.sin(a)
    if cut=='slant':
        neck=1.059+.054*side-.023*front
    elif cut in ('tank','square'):
        neck=1.076-.10*front**(4 if cut=='square' else 2)+.031*np.abs(np.sin(2*a))
    elif cut=='bib':
        neck=1.118-.009*front
    else:
        throat=np.abs((a+np.pi)%TAU-np.pi)
        neck=1.126-.105*np.maximum(0,1-throat/.73)
    if name=='driver':
        neck=1.13-.016*front
    if name=='cape':
        neck=1.085-.092*front**3
    # 把领口、包边断面和裸露上胸分成真实的环，避免由粗网格量化领线。
    levels=np.concatenate((np.linspace(.662,neck-.005,31),[neck,neck+.003],np.linspace(neck+.006,1.142,14)))
    z,rx,rz=np.moveaxis(p._shirt_rings(np.clip(levels,.66,1.13)),-1,0)
    above=np.clip((levels-neck)/.020,0,1)
    rx*=1-.038*above
    rz*=1-.053*above
    # 在参数网格中预留门襟边界，不能在大三角上按重心切颜色。
    phase=np.broadcast_to((a+np.pi)%TAU-np.pi,levels.shape).copy()
    layered=cut in ('open','zip','shirt','polo') and name!='ivy'
    if layered:
        boundary=.30+np.clip((levels-.87)/.22,0,1)*(.37 if cut=='open' else .20)
        if cut in ('shirt','polo'):
            boundary=.015+np.maximum(0,(levels-.94)*3)
        angle=np.abs(phase)
        mid=TAU*6/len(a)
        phase=np.sign(phase)*np.where(angle<mid,angle/mid*boundary,boundary+(angle-mid)/(np.pi-mid)*(np.pi-boundary))
    waisted=name in ('rose','pinafore','clockwork','racer','court','rabbit','bowling')
    if waisted:
        rx*=1-.082*np.exp(-((levels-.79)/.094)**2)
    x=rx*np.sign(np.sin(phase))*abs(np.sin(phase))**.8
    zz=z+rz*np.sign(np.cos(phase))*abs(np.cos(phase))**.8
    points=np.stack((x,levels,zz),-1)
    centers=np.stack((x.mean(1)*0,levels.mean(1),z.mean(1)),-1)
    mesh=rings(points,centers)
    v,n,f=mesh
    pos=v[f].mean(1)
    xx,yy,zz=pos.T
    theta=np.arctan2(np.sign(xx)*np.abs(xx/.17)**1.25,np.sign(zz)*np.abs(zz/.105)**1.25)
    # 表面三角按环编号分区，开口、饰边与肩线使用相同的顶点。
    cellrow=np.minimum(f.min(1)//len(a),len(levels)-1)
    cloth=np.full(len(f),b.TOP)
    angle=abs(theta)
    if layered:
        columns=f%len(a)
        central=(columns<=6)|(columns>=len(a)-6)
        opening=central.all(axis=1)
        if cut=='polo':
            opening &= cellrow>=22
        cloth[opening]=b.CREAM
    if name in ('court','runner','racer'):
        cloth[(cellrow>=29)&(cellrow<=31)]=b.TIE if name=='racer' else b.CREAM
    if name=='pinafore':
        cloth[(cellrow>=29)&(cellrow<=31)]=b.TIE
    if name=='clockwork':
        cloth[(yy>1.001)&(angle<.64)]=b.CREAM
    if name=='denim':
        cloth[(cellrow<2)&(angle>.5)]=b.TRIM
    cloth[(cellrow>=32)&(cellrow<len(levels)-1)]=b.SKIN
    cloth[cellrow==len(levels)-1]=b.SKIN
    emit_bands(out,'torso',mesh,cloth)


def arm(out,name,sign,end):
    # 连续上臂补全原母版仅到短袖口的前臂。袖口上下共享腕、肘与肩的蒙皮。
    stations=np.array([[.660,.244,.013,.030,.030],[.697,.240,.015,.033,.032],
        [.766,.227,.012,.041,.039],[.837,.217,.010,.043,.041],[.883,.211,.009,.041,.039],
        [.940,.202,.008,.049,.047],[.992,.186,.005,.053,.052],
        [1.050,.161,.000,.057,.054],[1.081,.121,-.002,.050,.047],[1.095,.089,-.002,.025,.029]])
    armshape=PchipInterpolator(stations[:,0],stations[:,1:])
    ys=np.unique(np.clip(np.r_[np.linspace(.66,1.095,38),end-.004,end,end+.006,.921,.946,.984],.66,1.095))
    cx,cz,rx,rz=armshape(ys).T
    if name in ('runner','denim'):
        vol=1+.10*np.exp(-((ys-.976)/.095)**2)
        rx*=vol;rz*=vol
    # 布料厚度在裸臂处收回，保留袖口厚度与完整的圆臂轮廓。
    cover=np.clip((ys-end+.001)/.005,0,1) if end<1.10 else np.zeros_like(ys)
    if name in ('racer','court','camera'):
        cover=np.clip((end-ys)/.006,0,1) if end<1.10 else np.zeros_like(ys)
    radius=.008+(.005 if name in ('racer','court','pixel','clockwork') else 0)
    rx+=radius*cover;rz+=radius*cover
    if name in ('court','racer','camera'):
        roll=(.011+.009*np.cos((ys-end+.012)*65))*np.clip((ys-.68)/.08,0,1)*cover
        rx+=roll;rz+=roll
    a=np.arange(32)*TAU/32
    center=np.stack((sign*cx,ys,cz),-1)
    pts=center[:,None,:]+np.stack((rx[:,None]*np.sin(a),np.zeros((len(ys),32)),rz[:,None]*np.cos(a)),-1)
    mesh=rings(pts,center)
    pos=mesh[0][mesh[2]].mean(1);x,y,z=pos.T
    visible=y>=end if end<1.10 else np.zeros_like(y,dtype=bool)
    if name in ('racer','court','camera'):
        visible=y<end if end<1.10 else np.zeros_like(y,dtype=bool)
    sleeve=b.CREAM if name in ('pinafore','clockwork','racer') else b.TOP
    paints=np.where(visible,sleeve,b.SKIN)
    if name in ('racer','court','camera'):paints[visible & (abs(y-end)<.015)]=b.TIE
    if name=='racer':paints[visible & (z<.01)]=b.TRIM
    if name=='racer':
        # 将袖条的格纹落在连续网格环上，图案随手臂旋转。
        row=np.minimum(mesh[2].min(1)//len(a),len(ys)-1)
        check=(row//2 + (mesh[2][:,1]%len(a))//4)%2
        stripe=visible & (y>.897) & (y<.958)
        paints[stripe]=np.where(check[stripe]!=0,IVORY,b.TOP)
    if name in ('moto','openhood','pixel','cassette'):
        cols=mesh[2]%len(a)
        outer=((cols>=2)&(cols<=13) if sign>0 else (cols>=18)&(cols<=29)).all(1)
        paints[visible & (y>=.984) & outer]=b.TIE if name in ('moto','cassette') else b.CREAM
    emit_bands(out,'sleeve',mesh,paints)


def draped_back(out,name):
    # 从肩线落到臂弯的外套后片，侧面与一只或两只袖筒相连。
    a=np.linspace(.86,TAU-.86,50)
    sa,ca=np.sin(a),np.cos(a)
    upper=.977+.050*np.abs(sa)
    if name=='racer':
        upper-=.16*np.clip((sa+.3)/1.3,0,1)
    y=np.linspace(.677,upper,22)
    rx=.183+.006*np.cos(y*31+a)*np.clip((upper-y)/.2,0,1)
    rz=.122+.008*np.cos(a*6+y*5)
    x=rx*np.sign(sa)*abs(sa)**.8
    z=rz*np.sign(ca)*abs(ca)**.8
    outer=np.stack((x,y,z),-1)
    inner=outer.copy();inner[:,:,0]*=.974;inner[:,:,2]*=.968
    v=np.concatenate((outer.reshape(-1,3),inner.reshape(-1,3)))
    nr,nc=y.shape
    r,c=np.indices((nr-1,nc-1))
    k=r*nc+c
    f=np.vstack((np.stack((k,k+1,k+nc),-1).reshape(-1,3),np.stack((k+1,k+nc+1,k+nc),-1).reshape(-1,3)))
    offset=nr*nc
    rims=[np.arange(nc),np.arange(offset-nc,offset)[::-1],np.arange(0,offset,nc)[::-1],np.arange(nc-1,offset,nc)]
    edges=np.vstack([np.column_stack((e[:-1],e[1:])) for e in rims])
    u,w=edges.T
    faces=np.vstack((f,f[:,::-1]+offset,np.stack((u,w+offset,w),-1),np.stack((u,u+offset,w+offset),-1)))
    colors=np.full(len(faces),b.CREAM if name=='racer' else b.TOP)
    colors[v[faces,1].mean(1)<.699]=b.TRIM
    emit_bands(out,'torso',(v,mesh_normals(v,faces),faces),colors)


def motif(p,out,name):
    def patch(mat,xy,lift=.006):
        p.panel_patch(out,mat|NO,'torso',xy,None,lift=lift)
    def dot(mat,x,y,w,h,lift=.010):
        p.disk(out,mat|NO,'torso',(x,y,.13),(w,h,.005),150,lift=lift)
    def seam(mat,xy,r=.003,lift=.010):
        p.line(out,mat,'torso',[(x,y,.12) for x,y in xy],r,lift)
    if name in ('openhood','pixel','streamer'):
        p.hood(out)
    if name in ('moto','driver','denim','map','critic','cape'):
        for side in (-1,1):
            patch(b.TOP,[(side*x,y) for x,y in ((.044,1.118),(.085,1.092),(.116,1.029),(.093,1.033),(.089,1.003),(.046,.964))])
            seam(b.TRIM,[(side*.081,1.091),(side*.113,1.031),(side*.091,1.035),(side*.086,1.004),(side*.048,.966)])
    if name in ('ivy','bowling','orchid'):
        p.collar(out,mat=b.TOP)
    if name=='golf':
        for sign in (-1,1):
            xy=[(sign*x,y) for x,y in ((.024,1.129),(.058,1.119),(.102,1.076),(.086,1.040),(.037,1.076),(.012,1.099))]
            patch(b.TOP,xy,.010)
            seam(b.TIE,[xy[i] for i in (1,2,3,4)],.0028,.014)
        for y in (1.002,.976):
            dot(b.TRIM,0,y,.005,.005,.012)
    if name=='ivy':
        for side in (-1,1):
            patch(b.CREAM,[(side*x,y) for x,y in ((.045,1.128),(.098,1.110),(.158,1.06),(.104,1.028),(.04,.938),(.003,.947))],.012)
        p.disk(out,b.CREAM,'torso',(0,.944,.12),(.026,.021,.012))
    if name=='sailor':
        for side in (-1,1):
            pts=[(side*x,y) for x,y in ((.046,1.128),(.105,1.099),(.15,1.067),(.016,.991),(.001,1.011))]
            patch(b.TIE,pts,.010)
            seam(b.CREAM,[(side*.14,1.067),(side*.020,1.002)],.003,.014)
        patch(b.TRIM,[(-.015,1.012),(.015,1.012),(.024,.934),(0,.917),(-.021,.936)],.015)
    if name=='clockwork':
        p.collar(out,True,lift=.015)
        for side in (-1,1):
            seam(b.TIE,[(side*.074,.68),(side*.068,.86),(side*.09,1.001)],.003)
        dot(b.TIE,0,1.048,.017,.017,.030)
    if name=='rose':
        # 手织玫瑰的花瓣从外圈逐层合拢，固定在有肩带的一边。
        for i in range(7):
            a=i*TAU/7
            dot(b.TIE,.094+.023*np.sin(a),1.054+.022*np.cos(a),.019,.018,.017)
        for i in range(4):
            a=(i+.5)*TAU/4
            dot(b.TIE,.094+.009*np.sin(a),1.054+.010*np.cos(a),.013,.012,.026)
        dot(b.TIE,.094,1.055,.009,.009,.033)
        seam(b.CREAM,[(-.038,1.098),(0,1.046),(.040,1.098)],.0025)
    if name in ('rabbit','map','moto','pixel','cassette','camera'):
        side=-1
        seam(b.TRIM,[(side*.101,1.093),(side*.099,1.012),(side*.071,.925),(side*.049,.85)],.008)
        if name=='rabbit':
            dot(IVORY,-.092,.890,.024,.031,.026)
            dot(IVORY,-.088,.934,.036,.031,.034)
            for x in (-.122,-.054):
                dot(IVORY,x,.919,.011,.035,.032)
            for x in (-.103,-.074):dot(b.BROW,x,.938,.004,.005,.044)
            dot(b.TIE,-.088,.924,.006,.004,.044)
        elif name in ('moto','pixel','cassette','camera'):
            width=.043 if name=='camera' else .023
            patch(b.TRIM,[(x-.078,y+.927) for x,y in ((-width,-.021),(width,-.021),(width,.021),(-width,.021))],.018)
            dot(b.TIE,-.079,.93,.015,.011,.025)
        else:
            patch(b.CREAM,[(.063,.987),(.117,.995),(.111,1.021),(.068,1.014)])
            p.pocket(out,1,b.TOP,.989)
    if name in ('moto','streamer'):
        # 风镜与耳机放在胸前，避开头部绕颈旋转。
        seam(b.EYE,[(-.083,1.076),(-.061,1.031),(0,1.005),(.061,1.031),(.083,1.076)],.007,.012)
        for side in (-1,1):
            cx=.079 if name=='streamer' else .043
            dot(b.EYE,side*cx,1.025,.033 if name=='streamer' else .027,.032 if name=='streamer' else .019,.018)
            dot(b.TIE,side*cx,1.025,.024 if name=='streamer' else .020,.020 if name=='streamer' else .013,.025)
    if name in ('runner','court','racer'):
        patch(b.CREAM if name!='racer' else b.TIE,[(-.136,.918),(-.070,.930),(.106,.980),(.106,.987),(-.071,.943),(-.136,.928)],.002)
    if name=='runner':
        # 正反两端搭在肩上；工作抬臂时毛巾随躯干，而非锁在上臂上。
        path=[(-.128,.890,.084),(-.120,.975,.090),(-.109,1.058,.079),
              (-.103,1.106,.045),(-.104,1.112,-.010),(-.118,1.072,-.069),
              (-.131,.989,-.085),(-.125,.932,-.092)]
        tube(out,b.CREAM,'torso',path,[.006,.009,.010,.011,.010,.010,.008,.006],
             [.032,.035,.034,.036,.032,.029,.031,.032],guide=(1,0,0))
    if name in ('court','racer'):
        draped_back(out,name)
    if name=='pinafore':
        for side in (-1,1):
            patch(b.TOP,[(side*x,y) for x,y in ((.062,1.119),(.083,1.109),(.103,.985),(.077,.973))],.007)
            dot(b.TIE,side*.088,.989,.006,.006,.013)
        for side in (-1,1):
            patch(b.TIE,[(side*.004,.801),(side*.051,.818),(side*.057,.791),(side*.014,.785)],.010)
    if name=='bowling':
        p.scarf(out)
    if name=='orchid':
        for x,y in ((.077,1.042),(.066,1.034),(.084,1.031)):dot(b.TIE,x,y,.008,.007)
    if name=='denim':
        p.pocket(out,1,b.TOP,.983)
        patch(b.TIE,[(-.10,.997),(-.087,1.007),(-.08,1.029),(-.070,1.013),(-.06,1.010),(-.068,.998),(-.064,.986),(-.083,.986)])
    if name=='golf':
        seam(b.TIE,[(-.062,1.104),(-.038,1.068)],.003)
        patch(b.CREAM,[(.081,1.003),(.095,1.013),(.115,1.017),(.104,1.005),(.089,.999)])
    if name=='driver':
        seam(b.CREAM,[(-.033,1.101),(0,1.029),(.037,1.100)],.002)
        dot(b.TIE,0,1.019,.006,.013)
    if name=='pixel':
        for i in range(4):
            x=.018+i*.009;y=.921+i*.009
            patch(b.TIE,[(x,y),(x+.010,y),(x+.010,y+.010),(x,y+.010)])


def top(p,name):
    cut,left,right=CUTS[name]
    # 原版颈、手掌和手指原样复用。新衣袖与上臂共用原肩肘腕权重。
    out=[section for section in p.cache('common-body-v1',p.common_body) if section[0]==b.SKIN]
    torso(p,out,name,cut)
    arm(out,name,-1,left)
    arm(out,name,1,right)
    motif(p,out,name)
    return mirror_pieces(out) if name in ('rose','racer','runner','rabbit') else out


