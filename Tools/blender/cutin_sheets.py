"""컷인 C안 후보 시트(유닛마다 한 줄, ★ = 고른 것) + 41기 연속 영상. run_cutin_c_all.sh 마지막 단계 — 고르기만 바꿨을 때 따로 돌린다."""
import glob, json, os, subprocess
from PIL import Image, ImageDraw, ImageFont
_CH = os.environ.get("CUTIN_HOME", "~/GRD_cutin"); R = os.path.expanduser(_CH + "/render"); D = os.path.expanduser(_CH + "/C_all")
F = ImageFont.truetype(os.path.expanduser("~/GRD_cutin/fonts/NanumGothic-ExtraBold.ttf"), 18)
infos = [json.load(open(f)) for f in sorted(glob.glob(os.path.join(R, "C_*_cands.json")))]
S, cols = 180, 8
sheet = Image.new("RGB", (S * cols + 220, (S + 26) * len(infos)), (40, 42, 50)); d = ImageDraw.Draw(sheet)
for r, inf in enumerate(infos):
    y = r * (S + 26)
    d.text((8, y + 8), inf["unit"], font=F, fill=(255, 255, 255)); d.text((8, y + 34), inf.get("source", ""), font=F, fill=(180, 200, 255))
    for c, cd in enumerate(inf["candidates"][:cols]):
        im = Image.open(os.path.join(R, cd["file"])).convert("RGBA").resize((S, S))
        bg = Image.new("RGBA", (S, S), (70, 74, 86, 255) if cd["id"] != inf["chosen"] else (120, 100, 40, 255)); bg.alpha_composite(im)
        sheet.paste(bg.convert("RGB"), (220 + c * S, y))
        d.text((224 + c * S, y + S + 2), ("★ " if cd["id"] == inf["chosen"] else "") + cd["id"], font=F, fill=(255, 220, 120) if cd["id"] == inf["chosen"] else (220, 220, 220))
sheet.save(os.path.join(D, "_candidates_sheet.png"))
vids = sorted(glob.glob(os.path.join(D, "C_*.mp4")))
with open(os.path.join(D, "_concat.txt"), "w") as fp:
    fp.write("".join(f"file '{v}'\n" for v in vids))
subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-f", "concat", "-safe", "0", "-i", os.path.join(D, "_concat.txt"), "-c", "copy", os.path.join(D, "컷인_C안_41기_연속.mp4")], check=True)
print("후보 시트", len(infos), "연속 영상", len(vids))
