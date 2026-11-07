"""保留最高精度网格，创建骨骼、材质和接地动画，导出 Godot 可用 GLB。"""
import bpy, math, json, numpy as np
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion
HERE=Path(__file__).resolve().parent;ASSET=HERE.parent;ROOT=HERE.parents[5-1]
OUT=ROOT/'Build/LvBuHybridV1'
definition=json.loads((HERE/'RigDefinition.json').read_text(encoding='utf-8'));bones=definition['bones']
bpy.ops.wm.open_mainfile(filepath=str(OUT/'Inspect.blend'))
obj=next(o for o in bpy.context.scene.objects if o.type=='MESH');obj.name='LvBuHunyuanUltra'
data=np.load(OUT/'Binding.npz');uv=data['uv'];cat=data['category'];indices=data['indices'];weights=data['weights']
mesh=obj.data
loopverts=np.empty(len(mesh.loops),np.int32);mesh.loops.foreach_get('vertex_index',loopverts)
uvlayer=mesh.uv_layers.new(name='ReferenceProjection');uvlayer.data.foreach_set('uv',uv[loopverts].reshape(-1))
image=bpy.data.images.load(str(ASSET/'LvBuAlbedo.png'),check_existing=True);image.pack()
mesh.materials.clear()
for name,metal,rough in [('BlackIron',.35,.64),('OldGold',.5,.48),('DarkRedCloth',0,.9),('Skin',0,.85),('Hair',0,.8),('Steel',.65,.43)]:
    mat=bpy.data.materials.new(name);mat.use_nodes=True
    mat.node_tree.nodes.clear()
    shader=mat.node_tree.nodes.new('ShaderNodeBsdfPrincipled');shader.inputs['Metallic'].default_value=metal;shader.inputs['Roughness'].default_value=rough
    output=mat.node_tree.nodes.new('ShaderNodeOutputMaterial');mat.node_tree.links.new(shader.outputs['BSDF'],output.inputs['Surface'])
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation='Linear'
    mat.node_tree.links.new(tex.outputs['Color'],shader.inputs['Base Color']);mesh.materials.append(mat)
facecat=cat[loopverts.reshape(-1,3)[:,0]];mesh.polygons.foreach_set('material_index',facecat)
arm=bpy.data.armatures.new('LvBuSkeleton');rig=bpy.data.objects.new('LvBuRig',arm);bpy.context.scene.collection.objects.link(rig)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT')
for b in bones:
    e=arm.edit_bones.new(b['name']);e.head=b['head'];e.tail=b['tail'];e.use_deform=True
    if b['parent']:e.parent=arm.edit_bones[b['parent']]
bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True
q=np.floor(weights*255).astype(np.int32);q[np.arange(len(q)),np.argmax(weights,1)]+=255-q.sum(1)
for i,b in enumerate(bones):
    group=obj.vertex_groups.new(name=b['name'])
    vi,slot=np.where(indices==i);vals=q[vi,slot]
    for value in np.unique(vals):
        if value>0:group.add(vi[vals==value].tolist(),float(value)/255,'REPLACE')
mod=obj.modifiers.new('SkeletonSkin','ARMATURE');mod.object=rig;obj.parent=rig
mesh.update();print('BINDING_COMPLETE',len(mesh.vertices),len(bones),flush=True)
rest={b.name:b.matrix_local.copy() for b in arm.bones};parents={b['name']:b['parent'] for b in bones}
rel={name:(rest[p].inverted()@m if (p:=parents[name]) else m.copy()) for name,m in rest.items()}
forward=Vector((.85,-.527,0)).normalized();right=Vector((.527,.85,0)).normalized()
def local_rot(name,axis,angle):return Quaternion(rest[name].to_quaternion().inverted()@Vector(axis),angle).to_matrix().to_4x4()
def from_direction(name,head,tail):
    old=arm.bones[name].tail_local-arm.bones[name].head_local
    rot=old.rotation_difference(tail-head)
    m=rot.to_matrix().to_4x4()@rest[name];m.translation=head;return m
def leg_ik(hip,ankle,la,lb):
    delta=ankle-hip;length=min(delta.length,la+lb-.0001);axis=delta.normalized()
    along=(la*la-lb*lb+length*length)/(2*length);height=math.sqrt(max(0,la*la-along*along))
    bend=(forward-axis*forward.dot(axis)).normalized()
    return hip+axis*along+bend*height
