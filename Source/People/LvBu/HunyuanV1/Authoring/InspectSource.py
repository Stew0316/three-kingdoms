import bpy, json, math
from pathlib import Path
from mathutils import Vector

ROOT=Path(r'G:\GitProgram\three-kingdoms')
OUT=ROOT/'Build/LvBuHybridV1'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r'G:\3D\Outputs\LvBuUltra_20261104_203934\Model.glb')
meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in meshes:
    bpy.context.view_layer.objects.active=o
    o.select_set(True)
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    for p in o.data.polygons:p.use_smooth=True
    print('MESH',o.name,len(o.data.vertices),len(o.data.polygons),'BOUNDS',list(o.dimensions), flush=True)
verts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
lo=Vector([min(v[i] for v in verts) for i in range(3)])
hi=Vector([max(v[i] for v in verts) for i in range(3)])
center=(lo+hi)*.5
print('BOUNDS',list(lo),list(hi),flush=True)
(OUT/'Bounds.json').write_text(json.dumps({'min':list(lo),'max':list(hi)}))
mat=bpy.data.materials.new('InspectClay');mat.diffuse_color=(.45,.48,.51,1)
for o in meshes:o.data.materials.clear();o.data.materials.append(mat)
scene=bpy.context.scene
scene.render.engine='BLENDER_EEVEE'
scene.render.resolution_x=800;scene.render.resolution_y=900;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.world=bpy.data.worlds.new('World');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.18,.21,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.4
for name,pos,energy,size in [('Key',(-3,-4,5),550,3),('Fill',(3,-2,2),220,3),('Rim',(0,3,3),400,2)]:
    d=bpy.data.lights.new(name,'AREA');d.energy=energy;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=pos
    o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
camd=bpy.data.cameras.new('Camera');cam=bpy.data.objects.new('Camera',camd);scene.collection.objects.link(cam);scene.camera=cam
camd.type='ORTHO';camd.ortho_scale=(hi.z-lo.z)*1.15
for name,direction in [('Front',(0,-4,0)),('Side',(4,0,0)),('Back',(0,4,0)),('Other',(-4,0,0))]:
    cam.location=center+Vector(direction);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(OUT/f'Clay{name}.png');bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Inspect.blend'))
