"""Bake the two named NPC sculpts using the B-style character rig."""

from pathlib import Path

import numpy as np

import sculpt_player_b as b


def hair(p, clerk):
    _, y, z = np.moveaxis(p, -1, 0)
    cap = b.ellipsoid(p, (0, 1.395, -.032), (.128, .108, .119))
    hairline = 1.268 + .145 * np.clip((z + .065) / .14, 0, 1)
    cap = np.maximum(cap, hairline - y)
    if clerk:
        shapes = [
            ((-.047, 1.425, .058), (.084, .028, .054), 28),
            ((.078, 1.419, .040), (.052, .027, .047), -36),
            ((.069, 1.282, -.115), (.050, .054, .047), -20),
        ]
        locks = [
            [(-.019, 1.468, .079), (-.087, 1.407, .087), (-.112, 1.338, .052), (-.106, 1.291, .048)],
            [(.041, 1.461, .084), (.097, 1.394, .075), (.117, 1.329, .041)],
        ]
    else:
        shapes = [
            ((-.031, 1.452, .063), (.101, .033, .057), -10),
            ((.082, 1.420, .043), (.050, .028, .048), -36),
            ((-.095, 1.420, .036), (.045, .025, .050), 22),
        ]
        locks = [
            [(.055, 1.478, .049), (-.021, 1.479, .079), (-.103, 1.434, .088)],
            [(.071, 1.464, .049), (.103, 1.421, .079), (.115, 1.389, .053)],
        ]
    field = b.mix_surface(cap, b.sculpt(p, shapes, .015), .020)
    for lock in locks if clerk else []:
        field = b.mix_surface(field, b.curve(p, lock, .006), .008)
    return field


def glasses(p):
    q = b.mirror_x(p)
    x, y, z = np.moveaxis(q, -1, 0)
    # Thin frames with open lenses. Angle each lens with the cheek.
    rim = np.abs(((np.abs(x-.056)/.043)**4 + (np.abs(y-1.332)/.024)**4)**.25 - 1) * .024
    rim = np.hypot(rim, z - (.137 - .70*x)) - .0022
    bridge = b.curve(p, [(-.017,1.337,.125),(0,1.343,.130),(.017,1.337,.125)], .0024)
    temple = b.curve(q, [(.096,1.342,.070),(.121,1.343,.036),(.128,1.330,.008)], .0024)
    return np.minimum.reduce([rim, bridge, temple])


def jacket(p, clerk):
    shell = b.loft(p, [
        (.660,0,0,.158,.091), (.690,0,0,.162,.097),
        (.785,0,0,.135 if clerk else .154,.088 if clerk else .100),
        (.910,0,.002,.148 if clerk else .160,.105),
        (1.015,0,.002,.158 if clerk else .170,.113),
        (1.070,0,0,.140,.101), (1.112,0,-.005,.087,.069),
        (1.131,0,-.010,.052,.058),
    ], 2.5)
    sleeves = b.loft(b.mirror_x(p), [
        (.660,.244,.010,.034,.034), (.686,.242,.010,.039,.039),
        (.790,.227,.010,.044,.044), (.920,.206,.010,.050,.049),
        (1.027,.183,.010,.058,.056), (1.071,.160,.002,.054,.050),
        (1.103,.130,-.002,.035,.033),
    ])
    shell = b.mix_surface(shell, sleeves, .018)
    x, y, z = np.moveaxis(p, -1, 0)
    width = np.interp(y, [.66, .88, 1.12], [.049, .048, .074])
    opening = np.maximum(np.abs(x) - width, .057 - z)
    return np.maximum(shell, -opening)


def shirt(p):
    return b.front_patch(p, [(-.050,1.112),(.050,1.112),(.070,1.060),(.061,.677),(-.061,.677),(-.070,1.060)],
                         lambda y: .096 - .45*np.maximum(0, y-1.035), .012, .005)


def lapels(p):
    return b.front_patch(b.mirror_x(p),
        [(.041,1.108),(.065,1.106),(.096,1.056),(.053,.960)],
        lambda y: np.interp(y,[.960,1.040,1.108],[.112,.120,.070]), .003, .002)


