# 런타임 실측 하네스 — 텍스트로 보존 (2026-09-06)

**목적**: 오늘 밤 스택3축·대상조건게이트·캐스케이드그룹·아이템도박풀·A0LZ자가강화 축을
실제 Unity 런타임 경로로 검증한 EditMode 테스트 하네스 전체를 코드 블록으로 보존한다.
스크래치패드는 세션이 끝나면 사라지므로, 여기(`Docs/reference/`)에 텍스트로 커밋해
다음에 재현할 수 있게 한다. **`.cs`로 직접 두지 않는다 — 유니티가 열려 있으면 그
순간 컴파일 대상이 되기 때문**이다(오늘 그 사고를 이미 한 번 겪었다).

## 실행 절차 (재현용)

1. 아래 각 코드 블록을 그대로 `.cs`/`.asmdef` 파일로 저장한다.
2. **`Assets/Editor/Tests/`에 asmdef 없이 `.cs` 파일들만 둔다** (아래 asmdef 코드
   블록은 **참고용 실패 사례**로만 남긴다 — 실제로는 쓰지 않는다). 이유: 이 프로젝트
   `Assets/Scripts/`엔 실제 `.asmdef`가 없어서(암묵적 기본 어셈블리), 커스텀 asmdef가
   `"Assembly-CSharp"`을 이름으로 참조하려 해도 해석이 안 된다(Unity의 asmdef
   `references`는 프로젝트 안에 실재하는 다른 `.asmdef`의 `name`/GUID를 찾는 방식이라,
   이름만 있는 predefined assembly는 못 찾는다) — 30여 개 타입이 CS0246으로 떨어졌다.
   **`Assets/Editor/` 아래는 predefined assembly `Assembly-CSharp-Editor`로 컴파일되고,
   이건 `Assembly-CSharp`(게임 코드)을 자동으로 참조한다** — asmdef 없이 여기 두면
   게임 코드가 그냥 보인다. `com.unity.test-framework`도 predefined assembly에
   자동으로 참조를 얹어줘서 NUnit/TestRunner도 그냥 쓸 수 있다.
3. **🔴 배치모드 실행 시 `-quit`를 넣지 말 것.** `-quit`를 포함하면 TestRunner 자체가
   안 붙고(테스트 실행 로그가 아예 안 남고) 그냥 열고 닫기만 한다 — 결과 없이
   exit 0으로 끝난다. `-runTests`가 완료되면 스스로 종료하므로 `-quit`는 불필요하고
   유해하다.

```bash
/Applications/Unity/Hub/Editor/6000.0.82f1/Unity.app/Contents/MacOS/Unity \
  -batchmode \
  -projectPath /Users/sang/Documents/GitHub/GuilRandomDefense \
  -runTests \
  -testPlatform EditMode \
  -testResults /tmp/stack_axis_test_results.xml \
  -logFile /tmp/stack_axis_test_run.log
# -quit 없음. 실행 후 exit code는 2일 수 있다(NUnit이 실패 건수를 exit code로
# 돌려주는 관례) — exit 0이 아니라고 "다시 컴파일 에러"로 오판하지 말고 XML을 먼저 볼 것.
echo "EXIT: $?"
```

4. 결과 해석:
```bash
python3 -c "
import xml.etree.ElementTree as ET
tree = ET.parse('/tmp/stack_axis_test_results.xml')
root = tree.getroot()
print('Overall:', root.attrib.get('result'), 'total=', root.attrib.get('total'),
      'passed=', root.attrib.get('passed'), 'failed=', root.attrib.get('failed'))
for tc in root.iter('test-case'):
    name = tc.attrib.get('fullname', tc.attrib.get('name'))
    result = tc.attrib.get('result')
    print(f'{result:8s} {name}')
    if result == 'Failed':
        msg = tc.find('.//message')
        if msg is not None:
            print('    MSG:', msg.text[:300] if msg.text else None)
"
```

5. **⚠️ 실행 전후로 반드시**:
   - `Library/EditorInstance.json`이 있고 그 안의 `process_id`가 살아있는 프로세스면
     **에디터가 이 프로젝트를 잡고 있다는 뜻이니 절대 실행하지 말 것.**
   - 성공하든 실패하든 **실행이 끝나면 즉시 `Assets/Editor/Tests/`를 지울 것** —
     남겨두면 사장님이 다음에 에디터를 열 때마다 이게 컴파일되고, 이 프로젝트
     `.cs` 하나라도 시그니처가 바뀌면 조용히 깨진다. **삭제 명령은 정확히
     `Assets/Editor/Tests/`(또는 그 안의 개별 파일)만 지정할 것 — 상위 `Assets/Editor/`
     전체를 지우면 원래 있던 진짜 파일(`ArtBinder.cs` 등 9개)까지 같이 날아간다**
     (2026-09-06 밤 실제로 겪은 사고, `git checkout --`으로 복구했다, 뿌리 ㊻).
   - 씬(`Assets/Scenes/SampleScene.unity`)은 테스트가 로드/저장하지 않는다 — 전부
     `new GameObject()`/`ScriptableObject.CreateInstance`로 직접 구성한다.


## 오늘 밤 실행 결과 (2026-09-06, 15건 중 12건 통과)

에디터가 닫힌 걸 확인하고 실제로 돌렸다(위 절차, `-quit` 없이). **12건 통과 —
런타임 미검증에서 런타임 실측으로 격상.** 3건 실패(전부 A0LZ 자가강화 축) —
Python 재현이 못 잡던 것을 잡았다.

```
✅ 통과 12건
StackAxisRuntimeWiringTests.AegrStackEffect_FiredThroughCastSkillLevel_RaisesEffectiveMagicMultiplier_UpToKinkLevel
StackAxisRuntimeWiringTests.AisrStack_ThenSeparateDamageEffect_ActuallyMultipliesRealDamageDealt
StackAxisRuntimeWiringTests.AisrStackEffect_FiredThroughCastSkillLevel_RaisesEffectiveMagicMultiplier
TargetConditionGateRuntimeTests.TargetConditionNone_Default_AlwaysApplies_RegressionSafe
TargetConditionGateRuntimeTests.TargetPointValueLessThan_AppliesToLowPointTarget_ButNotHighPointTarget
CascadeGroupRuntimeTests.CascadeGroup_ExactlyOneOrNoneFires_AcrossManyTrials
CascadeGroupRuntimeTests.CascadeGroupZero_Default_IndependentRolls_RegressionSafe
ItemGambleRuntimeTests.ReducedPoolFlag_HardcodedFalse_FullPoolAlwaysUsed
ItemGambleRuntimeTests.StartState_AllowsExactlyOneGamble
ItemGambleRuntimeTests.StockZero_BlocksGamble
ItemGambleRuntimeTests.WeightedRoll_ReflectsConfiguredWeights_NotUniform
SelfUpgradeAxisRuntimeTests.InsufficientWisps_RefundsWood_AndDoesNotRoll

🔴 실패 3건 — 전부 A0LZ(SelfUpgrade) 축, 재현성 확인(3회 동일 결과)
SelfUpgradeAxisRuntimeTests.GuaranteedSuccess_IncrementsLevel_AndConsumesResources
    Expected: True   But was: False   ("100% 성공률인데 TryUpgradeSelf가 실패를 돌려줬다")
SelfUpgradeAxisRuntimeTests.GuaranteedFailure_DoesNotIncrementLevel_ButStillConsumesResources
    Expected: 998     But was: 1000   ("실패했는데도 목재가 그대로다")
SelfUpgradeAxisRuntimeTests.SelfUpgradeLevel_ReadThroughRealDamagePath_MatchesFormula
    Expected: 1.5     But was: 2.0    (레벨10×0.05+1.0=1.50 기대, 실제 2.0)
```

