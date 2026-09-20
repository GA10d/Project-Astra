"""ASTRA: editable four-wall pressure cabin, authored procedurally in Blender.

Run: F:\\Blender\\blender.exe -b --python Source\\build_cabin.py
All dimensions are metres. Blender +Z is up; -Y is the A-wall direction.
The .blend retains individually editable parts. The FBX batches only static
geometry by wall/material; named interactive assemblies remain separate.
"""
import bpy
import math
import json
import random
from pathlib import Path
from mathutils import Vector, Matrix

random.seed(1943)
ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Source"
MODEL_DIR = ROOT / "UnityProject" / "Assets" / "Astra" / "Models"
SOURCE.mkdir(parents=True, exist_ok=True)
MODEL_DIR.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for d in list(bpy.data.materials):
    bpy.data.materials.remove(d)

MATS = {}
SPEC = {
    "HullPaint": ((0.22, 0.27, 0.205), 0.65, 0.65),
    "DarkSteel": ((0.052, 0.065, 0.062), 0.78, 0.50),
    "Brass": ((0.42, 0.30, 0.13), 0.75, 0.38),
    "RedPaint": ((0.34, 0.047, 0.028), 0.40, 0.49),
    "Rubber": ((0.013, 0.020, 0.020), 0.04, 0.84),
    "Canvas": ((0.25, 0.21, 0.135), 0.05, 0.91),
    "Paper": ((0.65, 0.61, 0.43), 0.05, 0.82),
    "Screen": ((0.025, 0.11, 0.078), 0.08, 0.28),
    "LampWarm": ((1.0, 0.60, 0.18), 0.05, 0.25),
    "LampRed": ((0.9, 0.045, 0.005), 0.05, 0.25),
    "LampGreen": ((0.29, 0.95, 0.14), 0.05, 0.25),
    "Glass": ((0.15, 0.27, 0.29), 0.06, 0.15),
}
for name, (color, metal, rough) in SPEC.items():
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    p = mat.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = (*color, 1)
    p.inputs["Metallic"].default_value = metal
    p.inputs["Roughness"].default_value = rough
    if name.startswith("Lamp"):
        p.inputs["Emission Color"].default_value = (*color, 1)
        p.inputs["Emission Strength"].default_value = 3.0
    elif name in ("HullPaint", "DarkSteel", "RedPaint", "Brass", "Canvas"):
        nodes = mat.node_tree.nodes
        links = mat.node_tree.links
        tex = nodes.new("ShaderNodeTexNoise")
        tex.inputs["Scale"].default_value = 32 if name != "Canvas" else 130
        tex.inputs["Detail"].default_value = 3.5
        ramp = nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.elements[0].position = 0.30
        ramp.color_ramp.elements[0].color = (0.028, 0.020, 0.012, 1)
        ramp.color_ramp.elements[1].position = 0.54
        ramp.color_ramp.elements[1].color = (*color, 1)
        links.new(tex.outputs["Fac"], ramp.inputs["Fac"])
        links.new(ramp.outputs["Color"], p.inputs["Base Color"])
        bump = nodes.new("ShaderNodeBump")
        bump.inputs["Strength"].default_value = 0.24
        bump.inputs["Distance"].default_value = 0.0015
        links.new(tex.outputs["Fac"], bump.inputs["Height"])
        links.new(bump.outputs["Normal"], p.inputs["Normal"])
    MATS[name] = mat

COLL = {}
for wall in ["A", "B", "C", "D", "Structure"]:
    c = bpy.data.collections.new("EDITABLE_" + wall)
    bpy.context.scene.collection.children.link(c)
    COLL[wall] = c

WALL = "A"
DYNAMIC = None
OBJECTS = []
INTERACTIONS = []
ANGLES = {"A": 0, "B": math.pi / 2, "C": math.pi, "D": -math.pi / 2}


def wp(u, h, d=0, wall=None):
    a = ANGLES.get(wall or WALL, 0)
    x, y = u, -1.6 + d
    return Vector((x * math.cos(a) - y * math.sin(a),
                   x * math.sin(a) + y * math.cos(a), h))


def world_to_unity(p):
    # Verified in the Unity editor: FBX handedness conversion reflects X.
    return [round(-p[0], 5), round(p[2], 5), round(-p[1], 5)]


def register(obj, material, bevel=0, smooth=False):
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    COLL[WALL].objects.link(obj)
    if material:
        obj.data.materials.append(MATS[material])
    obj["astra_wall"] = WALL
    obj["astra_material"] = material or ""
    obj["astra_dynamic"] = DYNAMIC.name if DYNAMIC else ""
    if DYNAMIC:
        obj.parent = DYNAMIC
        obj.matrix_parent_inverse = DYNAMIC.matrix_world.inverted()
    if bevel:
        mod = obj.modifiers.new("Manufactured edge bevel", "BEVEL")
        mod.width = bevel
        mod.segments = 2
        mod.affect = "EDGES"
    if smooth and obj.type == "MESH":
        for p in obj.data.polygons:
            p.use_smooth = True
    OBJECTS.append(obj)
    return obj


def mesh(name, vertices, faces, material, bevel=0, smooth=False):
    m = bpy.data.meshes.new(name + "_mesh")
    m.from_pydata(vertices, [], faces)
    m.update()
    ob = bpy.data.objects.new(name, m)
    COLL[WALL].objects.link(ob)
    return register(ob, material, bevel, smooth)


def box(name, u, h, d, w, height, depth, material="HullPaint", bevel=.01):
    coords = [wp(u + sx*w/2, h + sz*height/2, d + sy*depth/2)
              for sx, sy, sz in [(-1,-1,-1), (1,-1,-1), (1,1,-1), (-1,1,-1),
                                 (-1,-1,1), (1,-1,1), (1,1,1), (-1,1,1)]]
    return mesh(name, coords,
                [(0,3,2,1), (4,5,6,7), (0,1,5,4), (1,2,6,5), (2,3,7,6), (3,0,4,7)],
                material, min(bevel, w/5, height/5, depth/5))


