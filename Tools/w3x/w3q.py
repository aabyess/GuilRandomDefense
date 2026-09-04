"""war3map.w3q (업그레이드 오브젝트 데이터) 파서.
유닛(w3u)과 거의 같은데, 수정마다 [레벨][자료ID]가 더 붙는다."""
import struct
def _read(d,i,count):
    out=[]
    for _ in range(count):
        base=d[i:i+4].decode('ascii','replace'); i+=4
        new =d[i:i+4].decode('ascii','replace'); i+=4
        nmod,=struct.unpack('<I',d[i:i+4]); i+=4
        mods=[]
        for _ in range(nmod):
            fid=d[i:i+4].decode('ascii','replace'); i+=4
            vtype,=struct.unpack('<I',d[i:i+4]); i+=4
            lvl,=struct.unpack('<I',d[i:i+4]); i+=4
            dataid,=struct.unpack('<I',d[i:i+4]); i+=4
            if vtype==0: v,=struct.unpack('<i',d[i:i+4]); i+=4
            elif vtype in (1,2): v,=struct.unpack('<f',d[i:i+4]); i+=4
            else:
                e=d.index(b'\0',i); v=d[i:e].decode('utf-8','replace'); i=e+1
            i+=4
            mods.append((fid,lvl,v))
        out.append({'base':base,'id':new or base,'mods':mods})
    return out,i
def parse(path):
    d=open(path,'rb').read(); i=4
    n,=struct.unpack('<I',d[i:i+4]); i+=4
    a,i=_read(d,i,n)
    n,=struct.unpack('<I',d[i:i+4]); i+=4
    b,i=_read(d,i,n)
    return a+b