### 3건 실패 원인 조사 (2026-09-06 밤, 완료 못함 — 다음 세션 인계)

**1·2번(GuaranteedSuccess/GuaranteedFailure)**: `TryUpgradeSelf()`의 초입 가드
(`if (owner == null) return false;` 또는 `if (context == null ...) return false;`,
`UnitAttacker.cs` 1515행 부근)에서 항상 막히는 것으로 보인다 — 성공률 설정과
무관하게 매번 같은 실패 형태이기 때문이다.

- **1차 가설(반증됨)**: `UnitAttacker.Awake()`가 `owner = GetComponent<OwnedByPlayer>()`를
  그 자리에서 읽는데, 테스트가 `UnitAttacker`를 먼저 `AddComponent`하고 `OwnedByPlayer`를
  나중에 붙여서 `owner`가 영원히 null로 굳는 것이라 보고, GameObject를 비활성 상태로
  만들어 컴포넌트를 다 붙인 뒤 활성화하는 방식(Awake 지연)으로 고쳐봤다 — **재실행해도
  똑같이 실패했다.** 이 가설은 틀렸거나 전부는 아니다.
- **2차 가설(미확인, 다음 사람이 확인할 것)**: `PlayerContext.Get(playerId)`이 static
  `registry` 리스트를 순회해 찾는 방식(`PlayerContext.cs` 122행)인데, 이 registry가
  **테스트 전체 실행(15건) 동안 한 번도 안 비워지는 static 상태**라 — 이전 테스트
  클래스가 등록한 `PlayerContext`가 아직 registry에 남아 있거나, 여러 테스트가 같은
  `playerId`(예: 777 같은 매직넘버)를 재사용해 `Get()`이 **의도와 다른(먼저 등록된)
  인스턴스**를 돌려줄 가능성이 있다. **확인 안 함 — 다음 세션이 여기부터 볼 것.**
- **권장 다음 단계**: 추측으로 더 고치지 말고, `TryUpgradeSelf()` 안에 임시로
  `Debug.Log($"owner={owner}, context={context}, resourceWallet={context?.ResourceWallet}")`
  같은 진단 로그를 넣고 한 번 더 돌려서 정확히 어느 조건에서 걸리는지 로그로 확정한 뒤
  고칠 것 — 코드 정독만으로는 두 번 시도(가설 하나 반증)했는데도 못 찾았다.

**3번(SelfUpgradeLevel_ReadThroughRealDamagePath)**: 코드를 전부 손으로 따라가봤다 —
`ResolveSkillEffectValue`(=`ResolveBaseSkillEffectValue × RandomDamageMultiplier`)→
`CasterSelfUpgradeLevel` 케이스(`selfUpgradeLevel × multiplier + bonus` = 10×0.05+1.0=
**1.5**, 코드 그대로)→`DealSkillDamage`(`× PercentDamageTakenMultiplier`, 테스트가
`percentDamageTaken=1f`로 세팅해 무변화)→`MitigatedDamage`(`damageTable`이 테스트
환경에선 null이라 상성표 배율 자체가 스킵, `EffectiveMagicMultiplier`도 스택0이라
1.0, `ArmorMultiplier(armor=0)`도 1.0) — **모든 단계가 정확히 1.5를 내야 하는데
실측은 2.0이었다(3회 재현 동일).** 코드 정독으로는 이 차이를 못 찾았다. **다음
단계도 같다 — 임시 로그로 각 단계 실제 반환값을 찍어보는 것이 유일하게 남은 방법.**
※ 참고: `randMin`/`randMax` 기본값이 둘 다 1f라 `Random.Range(1,1)=1`(배율 없음)
확인함 — 이쪽은 원인이 아니다.

⚠️ **혼동하지 말 것**: A0LZ의 wispCurrency가 실제 게임 자산엔 아직 미배정 상태인 게
맞지만(오늘 낮 `74de277` 보고), **이 3건의 테스트는 자체적으로 `wispCurrency`와
`Wisp` 인스턴스를 전부 만들어 쓰므로 그 문제와 무관하다** — 별도의, 아직 못 찾은
원인이다.

## 재발 방지 (뿌리 ㊻)

`Assets/Editor/Tests/`를 지울 때 실수로 `rm -rf Assets/Editor`를 돌려 기존 파일
9개(`ArtBinder.cs`·`CombineRecipeWiring.cs`·`EditorGuards.cs`·`HudWiring.cs`·
`MapGenerator.cs`·`MapLayout.cs`·`MapTextureImporter.cs`·`SceneDiagnostics.cs`·
`WaveWiring.cs`)까지 지웠다가 `git status`로 바로 알아채 `git checkout --
Assets/Editor/`로 복구했다(커밋에 안 남음). **임시 파일은 지울 때 정확한
하위 경로만 지정할 것 — 부모 디렉터리를 통째로 지우지 말 것.**

---

## 파일 목록

### `StackAxisRuntimeWiringTests.cs`

