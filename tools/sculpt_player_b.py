"""Bake the B-style player into a Unity binary mesh asset.

Requires numpy, scipy, scikit-image and fast-simplification. Run with Python 3.
The sculpt is in character-local metres, facing +Z. Bone weights follow the
same rest-pose landmarks as PlayerCharacterModel.cs.
"""

from functools import lru_cache
from pathlib import Path
import struct

import fast_simplification
import numpy as np
from scipy.interpolate import PchipInterpolator
from skimage.measure import marching_cubes


# Material IDs shared with PlayerCharacterModel.
SKIN, HAIR, BROW, EYE, LIP, TOP, TRIM, PANTS, SOLE, SHOE, CREAM, TIE = range(12)


def ellipsoid(p, center, radius, tilt=0):
    q = p - center
    if tilt:
        angle = np.radians(tilt)
        c, s = np.cos(angle), np.sin(angle)
        q = np.stack((c*q[..., 0] + s*q[..., 1], -s*q[..., 0] + c*q[..., 1], q[..., 2]), axis=-1)
    q = q / radius
    return (np.linalg.norm(q, axis=-1) - 1) * float(min(radius))


def rounded_box(p, center, size, radius):
    q = np.abs(p - center) - (np.array(size) * .5 - radius)
    return np.linalg.norm(np.maximum(q, 0), axis=-1) + np.minimum(np.max(q, axis=-1), 0) - radius


def mix_surface(a, b, radius):
    weight = np.maximum(radius - np.abs(a-b), 0) / radius
    return np.minimum(a, b) - weight*weight*radius*.25


def sculpt(p, shapes, blend):
    surface = ellipsoid(p, *shapes[0])
    for shape in shapes[1:]:
        surface = mix_surface(surface, ellipsoid(p, *shape), blend)
    return surface


def loft(p, rings, exponent=2.0):
    """Cross-sections: height, center x, center z, width radius, depth radius."""
    rings = np.asarray(rings, dtype=np.float32)
    heights = rings[:, 0]
    y = p[..., 1]
    cx, cz, side, front = np.moveaxis(
        PchipInterpolator(heights, rings[:, 1:])(np.clip(y, heights[0], heights[-1])), -1, 0)
    dx = np.abs(p[..., 0] - cx) / side
    dz = np.abs(p[..., 2] - cz) / front
    edge = ((dx**exponent + dz**exponent)**(1/exponent) - 1) * np.minimum(side, front)
    cap = np.maximum(heights[0]-y, y-heights[-1])
    return -mix_surface(-edge, -cap, .009)


def front_patch(p, points, z_at, half_depth, roundness):
    x, y, z = np.moveaxis(p, -1, 0)
    polygon = np.asarray(points)
    if np.sum(polygon[:, 0]*np.roll(polygon[:, 1], -1)
              - polygon[:, 1]*np.roll(polygon[:, 0], -1)) < 0:
        polygon = polygon[::-1]
    edges, distances = [], []
    for a, b in zip(polygon, np.roll(polygon, -1, axis=0)):
        dx, dy = b-a
        edges.append((dy*(x-a[0])-dx*(y-a[1]))/np.hypot(dx, dy))
        t = np.clip(((x-a[0])*dx+(y-a[1])*dy)/(dx*dx+dy*dy),0,1)
        distances.append(np.sqrt((x-a[0]-t*dx)**2+(y-a[1]-t*dy)**2))
    signed = np.minimum.reduce(distances) * np.where(np.maximum.reduce(edges)>0,1,-1)
    depth = np.abs(z-z_at(y))-half_depth
    # Rounded extruded pattern pieces give collars/pockets a real soft edge.
    return (np.sqrt(np.maximum(signed,0)**2+np.maximum(depth,0)**2)
            +np.minimum(np.maximum(signed,depth),0)-roundness)


def capsule(p, first, last, radius):
    direction = np.asarray(last) - first
    q = p - first
    t = np.clip(np.sum(q * direction, axis=-1) / (direction @ direction), 0, 1)
    return np.linalg.norm(q - t[..., None] * direction, axis=-1) - radius


def curve(p, points, radius):
    return np.minimum.reduce([capsule(p, a, b, radius) for a, b in zip(points, points[1:])])


def mirror_x(p):
    return np.stack((np.abs(p[..., 0]), p[..., 1], p[..., 2]), axis=-1)


