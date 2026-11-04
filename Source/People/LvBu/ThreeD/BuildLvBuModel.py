"""吕布三维造型母版：真实网格、独立配件与骨架；在 Blender 后台运行。"""
import bpy
import math
import random
import json
import sys
from pathlib import Path
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[3]
OUT = ROOT / 'Build' / 'LvBuModelV1'
OUT.mkdir(parents=True, exist_ok=True)
random.seed(731)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for item in list(bpy.data.collections):
    if item.name != 'Collection':
        bpy.data.collections.remove(item)
base = bpy.data.collections.get('Collection')
base.name = '吕布 · 三维造型 V1'
COLLS = {}
for label in ['身体与面部', '发束与冠座', '双冠翎', '黑金层甲', '披风与织物', '方天画戟', '骨架', '展示环境', '造型参考']:
    c = bpy.data.collections.new(label)
    bpy.context.scene.collection.children.link(c)
    COLLS[label] = c
PARTS = []

def move_collection(obj, name):
    for coll in list(obj.users_collection):
        coll.objects.unlink(obj)
    COLLS[name].objects.link(obj)

def material(name, color, metallic=0, rough=.65, grain=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    nt = m.node_tree
    bs = nt.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*color, 1)
    bs.inputs['Metallic'].default_value = metallic
    bs.inputs['Roughness'].default_value = rough
    if grain:
        tex = nt.nodes.new('ShaderNodeTexNoise')
        tex.inputs['Scale'].default_value = 95
        tex.inputs['Detail'].default_value = 2
        ramp = nt.nodes.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].position = .18
        ramp.color_ramp.elements[0].color = (*(v*.65 for v in color), 1)
        ramp.color_ramp.elements[1].position = .84
        ramp.color_ramp.elements[1].color = (*(min(v*1.18,1) for v in color), 1)
        nt.links.new(tex.outputs['Fac'], ramp.inputs[0])
        nt.links.new(ramp.outputs['Color'], bs.inputs['Base Color'])
        bump = nt.nodes.new('ShaderNodeBump')
        bump.inputs['Strength'].default_value = grain
        bump.inputs['Distance'].default_value = .018
        nt.links.new(tex.outputs['Fac'], bump.inputs['Height'])
        nt.links.new(bump.outputs['Normal'], bs.inputs['Normal'])
    return m

SKIN = material('肤色 · 赭金', (.56,.30,.145), rough=.83)
SKIN_LIGHT = material('肤色 · 鼻梁与手背', (.66,.38,.19), rough=.83)
SKIN_DARK = material('肤色 · 眼窝与指缝', (.29,.125,.056), rough=.9)
IRON = material('黑铁 · 锻造颗粒', (.037,.045,.049), .62,.5,.13)
IRON_LIGHT = material('铁甲 · 隆起面', (.064,.073,.075), .55,.51,.1)
IRON_DARK = material('层甲 · 阴影', (.018,.022,.025), .38,.65)
GOLD = material('旧金 · 包边', (.44,.24,.065), .68,.45,.06)
GOLD_LIGHT = material('旧金 · 浮雕', (.63,.37,.10), .65,.43,.05)
GOLD_DARK = material('铜 · 内层', (.22,.10,.025), .6,.6)
RED = material('暗红 · 绒布', (.30,.025,.018), .0,.88,.15)
RED_LIGHT = material('朱红 · 翎面', (.43,.043,.025), .0,.7,.08)
RED_DARK = material('暗红 · 折缝', (.13,.007,.006), .0,.96)
HAIR = material('黑发 · 碳黑', (.018,.017,.017), .05,.69,.06)
HAIR_LIGHT = material('黑发 · 分束', (.039,.034,.030), .03,.74)
LEATHER = material('皮革 · 黝黑', (.025,.019,.015), 0,.87,.13)
STEEL = material('戟刃 · 冷铁', (.24,.29,.31), .8,.36,.05)
EDGE = material('刃口 · 磨亮', (.47,.53,.52), .82,.3)
EYE = material('眼白 · 骨色', (.79,.76,.61), 0,.58)
BLACK = material('眉眼 · 墨', (.004,.005,.005), 0,.82)
RUBY = material('冠宝 · 暗朱', (.37,.008,.008), .3,.26)

def finish(obj, mat, coll, bone=None, smooth=False):
    move_collection(obj,coll)
    if mat:
        obj.data.materials.append(mat)
    if obj.type=='MESH':
        for face in obj.data.polygons:
            face.use_smooth=smooth
    if bone:
        obj['绑定骨骼']=bone
        PARTS.append((obj,bone))
    return obj

def mesh(name, verts, faces, mat, coll='黑金层甲', bone='Chest', smooth=False, bevel=0):
    data=bpy.data.meshes.new(name+'Mesh')
    data.from_pydata(verts,[],faces)
    data.update()
    obj=bpy.data.objects.new(name,data)
    base.objects.link(obj)
    finish(obj,mat,coll,bone,smooth)
    if bevel:
        mod=obj.modifiers.new('边缘锻造倒角','BEVEL')
        mod.width=bevel
        mod.segments=2
    return obj

def uvball(name, p, scale, mat, coll='黑金层甲', bone='Chest', seg=24, rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=rings, location=p)
    obj=bpy.context.object
    obj.name=name
    obj.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(obj,mat,coll,bone,True)

def cube(name,p,scale,mat,coll='黑金层甲',bone='Chest',bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1, location=p)
    obj=bpy.context.object
    obj.name=name
    obj.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    finish(obj,mat,coll,bone)
    if bevel:
        mod=obj.modifiers.new('圆钝边缘','BEVEL');mod.width=bevel;mod.segments=3
        mod=obj.modifiers.new('加权面法线','WEIGHTED_NORMAL')
    return obj