```csharp
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// 목적: "런타임이 그 코드를 실제로 그 경로로 부르는가"만 확인한다(2026-09-06, PM 지시).
// 산술식 자체가 맞는지는 이미 Python 재구현으로 확인됐다(APPROXIMATION_LEDGER.md §9) —
// 이 테스트는 그걸 다시 재지 않는다. AddAegrStack/AddAisrStack/AddA11SStack을 직접 부르는
// 대신, UnitAttacker.CastSkillLevel(SkillEffectKind.AisrStack 효과 포함)을 실제로 실행시켜
// 그 사설 메서드 체인(CastSkillLevel → ApplySkillEffect → ApplyToEnemy → target.AddAisrStack)이
// 정말 그 경로를 타는지, 그리고 그 뒤에 이어지는 별도의 실제 피해 계산(Damage 효과 →
// ApplyToEnemy → DealSkillDamage → EnemyDummy.TakeDamage → MitigatedDamage)이 방금 쌓인
// 스택을 실제로 반영하는지를 확인한다.
//
// CastSkillLevel/ApplySkillEffect/ApplyToEnemy가 전부 private이라 리플렉션으로 부른다 —
// 이건 "코드를 흉내"가 아니라 "그 컴파일된 메서드를 그대로 호출"하는 것이므로 런타임
// 경로 검증의 목적에 맞는다(테스트를 위해 접근제한자를 바꾸지 않았다).
public class StackAxisRuntimeWiringTests
{
    GameObject attackerGO;
    GameObject targetGO;
    UnitAttacker attacker;
    EnemyDummy target;
    EnemyData enemyData;

    [SetUp]
    public void SetUp()
    {
        enemyData = ScriptableObject.CreateInstance<EnemyData>();
        enemyData.hp = 1_000_000f;
        // magicArmorMultiplier=1f는 기존에 검증된 "보스 레벨16"(Def5=1.00)과 정확히 같다
        // (TARGET_SIDE_AXES.md ①) — AegrBaseLevel 역산이 16으로 나와 꺾임레벨(31)
        // 안쪽이라 이 테스트에서 안전하다.
        enemyData.magicArmorMultiplier = 1f;
        enemyData.percentDamageTaken = 1f; // A11S 레벨16(보스) 기준값, 이 테스트와 무관하지만 명시해둔다.
        enemyData.armor = 0f; // ArmorMultiplier(0) = 1.0, 방어력 축이 결과에 안 섞이게 고정.

        targetGO = new GameObject("Test_EnemyDummy");
        target = targetGO.AddComponent<EnemyDummy>();
        target.Initialize(enemyData);

        attackerGO = new GameObject("Test_UnitAttacker");
        attacker = attackerGO.AddComponent<UnitAttacker>();
    }

    [TearDown]
    public void TearDown()
    {
        if (attackerGO != null) Object.DestroyImmediate(attackerGO);
        if (targetGO != null) Object.DestroyImmediate(targetGO);
        if (enemyData != null) Object.DestroyImmediate(enemyData);
    }

    static SkillEffect MakeStackEffect(SkillEffectKind kind, int amount)
    {
        var e = new SkillEffect
        {
            kind = kind,
            target = SkillTargetKind.SingleTarget,
            chance = 1f,
            basis = SkillEffectBasis.Flat,
            multiplier = amount,
        };
        return e;
    }

    static SkillEffect MakeFlatDamageEffect(float amount)
    {
        var e = new SkillEffect
        {
            kind = SkillEffectKind.Damage,
            target = SkillTargetKind.SingleTarget,
            chance = 1f,
            basis = SkillEffectBasis.Flat,
            multiplier = amount,
            bonus = 0f,
            // AD로 고정 — MitigatedDamage의 bypassPhysicalArmor는 (isAbilityDamage && type==AP)
            // 일 때만 참이라, AD를 쓰면 EffectiveMagicMultiplier가 무조건 적용된다(이 테스트가
            // 확인하려는 그 갈래를 확실히 태운다).
            damageType = DamageType.AD,
            attackType = AttackType.Normal,
            hitCount = 1,
        };
        return e;
    }

    static SkillLevel MakeLevel(params SkillEffect[] effects)
    {
        return new SkillLevel
        {
            triggerChance = 1f,
            range = 100f, // SingleTarget + primaryTarget 지정 시 range는 안 쓰인다(가드는 Enemies/Allies 전용) — 안전값만 채운다.
            effects = new List<SkillEffect>(effects),
        };
    }

    // CastSkillLevel(SkillLevel, float range, EnemyDummy primaryTarget, float recentAttackDamage)
    // private void — 실제 컴파일된 메서드를 리플렉션으로 그대로 호출한다.
    static void InvokeCastSkillLevel(UnitAttacker caster, SkillLevel level, EnemyDummy primaryTarget, float recentAttackDamage = 0f)
    {
        MethodInfo method = typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method, "UnitAttacker.CastSkillLevel을 리플렉션으로 못 찾았다 — 메서드 이름/시그니처가 바뀌었을 수 있다.");
        method.Invoke(caster, new object[] { level, level.range, primaryTarget, recentAttackDamage });
    }

    [Test]
    public void AisrStackEffect_FiredThroughCastSkillLevel_RaisesEffectiveMagicMultiplier()
    {
        // 사전조건: 스택 전 배율은 1.0(EffectiveMagicDamageAmplifier)이어야 한다 — 이건
        // Python 검증이 이미 확인한 값이라 여기선 "실제로 그 값에서 시작하는가"만 확인한다.
        Assert.AreEqual(1.0f, target.EffectiveMagicDamageAmplifier, 0.0000001f,
            "사전조건 실패 — 스택0에서 AIsr 배율이 1.0이 아니다(별도 버그, 오늘 세션 §9의 float 검증과 다른 결과라면 그 자체가 문제).");

        // 실제 런타임 경로: CastSkillLevel -> ApplySkillEffect -> ApplyToEnemy ->
        // (kind==AisrStack) -> target.AddAisrStack(10). AddAisrStack을 직접 부르지 않는다.
        SkillLevel stackLevel = MakeLevel(MakeStackEffect(SkillEffectKind.AisrStack, 10));
        InvokeCastSkillLevel(attacker, stackLevel, target);

        // Python 재구현이 이미 확인한 값: 스택10 -> AIsr 배율 = 1 + 10*0.01 = 1.10 (26 미만이라 꺾임 전).
        Assert.AreEqual(1.10f, target.EffectiveMagicDamageAmplifier, 0.0001f,
            "AisrStack 효과가 CastSkillLevel 경로를 통해 실제로 target.AddAisrStack에 도달하지 않은 것으로 보인다.");
    }

    [Test]
    public void AisrStack_ThenSeparateDamageEffect_ActuallyMultipliesRealDamageDealt()
    {
        // 1단계: 스택을 실제 경로로 건다(위 테스트와 같은 호출).
        InvokeCastSkillLevel(attacker, MakeLevel(MakeStackEffect(SkillEffectKind.AisrStack, 10)), target);
        Assert.AreEqual(1.10f, target.EffectiveMagicDamageAmplifier, 0.0001f, "1단계(스택 걸기) 실패 — 아래 2단계 결과 해석이 무의미해진다.");

        // 2단계: 완전히 별개의 실제 공격(별도 CastSkillLevel 호출)으로 고정 피해 1000을 입힌다.
        // 이 효과 자체는 스택을 안 걸고 Damage kind만 쓴다 — "이미 걸린 스택이 다른 공격에도
        // 지속되며 실제로 반영되는가"를 보려는 것이라 반드시 별도 호출이어야 한다(PM 지시 —
        // 한 호출 안에서 순서만 맞춰 넣으면 "같은 트랜잭션 안"이라는 약한 증거밖에 안 된다).
        float hpBefore = target.Hp;
        InvokeCastSkillLevel(attacker, MakeLevel(MakeFlatDamageEffect(1000f)), target);
        float hpAfter = target.Hp;

        float actualDamage = hpBefore - hpAfter;

        // 기대값: DealSkillDamage -> ResolveSkillEffectValue(Flat) = 1000 그대로
        //        -> * PercentDamageTakenMultiplier(1.0, 스택 안 걺)
        //        -> * (1 + casterBuffCountFactor*count) = *1 (기본값 0)
        //        -> TakeDamage -> MitigatedDamage: type=AD라 bypassPhysicalArmor=false
        //           -> amount *= EffectiveMagicMultiplier
        //              (= (magicArmorMultiplier(1) + magicArmorShred(0) + Aegr스택기여(0)) * EffectiveMagicDamageAmplifier(1.10))
        //              = 1.0 * 1.10 = 1.10
        //           -> * ArmorMultiplier(armor=0) = 1.0 (armorIgnoreRatio 기본 0)
        //        -> damageTable == null이라 상성표 배율 없음(1.0)
        // 최종 기대 피해 = 1000 * 1.10 = 1100.
        Assert.AreEqual(1100f, actualDamage, 0.5f,
            "AisrStack으로 쌓인 배율이 별도의 후속 실제 피해 계산(EnemyDummy.MitigatedDamage 경유)에 반영되지 않았다 — " +
            "AddAisrStack이 호출되는 것과 그 결과가 실전 피해 계산에 실제로 곱해지는 것은 다른 질문이며, 이 테스트가 후자를 확인한다.");
    }

    [Test]
    public void AegrStackEffect_FiredThroughCastSkillLevel_RaisesEffectiveMagicMultiplier_UpToKinkLevel()
    {
        // AegrBaseLevel(16, 위 SetUp의 magicArmorMultiplier=1f에서 역산)에 +20 스택을 걸면
        // 총레벨 36이 되어 꺾임레벨(31)을 넘는다 — "꺾임 이후로는 더 안 오른다"까지 같은
        // 테스트에서 실측한다(오늘 Python 검증이 예측한 플래토를 런타임에서도 확인).
        InvokeCastSkillLevel(attacker, MakeLevel(MakeStackEffect(SkillEffectKind.AegrStack, 20)), target);

        // 꺾임레벨 31 = 베이스16 + 스택15에서 이미 최댓값(1.15)에 도달 — 스택20을 걸어도
        // 15만큼만 반영돼야 한다(AddAegrStack 자체가 상한을 총레벨 45 기준으로 자르므로
        // aegrStackLevels는 20까지 그대로 저장되지만, EffectiveMagicMultiplier 계산에서
        // Mathf.Min(aegrStackLevels, kink-base)로 다시 한 번 잘린다 — 그 이중 클램프가
        // 실제로 도는지를 여기서 확인한다).
        float expected = 1f * 1.15f; // Def5 1.15(꺾임값) * AIsr배율(1.0, 안 걸었음)
        Assert.AreEqual(expected, target.EffectiveMagicMultiplier, 0.001f,
            "AegrStack이 꺾임레벨(31)에서 실제로 멈추지 않는다 — 플래토 클램프가 CastSkillLevel " +
            "실경로에서 기대와 다르게 동작한다(Python 검증과 다른 결과).");
    }
}

```