def iso(field, bounds, step, max_faces):
    axes = [np.linspace(a, b, int(np.ceil((b-a)/step)) + 1, dtype=np.float32) for a, b in bounds]
    grid = np.stack(np.meshgrid(*axes, indexing="ij"), axis=-1)
    distance = field(grid).astype(np.float32)
    vertices, faces, _, _ = marching_cubes(
        distance, 0, spacing=tuple(float(a[1]-a[0]) for a in axes),
        gradient_direction="ascent", allow_degenerate=False)
    vertices = vertices.astype(np.float32) + [a[0] for a in axes]
    if len(faces) > max_faces:
        vertices, faces = fast_simplification.simplify(vertices, faces, target_count=max_faces)
    epsilon = min(step*.25,.001)
    normals = np.column_stack([field(vertices+axis)-field(vertices-axis)
                               for axis in np.eye(3)*epsilon])
    normals /= np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-9)
    indices, adjacent = faces.ravel(), faces[:,(1,2,0)].ravel()
    counts = np.bincount(indices,minlength=len(vertices))+np.bincount(adjacent,minlength=len(vertices))
    for _ in range(2):
        averaged=np.zeros_like(normals)
        np.add.at(averaged,indices,normals[adjacent]);np.add.at(averaged,adjacent,normals[indices])
        normals=.65*normals+.35*averaged/np.maximum(counts[:,None],1)
        normals/=np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-9)
    # Orient triangles using the surface normals (marching-cubes convention can differ).
    corners=vertices[faces]
    if np.sum(np.cross(corners[:,1]-corners[:,0],corners[:,2]-corners[:,0])*normals[faces].mean(axis=1))<0:
        faces=faces[:,(0,2,1)]
    probe = vertices[::17]
    direction = normals[::17]
    if np.mean(field(probe + direction*.002)-field(probe-direction*.002)) < 0:
        normals = -normals
        faces = faces[:, (0, 2, 1)]
    return vertices, normals, faces


def blend(a, b, ratio):
    ratio = np.clip(ratio, 0, 1)
    ratio = ratio*ratio*(3-2*ratio)
    return (1-ratio[:, None])*a + ratio[:, None]*b


def weights(vertices, family):
    # 0 waist, 1 torso, 2 head, then left/right shoulder–elbow–hand
    # and left/right hip–knee–foot. Each geometric section uses these anchors.
    v = vertices
    x, y, z = v.T
    bones = np.eye(15, dtype=np.float32)
    if family == "head":
        return np.repeat(bones[2:3], len(v), axis=0)
    if family in ("top", "arm"):
        shoulder = bones[np.where(x < 0, 3, 6)]
        elbow = bones[np.where(x < 0, 4, 7)]
        hand = bones[np.where(x < 0, 5, 8)]
        limb = blend(elbow, shoulder, (y-.795)/.13)
        limb = blend(hand, limb, (y-.635)/.075)
        if family == "arm":
            return limb
        upper = blend(np.repeat(bones[0:1], len(v), axis=0),
                      np.repeat(bones[1:2], len(v), axis=0), (y-.68)/.16)
        inner = .145 + .028*np.clip((1-y)/.14, 0, 1)
        seam_width = .065 - .040*np.clip((.96-y)/.07, 0, 1)
        return blend(upper, limb, (np.abs(x) - inner)/seam_width)
    hip = bones[np.where(x < 0, 9, 12)]
    knee = bones[np.where(x < 0, 10, 13)]
    foot = bones[np.where(x < 0, 11, 14)]
    if family == "shoe":
        return foot
    lower = blend(knee, hip, (y-.285)/.15)
    if family == "pants":
        # Blend across the centre seam at the seat as the two legs swing.
        left = np.broadcast_to(bones[9], lower.shape)
        right = np.broadcast_to(bones[12], lower.shape)
        lower = blend(lower, blend(left, right, (x+.034)/.068), np.clip((y-.495)/.065, 0, 1))
        return blend(lower, np.repeat(bones[0:1], len(v), axis=0), (y-.535)/.156)
    return blend(foot, lower, (y-.071)/.085)


