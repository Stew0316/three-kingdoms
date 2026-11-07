"""从原图投射颜色并补侧背材质，按已校准解剖关节建立连续蒙皮数据。"""
from pathlib import Path
import json
import numpy as np
from PIL import Image
from scipy.interpolate import LinearNDInterpolator, NearestNDInterpolator
from scipy.ndimage import distance_transform_edt
from scipy.sparse import coo_matrix
from scipy.sparse.csgraph import connected_components

ROOT=Path(__file__).resolve().parents[5]
OUT=ROOT/'Build/LvBuHybridV1'
ASSET=Path(__file__).resolve().parent.parent
d=np.load(OUT/'Geometry.npz');v=d['vertices'];f=d['faces'];n=d['normals']
ref=ROOT/'Source/People/Portraits/LvBu/LvBuRightFacingV1.png'
im=np.array(Image.open(ref).convert('RGBA'));h,w=im.shape[:2]
# 透明区向外延展相邻颜色，避免 UV 边缘过滤产生黑缝；原参考图保持不变。
empty=im[:,:,3]<64
nearest=distance_transform_edt(empty,return_distances=False,return_indices=True)
filled=im[:,:,:3].copy();filled[empty]=im[nearest[0][empty],nearest[1][empty],:3]
atlas=np.zeros((2048,2048,3),np.uint8);atlas[:h,:w]=filled
palette=[(48,47,43),(164,112,43),(117,29,23),(175,113,68),(28,25,23),(119,127,132)]
rng=np.random.default_rng(741)
for i,col in enumerate(palette):
    yy,xx=np.mgrid[:256,:256]
    grain=rng.normal(0,2.5,(256,256))+2*np.sin(xx*.35)*np.sin(yy*.27)
    atlas[1536:1792,i*256:(i+1)*256]=np.clip(np.array(col)[None,None,:]+grain[:,:,None],0,255)