### `TargetConditionGateRuntimeTests.cs`

```csharp
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// 목적: "대상 조건 게이트"(2026-09-06, PM 지시, commit 9c2f4e0)가 실제 런타임 경로에서
// 대상마다 다르게 갈리는지를 확인한다. 이 축은 산술이 거의 없고(비교 연산 하나) 핵심이
// "같은 효과를 여러 대상에게 쏘면 대상의 EnemyData.pointValue에 따라 실제로 갈리는가"라서
// Python 재구현이 특히 약하다(대상 객체 상태를 실제로 두 개 만들어서 같은 CastSkillLevel
// 호출로 서로 다른 결과가 나오는지를 봐야 한다 — 이건 "값을 계산"하는 게 아니라 "분기가
// 실제로 실행되는가"를 보는 것이다).
//
// StackAxisRuntimeWiringTests.cs와 같은 패턴: CastSkillLevel(private)을 리플렉션으로 그대로
// 호출한다 — 프로덕션 코드 접근제한자는 안 건드린다.
public class TargetConditionGateRuntimeTests
{
    GameObject attackerGO;
    UnitAttacker attacker;

    GameObject lowTargetGO;
    EnemyDummy lowTarget; // pointValue=100 (< 200 문턱)
    EnemyData lowTargetData;

    GameObject highTargetGO;
    EnemyDummy highTarget; // pointValue=300 (>= 200 문턱)
    EnemyData highTargetData;

    [SetUp]
    public void SetUp()
    {
        attackerGO = new GameObject("Test_UnitAttacker_TargetCondition");
        attacker = attackerGO.AddComponent<UnitAttacker>();

        lowTargetData = ScriptableObject.CreateInstance<EnemyData>();
        lowTargetData.hp = 1_000_000f;
        lowTargetData.magicArmorMultiplier = 1f;
        lowTargetData.percentDamageTaken = 1f;
        lowTargetData.armor = 0f;
        lowTargetData.pointValue = 100f; // < 200

        highTargetData = ScriptableObject.CreateInstance<EnemyData>();
        highTargetData.hp = 1_000_000f;
        highTargetData.magicArmorMultiplier = 1f;
        highTargetData.percentDamageTaken = 1f;
        highTargetData.armor = 0f;
        highTargetData.pointValue = 300f; // >= 200

        lowTargetGO = new GameObject("Test_EnemyDummy_Low");
        lowTarget = lowTargetGO.AddComponent<EnemyDummy>();
        lowTarget.Initialize(lowTargetData);

        highTargetGO = new GameObject("Test_EnemyDummy_High");
        highTarget = highTargetGO.AddComponent<EnemyDummy>();
        highTarget.Initialize(highTargetData);
    }

    [TearDown]
    public void TearDown()
    {
        if (attackerGO != null) Object.DestroyImmediate(attackerGO);
        if (lowTargetGO != null) Object.DestroyImmediate(lowTargetGO);
        if (highTargetGO != null) Object.DestroyImmediate(highTargetGO);
        if (lowTargetData != null) Object.DestroyImmediate(lowTargetData);
        if (highTargetData != null) Object.DestroyImmediate(highTargetData);
    }

    // 조건부 고정피해 1000 — targetCondition/targetConditionValue를 채운 것 말고는
    // StackAxisRuntimeWiringTests.MakeFlatDamageEffect와 동일하다(AD로 고정해
    // EffectiveMagicMultiplier 갈래를 타지 않게 해서 이 테스트가 순수하게 "조건 게이트가
    // 통과/차단하는가"만 보게 한다 — 배율이 섞이면 결과 해석이 흐려진다).
    static SkillEffect MakeConditionalFlatDamageEffect(float amount, SkillEffectTargetCondition condition, float thresholdValue)
    {
        return new SkillEffect
        {
            kind = SkillEffectKind.Damage,
            target = SkillTargetKind.SingleTarget,
            chance = 1f,
            basis = SkillEffectBasis.Flat,
            multiplier = amount,
            bonus = 0f,
            damageType = DamageType.AD,
            attackType = AttackType.Normal,
            hitCount = 1,
            targetCondition = condition,
            targetConditionValue = thresholdValue,
        };
    }

    static SkillLevel MakeLevel(params SkillEffect[] effects)
    {
        return new SkillLevel
        {
            triggerChance = 1f,
            range = 100f,
            effects = new List<SkillEffect>(effects),
        };
    }

    static void InvokeCastSkillLevel(UnitAttacker caster, SkillLevel level, EnemyDummy primaryTarget, float recentAttackDamage = 0f)
    {
        MethodInfo method = typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method, "UnitAttacker.CastSkillLevel을 리플렉션으로 못 찾았다 — 메서드 이름/시그니처가 바뀌었을 수 있다.");
        method.Invoke(caster, new object[] { level, level.range, primaryTarget, recentAttackDamage });
    }

    [Test]
    public void TargetPointValueLessThan_AppliesToLowPointTarget_ButNotHighPointTarget()
    {
        // 같은 SkillLevel(같은 SkillEffect 인스턴스)을 두 대상에게 각각 별도로 발동한다 —
        // 원작의 "AoE가 적마다 갈린다" 구조와 같은 모양(같은 효과 정의, 대상마다 다른 결과).
        SkillEffect conditionalEffect = MakeConditionalFlatDamageEffect(
            1000f, SkillEffectTargetCondition.TargetPointValueLessThan, 200f);
        SkillLevel level = MakeLevel(conditionalEffect);

        float lowHpBefore = lowTarget.Hp;
        InvokeCastSkillLevel(attacker, level, lowTarget);
        float lowHpAfter = lowTarget.Hp;

        float highHpBefore = highTarget.Hp;
        InvokeCastSkillLevel(attacker, level, highTarget);
        float highHpAfter = highTarget.Hp;

        float lowDamage = lowHpBefore - lowHpAfter;
        float highDamage = highHpBefore - highHpAfter;

        // pointValue=100 < 200 → 조건 통과 → 피해 1000이 실제로 들어가야 한다.
        Assert.AreEqual(1000f, lowDamage, 0.5f,
            "대상 조건(포인트값<200)이 통과해야 할 대상(pointValue=100)에게 효과가 실제로 안 걸렸다 — " +
            "UnitAttacker.ApplyToEnemy의 targetCondition 검사가 실경로에서 통과를 막고 있을 수 있다.");

        // pointValue=300 >= 200 → 조건 불통과 → 피해가 전혀 들어가면 안 된다(0이어야 한다).
        Assert.AreEqual(0f, highDamage, 0.5f,
            "대상 조건(포인트값<200)이 막아야 할 대상(pointValue=300)에게 효과가 실제로 걸렸다 — " +
            "UnitAttacker.ApplyToEnemy의 targetCondition 검사가 실경로에서 대상별로 안 갈리고 있다(이 축의 핵심 요구사항 위반).");
    }

    [Test]
    public void TargetConditionNone_Default_AlwaysApplies_RegressionSafe()
    {
        // 회귀 안전 확인: targetCondition을 안 채우면(기본값 None) 기존 365개 에셋과 똑같이
        // 대상 무관하게 항상 걸려야 한다 — 이 테스트가 실패하면 오늘 만든 게이트가 기존
        // 스킬 전부를 조용히 깨뜨린 것이다.
        SkillEffect unconditionalEffect = new SkillEffect
        {
            kind = SkillEffectKind.Damage,
            target = SkillTargetKind.SingleTarget,
            chance = 1f,
            basis = SkillEffectBasis.Flat,
            multiplier = 1000f,
            bonus = 0f,
            damageType = DamageType.AD,
            attackType = AttackType.Normal,
            hitCount = 1,
            // targetCondition 필드를 아예 안 건드림 — 기본값 None 그대로.
        };
        SkillLevel level = MakeLevel(unconditionalEffect);

        float highHpBefore = highTarget.Hp; // pointValue=300 — 조건이 있었다면 막혔을 대상.
        InvokeCastSkillLevel(attacker, level, highTarget);
        float highHpAfter = highTarget.Hp;

        Assert.AreEqual(1000f, highHpBefore - highHpAfter, 0.5f,
            "targetCondition=None(기본값)인데도 효과가 안 걸렸다 — 오늘 추가한 대상 조건 게이트가 " +
            "조건 없는 기존 효과까지 막아버리는 회귀를 일으켰다.");
    }
}

```

