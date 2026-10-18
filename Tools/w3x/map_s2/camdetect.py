from PIL import Image
import glob,json,collections
files=sorted(glob.glob('mm/m_*.png'))
W,H=270,260
masks=[]
cnt=[[0]*W for _ in range(H)]
for f in files:
    im=Image.open(f).convert('RGB');px=im.load()
    m=set()
    for y in range(H):
        for x in range(W):
            r,g,b=px[x,y]
            if min(r,g,b)>=225:m.add((x,y));cnt[y][x]+=1
    masks.append(m)
static={(x,y) for y in range(H) for x in range(W) if cnt[y][x]>len(files)*0.35}
res=[]
for i,m in enumerate(masks):
    pts=[p for p in m if p not in static]
    # 가장 긴 가로 흰 선 두 개 찾기
    rows=collections.defaultdict(list)
    for x,y in pts:rows[y].append(x)
    best=[]
    for y,xs in rows.items():
        xs.sort();run=1;start=xs[0]
        for a,b in zip(xs,xs[1:]+[10**6]):
            if b==a+1:run+=1
            else:
                if run>=14:best.append((run,y,start,start+run-1))
                run=1;start=b
    # 같은 x범위를 공유하는 두 가로선(위·아래)
    cand=None
    for a in best:
        for b in best:
            if b[1]>a[1]+8 and abs(a[2]-b[2])<=3 and abs(a[3]-b[3])<=3:
                sc=a[0]+b[0]
                if cand is None or sc>cand[0]:cand=(sc,a[2],a[1],a[3],b[1])
    res.append((i,cand))
json.dump(res,open('cam_rects.json','w'))
ok=[r for r in res if r[1]]
print(len(files),'frames; rect found',len(ok))
print(ok[:5])