Image.fromarray(atlas).save(ASSET/'LvBuAlbedo.png')
# 对应点来自原图与正交白模；只映射材质，不改变三维轮廓。
pairs=[((400,450),(640,640)),((390,226),(660,270)),((403,284),(660,347)),((352,333),(608,437)),((435,353),(709,459)),((441,390),(711,513)),((283,428),(513,555)),((272,526),(470,728)),((295,621),(467,870)),((409,475),(694,642)),((411,580),(743,756)),((470,447),(779,639)),((503,521),(847,734)),((573,556),(915,771)),((291,744),(476,1071)),((286,820),(479,1197)),((409,745),(706,1063)),((435,820),(727,1150)),((610,425),(995,486)),((542,700),(860,1000)),((708,76),(1128,37)),((606,275),(926,302)),((759,299),(1196,369)),((760,400),(1200,580)),((514,791),(844,1116)),((360,220),(614,267)),((203,163),(382,205)),((43,343),(174,447)),((325,86),(603,135)),((150,700),(220,1040)),((210,580),(340,828)),((320,690),(543,1014)),((350,530),(590,723)),((450,650),(769,898))]
c=(v.min(0)+v.max(0))*.5;s=(v[:,2].max()-v[:,2].min())*1.15/900
pix=np.column_stack(((v[:,0]-c[0])/s+400,450-(v[:,2]-c[2])/s))
a=np.array([x[0] for x in pairs]);b=np.array([x[1] for x in pairs])
coord=LinearNDInterpolator(a,b)(pix);bad=~np.isfinite(coord[:,0]);coord[bad]=NearestNDInterpolator(a,b)(pix[bad])
coord=np.clip(coord,[0,0],[w-1,h-1])
color=filled[coord[:,1].astype(int),coord[:,0].astype(int)]/255
forward=v[:,0]*.85-v[:,1]*.527
plumes=((v[:,2]>.40)&(v[:,0]<.04)&((v[:,0]<-.16)|(v[:,2]>.66)))|((v[:,0]<-.55)&(v[:,2]>.08))
cape=(forward<-.27)&(v[:,2]<.12)&(v[:,2]>-.78)
# 肩背上段的披风更贴近身体；颜色区域沿挂点收拢，不能沿下摆的固定深度裁切。
# 仅扩大布料材质范围，沿用原披风蒙皮，避免改变肩甲和披风的运动连接。
cape_color=cape|((forward<np.interp(v[:,2],[-.78,-.20,.14],[-.27,-.27,-.18]))&(v[:,2]<.14)&(v[:,2]>-.78))
pole_x=.558+.315*v[:,2]
# 握点处暂时切断图连接，按上下戟体的连通分量识别兵器，防止误选投影重叠的鞋甲。
grip=(v[:,0]>.31)&(v[:,2]>-.37)&(v[:,2]<-.19)
edges=np.concatenate([f[:,:2],f[:,1:],f[:,[0,2]]]);edges=edges[~(grip[edges[:,0]]|grip[edges[:,1]])]
graph=coo_matrix((np.ones(len(edges),np.uint8),(edges[:,0],edges[:,1])),shape=(len(v),len(v))).tocsr()
_,components=connected_components(graph,directed=False)
weapon_components=[components[np.argmin(((v-np.array(p))**2).sum(1))] for p in [( .74,-.15,.75),(.30,-.08,-.78)]]
weapon=np.isin(components,weapon_components)|(grip&(abs(v[:,0]-pole_x)<.07)&(abs(v[:,1]-(-.122-.04*v[:,2]))<.06))
# 侧背采用与原图一致的黑发、暗红布和黑金甲，避免把正脸投到后脑。
back=(forward<-.19)&(~weapon)&(~plumes)
backhead=back&(v[:,2]>.14)&(v[:,2]<.65)
category=np.zeros(len(v),np.int16)
r,g,bb=color.T
category[(r>g*1.4)&(g>bb*1.25)&(r>.30)]=1
category[(r>g*1.9)&(r>bb*1.65)&(g<.30)]=2
category[(r>.40)&(g>.24)&(bb>.11)&(r/np.maximum(g,.001)<1.9)&(g/np.maximum(bb,.001)<2.7)]=3
category[(r<.18)&(g<.18)&(bb<.18)]=4
category[weapon&(v[:,2]>.35)&(abs(r-g)<.13)]=5
category[cape_color|plumes]=2
category[backhead]=4
category[back&(~cape_color)&(~backhead)&(v[:,2]<.14)]=0
use_palette=cape_color|plumes|back
base=np.array([[i*256+128,1664] for i in range(6)])
detail=np.column_stack((np.mod(v[:,0]*250+v[:,1]*170,180)-90,np.mod(v[:,2]*350,180)-90))
coord[use_palette]=base[category[use_palette]]+detail[use_palette]
uv=np.column_stack((coord[:,0]/2048,1-coord[:,1]/2048)).astype(np.float32)

bones=[]
def bone(name,head,tail,parent=None):bones.append({'name':name,'head':head,'tail':tail,'parent':parent})
bone('Root',(-.11,-.04,-.998),(-.11,-.04,-.8))
bone('Pelvis',(-.11,-.04,-.48),(-.11,-.04,-.29),'Root')
bone('Spine',(-.11,-.04,-.29),(-.11,-.04,-.04),'Pelvis')
bone('Chest',(-.11,-.04,-.04),(-.10,-.04,.13),'Spine')
bone('Neck',(-.10,-.04,.13),(-.075,-.10,.20),'Chest')
bone('Head',(-.075,-.10,.20),(-.09,-.10,.46),'Neck')
for suf,shoulder,elbow,wrist,tip in [('N',(-.29,-.30,-.015),(-.32,-.36,-.23),(-.285,-.40,-.405),(-.27,-.40,-.49)),('F',(.03,.17,.015),(.21,.12,-.19),(.405,-.09,-.27),(.47,-.10,-.28))]:
    bone('UpperArm'+suf,shoulder,elbow,'Chest');bone('Forearm'+suf,elbow,wrist,'UpperArm'+suf);bone('Hand'+suf,wrist,tip,'Forearm'+suf)
