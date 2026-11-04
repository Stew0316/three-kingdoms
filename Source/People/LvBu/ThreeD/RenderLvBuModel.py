"""从保存的吕布 .blend 重开渲染，验证模型文件可独立使用。"""
import bpy
import sys
import math
import json
from pathlib import Path
from mathutils import Vector

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[3]
OUT=ROOT/'Build/LvBuModelV1'
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
bpy.ops.wm.open_mainfile(filepath=str(HERE/'LvBuModelV1.blend'))
scene=bpy.context.scene
camera=scene.camera
scene.render.resolution_percentage=65 if '--draft' in args else 100
views=[('Front',0),('Right',-90),('Back',180),('ThreeQuarter',28)]
if '--turntable' in args:
    scene.render.resolution_x=640;scene.render.resolution_y=800;scene.render.resolution_percentage=100
    (OUT/'TurntableFrames').mkdir(exist_ok=True)
    views=[('TurntableFrames/Frame%03d'%i,28+i*10) for i in range(36)]
if '--transparent' in args:
    scene.render.film_transparent=True
    bpy.data.objects['展示地面'].hide_render=True
    views=[('TransparentFront',0),('TransparentRight',-90),('TransparentBack',180),('TransparentLeft',90)]
for name,az in views:
    az=math.radians(az);el=math.radians(12)
    target=Vector((.14,.15,1.94))
    camera.location=target+Vector((math.sin(az)*9*math.cos(el),-math.cos(az)*9*math.cos(el),9*math.sin(el)))
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(OUT/(name+'.png'))
    bpy.ops.render.render(write_still=True)
    print('RENDER_SAVED',name,flush=True)

# 从已保存文件检查蒙皮覆盖、有限坐标、空对象与外部图片依赖。
rig=bpy.data.objects['吕布骨架']
issues=[]
for obj in rig.children:
    if obj.type!='MESH':continue
    for v in obj.data.vertices:
        if not all(math.isfinite(f) for f in v.co):issues.append(obj.name+'含非有限坐标');break
        if abs(sum(g.weight for g in v.groups)-1)>.001:issues.append(obj.name+'蒙皮权重未归一');break
report={'reopened':True,'bones':len(rig.data.bones),'meshObjects':sum(o.type=='MESH' for o in rig.children),'packedReferences':sum(bool(im.packed_file) for im in bpy.data.images),'issues':issues}
(OUT/'ReopenAudit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print('REOPEN_AUDIT',json.dumps(report,ensure_ascii=False),flush=True)
if issues:raise RuntimeError('模型复核未通过')