def cylinder_world(name, start, end, radius, material="DarkSteel", sides=16, bevel=0):
    start, end = Vector(start), Vector(end)
    axis = (end - start).normalized()
    t = Vector((0,0,1)) if abs(axis.z) < .9 else Vector((1,0,0))
    x = axis.cross(t).normalized()
    y = axis.cross(x).normalized()
    verts = []
    for p in (start, end):
        for i in range(sides):
            a = i * math.tau / sides
            verts.append(p + radius * (math.cos(a)*x + math.sin(a)*y))
    faces = [tuple(reversed(range(sides))), tuple(range(sides, sides*2))]
    for i in range(sides):
        j = (i+1) % sides
        faces.append((i,j,j+sides,i+sides))
    ob = mesh(name, verts, faces, material, bevel)
    for p in ob.data.polygons[2:]:
        p.use_smooth = True
    return ob


def rod(name, p1, p2, radius=.025, material="DarkSteel", sides=12):
    return cylinder_world(name, wp(*p1), wp(*p2), radius, material, sides)


def disc(name, u, h, d, radius, depth=.02, material="Brass", sides=24):
    return rod(name, (u,h,d-depth/2), (u,h,d+depth/2), radius, material, sides)


def ring(name, u, h, d, radius, tube=.015, material="Brass", axis="face", sides=32, tubes=8):
    center = wp(u,h,d)
    normal = (wp(u,h,d+1)-center).normalized() if axis == "face" else Vector((0,0,1))
    tx = (wp(u+1,h,d)-center).normalized()
    ty = normal.cross(tx).normalized()
    verts=[]
    for i in range(sides):
        a=math.tau*i/sides
        v=math.cos(a)*tx+math.sin(a)*ty
        for j in range(tubes):
            b=math.tau*j/tubes
            verts.append(center + (radius+tube*math.cos(b))*v + tube*math.sin(b)*normal)
    faces=[]
    for i in range(sides):
        for j in range(tubes):
            faces.append((i*tubes+j, ((i+1)%sides)*tubes+j,
                          ((i+1)%sides)*tubes+(j+1)%tubes, i*tubes+(j+1)%tubes))
    return mesh(name, verts, faces, material, smooth=True)


def pipe(name, points, radius=.018, material="DarkSteel"):
    """Smooth continuous tubing, editable as a curve in the source blend."""
    c=bpy.data.curves.new(name, "CURVE")
    c.dimensions="3D"
    c.resolution_u=5
    c.bevel_depth=radius
    c.bevel_resolution=2
    c.resolution_u=8
    spline=c.splines.new("BEZIER")
    spline.bezier_points.add(len(points)-1)
    for bp, p in zip(spline.bezier_points, points):
        bp.co=wp(*p)
        bp.handle_left_type="AUTO"
        bp.handle_right_type="AUTO"
    ob=bpy.data.objects.new(name,c)
    COLL[WALL].objects.link(ob)
    return register(ob, material)


def text(name, words, u, h, d, size=.045, material="Paper", align="CENTER"):
    data=bpy.data.curves.new(name,"FONT")
    data.body=words
    data.align_x=align
    data.align_y="CENTER"
    data.size=size
    data.extrude=.0002
    data.resolution_u=3
    ob=bpy.data.objects.new(name,data)
    COLL[WALL].objects.link(ob)
    ob.location=wp(u,h,d)
    a=ANGLES.get(WALL,0)
    # Blender's right-handed view into the room sees -u on screen right.
    # Face the text toward the cabin, never relying on back-face rendering.
    ob.rotation_euler=(math.pi/2,0,a+math.pi)
    return register(ob,material)


def screw(u,h,d,size=.012):
    disc("Hex fastener",u,h,d,size,.012,"Brass",6)
    box("Bolt slot",u,h,d+.007,size*.95,.003,.002,"DarkSteel",0)


def perimeter(u,h,d,w,hh,spacing=.19):
    nx=max(2,int(w/spacing)+1)
    ny=max(2,int(hh/spacing)+1)
    for i in range(nx):
        x=u-w/2+i*w/(nx-1)
        screw(x,h-hh/2,d)
        screw(x,h+hh/2,d)
    for i in range(1,ny-1):
        y=h-hh/2+i*hh/(ny-1)
        screw(u-w/2,y,d)
        screw(u+w/2,y,d)


def panel(name,u,h,d,w,hh,depth=.08,bolts=True,material="HullPaint"):
    ob=box(name,u,h,d,w,hh,depth,material,.028)
    if bolts:
        perimeter(u,h,d+depth/2+.007,w-.06,hh-.06)
    # Very fine irregular edge chips stay geometry, avoiding a painted-in scene.
    for i in range(max(4,int(w*hh*12))):
        edge=random.choice([0,1,2,3])
        cu=u+random.uniform(-w*.45,w*.45)
        ch=h+random.uniform(-hh*.43,hh*.43)
        if edge<2: ch=h+(-1 if edge==0 else 1)*hh*.46
        else: cu=u+(-1 if edge==2 else 1)*w*.46
        box("Chipped paint",cu,ch,d+depth/2+.0015,random.uniform(.006,.035),
            random.uniform(.004,.017),.001,"DarkSteel",0)
    return ob


def frame(name,u,h,d,w,hh,thick=.065,depth=.09,material="HullPaint"):
    box(name+" top",u,h+hh/2-thick/2,d,w,thick,depth,material,.018)
    box(name+" bottom",u,h-hh/2+thick/2,d,w,thick,depth,material,.018)
    box(name+" left",u-w/2+thick/2,h,d,thick,hh,depth,material,.018)
    box(name+" right",u+w/2-thick/2,h,d,thick,hh,depth,material,.018)


def rounded_frame(name,u,h,d,w,hh,thick=.065,depth=.09,material="HullPaint",radius=.14):
    """Cast pressure frame with a real hole and radiused corners."""
    def contour(ww,hh0,rr):
        result=[]
        for cx,cy,angle in [(ww/2-rr,hh0/2-rr,0),(-ww/2+rr,hh0/2-rr,90),
                            (-ww/2+rr,-hh0/2+rr,180),(ww/2-rr,-hh0/2+rr,270)]:
            for i in range(7):
                a=math.radians(angle+i*90/6)
                result.append((cx+rr*math.cos(a),cy+rr*math.sin(a)))
        return result
    outside=contour(w,hh,radius)
    inside=contour(w-thick*2,hh-thick*2,max(.012,radius-thick))
    verts=[]
    for dd,points in [(d-depth/2,outside),(d-depth/2,inside),
                      (d+depth/2,outside),(d+depth/2,inside)]:
        verts.extend(wp(u+x,h+y,dd) for x,y in points)
    n=len(outside)
    faces=[]
    for i in range(n):
        j=(i+1)%n
        faces.extend([(i,j,j+n,i+n),(i+2*n,i+3*n,j+3*n,j+2*n),
                      (i,i+2*n,j+2*n,j),(i+n,j+n,j+3*n,i+3*n)])
    return mesh(name,verts,faces,material,.005)