def proportion_map(v, family):
    """B concept landmarks; map the sculpt and keep weights in its original rest space."""
    result = v.copy()
    if family == "head":
        result[:,0] *= .94
        result[:,1] = 1.17 + (v[:,1]-1.17)*.94 + .072
        result[:,2] *= .971
        return result
    x, y, z = v.T
    lifted = PchipInterpolator([0,.07,.36,.65,1.095,1.18,1.60],
                              [0,.07,.4125,.755,1.165,1.25,1.67])(y)
    # Shorten both arm segments slightly while preserving the hand shape and
    # shoulder attachment; relaxed fingertips stay on the middle of the thigh.
    arm_y = y + .07 - .015*np.clip((1.095-y)/.45, 0, 1)
    if family == "arm":
        result[:,1] = arm_y
    elif family == "top":
        t = np.clip((np.abs(x)-.145)/.085,0,1)
        t = t*t*(3-2*t)
        result[:,1] = lifted*(1-t) + arm_y*t
    else:
        result[:,1] = lifted
    # Narrow shoulders/waist while allowing relaxed hands to remain outside the hips.
    width = .85 + .09*np.clip((.92-y)/.27,0,1) if family in ("arm","top") else .88
    if family in ("arm", "top"):
        width *= 1 - .04*np.clip((y-.85)/.22, 0, 1)
    result[:,0] *= width
    result[:,2] *= .96
    return result


def proportion_mesh(vertices, normals, family):
    # Inverse-transpose Jacobian preserves smooth lighting after the nonuniform reshape.
    epsilon = .0001
    jacobian = np.stack([(proportion_map(vertices+axis, family)-
                         proportion_map(vertices-axis, family))/(2*epsilon)
                        for axis in np.eye(3)*epsilon],axis=2)
    normals = np.linalg.solve(jacobian.transpose(0,2,1), normals[...,None])[...,0]
    normals /= np.maximum(np.linalg.norm(normals,axis=1)[:,None],1e-9)
    return proportion_map(vertices,family), normals


def write_model(path, sections):
    with path.open("wb") as stream:
        stream.write(b"PBM1" + struct.pack("<I", len(sections)))
        for material, family, (vertices, normals, faces) in sections:
            if len(vertices) > 65535:
                raise ValueError("Section exceeds Unity's 16-bit index limit")
            if family == "head":
                vertices = vertices.copy()
                vertices[:, 1] -= .044
            w = weights(vertices, family)
            vertices, normals = proportion_mesh(vertices, normals, family)
            strongest = np.argsort(-w, axis=1)[:, :4]
            palette = np.take_along_axis(w, strongest, axis=1)
            palette /= palette.sum(axis=1)[:, None]
            vertex = np.empty(len(vertices), dtype=[
                ("position", "<f4", 3), ("normal", "<f4", 3),
                ("bones", "u1", 4), ("weights", "<f4", 4)])
            vertex["position"], vertex["normal"] = vertices, normals
            vertex["bones"], vertex["weights"] = strongest, palette
            stream.write(struct.pack("<BII", material, len(vertices), faces.size))
            stream.write(vertex.tobytes())
            stream.write(faces.astype("<u2").tobytes())
    print(path.name, sum(len(s[2][0]) for s in sections), "vertices")


def face(p):
    skull = loft(p, [
        (1.212, 0, .005, .028, .030),
        (1.226, 0, .012, .053, .053),
        (1.251, 0, .015, .080, .080),
        (1.285, 0, .006, .101, .102),
        (1.326, 0, -.007, .115, .117),
        (1.377, 0, -.017, .117, .121),
        (1.421, 0, -.021, .106, .110),
        (1.459, 0, -.022, .068, .071),
        (1.476, 0, -.022, .015, .019),
    ], 2.05)
    ears = ellipsoid(mirror_x(p), (.118, 1.309, -.007), (.019, .028, .022))
    nose = sculpt(p, [
        ((0, 1.322, .104), (.013, .025, .016)),
        ((0, 1.307, .119), (.019, .019, .019)),
    ], .014)
    return mix_surface(mix_surface(skull, ears, .012), nose, .015)


def face_front(x, y):
    ray = np.linspace(.08, .19, 480)
    points = np.column_stack((np.full_like(ray, x), np.full_like(ray, y), ray))
    return ray[np.nonzero(face(points) < 0)[0][-1]]


def facial_curve(p, profile, radius):
    return curve(p, [(x, y, face_front(x, y)+.001) for x, y in profile], radius)


def hair_base(p):
    x, y, z = np.moveaxis(p, -1, 0)
    cap = ellipsoid(p, (0, 1.387, -.027), (.126, .101, .120))
    hairline = 1.285 + np.clip((z + .055)/.15, 0, 1)*.113
    cap = np.maximum(cap, hairline-y)
    fringe = ellipsoid(p, (-.031, 1.444, .052), (.096, .034, .061), -8)
    side = ellipsoid(p, (.106, 1.378, .005), (.030, .057, .064), 12)
    return mix_surface(mix_surface(cap, fringe, .011), side, .008)


