"""원작 트리거(JASS 스테이지 머신)를 읽어 시간순 이벤트로 푸는 작은 해석기(2026-10-09, blender).

  from jass_sim import simulate  →  simulate("Enel_Mana") = dict(events=[...], notes=[...], unresolved=N)
- 지원: if/elseif/else/loop·exitwhen, set, call, 조건 함수(Trig_X_FuncNNNC), TV(TrigVariables) 슬롯, SleepForStage(…)/Next/Add, 하위 트리거 호출(ConditionalTriggerExecute).
- 이벤트: 더미 유닛 생성(CreateNUnitsAtLoc 계열)·특수효과(AddSpecialEffect 계열)·크기·비행 높이·타임스케일·사망·수명·카메라 흔들림·피해 범위·이동.
- 난수: GetRandomInt(a,b)와의 비교는 항상 참(= 확률 발동을 일어난 것으로 본다), GetRandomReal은 고정 seed 난수. 풀 수 없는 식은 UNKNOWN이며 참으로 가정하고 unresolved에 센다.
- 좌표계: 시전자 (0,0), 대상 (600,0) 고정, 각도 0° = 시전자→대상 방향.
"""
import math, os, random, re

HERE = os.path.dirname(os.path.abspath(__file__))
S2 = bool(os.environ.get("JASS_S2"))                                    # S2(2.323) war3map.j: 난독화 이름·16진 ID — 옛 방언으로 번역해서 같은 해석기를 쓴다(10-09 박민수_조로)
J = open(os.path.join(HERE, "원본_S2_2.323/풀린것/war3map.j" if S2 else "원본/풀린것/war3map.j"), encoding="utf8", errors="replace").read()


def translate_s2(t):
    """S2 j(난독화) → 옛 j 방언. 스테이지 머신 원시 함수만 바꾼다(BST=Setunit·BTV=Setlocation·BWT=Setreal·BVU=Setinteger·BJ3=SleepForStageNext·BJx=Flush·BJ7=SettingEx·HR[DR]=Stage·LoadXHandle=get_X)."""
    L = "ABCDEFGHIJKLMNOP"
    for a, b in (("BST", "Setunit"), ("BTV", "SetlocationAutoRemove"), ("BWT", "Setreal"), ("BVU", "Setinteger")):
        t = re.sub(rf"\b{a}\(DR,", f"s__TrigVariables_{b}(GlobalTV,", t)
    t = re.sub(r"\bBJ3\(DR,", "s__TrigVariables_SleepForStageNext(GlobalTV,", t)
    t = re.sub(r"\bBJ5\(DR,([^,]+),", r"s__TrigVariables_SleepForStageAdd(GlobalTV,\1,", t) if False else t
    t = re.sub(r"\bBJx\(DR\)", "s__TrigVariables_Flush(GlobalTV)", t)
    t = re.sub(r"\bBJ7\((\d+)\)", r"s__TrigVariables_SettingEx(\1)", t)
    t = re.sub(r"\bHR\[DR\]", "s__TrigVariables_Stage[GlobalTV]", t)
    for h, g, kd in (("LoadUnitHandle(E2", None, "unit"), ("LoadLocationHandle(E9", None, "location"), ("LoadReal(FW", None, "real"), ("LoadInteger(FP", None, "integer")):
        t = re.sub(re.escape(h) + r",0,DR\*EM\+(\d+)\)", lambda m, kd=kd: f"s__TrigVariables__get_{kd}{L[int(m.group(1))]}(GlobalTV)", t)
    t = re.sub(r"\bBFi\(", "GetRandomInt(", t); t = re.sub(r"\bBFj\(", "GetRandomReal(", t)
    def hexid(m):
        try:
            v = bytes.fromhex(m.group(1)).decode("ascii")
            return f"'{v}'" if v.isprintable() and len(v) == 4 else m.group(0)
        except Exception: return m.group(0)
    t = re.sub(r"\$([0-9A-Fa-f]{8})\b", hexid, t)
    return t


if S2: J = translate_s2(J)
FN = {}
for m in re.finditer(r"function (\w+) takes [^\n]*?returns \w+(.*?)endfunction", J, re.S):
    FN[m.group(1)] = m.group(2)
TARGET_X = 600.0


