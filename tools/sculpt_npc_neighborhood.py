"""Bake Su Xiaoqing and Zheng He with the approved B-style character rig."""

from pathlib import Path

import numpy as np

import sculpt_player_b as b
import sculpt_npc_b as npc


def bob_hair(p):
    _, y, z = np.moveaxis(p, -1, 0)
    shell = b.loft(p, [
        (1.191,0,-.034,.091,.085), (1.210,0,-.035,.125,.113),
        (1.300,0,-.035,.137,.124), (1.407,0,-.027,.126,.118),
        (1.469,0,-.027,.091,.085), (1.495,0,-.027,.022,.025),
    ])
    face_opening = np.maximum(y-1.398, .035-z)
    shell = np.maximum(shell, -face_opening)
    fringe = b.sculpt(p, [
        ((-.043,1.422,.063),(.087,.028,.059),29),
        ((-.105,1.350,.035),(.026,.081,.044),-8),
        ((.098,1.397,.037),(.039,.050,.044),-25),
    ], .010)
    return b.mix_surface(shell, fringe, .013)


def cropped_hair(p):
    x, y, z = np.moveaxis(p, -1, 0)
    cap = b.ellipsoid(p,(0,1.399,-.030),(.123,.084,.114))
    hairline = 1.318+.070*np.clip((z+.06)/.16,0,1)
    cap = np.maximum(cap, hairline-y)
    for x_center, y_center in [(-.087,1.438),(-.052,1.458),(-.014,1.471),(.027,1.465),(.068,1.447)]:
        lock = b.ellipsoid(p,(x_center,y_center,.040),(.028,.027,.061),-24)
        cap = b.mix_surface(cap,lock,.008)
    return cap


def flower(p, center, radius):
    petal_fields = []
    for angle in np.linspace(0,2*np.pi,5,endpoint=False):
        x = center[0]+np.sin(angle)*radius*.55
        y = center[1]+np.cos(angle)*radius*.55
        petal_fields.append(b.ellipsoid(p,(x,y,center[2]),(radius*.43,radius*.55,.0035),
                                         -np.degrees(angle)))
    return np.minimum.reduce(petal_fields)


def surface_stripe(field, profile):
    """Place a stripe just above the garment so it stays visible along bends."""
    profile = np.asarray(sorted(profile, key=lambda point: point[1]))
    path = []
    for y in np.linspace(profile[0,1], profile[-1,1], 65):
        x = np.interp(y, profile[:,1], profile[:,0])
        depths = np.linspace(-.01,.18,400)
        points = np.column_stack((np.full_like(depths,x),np.full_like(depths,y),depths))
        inside = np.nonzero(field(points)<0)[0]
        if len(inside):
            path.append((x,y,depths[inside[-1]]+.0015))
    return path


def skirt(p):
    x, y, z = np.moveaxis(p, -1, 0)
    # Shallow pleats retain a continuous silhouette as the legs move.
    angle = np.arctan2(z/.13,x/.20)
    pleat = .0025*np.cos(angle*14)*np.clip((.67-y)/.23,0,1)
    return b.loft(p, [
        (.403,0,0,.212,.140),(.417,0,0,.214,.142),
        (.520,0,0,.188,.126),(.620,0,-.004,.167,.109),
        (.687,0,-.004,.150,.100),
    ],2.4)-pleat


