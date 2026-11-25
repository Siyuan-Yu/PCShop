"""Bake the painted street elevations and ground surfaces into the old-quarter material set."""
import numpy as np
from PIL import Image, ImageDraw, ImageFilter


def image_rgb(values):
    return Image.fromarray(np.clip(values, 0, 255).astype(np.uint8))


def elevation(art, columns, width, height, drawing):
    # Register the painted jambs to the actual mesh opening in each bay. The
    # three stories remain one contiguous UV island, including plaster and belt courses.
    section = np.asarray(art.convert('RGB'))
    h, w = section.shape[:2]
    if columns == 3:
        panes=[[(49,64,131,116),(209,68,295,122),(369,86,452,134)],
               [(36,207,130,270),(203,214,292,251),(367,228,448,282)],
               [(36,350,129,374),(204,360,299,427),(356,373,445,426)]]
        ranges = [(107,416), (611,917), (1111,1412)]
        rows = [(48,247), (395,593), (742,938)]
        cuts = [0,334,682,1040]
        spans = [.57,.60,.56]
    else:
        panes=[[(54,57,100,109),(166,63,232,114),(287,59,349,107),(407,60,467,107)],
               [(41,202,105,258),(167,205,225,251),(280,206,344,243),(410,193,462,217)],
               [(46,341,105,365),(166,341,227,398),(283,343,340,373),(405,343,466,396)]]
        ranges = [(55,316),(437,695),(817,1079),(1201,1454)]
        rows = [(49,254),(396,601),(744,948)]
        cuts = [0,347,691,1040]
        spans = [.61,.63,.62,.59]
    x = (np.arange(width)+.5)/width
    y = (np.arange(height)+.5)/height
    xknots, samples = [0.], [0.]
    for i, (left,right) in enumerate(ranges):
        xknots += [(i+.5-spans[i]*.5)/columns, (i+.5+spans[i]*.5)/columns, (i+1)/columns]
        samples += [left, right, (i+1)*w/columns]
    yknots, scanlines = [0.], [0.]
    for r, (head,sill) in enumerate(rows):
        half=(1.27,1.32,1.26)[r]/(2*2.43)
        center = r+.5-.10/2.43
        yknots += [(center-half)/3,(center+half)/3,(r+1)/3]
        scanlines += [head,sill,cuts[r+1]]
    sx=np.interp(x,xknots,samples).clip(0,w-1.001)
    sy=np.interp(y,yknots,scanlines).clip(0,h-1.001)
    ix,iy=sx.astype(int),sy.astype(int)
    fx=(sx-ix)[None,:,None]; fy=(sy-iy)[:,None,None]
    rgb=(section[iy[:,None],ix]*(1-fx)+section[iy[:,None],ix+1]*fx)*(1-fy)
    rgb+=(section[iy[:,None]+1,ix]*(1-fx)+section[iy[:,None]+1,ix+1]*fx)*fy
    # Compensate for the daylight in the art; runtime lighting remains directional.
    if columns==3:
        rgb += [5,10,13]
    else:
        value=rgb@np.array([.24,.63,.13])
        rgb=(value[:,:,None]+.86*(rgb-value[:,:,None]))*[.94,.97,.99]+[4,4,4]
    # The projecting sills and hoods are now geometry. Leave painted detail inside
    # the opening, and remove their old painted doubles from the surrounding limewash.
    xx, yy=np.meshgrid(x,y)
    panel=np.zeros_like(xx)
    drip=np.zeros_like(xx)
    contact=np.zeros_like(xx)
    pigment=Image.new('L',(width,height),128)
    pen=ImageDraw.Draw(pigment)
    rng=np.random.default_rng(8401+columns)
    for row in range(3):
        center=(row+.5-.10/2.43)/3
        half=(1.27,1.32,1.26)[row]/(2*2.43*3)
        for i,span in enumerate(spans):
            left=(i+.5-span*.5)/columns
            right=(i+.5+span*.5)/columns
            sill=center+half
            inside=np.minimum.reduce((xx-left,right-xx,yy-center+half,sill-yy))
            panel=np.maximum(panel,np.clip(inside*width+1,0,1))
            # Use only the interior of this particular window. The reference's
            # overhanging AC units, laundry and rain hoods are separate geometry.
            x0,x1=round(left*width),round(right*width)
            y0,y1=round((center-half)*height),round(sill*height)
            insert=drawing.crop(panes[row][i])
            pane=np.asarray(insert.resize((x1-x0,y1-y0),Image.Resampling.BICUBIC),dtype=float)
            pane=pane*(1.38 if columns==3 else 1.07)+[3,4,4]
            amount=.81
            rgb[y0:y1,x0:x1]=rgb[y0:y1,x0:x1]*(1-amount)+pane*amount
            # Local occlusion at the sill, including the soft shadow that disappears
            # below its ends. The sun and the cage itself still cast moving shadows.
            across=np.minimum(np.clip((xx-left+.012)/.018,0,1),np.clip((right+.012-xx)/.018,0,1))
            contact+=across*np.exp(-np.maximum(0,yy-sill)/.009)*(yy>sill)
            for jamb in (left,right):
                tail=np.maximum(yy-sill,0)
                drip+=np.exp(-((xx-jamb)/.0055)**2-tail/.026)*(yy>sill)
                if rng.random()<.55:
                    px,py=round(jamb*width),round(sill*height)
                    for j in range(3):
                        nx,ny=px+int(rng.integers(-3,4)),py+int(rng.integers(4,13))
                        pen.line((px,py,nx,ny),fill=int(rng.integers(67,102)),width=2)
                        px,py=nx,ny
        # A handful of panel joints stop at belt courses and turn at repairs.
        for u in (.035,.322,.667,.974):
            px=int((u+rng.uniform(-.015,.015))*width)
            y0=int((row+rng.uniform(.03,.24))*height/3)
            y1=int((row+rng.uniform(.72,.99))*height/3)
            pen.line([(px,y0),(px+1,(y0+y1)//2),(px-1,y1)],fill=101,width=2)
    fine=np.asarray(pigment,dtype=float)-128
    field=Image.fromarray(rng.integers(75,182,(15,21),dtype=np.uint8))
    field=np.asarray(field.resize((width,height),Image.Resampling.BICUBIC),dtype=float)-128
    shade=rgb@np.array([.26,.60,.14])
    # Recover the small broken strokes in the plaster between the painted fittings.
    # Discard coarse value changes around the drawn projections; geometry supplies those.
    wall=drawing.convert('L').resize((width,height),Image.Resampling.BICUBIC)
    detail=np.asarray(wall,dtype=float)-np.asarray(wall.filter(ImageFilter.GaussianBlur(2.4)),dtype=float)
    wash=(shade-np.median(shade))*.19+field*.085+fine*.72-9*np.clip(drip,0,1.3)
    wash+=np.clip(detail,-9,7)*.80-np.minimum(contact,1)*16
    base=np.array([147,131,112] if columns==3 else [189,155,117])
    rgb=rgb*panel[:,:,None]+(base+wash[:,:,None])*(1-panel[:,:,None])
    lamp=np.zeros((height,width))
    for c,r in ([(2,0),(1,2)] if columns==3 else [(0,0),(3,1)]):
        center=r+.5-.10/2.43
        in_glass=(abs(x*columns-(c+.5))<spans[c]*.39)[None,:] & (abs(y*3-center)<.205)[:,None]
        # Blue glass receives light. The painted sash, curtains and ironwork stay legible.
        glass=np.clip((rgb[:,:,2]-rgb[:,:,0]+23)/55,0,1)
        lamp=np.maximum(lamp,in_glass*glass*.43)
    return np.dstack((np.clip(rgb,0,255),lamp*255)).astype('uint8')


def frontage_wear(size, pigment):
    # R carries polished warm stone, G earth and damp. The surface shader keeps the grout.
    y,x=np.mgrid[0:size,0:size]/(size-1)
    y=1-y  # UV origin is at the foot of the wall
    def wash(cx,cy,rx,ry):
        return np.exp(-((x-cx)/rx)**2-((y-cy)/ry)**2)
    grit=(pigment-pigment.mean())/max(1,pigment.std())
    polished=wash(.49,.26,.13,.34)+.77*wash(.77,.29,.10,.27)+.33*wash(.58,.61,.19,.12)
    damp=wash(.14,.045,.12,.080)+wash(.87,.047,.095,.085)+.54*wash(.35,.06,.07,.07)
    edge=np.clip(np.minimum.reduce([x,1-x,y,1-y])/.022,0,1)
    polished=np.clip(polished*(.61+.24*grit),0,1)*edge
    damp=np.clip(damp*(.82+.32*grit),0,1)*edge
    return image_rgb(np.stack([polished,damp,np.zeros_like(y)],axis=-1)*255)


def canopy_atlas(reference):
    # Trace the two unobstructed crowns in the art board. Separate silhouettes,
    # including their painted dark edge, can sit at independent depths above 3D branches.
    crowns=[
        ((263,480),[(10,66),(11,57),(15,52),(15,47),(21,46),(23,39),(27,38),(29,31),
                    (34,31),(37,25),(44,23),(48,19),(53,17),(57,13),(64,12),(69,15),
                    (72,10),(79,11),(83,13),(90,11),(96,13),(100,14),(104,18),(112,20),
                    (118,22),(121,28),(122,35),(125,39),(127,46),(130,50),(130,57),(128,62),
                    (129,68),(126,72),(122,72),(120,80),(117,82),(112,84),(110,89),(102,88),
                    (99,91),(94,91),(92,87),(85,88),(81,92),(76,94),(72,96),(67,94),
                    (62,96),(57,97),(50,99),(42,98),(38,92),(32,91),(28,87),(21,87),(17,83),
                    (16,77),(13,74),(14,70)]),
        ((677,508),[(17,77),(19,68),(19,57),(21,52),(23,50),(24,44),(28,42),(31,37),
                    (35,34),(35,30),(40,30),(41,25),(49,23),(53,21),(56,16),(63,15),
                    (69,15),(72,20),(80,16),(88,15),(93,19),(99,21),(105,22),(110,26),
                    (112,33),(117,38),(119,41),(123,43),(124,49),(127,53),(131,55),(134,61),
                    (135,66),(138,70),(139,77),(143,81),(143,88),(140,90),(138,96),(135,99),
                    (128,99),(126,105),(122,105),(119,109),(110,111),(105,108),(101,109),
                    (96,108),(92,106),(86,109),(79,110),(76,106),(70,107),(65,106),(61,102),
                    (55,101),(50,99),(49,94),(42,96),(36,93),(32,90),(27,90),(25,87),(21,87),(19,84)])]
    atlas=Image.new('RGBA',(512,256))
    for index,((ox,oy),points) in enumerate(crowns):
        xs,ys=zip(*points)
        l,t,r,b=min(xs)-2,min(ys)-2,max(xs)+3,max(ys)+3
        rgb=reference.crop((ox+l,oy+t,ox+r,oy+b)).convert('RGBA')
        mask=Image.new('L',(rgb.width*4,rgb.height*4),0)
        ImageDraw.Draw(mask).polygon([((x-l)*4,(y-t)*4) for x,y in points],fill=255)
        rgb.putalpha(mask.resize(rgb.size,Image.Resampling.LANCZOS))
        rgb=rgb.resize((242,242),Image.Resampling.LANCZOS)
        atlas.paste(rgb,(index*256+7,7))
    return atlas


def bake_surfaces(source, dest, atlas, slots, reference):
    master=Image.open(source/'walkup-wall.png').convert('RGB')
    front=Image.open(source/'street-wall.png').convert('RGB')
    # Rectify the two illustrated walls before extracting their individual windows.
    end=reference.transform((515,490),Image.Transform.QUAD,(292,83,292,318,511,406,511,171),Image.Resampling.BICUBIC)
    street=reference.transform((515,490),Image.Transform.QUAD,(518,171,518,417,739,252,739,6),Image.Resampling.BICUBIC)
    walls=[]
    for art,bays in [(master,3),(front,4)]:
        plane=elevation(art,bays,1018,1018,end if bays==3 else street)
        walls.append(np.pad(plane,((3,3),(3,3),(0,0)),mode='edge'))
    Image.fromarray(np.concatenate(walls,axis=1)).save(dest/'Elevations.png')

    tile_size=atlas.width//8
    # These swatches follow the folds in the canvas. Map the lip to the top
    # of each UV tile so the light streaks run from the hinge to the hem.
    swatches={
        'rustawning': (552,415,530,396,579,356,602,376),
        'awning': (609,363,589,345,619,321,638,342),
        'oldcanvas': (346,364,363,345,436,374,418,394),
    }
    for name,coords in swatches.items():
        swatch=reference.transform((tile_size,tile_size),Image.Transform.QUAD,coords,Image.Resampling.BICUBIC)
        values=np.asarray(swatch.convert('RGB'),dtype=float)
        if name=='awning':
            values=values*[.90,1.04,1.13]
        gain=1.34 if name=='oldcanvas' else 1.10
        tile=slots[name]
        atlas.paste(image_rgb(values*gain),(tile%8*tile_size,tile//8*tile_size))
    # The UV is stretched onto a wide unit. An elliptical grille in the tile
    # therefore reads as a circular fan, with a separate slotted service cover.
    unit=Image.new('RGB',(192,192),(155,142,119))
    grille=ImageDraw.Draw(unit)
    grille.rectangle((4,6,187,183),outline=(109,98,81),width=3)
    grille.ellipse((16,22,94,170),fill=(72,71,61),outline=(98,91,75),width=4)
    for inset in (5,11,17):
        grille.ellipse((16+inset,22+2*inset,94-inset,170-2*inset),outline=(113,107,87),width=2)
    for v in range(35,163,13):
        half=round(36*np.sqrt(max(0,1-((v-96)/71)**2)))
        grille.line((55-half,v,55+half,v),fill=(130,121,98),width=3)
    for v in range(51,142,12):
        grille.line((111,v,172,v),fill=(99,94,79),width=3)
    grille.rectangle((147,22,174,31),fill=(92,107,99))
    tile=slots['ac']
    atlas.paste(unit.resize((tile_size,tile_size)),(tile%8*tile_size,tile//8*tile_size))
    # Garments span only a few pixels on the street. Give each fabric two
    # unequal folds and a turned hem, leaving quiet pigment between the folds.
    cy,cx=np.mgrid[:tile_size,:tile_size]/tile_size
    for j,(name,color) in enumerate([('linen',(179,169,145)),('blue',(103,122,135)),('oxidecloth',(151,89,75))]):
        crease=cx-.21-.052*np.sin(cy*3.7+j)
        fold=-29*np.exp(-(crease/.045)**2)+11*np.exp(-((crease-.066)/.071)**2)
        fold-=17*np.exp(-((cx-.72+.045*np.sin(cy*4.1))/.060)**2)
        fold-=13*np.exp(-((cy-.96)/.013)**2)
        tile=slots[name]
        atlas.paste(image_rgb(np.array(color)+fold[:,:,None]),(tile%8*tile_size,tile//8*tile_size))
    slot=slots['sand']
    atlas.paste(Image.open(source/'flat-roof.png'),(slot%8*tile_size,slot//8*tile_size))
    # Keep the roof tile drawing and introduce local variation from painted masonry.
    i=slots['roof']; xx,yy=i%8*tile_size,i//8*tile_size
    roof=np.array(atlas.crop((xx,yy,xx+tile_size,yy+tile_size)),dtype=float)
    dry=Image.open(source/'flat-roof.png').convert('L')
    stain=np.asarray(dry.resize((tile_size,tile_size)),dtype=float)
    atlas.paste(image_rgb(roof+(stain-stain.mean())[:,:,None]*.49-[4,11,15]),(xx,yy))
    sand=slots['sand']
    atlas.paste(Image.open(source/'lane-roof.png').convert('RGB').resize((tile_size,tile_size)),
                ((sand%8)*tile_size,(sand//8)*tile_size))
    atlas.save(dest/'QuarterAtlas.png')
    canopy_atlas(reference).save(dest/'Canopy.png')

    grass=np.asarray(Image.open(source/'verge.png').convert('RGB').resize((1024,1024),Image.Resampling.LANCZOS),dtype=float)
    average=grass.mean(axis=(0,1))
    grass=(grass-average)*1.30 + [130,133,74]
    image_rgb(grass).save(dest/'Grass.png')
    size=512
    pigment=np.asarray(dry.resize((size,size),Image.Resampling.BICUBIC),dtype=float)
    pavers=np.asarray(Image.open(dest/'Pavement.png').convert('RGB'),dtype=float)
    # Pigment variation follows individual stones while the wear mask follows the door locations.
    colors=np.random.default_rng(1763).normal(0,2,(8,8))
    stone=np.repeat(np.repeat(colors,64,axis=0),64,axis=1)
    pavers+=(stone+(pigment-pigment.mean())*.24)[:,:,None]*[1,.89,.70]
    image_rgb(pavers).save(dest/'Pavement.png')
    frontage_wear(size,pigment).save(dest/'FrontageWear.png')