class Unk:
    """풀 수 없는 값 — 연산·비교·호출 모두 Unk, 참으로 취급."""
    def __init__(self, why=""): self.why = why
    def __call__(self, *a, **k): return Unk(self.why)
    def __getitem__(self, i): return Unk(self.why)
    def __bool__(self): return True
    def _b(self, o): return Unk(self.why)
    __add__ = __radd__ = __sub__ = __rsub__ = __mul__ = __rmul__ = __truediv__ = __rtruediv__ = __neg__ = _b
    __lt__ = __le__ = __gt__ = __ge__ = __eq__ = __ne__ = _b
    def __float__(self): return 0.0
    def __hash__(self): return 0


class Rand(float):
    """GetRandomInt(a,b): 확률 게이트 — 어떤 비교든 참."""
    def __new__(cls, a, b): return float.__new__(cls, (a + b) / 2)
    def __eq__(self, o): return True
    def __ne__(self, o): return False
    def __lt__(self, o): return True
    __le__ = __gt__ = __ge__ = __lt__
    def __hash__(self): return 1


class Env(dict):
    def __missing__(self, k): return Unk(k)


def split_args(s):
    out, d, cur, q = [], 0, "", False
    for c in s:
        if c == '"': q = not q
        if not q:
            if c in "([": d += 1
            elif c in ")]": d -= 1
            elif c == "," and d == 0: out.append(cur.strip()); cur = ""; continue
        cur += c
    if cur.strip(): out.append(cur.strip())
    return out


KW = re.compile(r"\b(call|set|if|elseif|else|endif|loop|endloop|exitwhen|return|local)\b")


def tokenize(body):
    strs = []
    def sub(m): strs.append(m.group(0)); return f'"@{len(strs) - 1}@"'
    b = re.sub(r'"(?:[^"\\]|\\.)*"', sub, body)
    pos = [(m.start(), m.group(1)) for m in KW.finditer(b)]
    segs = []
    for i, (p, k) in enumerate(pos):
        e = pos[i + 1][0] if i + 1 < len(pos) else len(b)
        segs.append((k, b[p + len(k):e].strip()))
    return segs, strs


def clean_cond(t):
    t = re.sub(r"\s*then\s*$", "", t.strip()).strip()
    while t.startswith("(") and t.endswith(")"):
        d = 0; ok = True
        for i, c in enumerate(t):
            d += (c == "(") - (c == ")")
            if d == 0 and i < len(t) - 1: ok = False; break
        if not ok: break
        t = t[1:-1].strip()
    return t


def parse(body):
    segs, strs = tokenize(body); i = 0
    def block(stop):
        nonlocal i
        out = []
        while i < len(segs) and segs[i][0] not in stop:
            k, t = segs[i]; i += 1
            if k == "if":
                cond = clean_cond(t); br = [(cond, block(("elseif", "else", "endif")))]; els = None
                while i < len(segs) and segs[i][0] in ("elseif", "else"):
                    k2, t2 = segs[i]; i += 1
                    if k2 == "elseif": br.append((clean_cond(t2), block(("elseif", "else", "endif"))))
                    else: els = block(("endif",))
                if i < len(segs) and segs[i][0] == "endif": i += 1
                out.append(("if", br, els))
            elif k == "loop":
                out.append(("loop", block(("endloop",)))); i += 1
            elif k == "exitwhen": out.append(("exitwhen", t))
            elif k == "set": out.append(("set", t))
            elif k == "call": out.append(("call", t))
            elif k == "return": out.append(("return", t))
        return out
    return block(()), strs


class Done(Exception): pass


class Sim:
    def __init__(self, seed=5):
        self.events = []; self.notes = []; self.unres = 0; self.rng = random.Random(seed); self.t = 0.0
        self.truncated = False; self.end_t = 0.0; self.udg = {}; self.uid = 0; self.units = {}; self.depth = 0; self.last = None; self.last_eff = None

    def u_new(self, code, loc, t, owner=None, facing=None, **kw):
        self.uid += 1; uid = f"u{self.uid}"
        e = dict(t=round(t, 3), op="spawn", id=uid, code=code, at=loc.d(), owner=owner)
        e.update({k: v for k, v in kw.items() if v is not None}); self.events.append(e)
        u = dict(id=uid, kind="dummy", code=code, loc=loc); self.units[uid] = u; self.last = u; return u