def make_character(folder, florist):
    sections = []
    head = ((-.18,.18),(1.17,1.52),(-.18,.17))
    face_bounds = ((-.15,.15),(1.24,1.40),(.055,.17))
    body = ((-.32,.32),(.56,1.23),(-.18,.18))

    def add(paint, family, field, bounds, step=.0038, faces=2400):
        b.add(sections,paint,family,field,bounds,step,faces)

    def face(p):
        q = p.copy()
        q[...,0] /= np.interp(q[...,1],[1.21,1.28,1.40],
                             [.84,.95,1.] if florist else [1.09,1.06,1.])
        return b.face(q)

    add(b.SKIN,"head",face,head,.0032,4300)
    add(b.HAIR,"head",bob_hair if florist else cropped_hair,head,.003,5000)
    for sign in (-1,1):
        x, y = sign*.050, 1.334
        add(b.EYE,"head",lambda p:b.ellipsoid(p,(x,y,b.face_front(x,y)+.001),
            (.011,.015 if florist else .012,.007)),face_bounds,.003,170)
        add(b.BROW,"head",lambda p:b.facial_curve(p,[
            (sign*.026,1.361),(sign*.045,1.369),(sign*.070,1.364)],
            .0028 if florist else .005),face_bounds,.0028,180)
    add(b.LIP,"head",lambda p:b.facial_curve(p,[
        (-.026,1.272),(-.010,1.266),(.012,1.266),(.028,1.275)],.0026),
        face_bounds,.0028,180)
    if florist:
        add(b.TIE,"head",lambda p:flower(b.mirror_x(p),(.122,1.276,.022),.012),
            ((-.15,.15),(1.250,1.301),(.010,.034)),.002,450)
    add(b.SKIN,"top",lambda p:b.ellipsoid(p,(0,1.151,-.002),
        (.043 if florist else .050,.103,.049)),body)
    add(b.SKIN,"arm",lambda p:np.maximum(b.bare_arms(p),p[...,1]-.675),
        ((-.315,.315),(.535,.70),(-.096,.102)),.0038,1800)
    add(b.TOP,"top",lambda p:npc.jacket(p,florist),body,.004,6500)
    add(b.CREAM,"top",npc.shirt,body,.0038,1800)
    if florist:
        # Folded shawl edges follow the open front of the cardigan.
        add(b.TRIM,"top",lambda p:b.front_patch(b.mirror_x(p),
            [(.047,1.107),(.065,1.095),(.071,.679),(.049,.679)],
            lambda y:.116-.62*np.maximum(y-1.04,0),.003,.003),body,.003,1200)
        for center, radius in [((-.110,1.003,.102),.024),((.117,.900,.105),.021),
                               ((-.117,.753,.096),.020)]:
            add(b.CREAM,"top",lambda p:flower(p,center,radius),body,.0027,550)
            add(b.TIE,"top",lambda p:b.ellipsoid(p,center,(.004,.004,.006)),body,.003,100)
        add(b.TRIM,"top",lambda p:b.rounded_box(p,(0,.692,.103),(.269,.036,.014),.005),
            body,.0038,650)
        add(b.TIE,"top",lambda p:b.curve(p,[
            (-.044,1.104,.076),(0,1.059,.120),(.044,1.104,.076)],.0023),body,.0026,500)
        add(b.TIE,"top",lambda p:flower(p,(0,1.052,.121),.011),body,.0025,300)
        add(b.PANTS,"pants",skirt,((-.24,.24),(.38,.71),(-.17,.17)),.004,4400)
        add(b.SKIN,"calf",b.calves,((-.20,.20),(.04,.40),(-.11,.12)),.004,2400)
    else:
        # Stand collar, zip edges, and stripes identify the coach's tracksuit.
        add(b.TOP,"top",lambda p:b.front_patch(b.mirror_x(p),
            [(.035,1.108),(.041,1.174),(.072,1.150),(.095,1.099)],
            lambda y:.065-.4*(y-1.11),.006,.005),body,.003,1000)
        add(b.TRIM,"top",lambda p:b.curve(b.mirror_x(p),[
            (.072,1.09,.081),(.056,1.020,.117),(.051,.690,.110)],.003),body,.003,900)
        stripe = surface_stripe(lambda p:npc.jacket(p,False),
            [(.098,1.089),(.157,1.055),(.211,.973),(.247,.896),(.267,.793),(.273,.685)])
        add(b.CREAM,"top",lambda p:b.curve(b.mirror_x(p),stripe,.005),body,.0028,1800)
        add(b.PANTS,"pants",lambda p:b.shorts_or_pants(p,False),
            ((-.22,.22),(.04,.73),(-.15,.15)),.004,3500)
        trouser_stripe = surface_stripe(lambda p:b.shorts_or_pants(p,False),
            [(.178,.632),(.180,.516),(.165,.351),(.151,.196),(.142,.092)])
        add(b.CREAM,"pants",lambda p:b.curve(b.mirror_x(p),trouser_stripe,.004),
            ((-.20,.20),(.08,.66),(-.01,.12)),.003,1600)
    add(b.SHOE,"shoe",b.shoes,((-.20,.20),(.004,.165),(-.10,.19)),.0048,1700)
    add(b.SOLE,"shoe",lambda p:b.loft(b.mirror_x(p),[
        (.002,.104,.041,.055,.094),(.008,.104,.041,.061,.102),
        (.023,.104,.041,.062,.103)],2.7),((-.20,.20),(-.004,.044),(-.10,.174)),.0048,850)
    name = "Su" if florist else "Zheng"
    b.write_model(folder / f"PlayerB-{name}.bytes",sections)


if __name__ == "__main__":
    folder = Path(__file__).resolve().parents[1] / "Assets/Resources/Characters"
    make_character(folder,True)
    make_character(folder,False)
