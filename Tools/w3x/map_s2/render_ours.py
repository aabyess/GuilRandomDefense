import pickle,os,sys
sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
from PIL import Image,ImageDraw
import render_map as R
S=4.167
go,tf=pickle.load(open('scene_tf.pkl','rb'))
byg={v[0]:(k,v) for k,v in tf.items()}
img=Image.new('RGB',(R.W,R.H),(20,40,100));dr=ImageDraw.Draw(img);f=R.font(11)
items=[]
for g,n in go.items():
    if g not in byg:continue
    k,(gg,p,s,par)=byg[g]
    if tf.get(par,(0,))[0] and go.get(tf[par][0])!='Map' and par!='0':continue
    if s[1]<=1.0 and s[0]>=25 and s[2]>=25 and abs(p[1]-7.8)<0.5:
        if n.startswith('"'):
            n=n.strip('"').encode('ascii').decode('unicode_escape')
        items.append((n,p[0]*S,p[2]*S,s[0]*S,s[2]*S))
col={'Lane':(190,170,110),'Warehouse':(90,190,190),'Story':(150,90,60),'Gacha':(90,160,90),'Combine':(200,90,60),'Punk':(160,60,160),'Seal':(220,220,100),'Display':(100,100,200)}
for n,x,y,w,h in items:
    c=(120,160,120)
    for k,v in col.items():
        if k in n:c=v
    a=R.P(x-w/2,y+h/2);b=R.P(x+w/2,y-h/2)
    dr.rectangle([a[0],a[1],b[0],b[1]],fill=c,outline=(255,255,255))
    if (w>1000 or h>1000) and not n.startswith(('Portal','흔함선택')):
        dr.text((a[0]+2,a[1]+1),n,fill=(0,0,0),font=f)
img.save(R.BASE+'ours_map.png')
import json
json.dump(items,open(R.BASE+'ours_islands.json','w'),ensure_ascii=False,indent=0)
print(len(items),img.size)
for it in sorted(items):print(it[0],[round(v) for v in it[1:]])