def gauge(u,h,d=.24,r=.11,label="BAR"):
    disc("Gauge body",u,h,d,r,.065,"DarkSteel")
    ring("Brass instrument bezel",u,h,d+.036,r*.89,.013,"Brass")
    disc("Ivory gauge dial",u,h,d+.039,r*.78,.008,"Paper")
    for i in range(13):
        a=math.radians(-40+i*22)
        r1,r2=r*.59,r*.70
        rod("Gauge tick",(u+math.cos(a)*r1,h+math.sin(a)*r1,d+.047),
            (u+math.cos(a)*r2,h+math.sin(a)*r2,d+.047),.0018,"DarkSteel",6)
    a=1.05
    rod("Gauge needle",(u,h,d+.052),(u+r*.6*math.cos(a),h+r*.6*math.sin(a),d+.052),.003,"RedPaint",6)
    disc("Gauge needle axle",u,h,d+.054,.009,.005,"Brass",12)
    text("Dial legend",label,u,h-r*.36,d+.05,r*.17,"DarkSteel")


def indicator(u,h,d=.25,color="LampGreen",r=.023):
    disc("Indicator bezel",u,h,d,r*1.35,.025,"Brass",16)
    disc("Indicator glass",u,h,d+.017,r,.015,color,16)


def vent(u,h,d,w=.3,hh=.25):
    panel("Vent casing",u,h,d,w,hh,.055)
    box("Vent black recess",u,h,d+.03,w*.77,hh*.77,.005,"Rubber",0)
    for i in range(6):
        box("Vent grille",u,h-hh*.29+i*hh*.115,d+.042,w*.77,.011,.026,"Brass",.003)


def activate(name,u,h,d,axis,action):
    global DYNAMIC
    ob=bpy.data.objects.new(name,None)
    COLL[WALL].objects.link(ob)
    ob.location=wp(u,h,d)
    ob.empty_display_size=.075
    ob["astra_wall"]=WALL
    ob["astra_interaction"]=action
    bpy.context.view_layer.update()
    DYNAMIC=ob
    INTERACTIONS.append({"name":name,"wall":WALL,"blender_position":list(ob.location),
                         "unity_expected_position":world_to_unity(ob.location),
                         "unity_world_axis":axis,"action":action})
    return ob


def deactivate():
    global DYNAMIC
    DYNAMIC=None


def lamp(wall):
    u,h,d=0,2.28,.27
    box("Lamp mounting bracket",u,2.41,.12,.22,.08,.18,"DarkSteel",.01)
    rod("Lamp suspension",(u,2.42,d),(u,2.36,d),.027,"Brass")
    ob=rod("LAMP_"+wall,(u,h-.055,d),(u,h+.055,d),.048,"LampWarm",20)
    ob["astra_keep_name"]=True
    for hh in [h-.10,h-.04,h+.045,h+.10]:
        ring("Lamp cage ring",u,hh,d,.079,.009,"Brass",axis="up",sides=20,tubes=6)
    for i in range(8):
        a=math.tau*i/8
        x=u+.079*math.cos(a)
        dd=d+.079*math.sin(a)
        rod("Lamp cage bar",(x,h-.10,dd),(x,h+.10,dd),.007,"DarkSteel",8)
    rod("Lamp cap",(u,h+.10,d),(u,h+.12,d),.085,"DarkSteel",20)
    rod("Lamp cap",(u,h-.10,d),(u,h-.12,d),.075,"DarkSteel",20)


def common_wall(wall):
    global WALL
    WALL=wall
    if wall != "D":
        for ix in range(4):
            for iy in range(3):
                u=-1.2+ix*.8
                h=.425+iy*.82
                panel("Pressure hull plate",u,h,-.06,.79,.81,.09,False)
    else:
        # These are four actual pieces surrounding an aperture. NO backing plane.
        panel("Window wall lower hull",0,.49,-.06,3.2,.98,.09,False)
        panel("Window wall upper hull",0,2.325,-.06,3.2,.35,.09,False)
        panel("Window wall left hull",-1.30,1.57,-.06,.60,1.17,.09,False)
        panel("Window wall right hull",1.30,1.57,-.06,.60,1.17,.09,False)
    for u in [-1.50,1.50]:
        box("Structural vertical rib",u,1.25,.015,.095,2.5,.20,"HullPaint",.02)
        for hh in [.18,.52,.86,1.20,1.54,1.88,2.22,2.43]: screw(u,hh,.122,.016)
    for h in [.11,2.46]:
        box("Structural horizontal rib",0,h,.05,3.1,.1,.18,"HullPaint",.015)
        for i in range(15): screw(-1.4+i*.2,h,.15,.014)
    # Horizontal utility main and clamps.
    rod("Ceiling pressure pipe",(-1.48,2.35,.12),(1.48,2.35,.12),.071,"DarkSteel",20)
    for u in [-1.18,-.68,.69,1.2]:
        box("Pipe saddle",u,2.35,.17,.042,.20,.13,"Brass",.007)
        screw(u,2.44,.245)
    # A pair of continuous insulated wire bundles on both sides.
    for sign in [-1,1]:
        for i in range(3):
            u=sign*(1.32-i*.042)
            pipe("Corner cable conduit",[(u,2.25,.14),(u+sign*.04,2.12,.15),
                (u+sign*.04,.36,.15),(u,.24,.17),(u-sign*.22,.20,.18)],.014,"Rubber")
        for hh in [.42,1.05,1.78,2.05]:
            box("Cable clamp",sign*1.29,hh,.18,.18,.04,.07,"Brass",.005)
            screw(sign*1.34,hh,.222,.01)
    lamp(wall)
    text("Wall station stencil",{"A":"01 / NAVIGATION","B":"02 / FABRICATION", "C":"03 / HABITAT", "D":"04 / OBSERVATION"}[wall],0,2.11,.071,.041,"Paper")
    # Welded service pipe close to deck.
    rod("Lower service main",(-1.45,.15,.19),(1.45,.15,.19),.042,"DarkSteel",16)
    for u in [-1.1,-.3,.55,1.12]: disc("Pipe flange",u,.15,.20,.07,.065,"Brass",16)


