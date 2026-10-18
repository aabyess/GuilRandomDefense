import sys,os,json,collections
sys.path.insert(0,'/Users/sang/GitHub/GuilRandomDefense/Tools/w3x');sys.path.insert(0,os.path.dirname(os.path.abspath(__file__)))
import w3map,render_map as R,render2
from PIL import Image,ImageDraw
b=R.BASE
img=Image.open(b+'s2_terrain.png').convert('RGB');dr=ImageDraw.Draw(img);f=R.font(14);f2=R.font(11)
rs=json.load(open(b+'s2_info.json'))['rects']
def rect(i):return rs[i]
Z=[('레인1',53,(255,80,80)),('레인2',60,(255,80,80)),('레인3',67,(255,80,80)),('레인4',74,(255,80,80)),
   ('중앙 허브',112,(255,200,0)),('상점·조합 구역',40,(0,255,255)),('창고4(추정)',92,(120,255,120)),('',121,(120,255,120)),('',122,(120,255,120)),('',123,(120,255,120)),
   ('봉인섬1',9,(255,0,255)),('봉인섬2',10,(255,0,255)),('봉인섬3',11,(255,0,255)),('봉인섬4',12,(255,0,255)),('#0 칸',0,(255,0,255)),
   ('스토리 입구(#102)',102,(255,160,0)),('플레이영역',75,(255,255,255))]
for nm,i,c in Z:
    n,x0,y0,x1,y1=rs[i];a=R.P(x0,y1);bb=R.P(x1,y0)
    dr.rectangle([a[0],a[1],bb[0],bb[1]],outline=c,width=2)
    if nm:dr.text((a[0]+3,a[1]+2),nm,fill=c,font=f)
for lab,i in (('상점열1',104),('상점열2',105),('상점열3',106),('상점열4',107)):
    n,x0,y0,x1,y1=rs[i];xa=x0;ya=y0
    a=R.P(xa,ya);bb=R.P(xa+40,ya-1800)
    dr.rectangle([a[0]-3,a[1],bb[0]+3,bb[1]],outline=(80,255,80),width=2);dr.text((a[0]+6,a[1]-14),lab,fill=(80,255,80),font=f2)
for lab,(x0,y0,x1,y1),c in (('오로성 3기(중앙 허브)',(-1184,1888,-1120,1952),(255,120,255)),('퀘스트 NPC 3(노란원숭이·붉은개·푸른꿩)',(6624,-928,6784,-608),(255,255,0)),('퀘스트 NPC 후샤',(-8864,1888,-8640,2144),(255,255,0)),('스토리 몬스터 소환점',(-6880,-5184,-6656,-4960),(255,140,0)),('시작 위습·진화체 위습 소환',(-3584,-9824,-3328,-9568),(0,255,255)),('보상 지급점 r',(-3584,-8192,-3328,-8000),(0,255,255))):
    a=R.P(x0,y1);bb=R.P(x1,y0);dr.rectangle([a[0]-4,a[1]-4,bb[0]+4,bb[1]+4],outline=c,width=2);dr.text((a[0]+8,a[1]-12),lab,fill=c,font=f2)
pins=[('포네그리프',-9136,6894,(255,255,255)),('롬바 해적단 배(룸바 영혼?)',9159,6455,(255,255,255)),('고잉메리호',-8019,-4085,(255,255,255)),('대형 스토리 포탈',-6701,-3954,(255,255,255)),('불타는 화로',3968,-8013,(255,255,255)),('위습동상 4',-3460,-6500,(255,255,255)),('모래 대륙→',-8200,-9800,(255,255,255))]
for nm,x,y,c in pins:
    p=R.P(x,y);dr.ellipse([p[0]-5,p[1]-5,p[0]+5,p[1]+5],outline=(255,255,255),width=2);dr.text((p[0]+7,p[1]-7),nm,fill=c,font=f)
img.save(b+'s2_zone_map.png')
# 미니맵 겹쳐 보기(검증)
x0,y0,x1,y1=render2.PLAY['s2']
a=R.P(x0,y1);bb=R.P(x1,y0)
terr0=Image.open(b+'s2_terrain.png').convert('RGB').crop((int(a[0]),int(a[1]),int(bb[0]),int(bb[1]))).resize((512,478))
terr=Image.new('RGB',(512,512),(0,0,0));terr.paste(terr0,(0,17))
mm=Image.open(b+'s2/war3mapMap.blp').convert('RGB').resize((512,512))
bl=Image.blend(terr,mm,0.5)
out=Image.new('RGB',(1536,512));out.paste(terr,(0,0));out.paste(mm,(512,0));out.paste(bl,(1024,0));out.save(b+'s2_check_terrain_vs_minimap.png')
print('ok')