bone('Weapon',(.47,-.10,-.28),(.60,-.18,.20),'HandF')
for suf,hip,knee,ankle,toe in [('N',(-.17,-.22,-.48),(-.25,-.26,-.72),(-.30,-.28,-.90),(-.23,-.34,-.96)),('F',(-.045,.14,-.48),(.015,.12,-.72),(.03,.14,-.90),(.14,.07,-.96))]:
    bone('Thigh'+suf,hip,knee,'Pelvis');bone('Shin'+suf,knee,ankle,'Thigh'+suf);bone('Foot'+suf,ankle,toe,'Shin'+suf)
for prefix,chain,parent in [('Cape',[(-.28,.09,.025),(-.38,.18,-.20),(-.45,.28,-.43),(-.40,.35,-.68)],'Chest'),('PlumeN',[(-.05,-.08,.43),(-.24,-.12,.64),(-.47,-.14,.70),(-.72,-.16,.53),(-.90,-.16,.29)],'Head'),('PlumeF',[(-.04,.05,.43),(-.16,.12,.76),(-.37,.14,.88),(-.53,.12,.64)],'Head')]:
    for j in range(len(chain)-1):bone(prefix+str(j),chain[j],chain[j+1],parent if j==0 else prefix+str(j-1))
def segdist(p,a,b):
    a=np.array(a);b=np.array(b);ab=b-a;t=np.clip(((p-a)@ab)/(ab@ab),0,1)
    return np.sqrt(((p-a-t[:,None]*ab)**2).sum(1))
dist=np.stack([segdist(v,b['head'],b['tail']) for b in bones],1)
dist[:,0]=100
ids={b['name']:i for i,b in enumerate(bones)}
# 空手贴近腰甲，单纯取最近骨骼会把裙甲绑到拳头上；按前臂外形限定低处影响范围。
# 肩部保留原有连续过渡，拳头下方和内侧腰甲交回躯干、骨盆与腿骨。
near_arm=(v[:,2]>-.535)&(v[:,0]<-.18)&(v[:,1]<np.interp(v[:,2],[-.535,-.35,-.13],[-.395,-.365,-.27]))
not_near_arm=(v[:,2]<-.13)&(~near_arm)
for name in ['UpperArmN','ForearmN','HandN']:dist[not_near_arm,ids[name]]=100
special=np.array([i for i,b in enumerate(bones) if b['name'].startswith(('Cape','Plume')) or b['name']=='Weapon'])
dist[:,special]=100
head=(v[:,2]>.16)&(~plumes)&(~weapon)
for i,b in enumerate(bones):
    if b['name'] not in ['Head','Neck']:dist[head,i]=100
for mask,prefix in [(cape,'Cape'),(plumes,'Plume'),(weapon,'Weapon')]:
    dist[mask,:]=100
    for i,b in enumerate(bones):
        if b['name'].startswith(prefix):dist[mask,i]=segdist(v[mask],b['head'],b['tail'])
# 脚掌保持刚性；躯干和大腿交界保留连续权重，避免转动时裂开。
for suffix in ['N','F']:
    foot=ids['Foot'+suffix];opposite=ids['Foot'+('F' if suffix=='N' else 'N')]
    mask=(v[:,2]<-.87)&(~weapon)&(dist[:,foot]<dist[:,opposite])
    dist[mask,:]=100;dist[mask,foot]=.001
idx=np.argpartition(dist,4,axis=1)[:,:4]
best=np.take_along_axis(dist,idx,axis=1)
weights=np.exp(-((best-best.min(1)[:,None])/.047)**2)
weights[best>10]=0;weights/=weights.sum(1)[:,None]
np.savez(OUT/'Binding.npz',uv=uv,category=category,indices=idx.astype(np.int32),weights=weights.astype(np.float32))
(ASSET/'Authoring/RigDefinition.json').write_text(json.dumps({'bones':bones,'source':str(ref),'forward':[.85,-.527,0]},ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'vertices':len(v),'triangles':len(f),'bones':len(bones),'cape_vertices':int(cape.sum()),'cape_color_vertices':int(cape_color.sum()),'plume_vertices':int(plumes.sum()),'weapon_vertices':int(weapon.sum()),'weight_sum_max_error':float(abs(weights.sum(1)-1).max())}),flush=True)