def wall_a():
    common_wall("A")
    # Layered CRT assembly with a substantial deep pressure-resistant housing.
    panel("CRT rear casting",0,1.51,.16,2.02,1.20,.24)
    rounded_frame("CRT rubber gasket",0,1.53,.304,1.83,1.035,.075,.055,"Rubber",.14)
    rounded_frame("CRT bronze rim",0,1.53,.346,1.78,.988,.036,.06,"Brass",.12)
    screen=box("SCREEN_Main",0,1.53,.357,1.65,.876,.012,"Screen",.018)
    screen["astra_keep_name"]=True
    # Back-lit lines built into original Blender reference; Unity can replace screen material.
    text("CRT top ID","PIGEON / DEEP SPACE TELEMETRY",0,1.86,.370,.030,"LampGreen")["astra_preview_only"]=True
    text("CRT status","ASTRA - 07",0,1.68,.370,.085,"LampGreen")["astra_preview_only"]=True
    text("CRT status line","AUTONOMOUS TRANSIT    /    SYSTEM ONLINE",0,1.26,.370,.027,"LampGreen")["astra_preview_only"]=True
    for u in [-1.08,1.08]:
        rod("CRT grab handle",(u,1.14,.31),(u,1.83,.31),.022,"Brass",12)
        for hh in [1.14,1.83]: rod("CRT handle foot",(u,hh,.17),(u,hh,.31),.025,"Brass",12)
    # Console has a real sloped deck plus raised keys.
    panel("Console body",0,.76,.34,1.98,.28,.49)
    for i in range(4):
        for j in range(13):
            u=-.77+j*.09
            h=.924
            d=.21+i*.07
            box("Keyboard key",u,h,d,.07,.024,.05,"Paper" if (i+j)%9 else "RedPaint",.005)
    box("Keyboard space bar",-.28,.924,.51,.44,.025,.044,"DarkSteel",.004)
    for i in range(3):
        indicator(.56+i*.125,.85,.596,"LampGreen" if i<2 else "LampRed",.027)
    text("Console serial","CONTROL TERMINAL / 1943-A",0,.709,.601,.031)
    # Auxiliary gauges and toggle switches flank the CRT.
    gauge(-1.18,1.88,.26,.106,"O2")
    gauge(1.18,1.89,.26,.106,"PWR")
    panel("Left switch station",-1.18,1.43,.18,.35,.54,.13)
    indicator(-1.24,1.59,.265,"LampGreen")
    indicator(-1.10,1.59,.265,"LampWarm")
    text("Lights legend","LIGHT",-1.18,1.46,.262,.034)
    disc("Light toggle ring",-1.18,1.31,.265,.04,.025,"Brass",20)
    activate("ACT_LightSwitch",-1.18,1.31,.291,[1,0,0],"Rotate 26 degrees around world X; cabin lights")
    rod("Light toggle stem",(-1.18,1.31,.292),(-1.18,1.36,.365),.016,"Brass",12)
    box("Light toggle grip",-1.18,1.37,.37,.055,.055,.035,"RedPaint",.008)
    deactivate()
    panel("Power lever mounting plate",1.19,1.32,.16,.32,.69,.11)
    box("Power lever slot",1.19,1.33,.224,.09,.40,.01,"Rubber",0)
    text("Power legend","REACTOR",1.19,1.59,.228,.030)
    for j in range(5):
        box("Hazard yellow marker",1.07,1.15+j*.067,.22,.055,.026,.013,"Brass",0)
    activate("ACT_PowerLever",1.19,1.11,.28,[1,0,0],"Rotate -48 degrees around world X; reactor mode")
    rod("Reactor lever shaft",(1.19,1.11,.28),(1.19,1.47,.34),.023,"Brass",14)
    rod("Reactor red cross handle",(1.09,1.47,.34),(1.29,1.47,.34),.043,"RedPaint",20)
    disc("Lever pivot cap",1.19,1.11,.302,.052,.033,"DarkSteel",20)
    deactivate()
    vent(-1.12,.79,.20,.36,.30)
    vent(1.14,.56,.21,.37,.40)
    panel("Under-console electronics",.31,.39,.25,1.18,.42,.25)
    vent(.53,.38,.39,.40,.24)
    text("Machine placard","AUTHORIZED PERSONNEL ONLY",-.16,.45,.386,.025)
    for i in range(5):
        pipe("Console umbilical",[(-.68+i*.09,.68,.27),(-.73+i*.09,.53,.18),
             (-.75+i*.09,.26,.20),(-.28+i*.10,.23,.21)],.016,"Rubber")
    panel("Small junction box",-1.08,.35,.24,.39,.36,.21)


def wrench(u,h,size=1):
    rod("Wrench shaft",(u,h-.17*size,.255),(u,h+.12*size,.255),.016*size,"Brass",8)
    ring("Wrench closed end",u,h-.19*size,.258,.035*size,.013*size,"Brass",sides=16,tubes=6)
    box("Wrench open jaw base",u,h+.13*size,.255,.095*size,.048*size,.032,"Brass",.005)
    for s in [-1,1]:
        box("Wrench jaw",u+s*.04*size,h+.18*size,.255,.023*size,.08*size,.032,"Brass",.004)
    box("Tool hanger",u,h+.09*size,.23,.047,.029,.065,"DarkSteel",.002)


