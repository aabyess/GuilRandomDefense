import re,os
os.chdir('/Users/sang/GitHub/GuilRandomDefense')
def rw(p,fn):
    s=open(p,encoding='utf-8').read(); n=fn(s); assert n!=s,p; open(p,'w',encoding='utf-8').write(n)
def sd(s):
    m=re.search(r'public enum SkillEffectKind\s*\{',s); i=m.end()
    depth=1; j=i
    while depth:
        c=s[j]; depth+= (c=='{')-(c=='}'); j+=1
    end=j-1
    add='''
    // ⚠️ 맨 뒤에 추가(2026-10-06, 초월 김만경 「빽」 — 사장님 확정) — 직렬화 순서를 지킨다.
    // 상위 유닛 개수 비례 **스플래시 피해** 강화(패시브, Self): 이 유닛 평타 광역(ApplyAttackSplash) 피해가 ×(1 + min(bonus, multiplier × 주인의 전설 이상 유닛 수))(소환수·초월위습 제외 — CountHighGradeUnits, 구일 공격 오라와 같은 기준).
    // 예: multiplier 0.02 · bonus 1.0 = 1기당 +2%·상한 +100%. 원작 근거 없음 — 제안값(사장님 확정).
    SkillDamagePerHighGradeUnit,
'''
    return s[:end].rstrip('\n')+'\n'+add+s[end:]
rw('Assets/Scripts/Data/SkillData.cs',sd)
def ua(s):
    s=s.replace('''    float SplashDamageFactor(UnitData unitData)
    {''','''    // 빽(SkillDamagePerHighGradeUnit) — 전설 이상 유닛 수 비례 스플래시 피해 배율. 개수는 1초 캐시(공격마다 전 유닛을 세지 않게).
    UnitData highGradeSplashFor;
    float highGradeSplashRate, highGradeSplashCap;
    float highGradeSplashCountedAt = -10f;
    int highGradeSplashCount;

    float HighGradeSplashFactor(UnitData unitData)
    {
        if (highGradeSplashFor != unitData)
        {
            highGradeSplashFor = unitData;
            highGradeSplashRate = 0f; highGradeSplashCap = 0f;
            int count = BaseSkillCount(unitData);
            for (int i = 0; i < count; i++)
            {
                SkillData skill = ResolveSkillAt(unitData, i);
                if (skill == null || skill.levels == null || skill.levels.Count == 0 || skill.levels[0].effects == null) continue;
                foreach (SkillEffect effect in skill.levels[0].effects)
                    if (effect != null && effect.kind == SkillEffectKind.SkillDamagePerHighGradeUnit)
                    { highGradeSplashRate += effect.multiplier; highGradeSplashCap = Mathf.Max(highGradeSplashCap, effect.bonus); }
            }
        }
        if (highGradeSplashRate <= 0f) return 1f;
        if (Time.time - highGradeSplashCountedAt >= 1f) { highGradeSplashCount = CountHighGradeUnits(); highGradeSplashCountedAt = Time.time; }
        float extra = highGradeSplashRate * highGradeSplashCount;
        if (highGradeSplashCap > 0f) extra = Mathf.Min(extra, highGradeSplashCap);
        return 1f + extra;
    }

    public float HighGradeSplashFactorNow => identity != null && identity.Data != null ? HighGradeSplashFactor(identity.Data) : 1f;   // 탐침용

    float SplashDamageFactor(UnitData unitData)
    {''',1)
    s=s.replace('float damage = AttackDamage * SplashDamageFactor(unitData);','float damage = AttackDamage * SplashDamageFactor(unitData) * HighGradeSplashFactor(unitData);',1)
    return s
rw('Assets/Scripts/Units/UnitAttacker.cs',ua)
print('patched')