@lru_cache(maxsize=1)
def hair_channels():
    channels = []
    for z_start in (.035, .067, .096):
        path = []
        for t in np.linspace(0,1,14):
            x = -.108 + .173*t
            z = z_start - .04*t*t
            ray = np.linspace(1.398,1.51,300)
            probes = np.column_stack((np.full_like(ray,x),ray,np.full_like(ray,z)))
            inside = np.nonzero(hair_base(probes)<0)[0]
            if len(inside):
                path.append((x,ray[inside[-1]]+.001,z))
        channels.append(path)
    return channels


def hair(p):
    field = hair_base(p)
    # Shallow swept grooves are baked into the mesh, not painted dark stripes.
    for channel in hair_channels():
        field = np.maximum(field, -curve(p,channel,.004))
    return field


def bare_arms(p):
    p = mirror_x(p)
    arm = loft(p, [
        (.651, .244, .025, .027, .025), (.699, .239, .022, .031, .029),
        (.754, .231, .016, .038, .035), (.822, .219, .015, .044, .042),
        (.881, .215, .007, .044, .042), (.916, .212, .010, .047, .044),
        (.939, .208, .010, .041, .039), (.957, .203, .010, .030, .030),
    ])
    hand = sculpt(p, [
        ((.250, .636, .022), (.029, .032, .024), 6),
        ((.230, .604, .036), (.0085, .029, .012), 4),
        ((.244, .597, .039), (.0085, .031, .012), 3),
        ((.258, .599, .039), (.0085, .029, .012), 1),
        ((.270, .606, .035), (.0075, .024, .011), -2),
        ((.213, .631, .054), (.012, .030, .014), -20),
    ], .003)
    return mix_surface(arm, hand, .012)


def top_field(p, cut):
    is_short = cut == "short"
    winter = cut == "winter"
    shell = loft(p, [
        (.664 if not winter else .628, 0, 0, .150, .086),
        (.680 if not winter else .644, 0, 0, .163, .100),
        (.756, 0, .002, .158, .100),
        (.893, 0, .003, .160, .111),
        (1.013, 0, .002, .170, .119),
        (1.067, 0, .002, .148, .111),
        (1.106, 0, -.001, .104, .085),
        (1.137, 0, -.010, .054, .065),
    ], 2.5) - (.012 if winter else .003)
    bottom = .922 if is_short else .659
    sleeve = loft(mirror_x(p), [
        (bottom, .211 if is_short else .244, .010, .058 if is_short else .038, .053 if is_short else .037),
        (bottom+.015, .209 if is_short else .242, .010, .064 if is_short else .045, .060 if is_short else .043),
        (.978 if is_short else .774, .196 if is_short else .230, .011, .062, .059),
        (1.033 if is_short else .920, .185 if is_short else .206, .011, .064, .063),
        (1.072, .161, .002, .059, .055), (1.108, .130, -.002, .038, .035),
    ]) - (.010 if winter else 0)
    clothing = mix_surface(shell, sleeve, .054)
    if cut in ("short", "formal"):
        x, y, z = np.moveaxis(p, -1, 0)
        # A shallow V opening and a central placket read at the portrait distance.
        neckline = np.maximum(1.075 + .91*np.abs(x)-y, .071-z)
        clothing = -mix_surface(-clothing, neckline, .007)
    return clothing


@lru_cache(maxsize=4)
def placket(cut):
    placket_points = []
    for y in np.linspace(.680, 1.042, 16):
        ray = np.linspace(.075, .173, 240)
        line = np.column_stack((np.full_like(ray, 0), np.full_like(ray, y), ray))
        z = ray[np.nonzero(top_field(line, cut) < 0)[0][-1]]
        placket_points.append((0, y, z+.0016))
    return placket_points


