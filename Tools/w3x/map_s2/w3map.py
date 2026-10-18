import struct,sys,os,collections
def parse_w3e(path):
    d=open(path,'rb').read();i=0
    assert d[:4]==b'W3E!'
    ver,=struct.unpack('<I',d[4:8]);tileset=chr(d[8]);custom,=struct.unpack('<I',d[9:13]);i=13
    ng,=struct.unpack('<I',d[i:i+4]);i+=4
    ground=[d[i+4*k:i+4*k+4].decode() for k in range(ng)];i+=4*ng
    nc,=struct.unpack('<I',d[i:i+4]);i+=4
    cliff=[d[i+4*k:i+4*k+4].decode() for k in range(nc)];i+=4*nc
    w,h=struct.unpack('<II',d[i:i+8]);i+=8
    ox,oy=struct.unpack('<ff',d[i:i+8]);i+=8
    pts=[]
    for k in range(w*h):
        hh,wf,tex,var,misc=struct.unpack('<HHBBB',d[i:i+7]);i+=7
        pts.append((hh,wf,tex,var,misc))
    return dict(ver=ver,tileset=tileset,ground=ground,cliff=cliff,w=w,h=h,ox=ox,oy=oy,pts=pts)
def parse_doo(path):
    d=open(path,'rb').read()
    assert d[:4]==b'W3do'
    ver,sub,n=struct.unpack('<III',d[4:16]);i=16;out=[]
    for _ in range(n):
        tid=d[i:i+4].decode('latin1');var,=struct.unpack('<I',d[i+4:i+8])
        x,y,z,ang,sx,sy,sz=struct.unpack('<fffffff',d[i+8:i+36]);i+=36
        flags=d[i];life=d[i+1];i+=2
        ptr,nsets=struct.unpack('<iI',d[i:i+8]);i+=8
        for _s in range(nsets):
            cnt,=struct.unpack('<I',d[i:i+4]);i+=4+8*cnt
        eid,=struct.unpack('<I',d[i:i+4]);i+=4
        out.append((tid,var,x,y,z,ang,sx,sy,sz,flags,life,eid))
    sver,scount=struct.unpack('<II',d[i:i+8]);i+=8
    specials=[]
    for _ in range(scount):
        tid=d[i:i+4].decode('latin1');z,x,y=struct.unpack('<iii',d[i+4:i+16]);i+=16;specials.append((tid,x,y,z))
    return ver,sub,out,specials,(len(d)-i)
