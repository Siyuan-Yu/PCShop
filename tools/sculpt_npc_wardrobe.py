"""Bake shared B-style heads and clothes for the city population."""

from pathlib import Path

import numpy as np

import sculpt_player_b as b
import sculpt_npc_b as npc
from sculpt_npc_neighborhood import bob_hair, cropped_hair, skirt, surface_stripe


HEAD = ((-.19,.19),(1.10,1.55),(-.20,.18))
FACE = ((-.15,.15),(1.235,1.405),(.055,.17))
BODY = ((-.33,.33),(.535,1.22),(-.19,.19))
LEGS = ((-.25,.25),(.035,.75),(-.185,.18))


def add(sections, material, family, field, bounds, step=.0045, faces=1100):
    b.add(sections, material, family, field, bounds, step, faces)


def hair_shape(p, style):
    if style in ("officer","worker"):
        crown = b.loft(p, [
            (1.410,0,-.016,.120,.114), (1.440,0,-.020,.126,.118),
            (1.485,0,-.023,.079,.077), (1.503,0,-.023,.019,.019),
        ])
        bill = b.ellipsoid(p,(0,1.412,.094),(.101,.011,.057))
        return b.mix_surface(crown,bill,.009)
    if style == "crop":
        return cropped_hair(p)
    if style == "swept":
        return npc.hair(p, False)
    if style == "tied":
        return npc.hair(p, True)
    if style == "bob":
        return bob_hair(p)
    if style == "senior":
        q = p.copy()
        q[...,1] = 1.396+(q[...,1]-1.396)*1.08
        crown = b.ellipsoid(p,(0,1.411,-.031),(.126,.080,.115))
        crown = np.maximum(crown,1.323+.076*np.clip((p[...,2]+.05)/.13,0,1)-p[...,1])
        return b.mix_surface(crown,b.hair_base(q),.008)
    _, y, z = np.moveaxis(p, -1, 0)
    if style == "waves":
        bob = bob_hair(p)
        locks = b.sculpt(b.mirror_x(p),[
            ((.107,1.339,-.041),(.036,.105,.087),9),
            ((.107,1.257,-.050),(.041,.093,.082),-10),
            ((.091,1.192,-.055),(.044,.062,.068),13),
        ], .009)
        return b.mix_surface(bob,locks,.012)
    if style == "fringe":
        fringe = b.ellipsoid(p,(0,1.392,.077),(.105,.036,.046),-7)
        return b.mix_surface(bob_hair(p),fringe,.010)
    cap = b.ellipsoid(p,(0,1.393,-.027),(.127,.100,.118))
    cap = np.maximum(cap,1.278+.095*np.clip((z+.045)/.14,0,1)-y)
    fringe = b.sculpt(p,[
        ((-.061,1.428,.067),(.063,.042,.060),31),
        ((-.004,1.433,.079),(.047,.047,.050),-16),
        ((.068,1.429,.060),(.060,.035,.054),-31),
    ], .012)
    return b.mix_surface(cap,fringe,.016)


def bake_head(folder, key, style, shape="standard", glasses=False):
    sections = []
    widths = {"standard": (1.,1.), "slim": (.88,.96), "broad": (1.10,1.05), "young": (.86,1.01)}

    def face(p):
        q = p.copy()
        jaw, cheek = widths[shape]
        q[...,0] /= np.interp(q[...,1],[1.21,1.28,1.40],[jaw,cheek,1.])
        return b.face(q)

    add(sections,b.SKIN,"head",face,HEAD,faces=2100)
    add(sections,b.HAIR,"head",lambda p:hair_shape(p,style),HEAD,.004,2600)
    for sign in (-1,1):
        eye_x = .053*sign
        eye_y = 1.334
        eye_z = b.face_front(eye_x,eye_y)+.001
        add(sections,b.EYE,"head",lambda p:b.ellipsoid(p,(eye_x,eye_y,eye_z),
            (.011,.014 if shape in ("slim","young") else .0115,.007)),FACE,.0035,130)
        add(sections,b.BROW,"head",lambda p:b.facial_curve(p,[
            (sign*.030,1.361),(sign*.048,1.367),(sign*.067,1.363)],
            .003 if shape == "slim" else .004),FACE,.0035,130)
    add(sections,b.LIP,"head",lambda p:b.facial_curve(p,[
        (-.024,1.273),(-.009,1.267),(.011,1.267),(.026,1.273)],.0026),FACE,.0035,145)
    if glasses:
        add(sections,b.BROW,"head",npc.glasses,((-.15,.15),(1.29,1.38),(-.005,.16)),.0025,1000)
    b.write_model(folder / f"PlayerB-NpcHead-{key}.bytes",sections)