def blouse_trim(p, cut):
    front = .008 if cut == "winter" else 0
    collar = front_patch(mirror_x(p), [
        (.014, 1.138), (.051, 1.139), (.086, 1.101), (.054, 1.084)],
        lambda y: .105 - .85*(y-1.083) + front, .004, .005)
    seam = curve(p, [(x,y,z+.002) for x,y,z in placket(cut)], .0035)
    pocket = front_patch(p, [(-.124,.974),(-.057,.974),(-.059,.894),(-.087,.886),(-.121,.894)],
        lambda y: .111 + front, .007, .006)
    trim = np.minimum.reduce([collar, seam, pocket])
    if cut == "short":
        cuff = loft(mirror_x(p), [
            (.920, .211, .010, .058, .054),
            (.924, .211, .010, .067, .064),
            (.945, .206, .010, .068, .064),
            (.948, .205, .010, .065, .062),
        ])
        trim = np.minimum(trim, cuff)
    return trim


def undershirt(p):
    return front_patch(p, [(-.029, 1.119), (.029, 1.119), (0, 1.104)],
        lambda y: .075 - .28*(y-1.082), .005, .002)


def shorts_or_pants(p, short):
    bottoms = loft(mirror_x(p), [
        (.385 if short else .075, .104, 0, .068 if short else .043, .069 if short else .050),
        (.396 if short else .101, .104, 0, .074 if short else .045, .074 if short else .054),
        (.413 if short else .122, .104, 0, .073 if short else .047, .073 if short else .056),
        (.453 if short else .275, .108, .005, .077 if short else .060, .084 if short else .069),
        (.526 if short else .462, .106, -.002, .080, .084),
        (.606, .097, -.005, .088, .096), (.664, .075, -.008, .079, .097),
    ], 2.3)
    seat = rounded_box(p, (0, .623, -.011), (.315, .119, .199), .050)
    return mix_surface(bottoms, seat, .028)


def calves(p):
    return loft(mirror_x(p), [
        (.064, .104, 0, .033, .033), (.117, .104, 0, .036, .039),
        (.196, .108, -.011, .047, .048), (.258, .107, -.011, .057, .055),
        (.323, .104, .004, .055, .057), (.366, .104, .004, .056, .057),
        (.400, .104, .004, .057, .063),
    ])


def shoe_upper(p):
    shoe = sculpt(mirror_x(p), [
        ((.104, .058, .066), (.062, .034, .092)),
        ((.104, .073, .006), (.056, .050, .066)),
        ((.104, .044, -.043), (.056, .036, .041)),
    ], .026)
    # Extend the upper down to the welt instead of leaving an ellipsoid-shaped
    # air gap above the sole at the toe and heel.
    welt = loft(mirror_x(p), [
        (.015, .104, .041, .060, .102),
        (.027, .104, .041, .061, .104),
        (.039, .104, .041, .059, .102),
    ], 2.7)
    return np.maximum(mix_surface(shoe, welt, .018), .014 - p[..., 1])


def shoes(p):
    upper=shoe_upper(p)
    opening=ellipsoid(mirror_x(p),(.104,.123,-.011),(.035,.035,.037))
    return np.maximum(upper,-opening)


@lru_cache(maxsize=2)
def pants_lines(short):
    paths=[[(.008,y) for y in np.linspace(.55,.654,20)]]
    for side in (-1,1):
        paths.append([(side*(.080+.072*t), .65-.075*np.sin(t*np.pi/2)) for t in np.linspace(0,1,24)])
    result=[]
    for path in paths:
        line=[]
        for x,y in path:
            ray=np.linspace(.01,.15,400)
            probes=np.column_stack((np.full_like(ray,x),np.full_like(ray,y),ray))
            occupied=np.nonzero(shorts_or_pants(probes,short)<0)[0]
            if len(occupied):line.append((x,y,ray[occupied[-1]]+.001))
        result.append(line)
    return result


def pants_seams(p,short):
    return np.minimum.reduce([curve(p,line,.0018) for line in pants_lines(short)])


@lru_cache(maxsize=1)
def shoe_apron():
    line=[]
    for a in np.linspace(-np.pi*.1,np.pi*1.1,40):
        x=.104+.047*np.cos(a);z=.040+.098*np.sin(a)
        ray=np.linspace(.035,.15,400)
        probes=np.column_stack((np.full_like(ray,x),ray,np.full_like(ray,z)))
        occupied=np.nonzero(shoe_upper(probes)<0)[0]
        if len(occupied):line.append((x,ray[occupied[-1]]+.001,z))
    return line


def shoe_trim(p):
    q=mirror_x(p)
    rim=curve(q,shoe_apron(),.0023)
    strap=rounded_box(q,(.104,.111,.035),(.088,.010,.022),.004)
    return np.minimum(rim,strap)


