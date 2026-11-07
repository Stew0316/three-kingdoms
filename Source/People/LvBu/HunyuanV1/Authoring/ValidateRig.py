"""重新打开交付的 Blend，实算蒙皮与动画接地、循环、兵器刚性和有限坐标。"""
import bpy,json,math,numpy as np
from pathlib import Path
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[4];OUT=ROOT/'Build/LvBuHybridV1'
bpy.ops.wm.open_mainfile(filepath=str(HERE/'LvBuHunyuanRig.blend'))
obj=bpy.data.objects['LvBuHunyuanUltra'];rig=bpy.data.objects['LvBuRig'];scene=bpy.context.scene
def posed():
    bpy.context.view_layer.update();e=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());m=e.to_mesh()
    a=np.empty(len(m.vertices)*3,np.float32);m.vertices.foreach_get('co',a);e.to_mesh_clear();return a.reshape(-1,3)
groups={name:obj.vertex_groups[name].index for name in ['FootN','FootF','Weapon']}
chosen={name:[] for name in groups};max_error=0
for vert in obj.data.vertices:
    max_error=max(max_error,abs(sum(g.weight for g in vert.groups)-1))
    for group in vert.groups:
        if group.weight>.999:
            for name,idx in groups.items():
                if group.group==idx:chosen[name].append(vert.index)
chosen={k:np.array(v,dtype=np.int32) for k,v in chosen.items()}
base=np.empty(len(obj.data.vertices)*3,np.float32);obj.data.vertices.foreach_get('co',base);base=base.reshape(-1,3)
weapon=chosen['Weapon'][::max(1,len(chosen['Weapon'])//30)][:30]
restlength=np.linalg.norm(base[weapon]-base[weapon[0]],axis=1)
report={'reopened':True,'vertices':len(base),'bones':len(rig.data.bones),'max_weight_sum_error':max_error,'actions':{},'issues':[]}
for name,seconds in [('Idle',2.4),('Walk',1.2),('Attack',1.4)]:
    rig.animation_data.action=bpy.data.actions[name];count=round(seconds*30);ground=0;rigidity=0;finite=True;first=None;last=None
    min_z=100;max_extent=0
    for frame in range(0,count+1,3):
        scene.frame_set(frame);a=posed();finite=finite and bool(np.isfinite(a).all())
        if first is None:first=a.copy()
        last=a
        min_z=min(min_z,float(a[:,2].min()));max_extent=max(max_extent,float(np.max(np.ptp(a,axis=0))))
        rigidity=max(rigidity,float(np.max(abs(np.linalg.norm(a[weapon]-a[weapon[0]],axis=1)-restlength))))
        for suffix,phase in [('N',0),('F',.5)]:
            if name=='Walk' and ((frame/count+phase)%1)>=.6:continue
            ids=chosen['Foot'+suffix];ground=max(ground,abs(float(a[ids,2].min()-base[ids,2].min())))
    loop_error=float(np.linalg.norm(first-last,axis=1).max()) if name!='Attack' else None
    report['actions'][name]={'samples':count//3+1,'finite':finite,'max_stance_ground_error':ground,'max_weapon_length_error':rigidity,'max_extent':max_extent,'loop_seam_error':loop_error,'lowest_vertex':min_z}
    if not finite or ground>.003 or rigidity>.003 or (loop_error is not None and loop_error>.003):report['issues'].append(name+'校验超差')
if max_error>.001:report['issues'].append('权重未归一')
(OUT/'BindingValidation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps(report,ensure_ascii=False),flush=True)
if report['issues']:raise RuntimeError('绑定检查失败')