class Loc:
    def __init__(self, x=0.0, y=0.0, anchor="caster"): self.x, self.y, self.anchor = x, y, anchor
    def d(self):
        ax = 0.0 if self.anchor == "caster" else TARGET_X
        return dict(anchor=self.anchor, offset=[round(self.x - ax, 2), round(self.y, 2)], world=[round(self.x, 2), round(self.y, 2)])
    def __bool__(self): return True


CASTER = dict(id="caster", kind="caster"); TARGET = dict(id="target", kind="target")
def loc_of(u):
    if u is None or isinstance(u, Unk): return Loc(0, 0, "caster")
    if u["kind"] == "caster": return Loc(0, 0, "caster")
    if u["kind"] == "target": return Loc(TARGET_X, 0, "target")
    return u["loc"]


class Run:
    """한 트리거의 실행 상태(TV 슬롯·스테이지·큐)."""
    def __init__(self, sim, name, t0):
        self.sim, self.name, self.t0 = sim, name, t0
        self.stage = 0; self.queue = []; self.slots = {}; self.flushed = False; self.loopidx = {"A": 0, "B": 0}
        self.vars = {}; self.gates = {}; self.env = self.make_env()

    def slot(self, kind, i): return self.slots.get((kind, int(i)))

    def make_env(self):
        sim, me = self.sim, self
        E = Env()
        E.update(true=True, false=False, null=None, GlobalTV="TV")
        for kd in ("integer", "real", "unit", "location", "group", "timer"):
            for li, L in enumerate("ABCDEFGHIJKLMNOP"):
                E[f"s__TrigVariables__get_{kd}{L}"] = (lambda tv, kd=kd, li=li: me.slots.get((kd, li), Unk(f"{kd}{li}")))
        class StageMap:
            def __getitem__(s, k): return me.stage
        E["s__TrigVariables_Stage"] = StageMap()
        E.update(I2R=float, R2I=lambda x: int(x) if not isinstance(x, Unk) else 0, ModuloInteger=lambda a, b: (int(a) % int(b)) if not isinstance(a, Unk) and b else Unk("mod"),
                 GetRandomReal=lambda a, b: sim.rng.uniform(float(a), float(b)), GetRandomInt=lambda a, b: Rand(float(a), float(b)), GetRandomDirectionDeg=lambda: sim.rng.uniform(0, 360),
                 GetAttacker=lambda: CASTER, GetTriggerUnit=lambda: TARGET, GetLastCreatedUnit=lambda: sim.last, GetUnitLoc=loc_of, GetForLoopIndexA=lambda: me.vars.get("bj_forLoopAIndex", 0), GetForLoopIndexB=lambda: me.vars.get("bj_forLoopBIndex", 0),
                 AngleBetweenPoints=lambda a, b: math.degrees(math.atan2(b.y - a.y, b.x - a.x)) if isinstance(a, Loc) and isinstance(b, Loc) else 0.0,
                 DistanceBetweenPoints=lambda a, b: math.hypot(b.x - a.x, b.y - a.y) if isinstance(a, Loc) and isinstance(b, Loc) else 0.0,
                 Location=lambda x, y: Loc(float(x), float(y), "target"), Player=lambda n: ("player", n), GetOwningPlayer=lambda u: ("player", 0 if (u and u.get("kind") == "caster") else 4),
                 bj_UNIT_FACING=0.0, bj_PI=math.pi, Cos=lambda d: math.cos(math.radians(d)), Sin=lambda d: math.sin(math.radians(d)), SquareRoot=lambda x: math.sqrt(max(float(x), 0)))
        def polar(l, dist, ang):
            if not isinstance(l, Loc): return Loc(0, 0, "caster")
            return Loc(l.x + float(dist) * math.cos(math.radians(float(ang))), l.y + float(dist) * math.sin(math.radians(float(ang))), l.anchor)
        def new_unit_fn(player, code, loc, facing=0):
            return sim.u_new(code, loc if isinstance(loc, Loc) else Loc(0, 0, "caster"), me.t0 + me.cur_t)
        def new_unit_xy(player, code, x, y, facing=0):
            return sim.u_new(code, Loc(float(x), float(y), "target"), me.t0 + me.cur_t)
        E.update(CreateUnitAtLoc=new_unit_fn, CreateUnit=new_unit_xy, GetUnitLifePercent=lambda u: 100.0, GetUnitUserData=lambda u: 1, CountUnitsInGroup=lambda g: 1, GroupPickRandomUnit=lambda g: TARGET,
                 GetUnitFacing=lambda u: 0.0, GetUnitX=lambda u: loc_of(u).x, GetUnitY=lambda u: loc_of(u).y, IsUnitType=lambda u, t: False, IsUnitEnemy=lambda u, p: True, IsUnitAliveBJ=lambda u: True, IsUnitDeadBJ=lambda u: False,
                 UnitAlive=lambda u: True, GetUnitAbilityLevel=lambda u, a: 1, GetUnitAbilityLevelSwapped=lambda a, u: 1, GetUnitStateSwap=lambda st, u: 100.0, GetPlayerId=lambda p: p[1] if isinstance(p, tuple) else 0,
                 GetConvertedPlayerId=lambda p: (p[1] + 1) if isinstance(p, tuple) else 1, GetUnitDefaultFlyHeight=lambda u: 0.0, UNIT_TYPE_SAPPER="SAPPER", GetUnitMoveSpeed=lambda u: 300.0)
        E["PolarProjectionBJ"] = polar
        E["OffsetLocation"] = lambda l, dx, dy: Loc(l.x + float(dx), l.y + float(dy), l.anchor)
        class Glob(dict):
            def __missing__(s, k): return Unk(k)
        for k in list(sim.udg): pass
        return E

    def ev(self, expr, strs):
        e = expr
        e = re.sub(r'"@(\d+)@"', lambda m: repr(strs[int(m.group(1))].strip('"')), e)
        e = re.sub(r"\bnot\b", " not ", e)
        e = re.sub(r"'(\w{4})'", lambda m: repr(m.group(1)), e)
        e = re.sub(r"\bTV\.", "s__TrigVariables_", e)
        e = e.replace("&&", " and ").replace("||", " or ")
        e = re.sub(r"\bs__TrigVariables_Stage\[GlobalTV\]", "s__TrigVariables_Stage[0]", e)
        env = Env(self.env)
        env.update(self.sim.udg_env())
        env.update(self.vars)
        try: return eval(e, {"__builtins__": {}}, env)
        except ZeroDivisionError: return Unk("div0")
        except Exception as ex: self.sim.unres += 1; return Unk(str(ex)[:40])

    def cond_fn(self, fname, strs_unused=None):
        body = FN.get(fname)
        if body is None: return Unk("nofn")
        flat = re.sub(r"\s+", " ", body)
        if "if(not(" in flat:                                           # 여러 개의 if(not(X))then return false endif … return true → X들이 모두 참이어야 참
            res = True; pos = 0
            while True:
                i = flat.find("if(not(", pos)
                if i < 0: break
                j = i + 7; d = 2
                while j < len(flat) and d > 0:
                    d += (flat[j] == "(") - (flat[j] == ")"); j += 1
                inner = flat[i + 7:j - 2]
                v = self.ev(inner, re.findall(r'"(?:[^"\\]|\\.)*"', inner))
                if isinstance(v, Unk): v = self.unk_gate(inner)
                if not v: res = False
                pos = j
            return res
        m = re.search(r"return\((.*)\)\s*$", re.sub(r"\s+", " ", body).strip())
        if m: return self.ev(m.group(1), re.findall(r'"(?:[^"\\]|\\.)*"', m.group(1)))
        return Unk("cond")

    def unk_gate(self, key):
        n = self.gates.get(key, 0); self.gates[key] = n + 1
        return n < 2

    def cond(self, c, strs):
        m = re.fullmatch(r"(Trig_\w+C)\(\)", c.strip())
        if m: return self.cond_fn(m.group(1))
        return self.ev(c, strs)

    def exec_block(self, blk, strs, t):
        for st in blk:
            k = st[0]
            if k == "if":
                _, br, els = st; done = False
                for c, b in br:
                    v = self.cond(c, strs)
                    if isinstance(v, Unk): self.sim.unres += 1; v = self.unk_gate(c)
                    if v:
                        self.exec_block(b, strs, t); done = True; break
                if not done and els is not None: self.exec_block(els, strs, t)
            elif k == "loop":
                n = 0
                try:
                    while n < 60:
                        self.exec_block(st[1], strs, t); n += 1
                except LoopExit: pass
            elif k == "exitwhen":
                v = self.ev(st[1], strs)
                if v and not isinstance(v, Unk): raise LoopExit()
                if isinstance(v, Unk): raise LoopExit()
            elif k == "set": self.do_set(st[1], strs)
            elif k == "call": self.do_call(st[1], strs)
            elif k == "return": raise Done()

    def do_set(self, t, strs):
        m = re.match(r"(\w+)(?:\[(.*?)\])?\s*=\s*(.*)$", t, re.S)
        if not m: return
        name, idx, rhs = m.groups(); v = self.ev(rhs, strs)
        if name.startswith("udg_"):
            key = name + (f"[{int(float(self.ev(idx, strs)))}]" if idx is not None and not isinstance(self.ev(idx, strs), Unk) else "")
            self.sim.udg[key] = v
        else: self.vars[name] = v

    def do_call(self, t, strs):
        m = re.match(r"([\w.]+)\((.*)\)\s*$", t, re.S)
        if not m: return
        fn, argstr = m.group(1), m.group(2); fn = fn.replace("TV.", "s__TrigVariables_")
        args = split_args(argstr); sim = self.sim; tt = self.t0 + self.cur_t
        A = lambda i: self.ev(args[i], strs) if i < len(args) else Unk("arg")
        if fn in ("DestroyEffectBJ", "DestroyEffect", "s__TrigVariables_Seteffect", "s__TrigVariables_SeteffectAutoRemove", "s__TrigVariables_SeteffectAutoRemove"):
            for a in args:                                              # 이펙트를 변수에 담거나 곧바로 파괴하는 호출 안의 AddSpecialEffect*는 실제로 한 번 뜬다(10-09: 「해석 실패」 중 이 꼴이 대부분)
                if "AddSpecialEffect" in a: self.do_call(a.strip(), strs)
            return
        mm = re.fullmatch(r"s__TrigVariables_(Set|Setting|SettingEx)(integer|real|unit|location|group|timer|locationAutoRemove|groupAutoRemove|timerAutoRemove)?", fn)
        if fn.startswith("s__TrigVariables_Set") and mm and mm.group(2):
            kd = mm.group(2).replace("AutoRemove", ""); i = A(1); v = A(2)
            if not isinstance(i, Unk): self.slots[(kd, int(i))] = v
            return
        if fn in ("s__TrigVariables_SettingEx", "s__TrigVariables_Setting"):
            if self.first:                                              # 첫 실행에서만 초기 스테이지를 정한다(Setting()=1, SettingEx(n)=n); 재진입 때는 안 건드림
                v = A(0) if fn.endswith("Ex") else 1
                self.stage = int(float(v)) if not isinstance(v, Unk) else 0
            return
        if fn in ("s__TrigVariables_SleepForStage", "s__TrigVariables_SleepForStageAdd"):
            d, s = A(1), A(2)
            if isinstance(d, Unk) or isinstance(s, Unk): d, s = 0.1, self.stage + 1; sim.unres += 1
            self.pending = (float(d), int(float(s))) if float(d) < 8 else None   # 8초 이상 대기는 재진입(쿨다운 성격) — 연출이 아니라 무시
            return
        if fn == "s__TrigVariables_SleepForStageNext":
            d = A(1); self.pending = (float(d) if not isinstance(d, Unk) else 0.1, self.stage + 1); return
        if fn == "s__TrigVariables_RegisterUnitEvent" and len(args) >= 5 and "EVENT_UNIT_DAMAGED" in args[2]:
            st = A(4)                                                   # 피해 이벤트에 걸린 스테이지: 시전자가 때린 직후(0.35초) 맞았다고 보고 그 스테이지를 예약한다(10-09, 「해석 실패」 16건의 원인)
            if not isinstance(st, Unk): self.extra.append((0.2, int(float(st))))
            return
        if fn in ("s__TrigVariables_Flush", "s__TrigVariables_DeleteAllTriggers"):
            if fn.endswith("Flush"): self.flushed = True; self.pending = None
            return
        if fn in ("TriggerSleepAction", "PolledWait"):
            d = A(0)
            if not isinstance(d, Unk): self.cur_t += float(d)
            return
        if fn in ("ConditionalTriggerExecute", "TriggerExecute", "TriggerExecuteBJ"):
            tg = re.search(r"gg_trg_(\w+)", args[0]);
            if tg and sim.depth < 2: sim.depth += 1; run_trigger(sim, tg.group(1), tt); sim.depth -= 1
            return
        if fn in ("CreateNUnitsAtLoc", "CreateNUnitsAtLocFacingLocBJ"):
            n = A(0); code = re.search(r"'(\w{4})'", args[1]); loc = A(3)
            if code:
                for _ in range(max(1, int(n) if not isinstance(n, Unk) else 1)):
                    sim.u_new(code.group(1), loc if isinstance(loc, Loc) else Loc(0, 0, "caster"), tt, owner=("caster" if self.ev(args[2], strs) == ("player", 0) else "neutral"))
            return
        if fn == "CreateUnitAtLoc":
            code = re.search(r"'(\w{4})'", args[1]); loc = A(2)
            if code: sim.u_new(code.group(1), loc if isinstance(loc, Loc) else Loc(0, 0, "caster"), tt)
            return
        if fn == "CreateUnit":
            code = re.search(r"'(\w{4})'", args[1]); x, y = A(2), A(3)
            if code: sim.u_new(code.group(1), Loc(float(x) if not isinstance(x, Unk) else 0, float(y) if not isinstance(y, Unk) else 0, "target"), tt)
            return
        if fn in ("AddSpecialEffectLoc", "AddSpecialEffectTarget", "AddSpecialEffectTargetUnitBJ", "AddSpecialEffect"):
            sraw = [a for a in args if '"@' in a]
            if not sraw: return
            path = strs[int(re.search(r'@(\d+)@', sraw[0] if fn != "AddSpecialEffectTargetUnitBJ" else sraw[-1]).group(1))].strip('"')
            if fn == "AddSpecialEffectTargetUnitBJ": path = strs[int(re.search(r'@(\d+)@', args[2]).group(1))].strip('"')
            if not re.search(r"\.md[lx]$", path, re.I): return
            where = Loc(0, 0, "caster")
            if fn == "AddSpecialEffectLoc": l = A(1); where = l if isinstance(l, Loc) else where
            elif fn == "AddSpecialEffectTarget": where = loc_of(A(1))
            elif fn == "AddSpecialEffectTargetUnitBJ": where = loc_of(A(1))
            sim.uid += 1; e = dict(t=round(tt, 3), op="spawn", id=f"u{sim.uid}", code="(이펙트)", effectModelPath=path, at=where.d(), owner="caster"); sim.events.append(e); sim.last_eff = e
            return
        if S2 and fn in ("UnitApplyTimedLife", "SetUnitScale", "SetUnitVertexColor", "SetUnitAnimationByIndex", "SetUnitFlyHeight", "SetUnitTimeScale", "SetUnitAnimation"):
            u = A(0)
            if not isinstance(u, dict) or u.get("kind") != "dummy": return
            base = dict(t=round(tt, 3), id=u["id"])
            if fn == "UnitApplyTimedLife": d = A(2); sim.events.append(dict(base, op="set", lifeSec=float(d) if not isinstance(d, Unk) else 1))
            elif fn == "SetUnitScale": v = A(1); sim.events.append(dict(base, op="set", scalePercent=round(float(v) * 100, 1) if not isinstance(v, Unk) else 100))
            elif fn == "SetUnitVertexColor":
                a = [A(i) for i in (1, 2, 3, 4)]
                if not any(isinstance(x, Unk) for x in a): sim.events.append(dict(base, op="set", vertexColor=[round(x / 255, 3) for x in a[:3]], vertexAlpha=round(a[3] / 255, 3)))
            elif fn == "SetUnitAnimationByIndex": v = A(1); sim.events.append(dict(base, op="set", animIndex=int(v) if not isinstance(v, Unk) else 0))
            elif fn == "SetUnitFlyHeight":
                h, r = A(1), A(2)
                if not isinstance(h, Unk): sim.events.append(dict(base, op="ramp", prop="flyHeight", to=float(h), ratePerSec=float(r) if not isinstance(r, Unk) and float(r) > 0 else 1e6))
            elif fn == "SetUnitTimeScale": v = A(1); sim.events.append(dict(base, op="set", timescale=float(v) if not isinstance(v, Unk) else 1))
            elif fn == "SetUnitAnimation": sim.events.append(dict(base, op="set", anim=strs[int(re.search(r'@(\d+)@', args[1]).group(1))].strip('"')))
            return
        if S2 and fn == "Bcc":                                          # Bcc(시전자, 지연, 반경, 위치, 배율, 최소, 최대, 공격타입, 피해타입) = UnitDamagePointLoc(… 랜덤×배율 …)
            r, l, mx = A(2), A(3), A(4)
            sim.events.append(dict(t=round(tt, 3), op="damage", shape="circle", radius=float(r) if not isinstance(r, Unk) else None, around=(l.d() if isinstance(l, Loc) else None), amount=None)); return
        if S2 and fn == "BFw":                                          # BFw("모델.mdx", 유닛, "attach") = 유닛에 붙는 특수효과
            m_ = re.search(r'@(\d+)@', args[0]); path = strs[int(m_.group(1))].strip('"') if m_ else ""
            if re.search(r"\.md[lx]$", path, re.I):
                sim.uid += 1; e = dict(t=round(tt, 3), op="spawn", id=f"u{sim.uid}", code="(이펙트)", effectModelPath=path, at=loc_of(A(1)).d(), owner="caster"); sim.events.append(e); sim.last_eff = e
            return
        if S2 and fn == "BcY":                                          # BcY(유닛, 위치, 시간, 간격) = 유닛을 위치로 그 시간 동안 이동
            u, l, dur = A(0), A(1), A(2)
            if isinstance(u, dict) and u.get("kind") == "dummy" and isinstance(l, Loc): sim.events.append(dict(t=round(tt, 3), op="move", id=u["id"], to=l.d(), durationSec=float(dur) if not isinstance(dur, Unk) else 0.14))
            return
        if S2 and fn == "IssuePointOrderById" and "OrderId" in argstr:
            o = re.search(r'OrderId\((@\d+@)\)', argstr); name_ = strs[int(re.search(r'\d+', o.group(1)).group(0))].strip('"') if o else "?"
            sim.events.append(dict(t=round(tt, 3), op="note", text=f"더미에 {name_} 명령 — 범위·피해는 그 더미의 능력(S2 w3a)")); return
        if fn in ("SetUnitScalePercent", "SetUnitFlyHeightBJ", "SetUnitTimeScale", "SetUnitAnimation", "SetUnitVertexColorBJ", "UnitApplyTimedLifeBJ", "KillUnit", "RemoveUnit", "SetUnitPositionLoc", "SetUnitFacing"):
            ui = 2 if fn == "UnitApplyTimedLifeBJ" else 0
            u = A(ui)
            if not isinstance(u, dict) or u.get("kind") != "dummy": return
            idv = u["id"]; base = dict(t=round(tt, 3), id=idv)
            if fn == "SetUnitScalePercent": v = A(1); sim.events.append(dict(base, op="set", scalePercent=round(float(v), 1) if not isinstance(v, Unk) else 100))
            elif fn == "SetUnitTimeScale": v = A(1); sim.events.append(dict(base, op="set", timescale=float(v) if not isinstance(v, Unk) else 1))
            elif fn == "SetUnitFlyHeightBJ":
                h, r = A(1), A(2)
                if not isinstance(h, Unk): sim.events.append(dict(base, op="ramp", prop="flyHeight", to=float(h), ratePerSec=float(r) if not isinstance(r, Unk) and float(r) > 0 else 1e6))
            elif fn == "SetUnitAnimation": sim.events.append(dict(base, op="set", anim=strs[int(re.search(r'@(\d+)@', args[1]).group(1))].strip('"')))
            elif fn == "SetUnitVertexColorBJ":
                tr = A(4); sim.events.append(dict(base, op="set", vertexAlpha=round(1 - float(tr) / 100, 2) if not isinstance(tr, Unk) else 1))
            elif fn == "UnitApplyTimedLifeBJ":
                d = A(0); sim.events.append(dict(base, op="set", lifeSec=float(d) if not isinstance(d, Unk) else 1))
            elif fn in ("KillUnit", "RemoveUnit"): sim.events.append(dict(base, op="kill"))
            elif fn == "SetUnitPositionLoc":
                l = A(1)
                if isinstance(l, Loc): u["loc"] = l; sim.events.append(dict(base, op="teleport", to=l.d()))
            return
        if fn == "vibration":
            a, b = A(1), A(2); sim.events.append(dict(t=round(tt, 3), op="camera_shake", magnitude=float(a) if not isinstance(a, Unk) else 1, durationSec=float(b) if not isinstance(b, Unk) else 1)); return
        if fn == "UnitDamagePointLoc":
            r, l, amt = A(2), A(3), A(4)
            sim.events.append(dict(t=round(tt, 3), op="damage", shape="circle", radius=float(r) if not isinstance(r, Unk) else None, around=(l.d() if isinstance(l, Loc) else None), amount=float(amt) if not isinstance(amt, Unk) else None)); return
        if fn == "ForGroupBJ" or fn == "GroupEnumUnitsInRangeOfLoc":
            mr = re.search(r"GetUnitsInRangeOfLocMatching\(([^,]+),(.*?),Condition", argstr) if fn == "ForGroupBJ" else None
            if mr:
                r = self.ev(mr.group(1), strs); l = self.ev(mr.group(2), strs)
                sim.events.append(dict(t=round(tt, 3), op="damage", shape="circle", radius=float(r) if not isinstance(r, Unk) else None, around=(l.d() if isinstance(l, Loc) else None)))
            elif fn == "GroupEnumUnitsInRangeOfLoc":
                l, r = A(1), A(2)
                sim.events.append(dict(t=round(tt, 3), op="damage", shape="circle", radius=float(r) if not isinstance(r, Unk) else None, around=(l.d() if isinstance(l, Loc) else None)))
            return
        if fn.startswith("Issue") and "stomp" in argstr.lower():
            sim.events.append(dict(t=round(tt, 3), op="note", text="더미에 stomp 명령 — 범위·피해는 그 더미의 능력(w3u uabi → w3a)")); return