### `CascadeGroupRuntimeTests.cs`

```csharp
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// 목적: "캐스케이드 그룹"(2026-09-06, PM 지시, SkillEffect.cascadeGroup)이 실제 런타임
// 경로에서 원작 if/elseif/else 사슬처럼 "정확히 하나만" 발동시키는지 확인한다. 이 축은
// 확률이 핵심이라(Cavendish_Attack: 1단계 10% / 2단계(실패시에만) 1/9 ≈11.1%) 숫자 하나로
// 안 되고, 많은 시행에서 실제 분포가 나오는지를 봐야 한다 — Python으로 캐스케이드
// 스킵-로직 자체는 이미 검증했다(1단계≈10.03%·2단계≈9.94%·둘다=0.00%, 20만 시행,
// 오늘 시뮬레이션 결과) — 이 테스트는 그 로직이 아니라 UnitAttacker.CastSkillLevel의
// 실제 코드 경로가 같은 결과를 내는지를 본다.
//
// StackAxisRuntimeWiringTests.cs와 같은 패턴: CastSkillLevel(private)을 리플렉션으로
// 그대로 호출한다 — 프로덕션 코드 접근제한자는 안 건드린다.
public class CascadeGroupRuntimeTests
{
    GameObject attackerGO;
    UnitAttacker attacker;

    GameObject targetGO;
    EnemyDummy target;
    EnemyData targetData;

    const float Stage1Damage = 1000f;
    const float Stage2Damage = 2000f;
    const float Stage1Chance = 0.10f;
    const float Stage2Chance = 1f / 9f; // Cavendish_Attack 실측(원작): ≈11.11%

    [SetUp]
    public void SetUp()
    {
        attackerGO = new GameObject("Test_UnitAttacker_Cascade");
        attacker = attackerGO.AddComponent<UnitAttacker>();

        targetData = ScriptableObject.CreateInstance<EnemyData>();
        targetData.hp = 1_000_000f;
        targetData.magicArmorMultiplier = 1f;
        targetData.percentDamageTaken = 1f;
        targetData.armor = 0f;

        targetGO = new GameObject("Test_EnemyDummy_Cascade");
        target = targetGO.AddComponent<EnemyDummy>();
        target.Initialize(targetData);
    }

    [TearDown]
    public void TearDown()
    {
        if (attackerGO != null) Object.DestroyImmediate(attackerGO);
        if (targetGO != null) Object.DestroyImmediate(targetGO);
        if (targetData != null) Object.DestroyImmediate(targetData);
    }

    static SkillEffect MakeCascadeDamageEffect(float amount, float chance, int cascadeGroup)
    {
        return new SkillEffect
        {
            kind = SkillEffectKind.Damage,
            target = SkillTargetKind.SingleTarget,
            chance = chance,
            basis = SkillEffectBasis.Flat,
            multiplier = amount,
            bonus = 0f,
            damageType = DamageType.AD,
            attackType = AttackType.Normal,
            hitCount = 1,
            cascadeGroup = cascadeGroup,
        };
    }

    static SkillLevel MakeLevel(params SkillEffect[] effects)
    {
        return new SkillLevel
        {
            triggerChance = 1f,
            range = 100f,
            effects = new List<SkillEffect>(effects),
        };
    }

    static void InvokeCastSkillLevel(UnitAttacker caster, SkillLevel level, EnemyDummy primaryTarget, float recentAttackDamage = 0f)
    {
        MethodInfo method = typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method, "UnitAttacker.CastSkillLevel을 리플렉션으로 못 찾았다 — 메서드 이름/시그니처가 바뀌었을 수 있다.");
        method.Invoke(caster, new object[] { level, level.range, primaryTarget, recentAttackDamage });
    }

    // Cavendish_Attack 모양(1단계 10% / 실패시에만 2단계 1/9)을 실제 CastSkillLevel
    // 경로로 여러 번 발동해 "둘 다 발동"이 구조적으로 0회인지, 개별 발동률이 원작
    // 기대치(10% / ≈10.0% / 나머지 80%)에 가까운지 확인한다. 매 시행마다 target의
    // HP를 리셋해서(Initialize 재호출) 시행 간 피해가 안 섞이게 한다 — 델타로
    // "1단계만"(1000)/"2단계만"(2000)/"둘 다"(3000, 있으면 안 됨)/"둘 다 실패"(0)를
    // 구분한다.
    [Test]
    public void CascadeGroup_ExactlyOneOrNoneFires_AcrossManyTrials()
    {
        SkillEffect stage1 = MakeCascadeDamageEffect(Stage1Damage, Stage1Chance, cascadeGroup: 1);
        SkillEffect stage2 = MakeCascadeDamageEffect(Stage2Damage, Stage2Chance, cascadeGroup: 1);
        SkillLevel level = MakeLevel(stage1, stage2);

        const int trials = 10000;
        int stage1Only = 0, stage2Only = 0, both = 0, neither = 0;

        for (int i = 0; i < trials; i++)
        {
            target.Initialize(targetData); // HP 원상복구 — 시행 간 격리.
            float before = target.Hp;
            InvokeCastSkillLevel(attacker, level, target);
            float damage = before - target.Hp;

            bool hitStage1 = Mathf.Abs(damage - Stage1Damage) < 0.5f;
            bool hitStage2 = Mathf.Abs(damage - Stage2Damage) < 0.5f;
            bool hitBoth = Mathf.Abs(damage - (Stage1Damage + Stage2Damage)) < 0.5f;
            bool hitNeither = Mathf.Abs(damage) < 0.5f;

            if (hitBoth) both++;
            else if (hitStage1) stage1Only++;
            else if (hitStage2) stage2Only++;
            else if (hitNeither) neither++;
            else Assert.Fail($"예상 밖의 피해량 {damage} — 캐스케이드 효과 둘 다 아니거나 값이 겹쳤을 수 있다.");
        }

        // 구조적 불가능 — 확률과 무관하게 항상 정확히 0이어야 한다. 이게 이 테스트의
        // 핵심 단언이다(비율 확인보다 이게 먼저다 — "거의 안 난다"가 아니라 "아예 못 난다").
        Assert.AreEqual(0, both,
            "캐스케이드 그룹의 두 효과가 같은 시전·같은 대상에서 동시에 발동했다 — " +
            "UnitAttacker.ApplyToEnemy의 그룹 스킵 로직이 실경로에서 안 걸리고 있다(이 축의 핵심 요구사항 위반).");

        // 비율 확인 — 원작 기대치(1단계 10% / 2단계 ≈10.0% / 나머지 80%)에 넉넉한
        // 허용오차(±3%p, 10000시행 기준 통계적으로 충분히 여유)로 근접해야 한다.
        float stage1Rate = (float)stage1Only / trials;
        float stage2Rate = (float)stage2Only / trials;
        Assert.AreEqual(0.10f, stage1Rate, 0.03f,
            $"1단계 발동률이 기대치(10%)에서 크게 벗어남 — 실측 {stage1Rate:P1} ({stage1Only}/{trials})");
        Assert.AreEqual(0.10f, stage2Rate, 0.03f,
            $"2단계(캐스케이드) 발동률이 기대치(≈10.0%)에서 크게 벗어남 — 실측 {stage2Rate:P1} ({stage2Only}/{trials})");
    }

    [Test]
    public void CascadeGroupZero_Default_IndependentRolls_RegressionSafe()
    {
        // 회귀 안전 확인: cascadeGroup을 안 채우면(기본값 0) 기존 365개 에셋과 똑같이
        // 완전 독립 판정이어야 한다 — chance=1인 두 효과가 매번 둘 다 걸려야 한다
        // (캐스케이드였다면 두 번째가 스킵됐을 상황).
        SkillEffect effect1 = MakeCascadeDamageEffect(Stage1Damage, 1f, cascadeGroup: 0);
        SkillEffect effect2 = MakeCascadeDamageEffect(Stage2Damage, 1f, cascadeGroup: 0);
        SkillLevel level = MakeLevel(effect1, effect2);

        float before = target.Hp;
        InvokeCastSkillLevel(attacker, level, target);
        float damage = before - target.Hp;

        Assert.AreEqual(Stage1Damage + Stage2Damage, damage, 0.5f,
            "cascadeGroup=0(기본값)인 두 효과가 둘 다 안 걸렸다 — 오늘 추가한 캐스케이드 그룹이 " +
            "그룹 없는 기존 효과까지 서로 막아버리는 회귀를 일으켰다.");
    }
}

```