def rod(name,a,b,r,mat,coll='黑金层甲',bone='Chest',r2=None,segments=12):
    a,b=Vector(a),Vector(b)
    bpy.ops.mesh.primitive_cone_add(vertices=segments,radius1=r,radius2=r if r2 is None else r2,depth=(b-a).length,location=(a+b)*.5)
    obj=bpy.context.object;obj.name=name
    obj.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
    return finish(obj,mat,coll,bone,True)

def line(name,points,r,mat,coll='黑金层甲',bone='Chest',cyclic=False):
    data=bpy.data.curves.new(name+'Curve','CURVE');data.dimensions='3D'
    data.resolution_u=12;data.bevel_depth=r;data.bevel_resolution=2
    spline=data.splines.new('POLY');spline.points.add(len(points)-1)
    for p,co in zip(spline.points,points):p.co=(*co,1)
    spline.use_cyclic_u=cyclic
    obj=bpy.data.objects.new(name,data);base.objects.link(obj)
    return finish(obj,mat,coll,bone)

def ring(name,center,rx,ry,z,mat=GOLD,bone='Chest',thick=.018,coll='黑金层甲',start=0,end=math.tau):
    return line(name,[(center[0]+rx*math.cos(t),center[1]+ry*math.sin(t),z) for t in [start+(end-start)*i/64 for i in range(65)]],thick,mat,coll,bone)

def loft(name,rings,mat,coll='黑金层甲',bone='Chest',n=32,exp=1,smooth=True,cap=True):
    # 各环为高度、半宽、半厚、前后偏移；扁椭圆用于厚实方脸和胸甲。
    verts=[]
    for z,rx,ry,cy in rings:
        for i in range(n):
            t=math.tau*i/n
            sx,sy=math.cos(t),math.sin(t)
            verts.append((rx*math.copysign(abs(sx)**exp,sx),cy+ry*math.copysign(abs(sy)**exp,sy),z))
    faces=[]
    for row in range(len(rings)-1):
        for i in range(n):
            j=(i+1)%n;faces.append((row*n+i,row*n+j,(row+1)*n+j,(row+1)*n+i))
    if cap:
        faces.extend([tuple(reversed(range(n))),tuple((len(rings)-1)*n+i for i in range(n))])
    return mesh(name,verts,faces,mat,coll,bone,smooth)

def plate(name,coords,depth,mat=IRON,bone='Chest',coll='黑金层甲',bevel=.012,trim=True):
    # 正面为朝-Y的多边形，内挤出形成真实厚度，边缘独立铜饰。
    n=len(coords)
    verts=list(coords)+[(x,y+depth,z) for x,y,z in coords]
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]
    faces.extend((i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n))
    obj=mesh(name,verts,faces,mat,coll,bone,False,bevel)
    if trim:line(name+' · 铜缘',coords,.013,GOLD,coll,bone,True)
    return obj