class LoopExit(Exception): pass


def udg_env(self):
    out = Env()
    for k, v in self.udg.items():
        m = re.match(r"(\w+)\[(\d+)\]$", k)
        if m:
            out.setdefault(m.group(1), ArrayVar())[int(m.group(2))] = v
        else: out[k] = v
    return out
class ArrayVar(dict):
    def __missing__(self, k): return Unk("arr")
    def __getitem__(self, k):
        return dict.get(self, k, Unk("udgarr"))
Sim.udg_env = udg_env


def run_trigger(sim, name, t0):
    r = Run(sim, name, t0); fn = FN.get(f"Trig_{name}_Actions") or (FN.get(name) if S2 else None)
    if fn is None: return
    blk, strs = parse(fn); q = [(0.0, 0)]; n = 0; seen_self = 0
    r.slots[("unit", 0)] = CASTER; r.slots[("unit", 1)] = TARGET
    # TV 유닛 슬롯은 보통 udg_Hero_*[0/2]=시전자, [1/3]=대상 — Setunit 문장에서 채워진다(Unk면 슬롯 0=시전자·1=대상 기본값)
    while q and n < 400:
        q.sort(key=lambda x: x[0]); ct, st = q.pop(0); n += 1
        if ct > 25 or len(sim.events) > 600: sim.truncated = True; break
        r.first = (n == 1); r.stage = st; r.cur_t = ct; r.pending = None; r.extra = []; sim.end_t = max(sim.end_t, t0 + ct)
        try: r.exec_block(blk, strs, ct)
        except Done: pass
        except LoopExit: pass
        if r.flushed: break
        if r.pending is not None:
            d, s = r.pending; q.append((r.cur_t + d, s))
        for d, s in r.extra:
            if (r.cur_t + d, s) not in q: q.append((r.cur_t + d, s))
    return r


def simulate(name, seed=5):
    sim = Sim(seed)
    # udg_Hero_*[짝수]=시전자, [홀수]=대상 기본 가정
    class Heroes(dict):
        pass
    for pre in re.findall(r"udg_\w*Hero_\w+", J):
        for i in range(8): sim.udg[f"{pre}[{i}]"] = CASTER if i % 2 == 0 else TARGET
    try: run_trigger(sim, name, 0.0)
    except RecursionError: sim.truncated = True
    return dict(events=sorted(sim.events, key=lambda e: e["t"]), unresolved=sim.unres, endT=round(sim.end_t, 3), truncated=sim.truncated)