def wall_b():
    common_wall("B")
    panel("Perforated toolboard backing",0,1.70,.11,2.36,.72,.12)
    # Black small studs/recesses, batched on FBX export.
    for ix in range(39):
        for iy in range(11):
            disc("Pegboard perforation",-1.12+ix*.059,1.40+iy*.059,.174,.006,.002,"Rubber",8)
    for i in range(4): wrench(-.99+i*.19,1.72,1.12-i*.12)
    for i in range(3):
        u=-.17+i*.10
        rod("Screwdriver shaft",(u,1.44,.25),(u,1.85,.25),.011,"Brass",8)
        box("Screwdriver handle",u,1.82,.25,.047,.16,.06,"RedPaint",.012)
        box("Screwdriver hanger",u,1.72,.22,.071,.029,.065,"DarkSteel",.002)
    # Drill silhouette: front shaft and pistol grip.
    rod("Drill motor",(.31,1.87,.25),(.66,1.87,.25),.063,"HullPaint",20)
    rod("Drill chuck",(.22,1.87,.25),(.33,1.87,.25),.035,"DarkSteel",16)
    rod("Drill bit",(.11,1.87,.25),(.24,1.87,.25),.012,"Brass",10)
    box("Drill grip",.55,1.72,.25,.067,.23,.095,"RedPaint",.014)
    for r in [.11,.15,.19]: ring("Coiled service cable",.91,1.74,.245,r,.013,"Rubber",sides=28,tubes=6)
    for u in [.75,1.08]: box("Coil retaining band",u,1.75,.267,.055,.07,.06,"Brass",.004)
    # Printer is deliberately open: no flat image, front glazing, or solid front box.
    box("Printer rear dark cavity",0,.79,.025,1.77,.98,.08,"DarkSteel",.025)
    rounded_frame("Printer heavy chassis",0,.77,.22,1.83,1.06,.12,.34,"HullPaint",.14)
    rounded_frame("Printer bronze gasket",0,.77,.406,1.59,.82,.028,.034,"Brass",.10)
    perimeter(0,.77,.403,1.74,.96,.20)
    box("Printer build platform",0,.405,.37,1.41,.055,.40,"DarkSteel",.013)
    for u in [-.64,.64]:
        rod("Printer vertical Z rail",(u,.43,.18),(u,1.12,.18),.018,"Brass",16)
        for hh in [.44,1.12]: box("Printer rail bearing",u,hh,.18,.073,.062,.081,"DarkSteel",.007)
    for hh in [.88,.93]: rod("Printer X carriage rail",(-.67,hh,.27),(.67,hh,.27),.014,"Brass",16)
    box("Printer toothed belt",0,.973,.28,1.33,.016,.02,"Rubber",0)
    head=box("PRINT_Head",.08,.855,.30,.20,.22,.18,"HullPaint",.018)
    head["astra_keep_name"]=True
    # Follow the real moving head by parenting the tip and status lamp.
    global DYNAMIC
    bpy.context.view_layer.update()
    DYNAMIC=head
    rod("Printer nozzle",(.08,.744,.33),(.08,.68,.33),.017,"Brass",12)
    indicator(.08,.862,.401,"LampWarm",.021)
    deactivate()
    for i in range(9):
        box("Printed test object layer",-.2,.443+i*.009,.36,.28-i*.011,.007,.17-i*.004,"Brass",.003)
    text("Printer station legend","FABRICATOR / MK.04",0,1.251,.403,.04)
    panel("Printer left control",-1.065,.79,.22,.24,.91,.22)
    for h in [1.04,.95,.86]: indicator(-1.065,h,.351,"LampGreen")
    text("Printer start label","PRINT",-1.065,.72,.342,.026)
    disc("Print button bezel",-1.065,.62,.356,.055,.033,"Brass",20)
    activate("ACT_PrintButton",-1.065,.62,.383,[-1,0,0],"Translate 0.025 m toward B wall; printer action")
    disc("Print button",-1.065,.62,.384,.043,.03,"RedPaint",24)
    deactivate()
    panel("Printer right services",1.065,.79,.20,.26,.91,.18)
    gauge(1.065,1.04,.318,.089,"TEMP")
    vent(1.065,.54,.31,.23,.26)
    for h in [.66,.82]: indicator(1.065,h,.313,"LampWarm",.018)
    # Filament spools stand to the side, making the tool wall read as a workshop.
    for h in [.42,.62]:
        disc("Filament spool",-1.30,h,.25,.087,.10,"DarkSteel",24)
        ring("Spool flange",-1.30,h,.31,.083,.01,"Brass",sides=24,tubes=6)
        disc("Filament spool axle",-1.30,h,.315,.02,.02,"Brass",12)
    pipe("Printer feed tube",[(-1.29,.64,.26),(-1.25,1.10,.24),(-.65,1.16,.22),(.08,1.06,.27),(.08,.93,.30)],.012,"Rubber")


def can(u,h,d=.29,r=.065,height=.15,material="Brass"):
    rod("Food tin body",(u,h-height/2,d),(u,h+height/2,d),r,material,20)
    for hh in [h-height/2+.008,h+height/2-.008]:
        ring("Tin rolled seam",u,hh,d,r,.007,"DarkSteel",axis="up",sides=20,tubes=6)
    box("Tin paper label",u,h,d+r+.002,r*1.55,height*.48,.008,"Paper",.002)


