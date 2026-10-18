import sys,os,collections,json,struct
sys.path.insert(0,'/Users/sang/GitHub/GuilRandomDefense/Tools/w3x');sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import w3map,render_map as R
from PIL import Image,ImageDraw
BASE=R.BASE
PLAY={'old':(-9216,-10880,9728,7040),'s2':(-9472,-10752,9728,7168)}
def terrain2(tag,minimap=True):
    e=w3map.parse_w3e(BASE+tag+'/war3map.w3e');w,h=e['w'],e['h'];pts=e['pts']
    mm=Image.open(BASE+tag+'/war3mapMap.blp').convert('RGB')
    x0,y0,x1,y1=PLAY[tag]
    def mmcol(x,y):
        u=(x-x0)/(x1-x0);v=(y1-y)/(y1-y0)
        if u<0 or u>=1 or v<0 or v>=1:return None
        return mm.getpixel((int(u*256),int(v*256)))
    # 질감별 미니맵 평균색(물 아닌 칸)
    acc=collections.defaultdict(list);wacc=[]
    info={}
    for iy in range(h-1):
        for ix in range(w-1):
            hh,wf,tex,var,misc=pts[iy*w+ix];layer=misc&0x0F
            gh=(hh-8192)/4.0+(layer-2)*128;wl=(wf&0x3FFF-8192)/4.0-89.6 if False else (((wf&0x3FFF)-8192)/4.0-89.6)
            dep=wl-gh;water=(tex&0x40)!=0 and dep>40
            info[(ix,iy)]=(gh,dep,water,tex&0x0F)
            x=e['ox']+ix*128+64;y=e['oy']+iy*128+64
            c=mmcol(x,y)
            if c is None:continue
            (wacc if water else acc[tex&0x0F]).append(c)
    def top(lst):
        lst=sorted(lst,key=lambda c:sum(c));lst=lst[len(lst)//2:]
        return tuple(sorted(c[i] for c in lst)[len(lst)//2] for i in range(3))
    pal={k:top(v) for k,v in acc.items() if v}
    wcol=top(wacc) if wacc else (20,20,60)
    img=Image.new('RGB',(R.W,R.H),wcol or (20,20,60));dr=ImageDraw.Draw(img)
    for (ix,iy),(gh,dep,water,tid) in info.items():
        col=pal.get(tid,(120,120,120))
        k=max(0.7,min(1.2,1.0+gh/1800.0));col=tuple(int(max(0,min(255,c*k))) for c in col)
        if water:
            a=max(0.5,min(1.0,dep/120.0));col=tuple(int(c*(1-a)+wc*a) for c,wc in zip(col,wcol))
        x=e['ox']+ix*128;y=e['oy']+iy*128
        a0=R.P(x,y+128);a1=R.P(x+128,y)
        dr.rectangle([a0[0],a0[1],a1[0],a1[1]],fill=col)
    return img,e,pal,wcol
if __name__=='__main__':
    tag=sys.argv[1]
    img,e,pal,wcol=terrain2(tag)
    img.save(BASE+tag+'_terrain.png')
    print(tag,'palette',{e['ground'][k]:v for k,v in pal.items() if k<len(e['ground'])},'water',wcol)
