"""Street-facing fittings for the old walk-up. All coordinates are in metres."""
from math import sin, cos, pi


# Each row is a floor, starting at the lowest inhabited floor.
END_WINDOWS = (
    ("wash", "plain", "basket"),
    ("bars", "basket", "unit"),
    ("hood", "basket", "hood"),
)
STREET_WINDOWS = (
    ("wash", "bars", "unit", "basket"),
    ("bars", "basket", "unit", "wash"),
    ("unit", "basket", "unit", "hood"),
)


def window_fittings(m, xyz, box, panel, kind, u, sill, head, span, seed, basket_unit=False, open_sash=False, warm_cloth=False):
    def rod(a, b, thickness, material="ink"):
        m.tube(xyz(*a), xyz(*b), thickness, thickness, material, 5)

    left, right = u-span*.5-.035, u+span*.5+.035
    depth = .33 + .04*(seed % 3)
    if open_sash:
        # The casement remains a separate pane when the camera sees its back.
        hinge=left+.11
        wing=span*.38
        x=hinge+wing*cos(.96)
        reach=.025+wing*sin(.96)
        low, high=sill+.11,head-.09
        frame=[(hinge,low,.025),(x,low,reach),(x,high,reach),(hinge,high,.025)]
        sheet=[xyz(*point) for point in frame]
        m.face(sheet,'side20',uv_rect=(.26,.38,.65,.74))
        m.face(sheet[::-1],'side20',uv_rect=(.26,.38,.65,.74))
        for a,b in zip(frame,frame[1:]+frame[:1]):
            rod(a,b,.031,'ink')
        rod((hinge,low+.18,.025),(x,low+.18,reach),.018)
    if kind in ("basket", "hood", "wash", "unit"):
        # Folded tin with a visible return on the two ends and a dark turned-down lip.
        deep = depth + (.05 if kind == "basket" else -.035)
        width = span+.18
        y = head+.085
        ends = (u-width/2, u+width/2)
        lid = [xyz(ends[0],y,.015), xyz(ends[1],y,.015),
               xyz(ends[1],y-.075,deep), xyz(ends[0],y-.075,deep)]
        m.face(lid,"tinhood",(0,1,0))
        m.face(lid[::-1],"jamb",(0,-1,0))
        box(u,y-.10,deep,width,.044,.036,"ink")
        for x in ends:
            rod((x,y-.015,.014),(x,y-.09,deep),.026)
    if kind in ("basket","bars"):
        reach = depth if kind == "basket" else .07
        floor = sill-.12 if kind == "basket" else sill+.04
        top = head+.01
        for y in (floor, floor+.15, top):
            box(u,y,reach,right-left,.038,.036,"ink")
        for j in range(7):
            x = left+(right-left)*j/6
            box(x,(top+floor)/2,reach,.032 if j in (0,6) else .023,top-floor,.032,"ink")
        if kind == "basket":
            box(u,floor+.023,depth*.49,right-left,.051,depth+.015,"frame")
            for x in (left,right):
                for y in (floor,top):
                    box(x,y,depth*.5,.045,.047,depth,"frame")
                rod((x,sill-.38,.012),(x,floor,depth),.028)
            if seed % 2:
                # A lopsided diamond in the middle band catches the light at this scale.
                for x in (u-.26,u+.26):
                    rod((x,floor+.17,reach+.022),(u,floor+.42,reach+.022),.015)
    if kind == "unit" or basket_unit:
        x = u+(.23 if seed%2 else -.22)*span
        for dx in (-.26,.26):
            rod((x+dx,sill-.56,.015),(x+dx,sill-.37,.40),.028)
        box(x,sill-.20,.22,.71,.41,.42,"sill")
        panel(x,sill-.20,.436,.65,.34,"ac")
        box(x,sill+.013,.21,.75,.042,.46,"concrete")
    if kind != "wash":
        return

    line_y = sill+.59
    box(u,line_y,depth,span+.21,.046,.042,"wood")
    for x in (left,right):
        rod((x,line_y-.15,.015),(x,line_y,depth),.028)
    # Separate folded pieces have different hem lengths. Both faces survive a camera turn.
    for j,(offset,width,length,mat) in enumerate(((-.26,.41,.76,"linen"),(.13,.32,.58,"blue"),(.34,.22,.43,"linen"))):
        x = u+offset*span
        y = line_y+.035-.018*j
        if warm_cloth and j==1:
            mat="oxidecloth"
        def edge(t):
            ripple=sin(t*pi*3.4+j)*.028
            return (xyz(x+(t-.5)*width,y-.022*sin(t*pi),depth+.025+ripple*.22),
                    xyz(x+(t-.5)*width+.036*(t-.4),y-length-.046*sin(t*pi)+.02*t,
                        depth+.075+ripple))
        # Leave a slit between the two trouser hems. The pale shirt has a pair
        # of short folded sleeves, which give it a distinct silhouette at 1x.
        strips=((0,.22),(.22,.46),(.54,.77),(.77,1)) if j==1 else [(k/5,(k+1)/5) for k in range(5)]
        for a,b in strips:
            top_a,low_a=edge(a)
            top_b,low_b=edge(b)
            corners=[low_a,low_b,top_b,top_a]
            m.face(corners,mat,uv_rect=(a,0,b,1))
            m.face(corners[::-1],mat,uv_rect=(a,0,b,1))
        if j==1:
            a,b=edge(.46)[0],edge(.54)[0]
            c=xyz(x+.04*width,y-length*.40,depth+.056)
            d=xyz(x-.04*width,y-length*.39,depth+.056)
            m.face([d,c,b,a],mat,uv_rect=(.46,.60,.54,1))
            m.face([a,b,c,d],mat,uv_rect=(.46,.60,.54,1))
        if j==0:
            for sign in (-1,1):
                sx=x+sign*width*.46
                sleeve=[xyz(sx,y-.024,depth+.028),xyz(sx+sign*.14,y-.094,depth+.035),
                        xyz(sx+sign*.095,y-.225,depth+.057),xyz(sx,y-.176,depth+.049)]
                m.face(sleeve,mat)
                m.face(sleeve[::-1],mat)
        for dx in (-width*.34,width*.34):
            box(x+dx,y+.016,depth+.026,.034,.082,.040,"wood")