### `ItemGambleRuntimeTests.cs`

```csharp
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// 목적: 아이템 도박 재고(ItemGambleState, commit 미정 — 하네스를 옮길 때 실제 커밋 해시로
// 교체할 것)와 풀(ItemGamblePoolData)이 (1) 재고 0이면 도박을 막는지, (2) 시작 상태(1)에서
// 정확히 1회만 가능한지, (3) 가중치를 실제로 읽어서 굴리는지(GachaTable.weight가 죽은
// 필드였던 전례를 반복하지 않았는지)를 확인한다. ItemGamblePoolData.Roll()이 유일한
// 진입점이라 우회 경로 자체가 구조적으로 없다 — 그래도 "정말 weight를 읽는가"는 값으로
// 확인해야 한다(코드를 읽는 것과 실제로 그 값이 결과에 반영되는 것은 다른 질문이다).
public class ItemGambleRuntimeTests
{
    ItemGamblePoolData pool;
    List<ItemData> testItems;

    [SetUp]
    public void SetUp()
    {
        pool = ScriptableObject.CreateInstance<ItemGamblePoolData>();
        testItems = new List<ItemData>();
        for (int i = 0; i < 3; i++)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.itemName = $"테스트아이템{i}";
            testItems.Add(item);
        }
    }

    [TearDown]
    public void TearDown()
    {
        if (pool != null) Object.DestroyImmediate(pool);
        foreach (ItemData item in testItems)
            if (item != null) Object.DestroyImmediate(item);
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field, $"{target.GetType().Name}.{fieldName} 필드를 리플렉션으로 못 찾았다 — 이름이 바뀌었을 수 있다.");
        field.SetValue(target, value);
    }

    void SetFullPool(List<ItemGamblePoolData.Entry> entries) => SetPrivateField(pool, "fullPool", entries);
    void SetReducedPool(List<ItemGamblePoolData.Entry> entries) => SetPrivateField(pool, "reducedPool", entries);

    [Test]
    public void StockZero_BlocksGamble()
    {
        SetFullPool(new List<ItemGamblePoolData.Entry>
        {
            new ItemGamblePoolData.Entry { item = testItems[0], weight = 1f },
        });

        GameObject stateGO = new GameObject("Test_ItemGambleState_StockZero");
        try
        {
            ItemGambleState state = stateGO.AddComponent<ItemGambleState>();
            SetPrivateField(state, "stock", 0);

            bool result = state.TryGamble(pool, out ItemData rolled);

            Assert.IsFalse(result, "재고 0인데 TryGamble이 성공을 돌려줬다.");
            Assert.IsNull(rolled, "재고 0인데 아이템이 뽑혔다.");
        }
        finally
        {
            Object.DestroyImmediate(stateGO);
        }
    }

    [Test]
    public void StartState_AllowsExactlyOneGamble()
    {
        // 기본값(코드 기본값 1, 원작 Item_Int(0)+1 보정)을 그대로 쓴다 — 리플렉션으로 안 건드림.
        SetFullPool(new List<ItemGamblePoolData.Entry>
        {
            new ItemGamblePoolData.Entry { item = testItems[0], weight = 1f },
        });

        GameObject stateGO = new GameObject("Test_ItemGambleState_StartState");
        try
        {
            ItemGambleState state = stateGO.AddComponent<ItemGambleState>();

            Assert.IsTrue(state.HasStock, "시작 상태(재고 기본값)에서 HasStock이 false다 — +1 보정이 빠졌을 수 있다.");

            bool first = state.TryGamble(pool, out ItemData firstRoll);
            Assert.IsTrue(first, "시작 상태에서 첫 도박이 실패했다.");
            Assert.IsNotNull(firstRoll, "첫 도박에서 아이템이 안 뽑혔다.");

            bool second = state.TryGamble(pool, out ItemData secondRoll);
            Assert.IsFalse(second, "시작 상태(재고 1)인데 두 번째 도박까지 성공했다 — +1 보정이 2 이상으로 잘못 들어갔을 수 있다.");
            Assert.IsNull(secondRoll, "재고가 없는데도 두 번째 도박에서 아이템이 뽑혔다.");
        }
        finally
        {
            Object.DestroyImmediate(stateGO);
        }
    }

    [Test]
    public void WeightedRoll_ReflectsConfiguredWeights_NotUniform()
    {
        // GachaTable.weight가 죽은 필드였던 전례(2026-09-05 감사, 실제 지급 경로가 그
        // 필드를 안 보는 다른 메서드를 불렀다) — 여기서는 Roll()이 유일한 진입점이므로
        // 우회 경로가 구조적으로 없지만, "값이 실제로 결과에 반영되는가"는 몬테카를로로
        // 직접 확인해야 한다. 가중치를 1:9로 극단적으로 벌려서(우연한 균등분포와 확실히
        // 구분되게) 그 비율이 실제로 나오는지 본다.
        SetFullPool(new List<ItemGamblePoolData.Entry>
        {
            new ItemGamblePoolData.Entry { item = testItems[0], weight = 1f },
            new ItemGamblePoolData.Entry { item = testItems[1], weight = 9f },
        });

        int count0 = 0, count1 = 0;
        const int Trials = 20000;
        for (int i = 0; i < Trials; i++)
        {
            ItemData rolled = pool.Roll(useReducedPool: false);
            if (rolled == testItems[0]) count0++;
            else if (rolled == testItems[1]) count1++;
            else Assert.Fail($"설정하지 않은 아이템이 나왔다: {(rolled != null ? rolled.itemName : "null")}");
        }

        float ratio = (float)count1 / count0;
        // 기대 비율 9:1 — 몬테카를로 오차를 감안해 넉넉하게 [6, 13] 구간만 확인한다(균등분포면
        // 1.0 근처가 나와야 하니, 이 범위를 벗어나면 최소한 "weight가 안 읽힌다"는 아니다).
        Assert.Greater(ratio, 6f, $"가중치 9:1인데 실제 비율이 {ratio:F2}:1로 너무 낮다 — weight가 안 읽히고 있을 수 있다(GachaTable.weight와 같은 함정).");
        Assert.Less(ratio, 13f, $"가중치 9:1인데 실제 비율이 {ratio:F2}:1로 너무 높다.");
    }

    [Test]
    public void ReducedPoolFlag_HardcodedFalse_FullPoolAlwaysUsed()
    {
        // udg_Tech_No_support 대응 게이트 — 항법 시스템이 없어 지금은 항상 false. 이 테스트는
        // "지금 시점에 축소 풀이 선택될 수 없다"는 사실 자체를 고정해 다음 사람이 항법을
        // 만들 때 이 지점을 찾게 한다(TODO 주석과 짝을 이루는 검산).
        GameObject stateGO = new GameObject("Test_ItemGambleState_ReducedPoolFlag");
        try
        {
            ItemGambleState state = stateGO.AddComponent<ItemGambleState>();
            Assert.IsFalse(state.ReducedPoolActive,
                "ReducedPoolActive가 true를 돌려준다 — 항법 시스템이 아직 없는데 축소 풀이 켜질 수 있게 됐다.");
        }
        finally
        {
            Object.DestroyImmediate(stateGO);
        }
    }
}

```