def wall_c():
    common_wall("C")
    # Recessed storage backing and very substantial shelf boards.
    panel("Habitat lockers backing",0,1.31,.055,2.42,1.64,.13)
    for u in [-.83,.81]:
        box("Side shelf upright",u,1.27,.27,.05,1.55,.40,"HullPaint",.008)
    for h in [.50,.91,1.36,1.91]:
        for u,w in [(-1.04,.40),(1.04,.40)]:
            box("Supplies shelf",u,h,.28,w,.038,.44,"DarkSteel",.005)
    for h in [.57,1.555,1.91]:
        box("Central locker shelf",0,h,.28,1.55,.038,.44,"DarkSteel",.005)
    # Two central locker doors; left one opens around its left upright.
    box("Locker interior shadow",0,1.15,.135,1.49,.83,.11,"Rubber",.006)
    activate("ACT_Cabinet",-.75,.95,.35,[0,1,0],"Rotate -72 degrees about world Y; locker left hinge")
    panel("Hinged central locker door",-.385,1.11,.35,.715,.89,.055)
    for h in [.85,1.35]:
        for j in range(4): box("Locker door vent",-.385,h+j*.028,.38,.24,.01,.007,"DarkSteel",.002)
    rod("Locker door pull",(-.135,1.03,.41),(-.135,1.19,.41),.014,"Brass",12)
    for h in [1.03,1.19]: rod("Locker pull standoff",(-.135,h,.38),(-.135,h,.41),.016,"Brass",12)
    deactivate()
    panel("Fixed right locker door",.385,1.11,.35,.715,.89,.055)
    for h in [.85,1.35]:
        for j in range(4): box("Locker door vent",.385,h+j*.028,.382,.24,.01,.007,"DarkSteel",.002)
    rod("Fixed locker handle",(.60,1.02,.42),(.60,1.18,.42),.014,"Brass",12)
    # Top personal objects: books, picture frame, radio.
    for i in range(5):
        u=-.62+i*.071
        hh=.23+random.random()*.075
        box("Personal book",u,1.56+hh/2,.27,.055,hh,.17,"RedPaint" if i%2 else "Canvas",.004)
        for dh in [-hh*.32,hh*.32]: box("Book spine band",u,1.56+hh/2+dh,.361,.057,.009,.006,"Brass",0)
    panel("Photograph frame",-.14,1.715,.295,.24,.27,.045,False,"Brass")
    box("Faded photograph",-.14,1.715,.322,.20,.23,.006,"Paper",0)
    # Simple dimensional commemorative silhouette, not an image pasted over a room.
    disc("Photo silhouette head",-.14,1.755,.327,.026,.003,"DarkSteel",16)
    box("Photo silhouette torso",-.14,1.677,.327,.078,.09,.003,"DarkSteel",.001)
    box("Medal ribbon",.05,1.75,.31,.033,.14,.012,"RedPaint",.002)
    disc("Commemorative medal",.05,1.64,.322,.04,.013,"Brass",20)
    panel("Radio chassis",.43,1.714,.29,.55,.30,.24,False,"DarkSteel")
    box("Radio speaker recess",.555,1.724,.414,.19,.21,.012,"Rubber",.008)
    for i in range(8): box("Radio grille",.479+i*.022,1.724,.425,.005,.20,.005,"Brass",0)
    box("Radio tuning dial",.294,1.775,.418,.18,.057,.012,"Paper",.002)
    for i in range(9): box("Radio tuning mark",.22+i*.018,1.778,.426,.002,.026,.004,"DarkSteel",0)
    text("Radio badge","VOLNA",.295,1.66,.420,.025,"Paper")
    disc("Radio knob bezel",.28,1.710,.429,.038,.024,"Brass",20)
    activate("ACT_RadioKnob",.28,1.710,.45,[0,0,1],"Rotate 85 degrees around world Z; radio tuning")
    disc("Radio tuning knob",.28,1.710,.45,.031,.035,"DarkSteel",20)
    box("Radio knob pointer",.28,1.729,.471,.006,.018,.003,"Paper",0)
    deactivate()
    # Side shelves full of cans, bottles and strapped cargo.
    for h in [.62,1.02,1.51]:
        for u in [-1.15,-.985]: can(u,h,.30,.061,.16)
    for u in [.96,1.13]:
        can(u,1.57,.28,.052,.22,"DarkSteel")
        rod("Bottle neck",(u,1.68,.28),(u,1.73,.28),.031,"Brass",16)
        rod("Bottle cap",(u,1.72,.28),(u,1.75,.28),.036,"RedPaint",16)
    for h in [.67,1.11]:
        box("Folded blanket",1.04,h,.28,.35,.21,.34,"Canvas",.034)
        for u in [.96,1.11]: box("Cargo webbing",u,h,.461,.038,.21,.016,"Rubber",.003)
    for u in [-1.04,1.04]:
        for h in [.73,1.17,1.76]:
            rod("Shelf safety rail",(u-.20,h,.515),(u+.20,h,.515),.009,"Brass",12)
            for uu in [u-.20,u+.20]: rod("Shelf rail foot",(uu,h-.13,.43),(uu,h,.515),.01,"Brass",10)
    # Bench / bed: padded segmented canvas, contrasting structural chest.
    panel("Bench storage chest",0,.28,.42,1.67,.40,.69)
    box("Bench cushion",0,.524,.43,1.67,.11,.71,"Canvas",.049)
    for u in [-.59,.59]:
        box("Bench retaining strap",u,.585,.43,.049,.007,.71,"Rubber",.003)
        box("Bench retaining strap front",u,.43,.79,.049,.28,.012,"Rubber",.003)
        frame("Bench buckle",u,.49,.804,.072,.076,.012,.012,"Brass")
    rod("Bench chest handle",(-.18,.29,.795),(.18,.29,.795),.016,"Brass",12)
    for u in [-.18,.18]: rod("Chest handle foot",(u,.29,.76),(u,.29,.80),.018,"Brass",12)
    panel("Medical kit",-1.03,.31,.30,.39,.35,.32,False,"RedPaint")
    box("Medical cross horizontal",-1.03,.31,.467,.18,.057,.008,"Paper",.003)
    box("Medical cross vertical",-1.03,.31,.468,.057,.18,.008,"Paper",.003)
    panel("Personal cargo case",1.03,.31,.31,.39,.35,.35)
    text("Habitat serial","ONE OCCUPANT / LONG DURATION TRANSIT",0,2.005,.135,.031)


def wall_d():
    common_wall("D")
    # Welded solid sill closes the unintended 95 mm hull gap between the
    # original lower pressure plate and the observation-window casting.
    # Its top stays below the designed aperture; transfer cavity is untouched.
    box("Observation sill pressure beam",0,1.08,.015,2.22,.26,.18,"HullPaint",.01)
    # Large real opening: 1.8 x 0.9 clear aperture, deep reveal and layered seals.
    rounded_frame("Observation pressure casting",0,1.65,.08,2.17,1.15,.17,.28,"HullPaint",.23)
    rounded_frame("Window inner dark seal",0,1.65,.245,1.90,.95,.046,.055,"Rubber",.13)
    rounded_frame("Window inner bronze lip",0,1.65,.278,1.85,.90,.025,.04,"Brass",.11)
    perimeter(0,1.65,.229,2.075,1.055,.19)
    # Deep black jambs terminate outside the hull without closing the aperture.
    rounded_frame("Window outside tunnel",0,1.65,-.23,1.89,.94,.04,.50,"DarkSteel",.10)
    text("Window warning","PRESSURE GLASS / DO NOT STRIKE",0,2.245,.235,.028)
    for u in [-1.14,1.14]:
        rod("Window grab rail",(u,1.30,.31),(u,1.98,.31),.02,"Brass",12)
        for hh in [1.30,1.98]: rod("Grab rail standoff",(u,hh,.18),(u,hh,.31),.022,"Brass",12)
    # Dark receiving cavity and lower hinged transfer hatch.
    box("Transfer cavity back",0,.65,.015,1.26,.64,.06,"DarkSteel",.015)
    frame("Transfer cavity reveal",0,.65,.17,1.29,.68,.075,.35,"DarkSteel")
    frame("Transfer hatch frame",0,.65,.34,1.43,.81,.093,.10,"HullPaint")
    perimeter(0,.65,.40,1.35,.73,.19)
    activate("ACT_Hatch",0,.32,.40,[0,0,1],"Rotate +78 degrees around world Z; bottom hinged transfer hatch")
    panel("Transfer hatch door",0,.65,.40,1.22,.62,.095)
    frame("Hatch reinforcement",0,.65,.451,1.08,.48,.034,.03,"DarkSteel")
    rod("Transfer hatch handle",(-.20,.69,.54),(.20,.69,.54),.021,"Brass",16)
    for u in [-.20,.20]: rod("Transfer handle foot",(u,.69,.455),(u,.69,.54),.024,"Brass",12)
    text("Transfer hatch label","EXCHANGE / 07",0,.51,.461,.038)
    deactivate()
    for u in [-.48,.48]:
        rod("Transfer hatch hinge",(u-.08,.32,.41),(u+.08,.32,.41),.039,"Brass",16)
    panel("Transfer signals panel",-.94,.68,.22,.30,.74,.19)
    for h,c in [(.87,"LampGreen"),(.67,"LampRed")]:
        indicator(-.94,h,.33,c,.043)
        ring("Signal cage ring",-.94,h,.36,.062,.006,"Brass",sides=20,tubes=6)
        for dx in [-.028,.028]: rod("Signal cage bar",(-.94+dx,h-.058,.369),(-.94+dx,h+.058,.369),.005,"Brass",8)
    text("Transfer station label","LOCK",-.94,.45,.324,.031)
    panel("Pneumatic services",.99,.65,.17,.40,.77,.19)
    gauge(.99,.89,.295,.117,"PSI")
    vent(.99,.51,.29,.34,.27)
    # Wheel with individual spokes, hub, and free rotation pivot.
    disc("Valve mounting flange",1.22,1.42,.08,.15,.045,"DarkSteel",20)
    rod("Valve spindle",(1.22,1.42,.08),(1.22,1.42,.16),.033,"Brass",16)
    activate("ACT_Valve",1.22,1.42,.16,[1,0,0],"Rotate 140 degrees around world X; pressure valve")
    ring("Red valve handwheel",1.22,1.42,.16,.143,.020,"RedPaint",sides=32,tubes=8)
    disc("Valve handwheel hub",1.22,1.42,.16,.039,.045,"Brass",16)
    for i in range(5):
        a=math.tau*i/5
        rod("Valve spoke",(1.22,1.42,.16),(1.22+.128*math.cos(a),1.42+.128*math.sin(a),.16),.012,"RedPaint",10)
    deactivate()
    vent(0,.17,.29,.73,.22)