def bezier(points,steps=36):
    # 分段三次贝塞尔；用于实心发束和独立羽带，不是平面图卡。
    out=[]
    for seg in range((len(points)-1)//3):
        a,b,c,d=[Vector(v) for v in points[seg*3:seg*3+4]]
        for i in range(steps+(1 if seg==(len(points)-1)//3-1 else 0)):
            t=i/steps;out.append(a*(1-t)**3+b*3*(1-t)**2*t+c*3*(1-t)*t*t+d*t**3)
    return out

def swept(name,points,widths,depths,mat,coll,bone,sections=10):
    # 椭圆截面沿空间曲线变化，头发与翎的背侧同样具有体积。
    verts=[]
    for i,c in enumerate(points):
        t=i/(len(points)-1)
        tangent=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized()
        side=tangent.cross(Vector((0,0,1)))
        if side.length<.1:side=tangent.cross(Vector((0,1,0)))
        side.normalize();normal=tangent.cross(side).normalized()
        at=t*(len(widths)-1);lo=min(int(at),len(widths)-2);f=at-lo
        w=widths[lo]*(1-f)+widths[lo+1]*f;d=depths[lo]*(1-f)+depths[lo+1]*f
        for j in range(sections):
            a=math.tau*j/sections;verts.append(tuple(c+side*(math.cos(a)*w)+normal*(math.sin(a)*d)))
    faces=[]
    for i in range(len(points)-1):
        for j in range(sections):faces.append((i*sections+j,i*sections+(j+1)%sections,(i+1)*sections+(j+1)%sections,(i+1)*sections+j))
    faces.extend([tuple(reversed(range(sections))),tuple((len(points)-1)*sections+j for j in range(sections))])
    return mesh(name,verts,faces,mat,coll,bone,True)

# 身体骨架比例：不含冠翎约3.4头身，宽肩、粗肢、方脸。
loft('颈部',[(2.08,.17,.15,0),(2.32,.18,.16,0)],SKIN,'身体与面部','Neck')
loft('头部 · 方颌',[(2.23,.25,.235,-.018),(2.29,.335,.29,-.012),(2.43,.405,.335,0),(2.66,.458,.365,0),(2.83,.457,.35,.01),(2.98,.39,.30,.025),(3.06,.22,.20,.02)],SKIN,'身体与面部','Head',48,.57)
for s in [-1,1]:
    uvball('耳廓'+str(s),(s*.453,.025,2.59),(.09,.078,.14),SKIN,'身体与面部','Head')
    uvball('耳内'+str(s),(s*.483,-.029,2.595),(.044,.037,.089),SKIN_DARK,'身体与面部','Head')
    inner,outer=.071,.354
    eye=[(s*inner,-.371,2.675),(s*outer,-.355,2.746),(s*.350,-.366,2.635),(s*.12,-.381,2.604)]
    outline=[(s*.057,-.366,2.684),(s*.367,-.350,2.766),(s*.365,-.357,2.621),(s*.112,-.375,2.591)]
    plate('眼线'+str(s),outline,.012,BLACK,'Head','身体与面部',.009,False)
    plate('眼白'+str(s),eye,.006,EYE,'Head','身体与面部',.006,False)
    uvball('瞳孔'+str(s),(s*.208,-.387,2.663),(.055,.016,.061),BLACK,'身体与面部','Head',20,12)
    uvball('瞳光'+str(s),(s*.195,-.402,2.691),(.012,.007,.016),EYE,'身体与面部','Head',12,8)
    brow=[(s*.054,-.387,2.738),(s*.332,-.365,2.867),(s*.391,-.339,2.797),(s*.111,-.391,2.697)]
    plate('浓眉'+str(s),brow,.028,HAIR,'Head','身体与面部',.01,False)
    line('下睑'+str(s),[(s*.12,-.382,2.59),(s*.26,-.375,2.60),(s*.35,-.355,2.63)],.009,SKIN_DARK,'身体与面部','Head')
# 鼻梁与楔形鼻尖共同组成可观察的侧面轮廓。
mesh('鼻梁', [(-.059,-.363,2.73),(.059,-.363,2.73),(-.068,-.455,2.49),(.068,-.455,2.49),(0,-.51,2.51),(0,-.398,2.76)],[(0,2,4,5),(1,5,4,3),(2,3,4),(0,1,3,2),(0,5,1)],SKIN_LIGHT,'身体与面部','Head',True,.015)
for s in [-1,1]:uvball('鼻翼'+str(s),(s*.064,-.437,2.49),(.043,.035,.032),SKIN,'身体与面部','Head',16,10)
line('唇线',[(-.13,-.352,2.386),(-.048,-.375,2.397),(.042,-.377,2.392),(.13,-.352,2.386)],.011,SKIN_DARK,'身体与面部','Head')
line('下唇',[(-.095,-.362,2.358),(0,-.377,2.347),(.095,-.362,2.358)],.016,SKIN_LIGHT,'身体与面部','Head')

# 后脑黑发；面部开口独立生成，避免黑色球壳遮住眉眼。
verts=[];faces=[];N=48;ROWS=12
for row in range(ROWS):
    u=row/(ROWS-1)
    for j in range(N):
        a=math.tau*j/N;front=max(0,-math.sin(a))
        bottom=2.34+.52*front**3
        z=bottom+(3.15-bottom)*u
        radial=(1-u**2.5)**.45
        verts.append((.48*math.cos(a)*radial,.02+.391*math.sin(a)*radial,z))
for row in range(ROWS-1):
    for j in range(N):faces.append((row*N+j,row*N+(j+1)%N,(row+1)*N+(j+1)%N,(row+1)*N+j))
mesh('黑发底层',verts,faces,HAIR,'发束与冠座','Head',True)
for s in [-1,1]:
    for k in range(6):
        a=k/5
        points=bezier([(s*(.10+.31*a),-.35+.11*a,2.85-.22*a),(s*(.34+.13*a),-.25+.12*a,2.97),(s*.37,.10+.13*a,3.15),(s*.10,.19+.06*a,3.25-.10*a)],18)
        swept('侧梳发束'+str(s)+'_'+str(k),points,[.055,.095,.07,.006],[.028,.048,.045,.003],HAIR if k%2==0 else HAIR_LIGHT,'发束与冠座','Head')
    for k in range(5):
        a=k/4
        points=bezier([(s*(.13+.27*a),.29,2.39+.09*a),(s*.49,.43,2.63),(s*.29,.45,2.92),(s*.04,.21,3.19)],18)
        swept('后脑向上发束'+str(s)+'_'+str(k),points,[.07,.105,.075,.005],[.025,.043,.035,.004],HAIR if k%2 else HAIR_LIGHT,'发束与冠座','Head')
    points=bezier([(s*.443,-.07,2.75),(s*.451,-.16,2.57),(s*.378,-.23,2.39),(s*.31,-.24,2.32)],16)
    swept('鬓角'+str(s),points,[.049,.052,.034,.003],[.026,.026,.022,.002],HAIR,'发束与冠座','Head')

# 冠座、束发箍和镶嵌。顶部两个独立插座随后连接两根冠翎。
loft('黑铁冠座',[(3.04,.25,.20,.035),(3.14,.23,.18,.04),(3.35,.12,.13,.045),(3.40,.035,.065,.04)],IRON,'发束与冠座','Head',32,.8)
ring('冠座下金箍',(0,.035),.255,.205,3.07,GOLD,'Head',.022,'发束与冠座')
for s in [-1,1]:
    line('冠脊'+str(s),[(s*.24,-.095,3.08),(s*.145,-.122,3.23),(s*.08,-.077,3.37)],.019,GOLD,'发束与冠座','Head')
plate('冠额饰',[(-.082,-.187,3.08),(-.095,-.17,3.24),(0,-.145,3.34),(.095,-.17,3.24),(.082,-.187,3.08),(0,-.20,3.02)],.025,GOLD_LIGHT,'Head','发束与冠座',.008,False)
uvball('冠额红宝',(0,-.213,3.17),(.039,.026,.081),RUBY,'发束与冠座','Head',16,10)

# 贴身底衣、胸腹甲、交叠腰带和分片裙甲。
loft('底衣',[(1.10,.39,.24,0),(1.34,.41,.27,0),(1.72,.52,.31,0),(2.02,.61,.31,0),(2.14,.44,.245,0)],LEATHER,'身体与面部','Chest',32,.76)
loft('上胸黑铁甲',[(1.57,.43,.29,-.015),(1.68,.49,.34,-.01),(1.96,.58,.345,0),(2.09,.44,.26,0)],IRON,'黑金层甲','Chest',40,.64)
for z,rx,ry in [(1.60,.445,.304),(1.73,.49,.346),(2.01,.52,.325)]:
    ring('胸甲分层金缘'+str(z),(0,-.01),rx,ry,z,GOLD,'Chest',.016)
plate('胸前中央甲', [(-.16,-.347,2.04),(.16,-.347,2.04),(.19,-.374,1.78),(0,-.387,1.69),(-.19,-.374,1.78)],.035,IRON_LIGHT)
for s in [-1,1]:
    line('胸甲横向嵌金'+str(s),[(s*.15,-.365,1.88),(s*.36,-.347,1.83),(s*.48,-.29,1.85)],.019,GOLD)
    line('胸前兽纹'+str(s),[(s*.02,-.40,1.98),(s*.12,-.396,1.93),(s*.11,-.404,1.85),(s*.042,-.410,1.80),(s*.085,-.405,1.75)],.016,GOLD_LIGHT)
    uvball('胸甲铆钉'+str(s),(s*.38,-.336,1.99),(.025,.015,.025),GOLD_LIGHT)
for i in range(3):
    z=1.48-i*.092
    loft('腹甲'+str(i),[(z-.06,.406,.285,0),(z+.05,.435,.302,0)],IRON if i%2 else IRON_LIGHT,'黑金层甲','Spine',36,.68)
    ring('腹甲下金边'+str(i),(0,0),.411,.294,z-.045,GOLD,'Spine',.013)
loft('暗红腰封',[(1.20,.44,.29,0),(1.34,.44,.29,0)],RED,'披风与织物','Pelvis',40,.60)
ring('腰封绳',(0,0),.45,.303,1.28,GOLD_DARK,'Pelvis',.027,'披风与织物')
cube('腰带扣',(0,-.328,1.275),(.19,.057,.14),GOLD,'黑金层甲','Pelvis',.018)
cube('腰带扣内铁',(0,-.361,1.275),(.12,.016,.081),IRON,'黑金层甲','Pelvis',.011)
for i,a in enumerate([-2.80,-2.25,-1.67,-1.12,-.56,0,.65,1.35,2.08,2.7]):
    w=.22 if i not in [1,2,3] else .23
    radial=Vector((math.cos(a),math.sin(a),0));side=Vector((-math.sin(a),math.cos(a),0))
    top=Vector((radial.x*.40,radial.y*.28,1.20))
    low=Vector((radial.x*.55,radial.y*.42,.80+(.035 if i%2 else 0)))
    points=[top-side*w*.58,top+side*w*.58,low+side*w*.64,low+radial*.018-Vector((0,0,.05)),low-side*w*.64]
    obj=mesh('分片裙甲'+str(i),[tuple(p) for p in points],[(0,1,2,3,4)],IRON,'黑金层甲','Pelvis',True)
    sol=obj.modifiers.new('甲片厚度','SOLIDIFY');sol.thickness=.04
    bevel=obj.modifiers.new('甲片边缘','BEVEL');bevel.width=.012;bevel.segments=2
    line('裙甲铜缘'+str(i),[tuple(p+radial*.025) for p in points],.018,GOLD,'黑金层甲','Pelvis',True)
    line('裙甲中脊'+str(i),[tuple(top+radial*.03),tuple(low+radial*.04)],.012,IRON_LIGHT,'黑金层甲','Pelvis')

def cloth_panel(name,center,width,height,depth,mat,bone,folds=5):
    verts=[];faces=[];nx=18;ny=18
    for j in range(ny+1):
        t=j/ny
        for i in range(nx+1):
            u=i/nx;wave=math.sin(u*math.pi*folds+t*.6)
            verts.append((center[0]+(u-.5)*width*(.8+.2*t),center[1]+depth*t+.025*wave*t,center[2]-height*t+.025*math.cos(u*math.pi*6)*t*t))
    for j in range(ny):
        for i in range(nx):
            a=j*(nx+1)+i;faces.append((a,a+1,a+nx+2,a+nx+1))
    obj=mesh(name,verts,faces,mat,'披风与织物',bone,True)
    sol=obj.modifiers.new('织物双面厚度','SOLIDIFY');sol.thickness=.012
    return obj

cloth_panel('前腰红绸',(0,-.322,1.22),.26,.47,-.105,RED,'Pelvis',5)
for s in [-1,1]:
    cloth_panel('侧腰红衬'+str(s),(s*.42,.025,1.20),.23,.45,.1,RED_DARK,'Pelvis',3)

# 领巾包住领口，系结在胸口，避免披风与脖颈悬空。
loft('颈部红巾',[(2.10,.32,.245,-.01),(2.19,.28,.226,-.01),(2.25,.19,.18,0)],RED,'披风与织物','Neck',40,.7)
for z in [2.13,2.18]:ring('领巾折边'+str(z),(0,-.012),.29,.235,z,RED_DARK,'Neck',.011,'披风与织物')
uvball('领巾结',(.13,-.24,2.13),(.07,.039,.058),RED_LIGHT,'披风与织物','Neck')

def axis_ring(name,center,axis,radius,mat,bone,thick=.015,coll='黑金层甲',oval=1):
    axis=Vector(axis).normalized();u=axis.cross(Vector((0,1,0))).normalized();v=axis.cross(u).normalized();center=Vector(center)
    line(name,[tuple(center+(u*math.cos(math.tau*i/40)+v*math.sin(math.tau*i/40)*oval)*radius) for i in range(41)],thick,mat,coll,bone)

JOINTS={}
for s,side in [(-1,'R'),(1,'L')]:
    shoulder=Vector((s*.61,0,2.005));elbow=Vector((s*.76,-.045,1.655))
    wrist=Vector((s*.755,-.135,1.305)) if s<0 else Vector((.82,-.298,1.475))
    JOINTS[side]=(shoulder,elbow,wrist)
    upper='UpperArm.'+side;lower='Forearm.'+side;hand='Hand.'+side
    rod('上臂底衣'+side,shoulder,elbow,.145,LEATHER,'身体与面部',upper,r2=.12)
    rod('上臂护甲'+side,shoulder.lerp(elbow,.25),shoulder.lerp(elbow,.83),.174,IRON,bone=upper,r2=.145)
    axis_ring('上臂镶边'+side,shoulder.lerp(elbow,.79),elbow-shoulder,.149,GOLD,upper)
    uvball('肘部连接'+side,elbow,(.123,.125,.128),LEATHER,'身体与面部',lower)
    uvball('肘甲'+side,elbow+Vector((0,.015,0)),(.15,.144,.12),IRON,bone=lower)
    rod('小臂底衣'+side,elbow,wrist,.116,LEATHER,'身体与面部',lower,r2=.086)
    rod('黑铁护臂'+side,elbow.lerp(wrist,.17),elbow.lerp(wrist,.90),.153,IRON,bone=lower,r2=.111,segments=10)
    for t,r in [(.20,.15),(.85,.113)]:axis_ring('护臂铜箍'+side+str(t),elbow.lerp(wrist,t),wrist-elbow,r,GOLD,lower,.022)
    axis_ring('袖口'+side,elbow.lerp(wrist,.99),wrist-elbow,.095,GOLD_DARK,lower,.014)
    # 皮带环和有起伏的金属压条让护臂从侧面仍可读。
    strip=[tuple(elbow.lerp(wrist,t)+Vector((0,-.13,0))) for t in [.23,.45,.70,.85]]
    line('护臂压条'+side,strip,.014,GOLD, bone=lower)

    # 肩甲为叠层弧壳，外沿覆盖上臂，顶部有兽面浮雕。
    for k in [2,1,0]:
        pos=(s*(.65+.018*k),.015,2.025-.10*k)
        uvball('肩甲弧壳'+side+str(k),pos,(.31-.018*k,.305-.012*k,.18-.016*k),IRON if k==0 else IRON_DARK,bone=upper)
        line('肩甲金缘'+side+str(k),[(pos[0]+(.285-.01*k)*math.cos(t),pos[1]+(.287-.01*k)*math.sin(t),pos[2]-.045+.028*math.sin(t)) for t in [math.tau*i/56 for i in range(57)]],.018,GOLD,bone=upper)
    crest_center=Vector((s*.655,-.276,2.09))
    uvball('肩兽面基座'+side,crest_center,(.125,.048,.105),GOLD_DARK,bone=upper)
    for k in range(7):
        a=math.pi*.08+k*math.pi*.14
        x=s*(.655+.148*math.cos(a));z=2.086+.103*math.sin(a)
        line('肩兽卷鬃'+side+str(k),[(x,-.284,z),(x+s*.02,-.30,z+.035),(x+s*.046,-.285,z+.013)],.018,GOLD_LIGHT,bone=upper)
    for ss in [-1,1]:
        uvball('肩兽眼'+side+str(ss),(s*.655+ss*.049,-.322,2.107),(.028,.016,.021),IRON_DARK,bone=upper)
        line('肩兽眉'+side+str(ss),[(s*.655+ss*.015,-.322,2.12),(s*.655+ss*.066,-.313,2.14)],.015,GOLD_LIGHT,bone=upper)
    uvball('肩兽吻'+side,(s*.655,-.326,2.062),(.054,.031,.037),GOLD_LIGHT,bone=upper)
    for ss in [-1,1]:rod('肩兽獠牙'+side+str(ss),(s*.655+ss*.042,-.323,2.051),(s*.655+ss*.027,-.328,2.016),.012,GOLD_LIGHT,bone=upper,r2=.003,segments=8)

    # 手掌和四指、拇指分开；持戟侧指节环绕真实杆身。
    if s>0:
        palm=Vector((.874,-.32,1.44))
        uvball('持戟手掌',palm,(.095,.073,.112),SKIN,'身体与面部',hand)
        for k in range(4):
            z=1.495-k*.043
            pts=[(.865,-.37,z),(.90,-.408,z-.008),(.958,-.38,z-.012),(.952,-.318,z-.012),(.922,-.30,z-.01)]
            line('持戟手指'+str(k),pts,.022,SKIN_LIGHT,'身体与面部',hand)
            uvball('持戟指节'+str(k),(.924,-.398,z-.008),(.025,.023,.021),SKIN,'身体与面部',hand,16,10)
        line('持戟拇指',[(.822,-.31,1.485),(.851,-.383,1.485),(.901,-.408,1.457)],.036,SKIN,'身体与面部',hand)
    else:
        palm=wrist+Vector((-.01,-.018,-.095))
        uvball('下垂手掌',palm,(.096,.07,.107),SKIN,'身体与面部',hand)
        for k in range(4):
            x=palm.x-.066+k*.043
            pts=[(x,palm.y-.042,palm.z+.025),(x,palm.y-.072,palm.z-.06),(x,palm.y-.045,palm.z-.102),(x,palm.y+.003,palm.z-.067)]
            line('松握手指'+str(k),pts,.025,SKIN_LIGHT,'身体与面部',hand)
        line('下垂拇指',[(palm.x+.075,palm.y-.018,palm.z+.053),(palm.x+.10,palm.y-.065,palm.z+.004),(palm.x+.058,palm.y-.085,palm.z-.025)],.032,SKIN,'身体与面部',hand)

# 双腿微松，靴底贴地；保持紧凑站距而非大跨步。
LEGS={}
for s,side in [(-1,'R'),(1,'L')]:
    x=s*.235;y=.035 if s<0 else -.025
    hip=Vector((x,0,1.20));knee=Vector((x,y-.035,.67));ankle=Vector((x,y,.18))
    LEGS[side]=(hip,knee,ankle)
    thigh='Thigh.'+side;shin='Shin.'+side;foot='Foot.'+side
    rod('裤腿'+side,hip,knee,.19,LEATHER,'身体与面部',thigh,r2=.162,segments=16)
    uvball('大腿甲'+side,(x,y-.006,.91),(.205,.206,.285),IRON,bone=thigh)
    axis_ring('大腿甲金边'+side,(x,y,.75),(0,0,1),.188,GOLD,thigh,.017,oval=1.06)
    uvball('膝甲'+side,(x,y-.10,.66),(.17,.17,.135),IRON_LIGHT,bone=shin)
    line('膝甲金纹'+side,[(x-.13,y-.19,.69),(x,y-.247,.61),(x+.13,y-.19,.69)],.021,GOLD,bone=shin)
    rod('小腿内衬'+side,knee,ankle,.14,LEATHER,'身体与面部',shin,r2=.115)
    plate('胫甲'+side,[(x-.12,y-.14,.60),(x+.12,y-.14,.60),(x+.11,y-.16,.25),(x,y-.21,.21),(x-.11,y-.16,.25)],.13,IRON,shin,bevel=.025)
    for z in [.34,.52]:axis_ring('胫甲束带'+side+str(z),(x,y,z),(0,0,1),.135,GOLD_DARK,shin,.019,oval=1.14)
    boot=cube('厚靴'+side,(x,y-.11,.14),(.31,.47,.24),LEATHER,bone=foot,bevel=.085)
    toe=uvball('靴头铁甲'+side,(x,y-.245,.175),(.16,.21,.095),IRON,bone=foot)
    cube('靴底'+side,(x,y-.12,.045),(.33,.49,.08),IRON_DARK,bone=foot,bevel=.036)
    line('靴底铜缘'+side,[(x-.14,y+.10,.084),(x-.16,y-.19,.084),(x-.105,y-.34,.084),(x+.105,y-.34,.084),(x+.16,y-.19,.084),(x+.14,y+.10,.084)],.014,GOLD,bone=foot,cyclic=True)
    line('靴面金纹'+side,[(x-.145,y-.18,.19),(x,y-.25,.25),(x+.145,y-.18,.19)],.016,GOLD,bone=foot)

# 披风网格从肩后领口垂落，背面可见连续的织物褶皱。
verts=[];faces=[];NX=32;NY=36
for j in range(NY+1):
    t=j/NY
    for i in range(NX+1):
        u=i/NX;xx=(u-.5)*2
        width=.45+.18*math.sin(t*math.pi*.7)
        x=xx*width+.10*t*t
        y=.245+.23*t+.11*math.sin(math.pi*t)+.048*math.cos(xx*math.pi*3.2+t*.9)*(.3+.7*t)
        z=2.12-1.16*t+.065*math.cos(xx*math.pi*3)*t**5+.045*xx*t
        verts.append((x,y,z))
for j in range(NY):
    for i in range(NX):
        a=j*(NX+1)+i;faces.append((a,a+1,a+NX+2,a+NX+1))
cape=mesh('暗红披风 · 连续厚布',verts,faces,RED,'披风与织物',None,True)
cape['柔性部件']='Cape'
mod=cape.modifiers.new('披风厚度','SOLIDIFY');mod.thickness=.016
line('披风下摆缝线',verts[-NX-1:],.012,RED_DARK,'披风与织物','Cape.03')
for idx in [0,NX]:line('披风侧边缝线'+str(idx),[verts[j*(NX+1)+idx] for j in range(NY+1)],.011,RED_DARK,'披风与织物','Chest')
line('披风领绳',[(-.41,.245,2.13),(-.22,.30,2.145),(0,.32,2.14),(.22,.30,2.145),(.41,.245,2.13)],.021,GOLD,'披风与织物','Chest')
uvball('披风背扣',(0,.344,2.14),(.065,.025,.065),GOLD,'披风与织物','Chest')

# 双冠翎：顶部窄拱，尾部较长；空间路径略有差异，后视根部保持间距。
PLUMES=[]
for s,side in [(-1,'R'),(1,'L')]:
    root=Vector((s*.115,.055,3.32))
    if s<0:
        controls=[root,(s*.23,-.05,3.86),(s*.43,.36,3.96),(s*.46,.63,3.66),(s*.52,.97,3.34),(s*.52,.76,2.79),(s*.54,.91,2.46),(s*.56,1.0,2.23),(s*.60,1.11,2.15),(s*.60,1.20,2.19)]
    else:
        controls=[root,(s*.20,-.08,3.78),(s*.37,.31,3.87),(s*.41,.59,3.63),(s*.47,.88,3.28),(s*.48,.78,2.81),(s*.53,.95,2.50),(s*.56,1.04,2.30),(s*.59,1.14,2.28),(s*.65,1.24,2.32)]
    path=bezier(controls,24)
    verts=[];faces=[];S=10
    for i,c in enumerate(path):
        t=i/(len(path)-1)
        tangent=(path[min(i+1,len(path)-1)]-path[max(0,i-1)]).normalized()
        # 厚度方向偏X，羽片展开方向留在弯曲平面；后侧可看到两条分开的细长羽带。
        normal=Vector((1,s*.20,.02));normal=(normal-tangent*normal.dot(tangent)).normalized()
        cross=tangent.cross(normal).normalized()
        w=(.032+.082*math.sin(math.pi*t)**.62)*(.90 if s>0 else 1)
        if t<.12:w*=.65+.35*t/.12
        w*=max(.015,min(1,(1-t)*14))
        for j in range(S):
            a=math.tau*j/S
            p=c+cross*(math.cos(a)*w)+normal*(math.sin(a)*.019*max(.15,1-t))
            verts.append(tuple(p))
    for i in range(len(path)-1):
        for j in range(S):faces.append((i*S+j,i*S+(j+1)%S,(i+1)*S+(j+1)%S,(i+1)*S+j))
    faces.extend([tuple(reversed(range(S))),tuple((len(path)-1)*S+j for j in range(S))])
    plume=mesh('冠翎.'+side,verts,faces,RED_LIGHT,'双冠翎',None,True)
    plume.data.materials.append(RED);plume.data.materials.append(RED_DARK)
    for p in plume.data.polygons:
        j=p.index%S;p.material_index=2 if j in [0,5] else 1 if j in [1,4,6,9] else 0
    plume['柔性部件']='Plume.'+side
    PLUMES.append((side,plume,path))
    rod('翎插座'+side,root-Vector((0,0,.055)),root+Vector((0,0,.038)),.045,GOLD,'发束与冠座','Head',r2=.036)

# 方天画戟为真正厚刃，含中央枪尖、双月牙刃、套筒、杆与尾鐏。
pole_a=Vector((.92,-.23,.07));pole_b=Vector((.92,-.51,3.18));axis=(pole_b-pole_a).normalized()
rod('乌铁戟杆',pole_a,pole_b,.031,IRON_DARK,'方天画戟','Weapon',segments=16)
rod('红缠握柄',pole_a.lerp(pole_b,.42),pole_a.lerp(pole_b,.55),.034,RED_DARK,'方天画戟','Weapon',segments=16)
for k,t in enumerate([.025,.08,.42,.55,.83,.87,.93]):
    c=pole_a.lerp(pole_b,t)
    rod('戟杆铜箍'+str(k),c-axis*.032,c+axis*.032,.047,GOLD,'方天画戟','Weapon',segments=12)
rod('尾鐏',pole_a-axis*.04,pole_a+axis*.07,.047,GOLD_DARK,'方天画戟','Weapon',r2=.035,segments=8)
rod('尾尖',pole_a-axis*.07,pole_a-axis*.01,.002,STEEL,'方天画戟','Weapon',r2=.044,segments=8)

# 戟头在局部XZ面建模；轻微倾斜跟随整根长杆，保持套筒连接。
origin=Vector((.92,-.495,3.00))
def wp(x,z,d=0):return tuple(origin+Vector((x,d-.09*z,z)))
def blade(name,outline):
    n=len(outline);verts=[wp(x,z,-.023) for x,z in outline]+[wp(x,z,.023) for x,z in outline]
    center=sum((Vector(v) for v in verts[:n]),Vector())/n;center.y-=.034;verts.append(tuple(center))
    back=center.copy();back.y+=.114;verts.append(tuple(back))
    faces=[]
    for i in range(n):
        j=(i+1)%n;faces.extend([(i,j,2*n),(n+j,n+i,2*n+1),(i,n+i,n+j,j)])
    obj=mesh(name,verts,faces,STEEL,'方天画戟','Weapon',False)
    line(name+'刃口',[wp(x,z,-.024) for x,z in outline],.008,EDGE,'方天画戟','Weapon',True)
    return obj
blade('中央菱形枪锋',[(-.068,-.02),(-.086,.22),(0,.65),(.086,.22),(.068,-.02),(0,-.065)])
for s in [-1,1]:
    blade('月牙戟刃'+str(s),[(s*.065,.05),(s*.18,.12),(s*.305,.47),(s*.29,.25),(s*.22,.045),(s*.28,-.19),(s*.12,-.08)])
    rod('戟刃连接榫'+str(s),wp(s*.035,.07),wp(s*.19,.075),.028,GOLD,'方天画戟','Weapon',segments=8)
rod('戟头铜套',wp(0,-.14),wp(0,.15),.065,GOLD_DARK,'方天画戟','Weapon',r2=.052,segments=12)
for z in [-.12,-.065,.12]:axis_ring('戟套箍'+str(z),wp(0,z),axis,.069,GOLD,'Weapon',.016,'方天画戟')
for s in [-1,1]:
    pts=[wp(s*.03,-.11),wp(s*.16,-.22),wp(s*.15,-.43),wp(s*.21,-.49)]
    points=bezier(pts,24)
    swept('戟缨飘带'+str(s),points,[.038,.061,.052,.003],[.015,.012,.008,.002],RED,'方天画戟','Weapon')

# 骨骼以当前自然站姿为静置姿态，硬甲随骨骼，披风和翎分段蒙皮。
armdata=bpy.data.armatures.new('LvBuSkeleton')
rig=bpy.data.objects.new('吕布骨架',armdata);COLLS['骨架'].objects.link(rig)
bpy.context.view_layer.objects.active=rig;rig.select_set(True)
bpy.ops.object.mode_set(mode='EDIT')
def bone(name,head,tail,parent=None):
    b=armdata.edit_bones.new(name);b.head=head;b.tail=tail
    if parent:b.parent=armdata.edit_bones[parent]
    return b
bone('Root',(0,0,0),(0,0,.25))
bone('Pelvis',(0,0,1.10),(0,0,1.40),'Root')
bone('Spine',(0,0,1.40),(0,0,1.75),'Pelvis')
bone('Chest',(0,0,1.75),(0,0,2.10),'Spine')
bone('Neck',(0,0,2.10),(0,0,2.30),'Chest')
bone('Head',(0,0,2.30),(0,0,3.35),'Neck')
for side,(sh,el,wr) in JOINTS.items():
    bone('Clavicle.'+side,(0,0,2.05),sh,'Chest')
    bone('UpperArm.'+side,sh,el,'Clavicle.'+side)
    bone('Forearm.'+side,el,wr,'UpperArm.'+side)
    bone('Hand.'+side,wr,wr+Vector((0,-.03,-.18)),'Forearm.'+side)
for side,(hip,knee,ankle) in LEGS.items():
    bone('Thigh.'+side,hip,knee,'Pelvis')
    bone('Shin.'+side,knee,ankle,'Thigh.'+side)
    bone('Foot.'+side,ankle,ankle+Vector((0,-.25,-.10)),'Shin.'+side)
bone('Weapon',(.92,-.35,1.44),pole_b,'Hand.L')
for i in range(3):
    bone('Cape.%02d'%(i+1),(0,.30+i*.10,2.10-i*.37),(0,.40+i*.10,1.73-i*.37),'Chest' if i==0 else 'Cape.%02d'%i)
for side,plume,path in PLUMES:
    for i in range(8):
        a=path[round((len(path)-1)*i/8)];b=path[round((len(path)-1)*(i+1)/8)]
        bone('Plume.'+side+'.%02d'%(i+1),a,b,'Head' if i==0 else 'Plume.'+side+'.%02d'%i)
bpy.ops.object.mode_set(mode='OBJECT')
rig.show_in_front=True;armdata.display_type='OCTAHEDRAL'
for obj,b in PARTS:
    # 使用顶点组和Armature变形，保持模型坐标与静置姿态一致。
    if obj.type=='CURVE':
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
        bpy.ops.object.convert(target='MESH')
    obj.parent=rig
    group=obj.vertex_groups.new(name=b);group.add(list(range(len(obj.data.vertices))),1,'REPLACE')
    mod=obj.modifiers.new('随骨架','ARMATURE');mod.object=rig

def soft_bind(obj,weights):
    obj.parent=rig
    groups={name:obj.vertex_groups.new(name=name) for name in weights[0]}
    for i,ws in enumerate(weights):
        for name,w in ws.items():
            if w>0:groups[name].add([i],w,'REPLACE')
    mod=obj.modifiers.new('连续柔性蒙皮','ARMATURE');mod.object=rig

def segment_weights(t,n,names):
    pos=max(0,min(n-1,t*n-.5));lo=int(pos);hi=min(n-1,lo+1);f=pos-lo
    ws={name:0 for name in names};ws[names[lo]]+=1-f;ws[names[hi]]+=f;return ws
soft_bind(cape,[segment_weights((v.co.z-2.12)/-1.16,3,['Cape.%02d'%k for k in range(1,4)]) for v in cape.data.vertices])
for side,plume,path in PLUMES:
    soft_bind(plume,[segment_weights((i//10)/(len(path)-1),8,['Plume.'+side+'.%02d'%k for k in range(1,9)]) for i in range(len(plume.data.vertices))])
rig['说明']='吕布参考造型三维母版。正面为-Y；当前为持戟自然站姿。甲片刚性绑定，双翎与披风分段权重。'
rig['制作边界']='造型与基础绑定；尚未制作完整战斗动作，未替换二维游戏运行时。'

# 灯光和相机独立分组，正交视角用于和原二维图比较。
scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=900;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
scene.render.film_transparent=False
scene.world.color=(.045,.045,.045)
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.16,.19,.20,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.32
scene.view_settings.view_transform='AgX'
scene.view_settings.exposure=.35
def area(name,loc,power,size,color):
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;data.color=color
    obj=bpy.data.objects.new(name,data);COLLS['展示环境'].objects.link(obj);obj.location=loc
    obj.rotation_euler=(Vector((0,0,1.8))-obj.location).to_track_quat('-Z','Y').to_euler()
area('主光 · 左前柔光',(-3.5,-5,7),650,4.0,(1,.86,.69))
area('补光 · 冷灰',(4,-2,4.5),420,3.0,(.66,.80,1))
area('轮廓光',(1.5,4,6),850,3.5,(1,.61,.35))
groundmat=material('展示地面',(.035,.047,.052),0,.95)
ground=cube('展示地面',(0,0,-.055),(200,200,.10),groundmat,'展示环境',None,0)
camera_data=bpy.data.cameras.new('正交预览相机');camera=bpy.data.objects.new('正交预览相机',camera_data)
COLLS['展示环境'].objects.link(camera);camera_data.type='ORTHO';camera_data.ortho_scale=4.8;camera_data.lens=50;scene.camera=camera
def set_camera(azimuth,elevation=12):
    az=math.radians(azimuth);el=math.radians(elevation)
    target=Vector((.14,.15,1.94))
    camera.location=target+Vector((math.sin(az)*9*math.cos(el),-math.cos(az)*9*math.cos(el),9*math.sin(el)))
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
set_camera(28)

# 原图以隐藏参考板保存于.blend，不参与模型渲染；用户可在视口打开对照。
for idx,(label,path) in enumerate([
    ('正面画风参考',ROOT/'Source/People/Portraits/LvBu/LvBuPixelReference.jpg'),
    ('右向最新参考',ROOT/'Build/LvBuRightV1/SlimCrestsIdle.png'),
    ('背面结构参考',ROOT/'Build/LvBuBackV1/PlumeProportionsIdle.png')]):
    obj=bpy.data.objects.new(label,None);obj.empty_display_type='IMAGE';obj.data=bpy.data.images.load(str(path),check_existing=True);obj.data.pack()
    obj.empty_display_size=3.0;obj.location=(5+idx*3.5,1,2);obj.rotation_euler=(math.pi/2,0,0)
    COLLS['造型参考'].objects.link(obj);obj.hide_render=True
COLLS['造型参考'].hide_viewport=True

# 打开文件后直接看到角色，地面灯光不妨碍编辑。
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig
for screen in bpy.data.screens:
    for a in screen.areas:
        if a.type=='VIEW_3D':
            a.spaces.active.region_3d.view_distance=6.5
            a.spaces.active.region_3d.view_location=(0,0,1.9)
            a.spaces.active.region_3d.view_rotation=camera.rotation_euler.to_quaternion()
            a.spaces.active.shading.type='MATERIAL'
            a.spaces.active.overlay.show_extras=False
scene['交付状态']='参考造型V1；真实网格和基础骨架，待用户检查；非四向动作成品。'
scene['参考说明']='正面沿用吕布最初像素参考；右向与后向沿用当前二维装配。冠翎、兽面肩甲和方天画戟为既有游戏化人物设计。'
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'LvBuModelV1.blend'))
report={'blender':bpy.app.version_string,'meshObjects':len([o for o in bpy.data.objects if o.type=='MESH' and o.parent==rig]),'bones':len(rig.data.bones),'vertices':sum(len(o.data.vertices) for o in rig.children if o.type=='MESH'),'boundObjects':len(rig.children),'source':'LvBuModelV1.blend','front':'-Y','status':'造型及基础绑定，未制作完整动作'}
(OUT/'ModelAudit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('MODEL_SAVED',json.dumps(report,ensure_ascii=False),flush=True)
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
views=[('ThreeQuarter',28),('Front',0),('Right',-90),('Back',180)]
if '--draft' in args:
    scene.render.resolution_percentage=65;views=views[:1]
for name,az in views:
    set_camera(az)
    scene.render.filepath=str(OUT/(name+'.png'))
    bpy.ops.render.render(write_still=True)
    print('RENDER_SAVED',name,flush=True)
set_camera(28)
scene.render.resolution_percentage=100
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'LvBuModelV1.blend'))