def add(section, material, family, field, bounds, step=.0055, max_faces=3500):
    section.append((material, family, iso(field, bounds, step, max_faces)))


def main():
    out = Path(__file__).resolve().parents[1] / "Assets/Resources/Characters"
    out.mkdir(parents=True, exist_ok=True)
    head = ((-.18, .18), (1.15, 1.55), (-.175, .19))
    face_detail = ((-.135, .135), (1.22, 1.395), (.078, .177))
    top = ((-.32, .32), (.59, 1.17), (-.175, .175))
    legs = ((-.22, .22), (.043, .74), (-.15, .15))
    core = []
    add(core, SKIN, "head", face, head, .003, 10000)
    add(core, HAIR, "head", hair, head, .0027, 10000)
    for sign in (-1, 1):
        eyes = (sign*.049, 1.334)
        add(core, EYE, "head", lambda p: ellipsoid(p,
            (*eyes, face_front(*eyes)+.0004), (.0105, .016, .007)),
            face_detail, .0039, 180)
        add(core, BROW, "head", lambda p: facial_curve(p, [
            (sign*.029, 1.362), (sign*.041, 1.367), (sign*.054, 1.369),
            (sign*.067, 1.366)], .0047), face_detail, .0039, 220)
    add(core, LIP, "head", lambda p: facial_curve(p, [
        (-.027, 1.272), (-.015, 1.266), (0, 1.264), (.015, 1.267), (.027, 1.273)], .0027),
        face_detail, .0038, 260)
    add(core, SKIN, "arm", bare_arms, ((-.315, .315), (.535, 1.14), (-.096, .102)), .0038, 6000)
    add(core, SKIN, "top", lambda p: ellipsoid(p, (0, 1.150, -.002), (.048, .103, .049)),
        ((-.067, .067), (1.034, 1.265), (-.069, .067)), .005, 600)
    add(core, SHOE, "shoe", shoes, ((-.20, .20), (.004, .165), (-.10, .19)), .0048, 1600)
    add(core, SOLE, "shoe", lambda p: loft(mirror_x(p), [
            (.002, .104, .041, .054, .093), (.005, .104, .041, .061, .102),
            (.018, .104, .041, .063, .105), (.023, .104, .041, .062, .103),
        ], 2.7),
        ((-.20, .20), (-.004, .044), (-.10, .174)), .0048, 900)
    add(core, SHOE, "shoe", shoe_trim, ((-.19,.19),(.025,.14),(-.03,.16)),.0025,1600)
    write_model(out / "PlayerB-Core.bytes", core)
    for cut in ("short", "long", "winter", "formal"):
        sections = []
        add(sections, TOP, "top", lambda p: top_field(p, cut), top, .004, max_faces=10000)
        add(sections, TRIM, "top", lambda p: blouse_trim(p, cut), top, .003, 2600)
        if cut in ("short", "formal"):
            add(sections, CREAM, "top", undershirt,
                ((-.065,.065), (1.018,1.156), (.035,.116)), .0047, 480)
        if cut == "short":
            add(sections, CREAM, "top", lambda p: rounded_box(p, (-.093, .982, .117), (.037, .013, .011), .002),
                ((-.122,-.063), (.969,1.000), (.103,.14)), .0034, 250)
        if cut == "formal":
            add(sections, TIE, "top", lambda p: front_patch(p, [
                (-.010, 1.109), (.010, 1.109), (.015,.925), (0,.904), (-.015,.925)],
                lambda y: .137 - .23*(y-.94), .013, .0025),
                ((-.03,.03), (.885,1.128), (.085,.160)), .0047, 450)
        write_model(out / f"PlayerB-{cut}.bytes", sections)
    for short in (True, False):
        sections = []
        add(sections, PANTS, "pants", lambda p: shorts_or_pants(p, short), legs)
        add(sections,PANTS,"pants",lambda p:pants_seams(p,short),
            ((-.18,.18),(.52,.68),(.02,.14)),.0024,1400)
        if short:
            add(sections, SKIN, "calf", calves, legs, max_faces=2500)
            add(sections, PANTS, "pants", lambda p: loft(mirror_x(p), [
                (.383, .104, 0, .072, .073), (.388, .104, 0, .077, .078),
                (.408, .104, .001, .079, .080), (.412, .104, .001, .076, .078),
            ], 2.3), legs, .004, 1000)
        write_model(out / f"PlayerB-{'shorts' if short else 'trousers'}.bytes", sections)


if __name__ == "__main__":
    main()