def structure():
    global WALL
    WALL="Structure"
    # Floor contains real negative space between steel grating bars.
    box("Deck undertray",0,-.06,1.6,3.20,.10,3.20,"DarkSteel",.012)
    for i in range(41):
        x=-1.55+i*.0775
        box("Deck grating bar",x,.035,1.6,.019,.065,3.12,"DarkSteel",.003)
    for i in range(17):
        d=.05+i*.19375
        box("Deck grating crossbar",0,.035,d,3.12,.065,.018,"Brass",.003)
    # Floor access frame around a central inspection panel.
    for d in [.90,2.30]: box("Deck access crossbeam",0,.076,d,1.42,.025,.045,"HullPaint",.004)
    for u in [-.71,.71]: box("Deck access sidebeam",u,.076,1.6,.045,.025,1.44,"HullPaint",.004)
    # Actual red low-level emergency strips sit beneath grating near the walls.
    for u in [-1.43,1.43]: box("Deck red strip",u,-.004,1.6,.028,.015,2.84,"LampRed",.002)
    for d in [.17,3.03]: box("Deck red strip",0,-.004,d,2.84,.015,.028,"LampRed",.002)
    box("Ceiling hull",0,2.57,1.6,3.30,.14,3.30,"HullPaint",.03)
    for u in [-1.1,-.38,.38,1.1]: box("Ceiling reinforcement",u,2.45,1.6,.09,.18,3.10,"DarkSteel",.016)
    # Crosswise thick main, with a small secondary cable tray.
    cylinder_world("Overhead pressure trunk",(-1.4,-.65,2.37),(1.4,-.65,2.37),.074,"DarkSteel",24)
    cylinder_world("Overhead cable conduit",(-1.4,.74,2.40),(1.4,.74,2.40),.038,"Brass",16)
    for x in [-.90,0,.90]:
        cylinder_world("Trunk flange",(x-.018,-.65,2.37),(x+.018,-.65,2.37),.10,"Brass",20)


print("Building editable cabin geometry...", flush=True)
wall_a()
print("A navigation wall complete", flush=True)
wall_b()
print("B fabrication wall complete", flush=True)
wall_c()
print("C habitat wall complete", flush=True)
wall_d()
print("D observation wall complete", flush=True)
structure()

# Blender presentation lights/cameras are retained for art review but excluded
# from FBX. The shipping Unity scene creates its own real-time lighting.
scene=bpy.context.scene
scene.unit_settings.system="METRIC"
scene.unit_settings.scale_length=1
scene.world.color=(.055,.055,.055)
scene.render.engine="CYCLES"
scene.cycles.samples=24
scene.render.resolution_x=1440
scene.render.resolution_y=900
scene.render.resolution_percentage=100
scene.view_settings.view_transform="AgX"
for wall in ["A","B","C","D"]:
    data=bpy.data.lights.new("Preview warm lamp "+wall,"POINT")
    data.energy=32
    data.color=(1,.64,.30)
    data.shadow_soft_size=.13
    ob=bpy.data.objects.new(data.name,data)
    scene.collection.objects.link(ob)
    ob.location=wp(0,2.19,.30,wall)
    fill=bpy.data.lights.new("Preview fill "+wall,"AREA")
    fill.energy=18
    fill.color=(.38,.55,.62)
    fill.shape="DISK"
    fill.size=1.5
    ob=bpy.data.objects.new(fill.name,fill)
    scene.collection.objects.link(ob)
    ob.location=wp(0,1.4,1.15,wall)
    ob.rotation_euler=(wp(0,1.3,.1,wall)-ob.location).to_track_quat("-Z","Y").to_euler()
for wall in ["A","B","C","D"]:
    data=bpy.data.cameras.new("VIEW_"+wall)
    data.lens=13.5
    ob=bpy.data.objects.new(data.name,data)
    scene.collection.objects.link(ob)
    ob.location=(0,0,1.28)
    ob.rotation_euler=(wp(0,1.32,0,wall)-ob.location).to_track_quat("-Z","Y").to_euler()
    if wall=="A":scene.camera=ob

bpy.context.view_layer.update()
blend_path=SOURCE/"Astra_Cabin.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
print("Saved editable Blender source:",blend_path,flush=True)

