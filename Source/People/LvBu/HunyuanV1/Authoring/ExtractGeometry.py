import bpy, numpy as np, json
from pathlib import Path
P=Path(r'G:\GitProgram\three-kingdoms\Build\LvBuHybridV1')
bpy.ops.wm.open_mainfile(filepath=str(P/'Inspect.blend'))
o=next(o for o in bpy.context.scene.objects if o.type=='MESH')
v=np.empty(len(o.data.vertices)*3,np.float32);o.data.vertices.foreach_get('co',v);v=v.reshape(-1,3)
f=np.empty(len(o.data.loops),np.int32);o.data.loops.foreach_get('vertex_index',f);f=f.reshape(-1,3)
n=np.empty(len(o.data.vertices)*3,np.float32);o.data.vertices.foreach_get('normal',n);n=n.reshape(-1,3)
np.savez(P/'Geometry.npz',vertices=v,faces=f,normals=n)
points={'head':(410,345),'neck':(417,400),'shoulder_near':(289,449),'elbow_near':(277,535),'wrist_near':(295,605),'shoulder_far':(459,450),'elbow_far':(501,524),'wrist_far':(559,552),'hip_near':(348,640),'hip_far':(423,640),'knee_near':(298,741),'knee_far':(409,742),'ankle_near':(283,797),'ankle_far':(414,797),'chest':(390,490),'pelvis':(390,610)}
c=(v.min(0)+v.max(0))*.5;s=(v[:,2].max()-v[:,2].min())*1.15/900
out={}
for name,(px,py) in points.items():
    x=c[0]+(px-400)*s;z=c[2]+(450-py)*s
    selected=v[(abs(v[:,0]-x)<.025)&(abs(v[:,2]-z)<.025)]
    out[name]=[float(x),[round(float(p),4) for p in np.percentile(selected[:,1],[0,10,25,50,75,90,100])],float(z)]
print(json.dumps(out,indent=2),flush=True)
(P/'JointSamples.json').write_text(json.dumps(out,indent=2))
