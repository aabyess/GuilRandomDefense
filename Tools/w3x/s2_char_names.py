# -*- coding: utf-8 -*-
"""S2 모델 파일 이름(!NNNN_캐릭터[_변형].mdl) 토큰 → 한글 캐릭터 이름 표(2026-10-09, blender2). 표에 없는 토큰은 모델명 그대로 쓴다(추정 금지).
cname(model, old_name) → (표시 이름, 근거). 근거: 「모델명 표」·「모델명 그대로」·「옛 ID 이름(캐릭터 모델 아님)」."""
import re
T = dict(nami="나미", zoro="조로", luffy="루피", sanji="상디", sanjiup="상디(강화)", usop="우솝", usopp="우솝", chopa="쵸파", chopper="쵸파", chopaguardpoint="쵸파(가드 포인트)", chopabrain="쵸파(두뇌 강화)", robin="니코 로빈",
    buggy="버기", franky="프랑키", brook="브룩", marine="해군", hawkins="호킨스", hokins="호킨스", hukuro="후쿠로", moria="모리아", bepo="베포", tashigi="타시기", inazma="이나즈마", perona="페로나", ace="에이스",
    blueno="블루노", lucci="루치", lucciawk="루치(각성)", captainkuro="캡틴 쿠로", bonkure="봉쿠레(Mr.2)", kuma="쿠마", jinbe="징베", chaka="챠카", croco="크로커다일", crocodie="크로커다일", killer="킬러", capone="카포네",
    smoker="스모커", law="트라팔가 로우", helmeppo="헤르메포", hancock="보아 핸콕", akainu="아카이누", kizaru="키자루", borsalino="보르살리노", beckman="벤 베크만", decken="반 더 데켄", momonga="모몬가", magellan="마젤란",
    tichi="티치", teach="티치", kaku="카쿠", wyper="와이퍼", shiryu="시류", sugar="슈가", sentoumaru="센토마루", aokiji="아오키지", kuzan="쿠잔", jozu="죠즈", kinemon="킨에몬", laboon="라분", dragon="드래곤",
    reiju="레이쥬", sengoku="센고쿠", shiki="시키", koby="코비", zephyr="제파", sabo="사보", bentham="벤담", redforce="레드포스", shinobu="시노부", fishertiger="피셔 타이거", mihawk="미호크", iceburg="아이스버그",
    strawman="스트로맨", king="킹", zeus="제우스", shirahoshi="시라호시", issho="잇쇼", shanks="샹크스", sakazuki="사카즈키", doflamingo="도플라밍고", vegapunk="베가펑크", snakeman="스네이크맨", kid="키드",
    yamato="야마토", yamatoup="야마토(강화)", nika="니카", aramaki="아라마키", bonney="보니", hazzi="하찌", enel="에넬", marco="마르코", absalom="압살롬", aron="아론", squard="스콰드", xdrake="X-드레이크", vivi="비비",
    garp="가프", burgess="버지스", bartolomeo="바르톨로메오", oars="오르스", ryuma="류마", ivankov="이바노프", zeff="제프", baby5="베이비 5", blackmaria="블랙 마리아", caesarclown="시저 클라운", lailley="레일리",
    rayleigh="레일리", edward="에드워드", kalgara="칼가라", cracker="크래커", sunny="써니호", baratie="바라티에", mobidick="모비딕호", ulti="울티", rebecca="레베카", koala="코알라", vergo="베르고", ain="아인",
    kiku="키쿠", gaban="가반", roger="로저", uta="우타", bigmom="빅맘", stussy="스터시", cavendish="카벤디시", mogan="모건", otama="오타마", bluegorilla="블루 고릴라", pell="페루", megumin="메구밍", tesoro="테조로",
    toki="토키", naruto="나루토", katakuri="카타쿠리", carrot="캐럿", kaido="카이도", bullet="불렛", sans="샌즈", byakuya="뱌쿠야", alvida="알비다", oden="오뎅", brulee="브륄레", bronya="브로냐", gojo="고죠 사토루",
    jotaro="죠타로", foxy="폭시", minato="미나토", ryogi="료우기", higma="히그마", tatsumaki="타츠마키", nezuko="네즈코", gaimon="가이몬", yujiro="유지로", konpaku="콘파쿠", higante="히간테", queen="퀸",
    hirari="히라리", ichigo="이치고", yagamilight="야가미 라이토", anya="아냐", rebecca5="레베카", koala2="코알라", luffynm="루피(NM)", dp="DP")
VAR = {"premium": "프리미엄", "monster": "몬스터", "marine": "해군", "pirate": "해적", "giant": "거인", "general": "장군", "change": "변신", "awk": "각성", "atlas": "아틀라스", "lilith": "릴리스", "york": "요크"}
def cname(model, old_name):
    f = (model or "").replace("\\", "/").rsplit("/", 1)[-1]; f = re.sub(r"\.md[lx]$", "", f, flags=re.I)
    m = re.match(r"^!\d{4}_(.+)$", f)
    if not m:
        t = re.sub(r"\d+$", "", f.lower())
        if t in T: return T[t], "모델명 표"
        return old_name, "옛 ID 이름(캐릭터 모델 아님)"
    parts = m.group(1).split("_"); tok = parts[0].lower(); var = "_".join(parts[1:])
    base = T.get(tok)
    if base is None: return m.group(1), "모델명 그대로(표에 없음)"
    if var: base += " (" + VAR.get(var.lower(), var) + ")"
    return base, "모델명 표"