def make_npc(folder, name, clerk):
    sections = []
    head = ((-.19,.19),(1.17,1.53),(-.19,.17))
    front = ((-.16,.16),(1.25,1.42),(.063,.17))
    body = ((-.32,.32),(.56,1.24),(-.18,.18))

    def add(paint, part, field, bounds, step=.004, faces=2400):
        b.add(sections, paint, part, field, bounds, step, faces)

    def face(p):
        # A narrower jaw for the clerk and broader cheeks for the classmate.
        q = p.copy()
        q[...,0] /= np.interp(q[...,1], [1.21,1.28,1.39], [.83,.92,1.] if clerk else [1.04,1.04,1.])
        return b.face(q)

    add(b.SKIN, "head", face, head, .0032, 4100)
    add(b.HAIR, "head", lambda p: hair(p, clerk), head, .0032, 4800)
    for sign in (-1, 1):
        x, y = sign*.050, 1.334
        add(b.EYE, "head", lambda p: b.ellipsoid(p,(x,y,b.face_front(x,y)+.0006),
            (.012,.016 if clerk else .013,.007)), front, .003, 170)
        add(b.BROW, "head", lambda p: b.facial_curve(p, [
            (sign*.028,1.36),(sign*.044,1.368),(sign*.067,1.365)], .0028 if clerk else .004), front, .0027, 170)
    add(b.LIP, "head", lambda p: b.facial_curve(p,
        [(-.024,1.273),(-.010,1.265),(.009,1.266),(.025,1.274)], .0026), front, .0029, 160)
    if clerk:
        add(b.TIE, "head", lambda p: b.ellipsoid(b.mirror_x(p),(.12,1.280,.004),(.008,.016,.007)),
            ((-.15,.15),(1.255,1.306),(-.018,.027)), .0026, 220)
    else:
        add(b.BROW, "head", glasses, ((-.145,.145),(1.292,1.374),(-.005,.165)), .002, 1700)

    add(b.SKIN, "top", lambda p: b.ellipsoid(p,(0,1.151,-.002),(.043 if clerk else .048,.103,.049)), body)
    add(b.SKIN, "arm", lambda p: np.maximum(b.bare_arms(p), p[...,1]-.675),
        ((-.315,.315),(.535,.70),(-.096,.102)), .0038, 1800)
    add(b.TOP, "top", lambda p: jacket(p, clerk), body, .0044, 6200)
    add(b.CREAM, "top", shirt, body, .004, 1400)
    add(b.TRIM, "top", lapels, body, .0025, 1600)
    if clerk:
        add(b.TIE, "top", lambda p: b.sculpt(p, [
            ((0,1.070,.095),(.009,.011,.009)),
            ((-.018,1.074,.093),(.019,.010,.008),23),
            ((.018,1.074,.093),(.019,.010,.008),-23)], .003), body, .0025, 650)
        add(b.TIE, "top", lambda p: b.front_patch(b.mirror_x(p),
            [(.004,1.068),(.014,1.067),(.031,1.016),(.017,1.027)],
            lambda y: .112 - .4*np.maximum(y-1.040,0), .002,.002), body,.0025,500)
    else:
        add(b.TRIM, "top", lambda p: b.front_patch(b.mirror_x(p),
            [(.080,.983),(.138,.983),(.137,.921),(.110,.912),(.078,.925)],
            lambda y: .113, .007,.005), body, .0036, 1100)
        add(b.TIE, "top", lambda p: b.curve(p,
            [(-.047,1.079,.130),(-.036,1.01,.137),(0,.911,.132),(.038,1.02,.137),(.046,1.079,.130)], .004),
            body, .003, 700)
        add(b.TRIM, "top", lambda p: b.rounded_box(p,(0,.877,.13),(.041,.062,.011),.003), body, .003, 440)

    add(b.PANTS, "pants", lambda p: b.shorts_or_pants(p, False), ((-.22,.22),(.043,.74),(-.15,.15)))
    add(b.SHOE, "shoe", b.shoes, ((-.20,.20),(.004,.165),(-.10,.19)), .0048, 1700)
    add(b.SOLE, "shoe", lambda p: b.loft(b.mirror_x(p), [
        (.002,.104,.041,.055,.094),(.008,.104,.041,.061,.102),(.023,.104,.041,.062,.103)], 2.7),
        ((-.20,.20),(-.004,.044),(-.10,.174)), .0048, 850)
    b.write_model(folder / f"PlayerB-{name}.bytes", sections)


if __name__ == "__main__":
    folder = Path(__file__).resolve().parents[1] / "Assets/Resources/Characters"
    make_npc(folder, "Tang", True)
    make_npc(folder, "Chen", False)
