#!/usr/bin/python3
"""PachinkoRecorder가 남긴 프레임(~/grd/GRD_pachinko_rec/<이름>/f_*.png)과 events.json으로 소리 포함 mp4를 만든다.
이벤트 이름: video:<파일>(StreamingAssets) · music:<클립>(Resources/Pachinko/Sfx) · musicfade(마지막 music을 vol초에 걸쳐 페이드아웃) · 그 밖 = 대사 클립(Sfx/<이름>.mp3).
사용: /usr/bin/python3 Tools/pachinko/build_rec.py 777   → ~/grd/GRD_pachinko_shots/rec_777.mp4  (게임 BGM·시스템 소리는 안 들어간다)"""
import json, os, subprocess, sys
name = sys.argv[1]
home = os.path.expanduser('~'); root = os.getcwd()
d = f'{home}/grd/GRD_pachinko_rec/{name}'
ev = json.load(open(f'{d}/events.json'))
sfx = f'{root}/Assets/Resources/Pachinko/Sfx'
sa = f'{root}/Assets/StreamingAssets'
def dur(p):
    return float(subprocess.check_output(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', p]).decode().strip())
def path_of(e):
    n = e['name']
    if n.startswith('video:'): return f"{sa}/{n[6:]}"
    if n.startswith('music:'): return f"{sfx}/{n[6:]}.mp3"
    return f"{sfx}/{n}.mp3"
def has_audio(p):
    out = subprocess.check_output(['ffprobe', '-v', 'error', '-select_streams', 'a', '-show_entries', 'stream=index', '-of', 'csv=p=0', p]).decode().strip()
    return bool(out)
events = [e for e in ev['events'] if e['name'] != 'musicfade' and (not e['name'].startswith('video:') or has_audio(path_of(e)))]   # 무음 영상은 소리 입력에서 뺀다
fades = [e for e in ev['events'] if e['name'] == 'musicfade']
lines = [(e['time'], dur(path_of(e))) for e in events if not e['name'].startswith(('video:', 'music:'))]
inputs, filters, labels = [], [], []
for i, e in enumerate(events):
    inputs += ['-i', path_of(e)]
    n = len(inputs) // 2
    ms = int(e['time'] * 1000)
    vol = max(0.05, e['vol'])
    f = f"[{n}:a]"
    if e['name'].startswith(('video:', 'music:')):   # 대사가 겹치면 ×0.4
        expr = '1'
        for (t, dd) in lines:
            expr = f"if(between(t,{t - e['time']:.2f},{t - e['time'] + dd:.2f}),0.4,{expr})"
        f += f"volume='{vol}*{expr}':eval=frame,"
        if e['name'].startswith('music:') and fades:
            fd = fades[0]; st = max(0.0, fd['time'] - e['time'])
            f += f"afade=t=out:st={st:.2f}:d={max(0.1, fd['vol']):.2f},atrim=0:{st + max(0.1, fd['vol']):.2f},"
    else:
        f += f"volume={vol},"
    f += f"adelay={ms}|{ms}[a{i}]"
    filters.append(f); labels.append(f'[a{i}]')
out = f'{home}/grd/GRD_pachinko_shots/rec_{name}.mp4'
cmd = ['ffmpeg', '-y', '-loglevel', 'error', '-framerate', '30', '-i', f'{d}/f_%05d.png'] + inputs
if labels:
    cmd += ['-filter_complex', ';'.join(filters) + ';' + ''.join(labels) + f"amix=inputs={len(labels)}:normalize=0:duration=longest[aout]", '-map', '0:v', '-map', '[aout]', '-c:a', 'aac']
cmd += ['-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-shortest', out]
subprocess.check_call(cmd)
print(out)