def shop_awning(m, xyz, u, span, style, seed):
    # Each canvas panel has its own sag and a narrow seam at the rib.
    high = 2.58 - .045*(seed%3)
    reach = .94 + .10*(seed%3)
    count=4
    def at(s, front):
        bow=sin(s*pi*count)**2
        return xyz(u+(s-.5)*span,high-(.35+.055*bow if front else 0),
                   reach+.038*bow if front else .015)
    for i in range(count):
        for j in (0,1):
            a,b=(i+j*.5)/count,(i+(j+1)*.5)/count
            face=[at(a,False),at(b,False),at(b,True),at(a,True)]
            m.face(face,style,(0,1,0),(a,0,b,1))
            m.face(face[::-1],"jamb",(0,-1,0))
            lip=[at(a,True),at(b,True)]
            hem=[(x,y-.13+.025*sin(t*pi*count)**2,z-.018) for t,(x,y,z) in zip((a,b),lip)]
            m.face([hem[0],hem[1],lip[1],lip[0]], style,uv_rect=(a,0,b,.16))
            m.face([lip[0],lip[1],hem[1],hem[0]], style,uv_rect=(a,0,b,.16))
    for frac in (-.46,0,.46):
        x=u+frac*span
        m.tube(xyz(x,high-.67,.016),xyz(x,high-.38,reach-.05),.024,.024,"frame",5)


def produce_crate(m, x, y, z, width=.59, depth=.48, height=.46, filled=False):
    # Recessed dark interior and separate slats remain readable from above.
    m.box(x,y+.043,z,width-.03,.086,depth-.03,"wood")
    m.box(x,y+height*.39,z,width-.083,height*.60,depth-.082,"frame")
    for sign in (-1,1):
        for level in (.25,.56,.88):
            m.box(x,y+height*level,z+sign*depth*.5,width,height*.23,.039,"crate")
            m.box(x+sign*width*.5,y+height*level,z,.040,height*.23,depth,"wood")
        for cross in (-1,1):
            m.box(x+sign*(width/2-.035),y+height*.5,z+cross*(depth/2-.020),.064,height,.057,"wood")
    if filled:
        for dx,dz,h in [(-.12,.06,.17),(.095,-.06,.23),(.10,.11,.15)]:
            m.tube((x+dx,y+height*.76,z+dz),(x+dx,y+height*.76+h,z+dz),.066,.058,"green",6)