scene=bpy.context.scene;scene.render.fps=30
audit={'source_faces':1493934,'imported_faces':len(mesh.polygons),'removed_degenerate_faces':1493934-len(mesh.polygons),'vertices':len(mesh.vertices),'bones':len(bones),'texture':'LvBuAlbedo.png','texture_method':'原图投射及侧背材质补色，非混元 Paint','actions':{},'issues':[]}
for action_name,seconds in [('Idle',2.4),('Walk',1.2),('Attack',1.4)]:
    action=bpy.data.actions.new(action_name);rig.animation_data_create();rig.animation_data.action=action
    count=round(seconds*30);scene.frame_start=0;scene.frame_end=count
    for frame in range(count+1):
        t=frame/count;phase=2*math.pi*t;rot={};translations={}
        if action_name=='Idle':
            rot['Chest']=local_rot('Chest',right,.018*math.sin(phase));rot['Head']=local_rot('Head',(0,0,1),.015*math.sin(phase))
        elif action_name=='Walk':
            translations['Pelvis']=Vector((0,0,.008*(1-math.cos(phase*2))))
            rot['Pelvis']=local_rot('Pelvis',(0,0,1),.035*math.sin(phase))
            rot['Chest']=local_rot('Chest',(0,0,1),-.04*math.sin(phase))
            rot['UpperArmN']=local_rot('UpperArmN',right,.19*math.sin(phase))
            rot['UpperArmF']=local_rot('UpperArmF',right,-.045*math.sin(phase))
        else:
            # 起势、蓄力、挥出与收势连续；兵器沿手骨继承，保持握柄接触。
            times=[0,.18,.34,.49,.68,1];values=[0,-.45,-.55,1,.38,0]
            k=max(0,min(4,next((i-1 for i,x in enumerate(times) if x>t),4)))
            a=(t-times[k])/(times[k+1]-times[k]);a=max(0,min(1,a));a=a*a*(3-2*a);sweep=values[k]*(1-a)+values[k+1]*a
            rot['Chest']=local_rot('Chest',(0,0,1),-.2*sweep)
            rot['UpperArmF']=local_rot('UpperArmF',right,.35*sweep);rot['ForearmF']=local_rot('ForearmF',right,.55*sweep)
            rot['UpperArmN']=local_rot('UpperArmN',right,-.25*max(0,sweep));rot['ForearmN']=local_rot('ForearmN',right,-.2*max(0,sweep))
            translations['Pelvis']=Vector((0,0,-.018*math.sin(math.pi*t)**2))
        # 空手侧略向前抬臂并弯肘；作为所有动作的共同基础姿势，切换动作时不会落回旧位置。
        for name,angle in [('UpperArmN',-.12),('ForearmN',-.32)]:
            rot[name]=local_rot(name,right,angle)@rot.get(name,Matrix.Identity(4))
        for prefix in ['Cape','PlumeN','PlumeF']:
            for name in rest:
                if name.startswith(prefix):
                    j=int(name[-1]);amp=(.006+j*.008)*(1.7 if action_name!='Idle' else 1)
                    rot[name]=local_rot(name,forward,amp*(math.sin(phase-j*.7)-math.sin(-j*.7)))
        world={}
        for b in bones:
            name=b['name'];p=b['parent'];basis=rot.get(name,Matrix.Identity(4))
            if name in translations:
                basis.translation=rest[name].to_quaternion().inverted()@translations[name]
            world[name]=(world[p] if p else Matrix.Identity(4))@rel[name]@basis
        for suffix,offset in [('N',0),('F',.5)]:
            th,sh,ft=['Thigh'+suffix,'Shin'+suffix,'Foot'+suffix]
            hip=world[th].translation.copy();ankle=rest[ft].translation.copy()
            if action_name=='Walk':
                cycle=(t+offset)%1
                if cycle<.6:travel=.09-.18*cycle/.6;lift=0
                else:
                    u=(cycle-.6)/.4;travel=-.09+.18*(u*u*(3-2*u));lift=.055*math.sin(math.pi*u)
                ankle+=forward*travel+Vector((0,0,lift))
            knee=leg_ik(hip,ankle,arm.bones[th].length,arm.bones[sh].length)
            world[th]=from_direction(th,hip,knee);world[sh]=from_direction(sh,knee,ankle)
            world[ft]=rest[ft].copy();world[ft].translation=ankle
        for b in bones:
            name=b['name'];p=b['parent'];pb=rig.pose.bones[name]
            pb.rotation_mode='QUATERNION'
            basis=rel[name].inverted()@(world[p].inverted() if p else Matrix.Identity(4))@world[name]
            pb.matrix_basis=basis
            pb.keyframe_insert('location',frame=frame,group=name);pb.keyframe_insert('rotation_quaternion',frame=frame,group=name);pb.keyframe_insert('scale',frame=frame,group=name)
    action.use_fake_user=True
    audit['actions'][action_name]={'seconds':seconds,'frames':count+1}
    print('ACTION_COMPLETE',action_name,flush=True)
rig.animation_data.action=bpy.data.actions['Idle'];scene.frame_start=0;scene.frame_end=72;scene.frame_set(0)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.65
for o in scene.objects:
    if o.type=='LIGHT':o.data.energy*=.65
scene.view_settings.view_transform='AgX'
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'LvBuHunyuanRig.blend'))
for name,direction in [('Front',(0,-4,0)),('Side',(4,0,0)),('Back',(0,4,0))]:
    center=Vector((-.005,0,-.002));cam=scene.camera;cam.location=center+Vector(direction);cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(OUT/f'Textured{name}.png');bpy.ops.render.render(write_still=True)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);obj.select_set(True);bpy.context.view_layer.objects.active=rig
kwargs=dict(filepath=str(ASSET/'LvBuHunyuanRig.glb'),export_format='GLB',use_selection=True,export_animations=True,export_skins=True,export_yup=True,export_cameras=False,export_lights=False)
props=bpy.ops.export_scene.gltf.get_rna_type().properties
if 'export_animation_mode' in props:kwargs['export_animation_mode']='ACTIONS'
if 'export_force_sampling' in props:kwargs['export_force_sampling']=True
if 'export_bake_animation' in props:kwargs['export_bake_animation']=True
bpy.ops.export_scene.gltf(**kwargs)
(OUT/'RigAudit.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2),encoding='utf-8')
print('EXPORT_COMPLETE',str(ASSET/'LvBuHunyuanRig.glb'),flush=True)