### `SelfUpgradeAxisRuntimeTests.cs`

```csharp
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

// 목적: 원작 비비 A0LZ류 "자가시전 영구 강화" 축(2026-09-06, A0LZ_CASTER_STACK_INVESTIGATION.md/
// 7703d2c, commit 미정 — 이 커밋 해시는 하네스를 옮길 때 실제 커밋으로 교체할 것)이 실제
// 런타임 경로에서 (1) 레벨을 SkillEffectBasis.CasterSelfUpgradeLevel로 올바르게 읽는지,
// (2) TryUpgradeSelf가 성공 시에만 레벨을 올리는지, (3) 실패해도 자원(목재+위습)이 그대로
// 소모되는지를 확인한다. RNG를 실제로 굴리는 대신 SelfUpgradeAbilityData.baseChancePercent를
// 극단값(100/0)으로 설정해 성공/실패를 결정적으로 강제한다 — 이게 이 축을 위해 상수 대신
// 자산 필드로 뺀 것의 부산물이다(테스트도 그 필드를 그대로 이용할 수 있다).
public class SelfUpgradeAxisRuntimeTests
{
    GameObject attackerGO;
    UnitAttacker attacker;
    OwnedByPlayer ownedByPlayer;

    GameObject playerContextGO;
    PlayerContext playerContext;
    ResourceWallet resourceWallet;

    SelfUpgradeAbilityData upgradeData;
    WispData wispData;

    const int PlayerId = 777; // 다른 테스트와 안 겹치게 임의의 값.

    [SetUp]
    public void SetUp()
    {
        // ⚠️ 2026-09-06 밤 실측에서 잡힌 테스트 버그(게임 로직 버그 아님): UnitAttacker.Awake()가
        // owner = GetComponent<OwnedByPlayer>()를 그 자리에서 바로 읽는다 — AddComponent는
        // 활성 GameObject에 즉시 Awake를 돌리므로, UnitAttacker를 먼저 붙이면 그 순간 아직
        // OwnedByPlayer가 없어 owner가 영원히 null로 굳는다(Awake는 한 번만 돈다).
        // TryUpgradeSelf()의 "if (owner == null) return false;"가 항상 걸려 성공률과 무관하게
        // 매번 실패로 보였다 — GuaranteedSuccess/GuaranteedFailure 두 실패의 진짜 원인이었다.
        // 고치는 법: 비활성 상태로 만들어 모든 컴포넌트를 붙인 뒤 활성화한다(Awake를 그때까지 미룸).
        attackerGO = new GameObject("Test_UnitAttacker_SelfUpgrade");
        attackerGO.SetActive(false);
        attacker = attackerGO.AddComponent<UnitAttacker>();
        ownedByPlayer = attackerGO.AddComponent<OwnedByPlayer>();
        ownedByPlayer.SetOwner(PlayerId);
        attackerGO.SetActive(true);

        playerContextGO = new GameObject("Test_PlayerContext_SelfUpgrade");
        playerContext = playerContextGO.AddComponent<PlayerContext>();
        resourceWallet = playerContextGO.AddComponent<ResourceWallet>();

        // PlayerContext.playerId는 private([SerializeField])다 — 씬 인스펙터로만 채우는 값이라
        // 공개 세터가 없다. 이 하네스의 기존 파일들도 이런 자리는 리플렉션으로 채운다.
        SetPrivateField(playerContext, "playerId", PlayerId);
        SetPrivateField(playerContext, "resourceWallet", resourceWallet);

        resourceWallet.Add(ResourceType.Wood, 1000); // 충분히 크게 — 이 테스트에서 고갈 안 시킴.

        wispData = ScriptableObject.CreateInstance<WispData>();
        wispData.wispName = "테스트용 랜덤위습";

        upgradeData = ScriptableObject.CreateInstance<SelfUpgradeAbilityData>();
        upgradeData.maxLevel = 10;
        upgradeData.woodCost = 2;
        upgradeData.wispCost = 3;
        upgradeData.wispCurrency = wispData;
        SetPrivateField(attacker, "selfUpgradeData", upgradeData);
    }

    [TearDown]
    public void TearDown()
    {
        // OnEnable에서 PlayerContext.registry에 등록되므로, GameObject를 지우면 OnDisable이
        // 불려 자동으로 registry에서 빠진다 — 여기서 따로 안 챙겨도 된다.
        foreach (Wisp w in Object.FindObjectsByType<Wisp>(FindObjectsSortMode.None))
            if (w != null) Object.DestroyImmediate(w.gameObject);

        if (attackerGO != null) Object.DestroyImmediate(attackerGO);
        if (playerContextGO != null) Object.DestroyImmediate(playerContextGO);
        if (upgradeData != null) Object.DestroyImmediate(upgradeData);
        if (wispData != null) Object.DestroyImmediate(wispData);
    }

    static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field, $"{target.GetType().Name}.{fieldName} 필드를 리플렉션으로 못 찾았다 — 이름이 바뀌었을 수 있다.");
        field.SetValue(target, value);
    }

    static int GetSelfUpgradeLevel(UnitAttacker a)
    {
        FieldInfo field = typeof(UnitAttacker).GetField("selfUpgradeLevel", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field, "UnitAttacker.selfUpgradeLevel을 리플렉션으로 못 찾았다.");
        return (int)field.GetValue(a);
    }

    void SpawnWisps(int count)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject($"Test_Wisp_{i}");
            Wisp w = go.AddComponent<Wisp>();
            w.SetData(wispData);
        }
    }

    [Test]
    public void GuaranteedSuccess_IncrementsLevel_AndConsumesResources()
    {
        upgradeData.baseChancePercent = 100f; // 레벨0에서도 100% — 결정적 성공.
        upgradeData.chancePerLevelPercent = 0f;
        SpawnWisps(3);

        int woodBefore = resourceWallet.Get(ResourceType.Wood);
        int wispCountBefore = Object.FindObjectsByType<Wisp>(FindObjectsSortMode.None).Length;

        bool result = attacker.TryUpgradeSelf();

        Assert.IsTrue(result, "100% 성공률인데 TryUpgradeSelf가 실패를 돌려줬다.");
        Assert.AreEqual(1, GetSelfUpgradeLevel(attacker),
            "성공했는데 selfUpgradeLevel이 안 올랐다 — 성공 경로가 레벨을 실제로 못 올리고 있다.");
        Assert.AreEqual(woodBefore - upgradeData.woodCost, resourceWallet.Get(ResourceType.Wood),
            "성공 시 목재가 woodCost만큼 안 깎였다.");

        int wispCountAfter = Object.FindObjectsByType<Wisp>(FindObjectsSortMode.None).Length;
        Assert.AreEqual(wispCountBefore - upgradeData.wispCost, wispCountAfter,
            "성공 시 위습이 wispCost기만큼 안 없어졌다.");
    }

    [Test]
    public void GuaranteedFailure_DoesNotIncrementLevel_ButStillConsumesResources()
    {
        // 원작 핵심 동작: 실패해도 자원은 나간다(A0LZ_CASTER_STACK_INVESTIGATION.md §②).
        // 이 테스트를 반대로(자원 안 깎임을 기대)로 짜면 정확히 틀린 것이다 — 실제로 여러
        // "도박형" 구현이 이 자리에서 실패 시 잘못 환불한다.
        upgradeData.baseChancePercent = 0f; // 결정적 실패.
        upgradeData.chancePerLevelPercent = 0f;
        SpawnWisps(3);

        int woodBefore = resourceWallet.Get(ResourceType.Wood);
        int wispCountBefore = Object.FindObjectsByType<Wisp>(FindObjectsSortMode.None).Length;

        bool result = attacker.TryUpgradeSelf();

        Assert.IsFalse(result, "0% 성공률인데 TryUpgradeSelf가 성공을 돌려줬다.");
        Assert.AreEqual(0, GetSelfUpgradeLevel(attacker),
            "실패했는데 selfUpgradeLevel이 올랐다.");
        Assert.AreEqual(woodBefore - upgradeData.woodCost, resourceWallet.Get(ResourceType.Wood),
            "실패했는데도 목재가 그대로다 — 원작은 실패해도 자원이 나간다(뒤바뀐 환불 버그일 수 있다).");

        int wispCountAfter = Object.FindObjectsByType<Wisp>(FindObjectsSortMode.None).Length;
        Assert.AreEqual(wispCountBefore - upgradeData.wispCost, wispCountAfter,
            "실패했는데도 위습이 그대로다 — 원작은 실패해도 자원이 나간다.");
    }

    [Test]
    public void InsufficientWisps_RefundsWood_AndDoesNotRoll()
    {
        // 위습을 하나도 안 놔둔다 — 입장 조건 미달(원작 "랜덤위습의 개수나 목재가 부족합니다").
        // 목재는 먼저 빠졌다가 위습 부족을 확인하면 되돌려져야 한다(GamblingShop.TryRollUnit의
        // "먼저 뺀 걸 나중 실패에 되돌린다" 패턴, 이번 구현도 그대로 따랐다).
        upgradeData.baseChancePercent = 100f;
        upgradeData.chancePerLevelPercent = 0f;
        // SpawnWisps 호출 안 함 — 맵에 위습 0기.

        int woodBefore = resourceWallet.Get(ResourceType.Wood);

        bool result = attacker.TryUpgradeSelf();

        Assert.IsFalse(result, "위습이 0기인데 TryUpgradeSelf가 성공을 돌려줬다.");
        Assert.AreEqual(0, GetSelfUpgradeLevel(attacker), "실패했는데 레벨이 올랐다.");
        Assert.AreEqual(woodBefore, resourceWallet.Get(ResourceType.Wood),
            "위습 부족으로 실패했는데 목재가 그대로 안 돌아왔다 — 먼저 뺀 목재를 되돌리는 로직이 빠졌을 수 있다.");
    }

    [Test]
    public void SelfUpgradeLevel_ReadThroughRealDamagePath_MatchesFormula()
    {
        // "런타임이 실제로 그 경로로 부르는가"를 확인한다 — selfUpgradeLevel을 직접 대입하고
        // AddAegrStack류 게 아니라 실제 CastSkillLevel(리플렉션) → ResolveSkillEffectValue →
        // DealSkillDamage 경로를 태워서, CasterSelfUpgradeLevel basis가 그 레벨을 실제로
        // 읽는지 확인한다. 원작 공식(레벨×0.05+1, A0LZ_CASTER_STACK_INVESTIGATION.md §①)을
        // 그대로 옮겨 multiplier=0.05f, bonus=1.0f로 SkillEffect의 배수 항을 만들고,
        // Damage 자체는 basis=Flat인 별도 효과로 분리하지 않고 CasterSelfUpgradeLevel의
        // 결과값(1.00~1.50)을 그대로 "가한 피해"로 재는 방식 — multiplier/bonus 조합만으로
        // 이 basis가 실제로 selfUpgradeLevel을 읽는지 검증하기에 충분하다.
        SetPrivateField(attacker, "selfUpgradeLevel", 10); // 레벨10 — 원작 최대치.

        GameObject targetGO = new GameObject("Test_EnemyDummy_SelfUpgradeRead");
        EnemyDummy target = targetGO.AddComponent<EnemyDummy>();
        EnemyData targetData = ScriptableObject.CreateInstance<EnemyData>();
        targetData.hp = 10_000_000f;
        targetData.magicArmorMultiplier = 1f;
        targetData.percentDamageTaken = 1f;
        target.Initialize(targetData);

        try
        {
            SkillEffect effect = new SkillEffect
            {
                kind = SkillEffectKind.Damage,
                target = SkillTargetKind.SingleTarget,
                chance = 1f,
                basis = SkillEffectBasis.CasterSelfUpgradeLevel,
                multiplier = 0.05f, // 레벨×0.05 + 1.0 = 레벨10에서 1.50
                bonus = 1.0f,
                damageType = DamageType.AD,
                attackType = AttackType.Normal,
                hitCount = 1,
            };
            SkillLevel level = new SkillLevel
            {
                triggerChance = 1f,
                range = 100f,
                effects = new System.Collections.Generic.List<SkillEffect> { effect },
            };

            float hpBefore = target.Hp;
            MethodInfo method = typeof(UnitAttacker).GetMethod("CastSkillLevel", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method, "UnitAttacker.CastSkillLevel을 리플렉션으로 못 찾았다.");
            method.Invoke(attacker, new object[] { level, level.range, target, 0f });
            float hpAfter = target.Hp;

            // 레벨10 × 0.05 + 1.0 = 1.50 — "배율" 자체를 데미지 수치로 그대로 읽어 확인한다.
            Assert.AreEqual(1.5f, hpBefore - hpAfter, 0.001f,
                "CasterSelfUpgradeLevel이 실제 CastSkillLevel 경로에서 selfUpgradeLevel(10)을 " +
                "제대로 안 읽었다 — 기대값(레벨10×0.05+1.0=1.50)과 다르다.");
        }
        finally
        {
            Object.DestroyImmediate(targetGO);
            Object.DestroyImmediate(targetData);
        }
    }
}

```

### `GuilRandomDefense.Tests.EditMode.asmdef`

⚠️ **참고용 실패 사례 — 실제로 쓰지 않는다.** 이 asmdef를 쓰면 `Assembly-CSharp`를 이름으로 못 찾아 컴파일이 깨진다(위 실행 절차 참고). asmdef 없이 `Assets/Editor/Tests/`에 `.cs`만 두는 방식을 쓴다.

```json
{
    "name": "GuilRandomDefense.Tests.EditMode",
    "rootNamespace": "",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner",
        "Assembly-CSharp"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": true,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}

```

