#!/usr/bin/python3
"""PachinkoRecorder가 남긴 프레임(~/GRD_pachinko_rec/<이름>/f_*.png)과 events.json으로 소리 포함 mp4를 만든다.
사용: /usr/bin/python3 Tools/pachinko/build_rec.py 777   → ~/GRD_pachinko_shots/rec_777.mp4  (게임 BGM·시스템 소리는 안 들어간다)"""
import json, os, subprocess, sys
name = sys.argv[1]
home = os.path.expanduser('~')
root = os.getcwd()
d = f'{home}/GRD_pachinko_rec/{name}'
ev = json.load(open(f'{d}/events.json'))
sfx = f'{root}/Assets/Resources/Pachinko/Sfx'
vid = f'{root}/Assets/StreamingAssets/pachinko_bg_15s_with_music.mp4'
def dur(p):
    return float(subprocess.check_output(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'csv=p=0', p]).decode().strip())
inputs, filters, labels = [], [], []
lines = [(e['time'], dur(f"{sfx}/{e['name']}.mp3")) for e in ev['events'] if e['name'] != 'bgvideo']
for i, e in enumerate(ev['events']):
    path = vid if e['name'] == 'bgvideo' else f"{sfx}/{e['name']}.mp3"
    inputs += ['-i', path]
    n = len(inputs) // 2   # 입력 번호(0은 프레임)
    ms = int(e['time'] * 1000)
    vol = max(0.05, e['vol'])
    f = f"[{n}:a]"
    if e['name'] == 'bgvideo':   # 대사가 겹치면 영상 소리 ×0.4
        expr = '1'
        for (t, dd) in lines:
            expr = f"if(between(t,{t - e['time']:.2f},{t - e['time'] + dd:.2f}),0.4,{expr})"
        f += f"volume='{vol}*{expr}':eval=frame,"
    else:
        f += f"volume={vol},"
    f += f"adelay={ms}|{ms}[a{i}]"
    filters.append(f); labels.append(f'[a{i}]')
out = f'{home}/GRD_pachinko_shots/rec_{name}.mp4'
cmd = ['ffmpeg', '-y', '-loglevel', 'error', '-framerate', '30', '-i', f'{d}/f_%05d.png'] + inputs
if labels:
    cmd += ['-filter_complex', ';'.join(filters) + ';' + ''.join(labels) + f"amix=inputs={len(labels)}:normalize=0:duration=longest[aout]", '-map', '0:v', '-map', '[aout]', '-c:a', 'aac']
cmd += ['-c:v', 'libx264', '-pix_fmt', 'yuv420p', '-shortest', out]
subprocess.check_call(cmd)
print(out)