# Mesh-copy evaluated objects for compact FBX. The original source stays untouched.
export_collection=bpy.data.collections.new("EXPORT_BATCHED")
scene.collection.children.link(export_collection)
deps=bpy.context.evaluated_depsgraph_get()
export_objs=[]
export_dynamic={}
for item in INTERACTIONS:
    src=bpy.data.objects[item["name"]]
    ob=bpy.data.objects.new(item["name"]+"__EXPORT",None)
    export_collection.objects.link(ob)
    ob.matrix_world=src.matrix_world.copy()
    export_dynamic[item["name"]]=ob
    export_objs.append(ob)

groups={}
for src in OBJECTS:
    if src.type not in {"MESH","CURVE","FONT"}:continue
    if src.get("astra_preview_only",False):continue
    evaluated=src.evaluated_get(deps)
    data=bpy.data.meshes.new_from_object(evaluated,preserve_all_data_layers=True,depsgraph=deps)
    if not data or not data.vertices:continue
    data.transform(src.matrix_world)
    data.update()
    # Simple per-face UVs keep PBR assets usable in Unity and external DCCs.
    if not data.uv_layers:
        uv=data.uv_layers.new(name="UVMap")
        for poly in data.polygons:
            normal=poly.normal
            major=max(range(3),key=lambda i:abs(normal[i]))
            axes=[i for i in range(3) if i!=major]
            for li in poly.loop_indices:
                co=data.vertices[data.loops[li].vertex_index].co
                if src.name=="SCREEN_Main":
                    uv.data[li].uv=(1-(co.x+.825)/1.65,(co.z-1.092)/.876)
                else:
                    uv.data[li].uv=(co[axes[0]],co[axes[1]])
    dyn=src.get("astra_dynamic","")
    keep=src.get("astra_keep_name",False)
    if keep: key=("NAMED",src.name)
    elif dyn:key=("DYNAMIC",dyn,src.get("astra_material",""))
    else:key=("STATIC",src.get("astra_wall",""),src.get("astra_material",""))
    name="__".join(key)
    ob=bpy.data.objects.new(name,data)
    export_collection.objects.link(ob)
    groups.setdefault(key,[]).append(ob)

for key,objs in groups.items():
    bpy.ops.object.select_all(action="DESELECT")
    for ob in objs:ob.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    if len(objs)>1:bpy.ops.object.join()
    ob=objs[0]
    if key[0]=="NAMED":
        ob.name=key[1]+"__EXPORT"
        # Dynamic head remains a mesh with a useful local centre origin.
        if key[1]=="PRINT_Head":
            center=wp(.08,.855,.30,"B")
            ob.data.transform(Matrix.Translation(-center))
            ob.location=center
            export_dynamic["PRINT_Head"]=ob
    elif key[0]=="DYNAMIC":
        ob.name=key[1]+"_"+key[2]+"__EXPORT"
    else:ob.name="STATIC_"+key[1]+"_"+key[2]
    export_objs.append(ob)

# Parent after PRINT_Head has been created, keeping all mesh coordinates aligned.
for ob in export_objs:
    for dynamic_name,parent in export_dynamic.items():
        if ob.name.startswith(dynamic_name+"_") and ob!=parent and ob.type=="MESH" and ob.name!=dynamic_name+"__EXPORT":
            ob.parent=parent
            ob.matrix_parent_inverse=parent.matrix_world.inverted()

# Temporarily free the public names for exported meshes/empties only.
rename_back=[]
for ob in export_objs:
    if ob.name.endswith("__EXPORT"):
        intended=ob.name[:-8]
        existing=bpy.data.objects.get(intended)
        if existing:
            rename_back.append((existing,intended))
            existing.name=intended+"__SOURCE"
        ob.name=intended
bpy.ops.object.select_all(action="DESELECT")
for ob in export_objs:ob.select_set(True)
bpy.context.view_layer.objects.active=export_objs[0]
fbx_path=MODEL_DIR/"Astra_Cabin.fbx"
bpy.ops.export_scene.fbx(filepath=str(fbx_path),use_selection=True,
    object_types={"MESH","EMPTY"},axis_forward="-Z",axis_up="Y",
    apply_unit_scale=True,apply_scale_options="FBX_SCALE_UNITS",global_scale=1.0,
    use_mesh_modifiers=True,use_triangles=True,mesh_smooth_type="FACE",
    bake_anim=False,add_leaf_bones=False,bake_space_transform=True,
    path_mode="AUTO",use_custom_props=True)
triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in export_objs if o.type=="MESH")
manifest={
    "title":"ASTRA single-occupant pressure cabin",
    "dimensions_metres":{"width":3.2,"depth":3.2,"height":2.5},
    "blender_coordinates":"Z up, A wall -Y; verified Unity import mapping (-x,z,-y). Keep instance scale (1,1,1).",
    "fbx_export":{"axis_forward":"-Z","axis_up":"Y","bake_space_transform":True,"units":"metres"},
    "eye_unity":[0,1.28,0],
    "wall_directions_unity":{"A":[0,0,1],"B":[-1,0,0],"C":[0,0,-1],"D":[1,0,0]},
    "materials":list(SPEC.keys()),"editable_source_object_count":len(OBJECTS),
    "export_object_count":len(export_objs),"export_triangle_count":triangles,
    "interactions":INTERACTIONS,
    "screen":{"name":"SCREEN_Main","center_unity":world_to_unity(wp(0,1.53,.357,"A")),"width":1.65,"height":.876},
    "printer_head":{"name":"PRINT_Head","center_unity":world_to_unity(wp(.08,.855,.30,"B")),"motion_axis_unity":[0,0,-1],"travel":.65},
    "window":{"wall":"D","center_unity":world_to_unity(wp(0,1.65,0,"D")),"clear_width":1.80,"clear_height":.85,"note":"Real open aperture; no image or backing mesh"},
    "lamps":[{"name":"LAMP_"+w,"center_unity":world_to_unity(wp(0,2.28,.27,w))} for w in ["A","B","C","D"]],
    "notes":[".blend retains independent objects/curves and review cameras; FBX static meshes merged per wall/material.",
        "Unity import verified: no root mirror or additional rotation. B is -X and D/window is +X. ACT local rotations are identity.",
        "Pivot axes in interactions are Unity WORLD axes, independent of importer local orientation."]
}
(SOURCE/"model_manifest.json").write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding="utf-8")
(MODEL_DIR/"model_manifest.json").write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding="utf-8")
print(json.dumps({"blend":str(blend_path),"fbx":str(fbx_path),"source_objects":len(OBJECTS),"export_objects":len(export_objs),"triangles":triangles}),flush=True)