def bake_top(folder, cut):
    sections = []
    short = cut in ("short","uniform","patrol")
    add(sections,b.SKIN,"top",lambda p:b.ellipsoid(p,(0,1.151,-.002),(.046,.103,.049)),BODY,faces=500)
    skin_limit = .947 if short else .675
    add(sections,b.SKIN,"arm",lambda p:np.maximum(b.bare_arms(p),p[...,1]-skin_limit),
        ((-.315,.315),(.535,skin_limit+.025),(-.096,.102)),.0045,1700)

    if short:
        add(sections,b.TOP,"top",lambda p:b.top_field(p,"short"),BODY,faces=4300)
        add(sections,b.CREAM,"top",b.undershirt,BODY,.0045,550)
        if cut == "patrol":
            add(sections,b.TRIM,"top",lambda p:b.loft(p,[
                (.728,0,0,.146,.099),(.747,0,0,.166,.106),
                (.870,0,.002,.166,.121),(1.025,0,0,.155,.121),
                (1.094,0,-.002,.095,.090),
            ],2.6),BODY,.004,1800)
            add(sections,b.CREAM,"top",lambda p:b.loft(p,[
                (.780,0,0,.164,.118),(.802,0,0,.167,.121),
            ],2.6),BODY,.004,800)
        else:
            add(sections,b.TRIM,"top",lambda p:b.blouse_trim(p,"short"),BODY,.004,1600)
        if cut in ("uniform","patrol"):
            add(sections,b.TIE,"top",lambda p:b.rounded_box(p,(-.095,1.002,.118),(.037,.025,.015),.005),
                BODY,.004,260)
    else:
        field = (lambda p:b.top_field(p,"long")) if cut == "apron" else (lambda p:npc.jacket(p,False))
        jacket_field = field if cut != "coat" else lambda p:field(p*np.array([.95,1.,.93]))-.001
        add(sections,b.TOP,"top",jacket_field,BODY,faces=4300)
        if cut == "apron":
            add(sections,b.TRIM,"top",lambda p:b.front_patch(p,[
                (-.091,1.019),(.091,1.019),(.123,.658),(-.123,.658)],
                lambda y:.112+.043*np.clip((y-.70)/.28,0,1),.011,.004),BODY,.004,1400)
            straps = surface_stripe(field,[(.052,1.112),(.065,1.068),(.071,1.018)])
            add(sections,b.TRIM,"top",lambda p:b.curve(b.mirror_x(p),straps,.011),BODY,.0038,800)
        else:
            add(sections,b.CREAM,"top",npc.shirt,BODY,faces=1300)
            add(sections,b.TRIM,"top",npc.lapels,BODY,.0032,1100)
            if cut == "formal":
                add(sections,b.TIE,"top",lambda p:b.front_patch(p,[
                    (0,1.099),(.012,1.078),(.010,.956),(0,.937),(-.010,.956),(-.012,1.078)],
                    lambda y:.114-.52*np.maximum(0,y-1.04),.004,.003),BODY,.0035,525)
            if cut == "track":
                stripe = surface_stripe(field,[
                    (.098,1.089),(.157,1.055),(.211,.973),(.247,.896),(.267,.793),(.273,.685)])
                add(sections,b.TIE,"top",lambda p:b.curve(b.mirror_x(p),stripe,.005),BODY,.003,1400)
    b.write_model(folder / f"PlayerB-NpcTop-{cut}.bytes",sections)


def bake_bottom(folder, cut):
    sections = []
    if cut == "skirt":
        add(sections,b.PANTS,"pants",skirt,LEGS,.005,2400)
    else:
        add(sections,b.PANTS,"pants",lambda p:b.shorts_or_pants(p,cut=="shorts"),LEGS,faces=2500)
    if cut != "pants":
        height = .425 if cut == "skirt" else .407
        add(sections,b.SKIN,"calf",b.calves,((-.20,.20),(.04,height),(-.11,.12)),faces=1700)
    add(sections,b.SHOE,"shoe",b.shoes,((-.20,.20),(.004,.165),(-.10,.19)),.0055,1300)
    add(sections,b.SOLE,"shoe",lambda p:b.loft(b.mirror_x(p),[
        (.002,.104,.041,.055,.094),(.008,.104,.041,.061,.102),(.023,.104,.041,.062,.103)],2.7),
        ((-.20,.20),(-.004,.044),(-.10,.174)),.0055,700)
    b.write_model(folder / f"PlayerB-NpcBottom-{cut}.bytes",sections)


if __name__ == "__main__":
    folder = Path(__file__).resolve().parents[1] / "Assets/Resources/Characters"
    for key, style, shape, glasses in [
        ("crop","crop","broad",False),("swept","swept","standard",False),
        ("tousled","tousled","standard",False),("senior","senior","broad",False),
        ("spectacles","tousled","slim",True),("young","tousled","young",False),
        ("bob","bob","slim",False),("fringe","fringe","young",False),
        ("waves","waves","slim",False),("tied","tied","slim",False),
        ("officer","officer","broad",False),("worker","worker","standard",False),
    ]:
        bake_head(folder,key,style,shape,glasses)
    for cut in ("short","jacket","formal","coat","apron","uniform","track","patrol"):
        bake_top(folder,cut)
    for cut in ("pants","shorts","skirt"):
        bake_bottom(folder,cut)
