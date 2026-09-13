# 눈 셰이프키 전환 구현 계획

> **[완료 · 2026-09-13] 이 문서는 실행 기록이다 — 지금 코드와 다르다.**
> 과제 1~6을 다 돌린 뒤 A/B 검수에서 **셰이프키가 선택됐고 구 경로를 지웠다.**
> 그래서 아래에 적힌 `m_UseBlendShapes`·`DevSetEyeMode`·`BuildBlendShapeMode`·
> `TestSetEyeMode`는 **더 이상 없다**(A/B용 임시 손잡이였다). 그 뒤에 감김 가중치도
> 100 → 140으로 올렸다(`BLINK_FULL_WEIGHT`).
>
> 지금 상태와 왜 그런지는 `docs/superpowers/specs/2026-09-13-eye-blendshape-design.md`를 봐라.
> 여기 코드를 복사해 쓰지 마라.


> **에이전트용:** 필수 하위 스킬 — superpowers:subagent-driven-development(권장) 또는
> superpowers:executing-plans로 과제 단위로 구현한다. 단계는 체크박스(`- [ ]`)다.

**목표:** `EyeLook`이 얹은 구 대신 **팩 메시의 셰이프키**로 눈동자·깜빡임을 굴리게 하고,
구 방식과 나란히 찍어 사용자가 고를 수 있게 한다.

**구조:** 시선·깜빡임 계산은 그대로 두고 **출력만** 갈아 끼운다. 순수 로직
(`EyeBlendShapeLogic`)이 두 시선 방향 + cover를 셰이프키 가중치 5개로 접고,
컴포넌트(`EyeBlendShapes`)가 그것을 `SkinnedMeshRenderer`에 넣는다. `EyeLook`은
`m_UseBlendShapes` 하나로 두 경로를 가른다 — **A/B 검수가 끝나기 전에는 구를 안 지운다.**

**기술:** Unity 6000.5.2f1 · C# · NUnit(EditMode) · 기존 `EyeLookLogic` 재사용

**설계:** `docs/superpowers/specs/2026-09-13-eye-blendshape-design.md`

---

## 파일 구조

| 파일 | 책임 |
|---|---|
| `Assets/Scripts/Player/EyeBlendShapeLogic.cs` **(신규)** | 순수 static. 방향 2개 + cover → 가중치 5개. 유니티 오브젝트·시간을 모른다 |
| `Assets/Scripts/Player/EyeBlendShapes.cs` **(신규)** | `SkinnedMeshRenderer` + 셰이프키 인덱스 5개를 Awake에 한 번 해석. `Apply`만 공개 |
| `Assets/Scripts/Player/EyeLook.cs` **(수정)** | 방향 준비를 `SetupDirections()`로 빼고, `Apply()`를 두 갈래로 |
| `Assets/Scripts/Tests/EditMode/EyeBlendShapeLogicTests.cs` **(신규)** | 과제 1 |
| `Assets/Scripts/Testing/TestManager.Eye.cs` **(신규)** | QA 훅 — 렌더러에 **실제로 들어간** 값을 읽는다 |
| `docs/design-vault/코드 다이어그램.md` **(수정)** | 규칙 8 |

---

## 과제 1: 순수 로직 `EyeBlendShapeLogic`

**파일:**
- 생성: `Assets/Scripts/Player/EyeBlendShapeLogic.cs`
- 테스트: `Assets/Scripts/Tests/EditMode/EyeBlendShapeLogicTests.cs`

**배경(구현자가 알아야 할 것):** `EyeLook`은 눈마다 따로 시선 방향을 갖는다(`m_GazeR`·`m_GazeL`,
**머리 본 로컬**). 셰이프키는 두 눈을 **거울 대칭으로 묶어** 움직이므로 좌우 비대칭 성분을
표현할 수 없다. 그래서 두 눈의 편차를 **평균**해 대칭 성분만 쓴다. 이것이 이 함수의 전부다.

- [ ] **1단계: 실패하는 테스트를 쓴다**

`Assets/Scripts/Tests/EditMode/EyeBlendShapeLogicTests.cs`:

```csharp
using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 시선 → 셰이프키 가중치(2026-09-13). 팩 셰이프키는 **두 눈이 거울 대칭으로 묶여** 있어
    /// 좌우 비대칭 시선을 표현할 수 없다 — 그 성질을 코드가 실제로 그렇게 다루는지까지 잰다.
    /// </summary>
    public class EyeBlendShapeLogicTests
    {
        // 옆눈: 쉬는 방향이 정면에서 좌우로 60도씩 벌어져 있다(쥐·고양이 둘 다 그런 꼴이다).
        private static readonly Vector3 PIVOT = Vector3.forward;
        private static readonly Vector3 REST_R =
            new Vector3(Mathf.Sin(Mathf.Deg2Rad * 60f), 0f, Mathf.Cos(Mathf.Deg2Rad * 60f));
        private static readonly Vector3 REST_L = EyeLookLogic.Mirror(REST_R);
        private const float MAX = 30f;

        private static EyeShapeWeights At(Vector3 _gazeR, Vector3 _gazeL, float _cover = 0f)
        {
            return EyeBlendShapeLogic.Weights(REST_R, REST_L, _gazeR, _gazeL, PIVOT, MAX, _cover);
        }

        [Test]
        public void Rest_AllDirectionWeightsAreZero()
        {
            EyeShapeWeights w = At(REST_R, REST_L);
            Assert.AreEqual(0f, w.LookIn, 0.01f);
            Assert.AreEqual(0f, w.LookOut, 0.01f);
            Assert.AreEqual(0f, w.LookUp, 0.01f);
            Assert.AreEqual(0f, w.LookDown, 0.01f);
        }

        [Test]
        public void FullInward_LookInIsFull_LookOutIsZero()
        {
            // 두 눈이 각자 코 쪽으로 최대각만큼 — 정면을 모아 보는 자세다.
            Vector3 gazeR = Quaternion.AngleAxis(-MAX, Vector3.up) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(+MAX, Vector3.up) * REST_L;
            EyeShapeWeights w = At(gazeR, gazeL);
            Assert.AreEqual(100f, w.LookIn, 0.5f);
            Assert.AreEqual(0f, w.LookOut, 0.01f);
        }

        [Test]
        public void FullOutward_LookOutIsFull_LookInIsZero()
        {
            Vector3 gazeR = Quaternion.AngleAxis(+MAX, Vector3.up) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(-MAX, Vector3.up) * REST_L;
            EyeShapeWeights w = At(gazeR, gazeL);
            Assert.AreEqual(100f, w.LookOut, 0.5f);
            Assert.AreEqual(0f, w.LookIn, 0.01f);
        }

        [Test]
        public void LookingUp_DrivesLookUp_NotLookDown()
        {
            Vector3 gazeR = Quaternion.AngleAxis(MAX, Vector3.Cross(Vector3.up, REST_R)) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(MAX, Vector3.Cross(Vector3.up, REST_L)) * REST_L;
            EyeShapeWeights w = At(gazeR, gazeL);
            Assert.Greater(w.LookUp, 90f);
            Assert.AreEqual(0f, w.LookDown, 0.01f);
        }

        [Test]
        public void BeyondMaxAngle_StaysAtHundred()
        {
            Vector3 gazeR = Quaternion.AngleAxis(-MAX * 3f, Vector3.up) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(+MAX * 3f, Vector3.up) * REST_L;
            EyeShapeWeights w = At(gazeR, gazeL);
            Assert.AreEqual(100f, w.LookIn, 0.01f);
        }

        [Test]
        public void AsymmetricGaze_CancelsToNearZero()
        {
            // 오른눈은 안쪽, 왼눈은 바깥쪽 — 「옆의 목표를 쫓는」 자세다.
            // 셰이프키로는 표현할 수 없으므로 **평균이 0에 가까워야** 한다(설계 §2).
            Vector3 gazeR = Quaternion.AngleAxis(-MAX, Vector3.up) * REST_R;
            Vector3 gazeL = Quaternion.AngleAxis(-MAX, Vector3.up) * REST_L;
            EyeShapeWeights w = At(gazeR, gazeL);
            Assert.Less(w.LookIn, 1f);
            Assert.Less(w.LookOut, 1f);
        }

        [Test]
        public void SameAxisPair_NeverBothNonZero()
        {
            for (int i = -12; i <= 12; i++)
            {
                Vector3 gazeR = Quaternion.AngleAxis(i * 5f, Vector3.up) * REST_R;
                Vector3 gazeL = Quaternion.AngleAxis(-i * 5f, Vector3.up) * REST_L;
                EyeShapeWeights w = At(gazeR, gazeL);
                Assert.IsTrue(w.LookIn <= 0.01f || w.LookOut <= 0.01f, "In/Out 동시 점등 i=" + i);
                Assert.IsTrue(w.LookUp <= 0.01f || w.LookDown <= 0.01f, "Up/Down 동시 점등 i=" + i);
            }
        }

        [Test]
        public void Blink_FollowsCover()
        {
            Assert.AreEqual(0f, At(REST_R, REST_L, 0f).Blink, 0.01f);
            Assert.AreEqual(50f, At(REST_R, REST_L, 0.5f).Blink, 0.01f);
            Assert.AreEqual(100f, At(REST_R, REST_L, 1f).Blink, 0.01f);
        }

        [Test]
        public void Weights_AlwaysWithinUnityRange()
        {
            for (int i = -20; i <= 20; i += 2)
            {
                for (int j = -20; j <= 20; j += 2)
                {
                    Vector3 gazeR = Quaternion.AngleAxis(i * 6f, Vector3.up) * REST_R;
                    Vector3 gazeL = Quaternion.AngleAxis(j * 6f, Vector3.up) * REST_L;
                    EyeShapeWeights w = At(gazeR, gazeL, 3f);   // cover도 범위 밖으로 준다
                    float[] all = { w.LookIn, w.LookOut, w.LookUp, w.LookDown, w.Blink };
                    foreach (float v in all)
                    {
                        Assert.GreaterOrEqual(v, 0f);
                        Assert.LessOrEqual(v, 100f);
                    }
                }
            }
        }
    }
}
```

- [ ] **2단계: 빨간지 확인**

메뉴 `Tools/CTC/Run EditMode Tests` (`u_editor_menu`로 호출 가능).
기대: `EyeBlendShapeLogic`·`EyeShapeWeights`가 없어 **컴파일 실패**.

- [ ] **3단계: 구현**

`Assets/Scripts/Player/EyeBlendShapeLogic.cs`:

```csharp
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>셰이프키 가중치 다섯(유니티 규약 0~100).</summary>
    public readonly struct EyeShapeWeights
    {
        public readonly float LookIn;
        public readonly float LookOut;
        public readonly float LookUp;
        public readonly float LookDown;
        public readonly float Blink;

        public EyeShapeWeights(float _in, float _out, float _up, float _down, float _blink)
        {
            LookIn = _in; LookOut = _out; LookUp = _up; LookDown = _down; Blink = _blink;
        }
    }

    /// <summary>
    /// 시선 방향을 팩 메시의 셰이프키 가중치로 접는다 — 트랜스폼도 시간도 모른다(EditMode 대상).
    /// 방향은 전부 **머리 본 로컬**이다.
    ///
    /// **왜 두 눈을 평균하는가**(2026-09-13 실측): 팩 셰이프키는 좌우 49정점을 **거울 대칭**으로
    /// 민다(lookIn이 왼눈 x −18.5 · 오른눈 x +18.5). 그래서 「왼눈은 앞, 오른눈은 뒤」 같은
    /// 비대칭 시선은 **표현할 방법이 없다.** 한쪽 눈에 맞추면 반대쪽 눈이 틀린 곳을 보므로,
    /// 표현 가능한 대칭 성분만 남기는 평균이 유일하게 옳다. 버려지는 비대칭 성분이 곧
    /// 이 방식이 구 방식에 비해 못 하는 것이다(설계 문서 §2).
    /// </summary>
    public static class EyeBlendShapeLogic
    {
        public static EyeShapeWeights Weights(Vector3 _restR, Vector3 _restL,
                                              Vector3 _gazeR, Vector3 _gazeL,
                                              Vector3 _pivot, float _maxAngleDeg, float _cover)
        {
            float max = Mathf.Max(1f, _maxAngleDeg);

            Vector3 fwd = Safe(_pivot, Vector3.forward);
            // 머리의 오른쪽 축 — 두 쉬는 방향이 x에 대해 거울이라 그 차가 곧 오른쪽이다.
            Vector3 right = Safe(_restR - _restL, Vector3.right);
            Vector3 up = Safe(Vector3.Cross(fwd, right), Vector3.up);
            // right가 fwd와 직교하지 않을 수 있다 — up을 얻은 뒤 직교하게 다시 세운다.
            right = Safe(Vector3.Cross(up, fwd), right);

            float h = 0.5f * (InwardDeg(_restR, _gazeR, fwd, right, up)
                            + InwardDeg(_restL, _gazeL, fwd, right, up)) / max;
            float v = 0.5f * (ElevationDeg(_gazeR, up) - ElevationDeg(_restR, up)
                            + ElevationDeg(_gazeL, up) - ElevationDeg(_restL, up)) / max;
            h = Mathf.Clamp(h, -1f, 1f);
            v = Mathf.Clamp(v, -1f, 1f);

            return new EyeShapeWeights(
                Mathf.Max(0f, h) * 100f,
                Mathf.Max(0f, -h) * 100f,
                Mathf.Max(0f, v) * 100f,
                Mathf.Max(0f, -v) * 100f,
                Mathf.Clamp01(_cover) * 100f);
        }

        private static Vector3 Safe(Vector3 _v, Vector3 _fallback)
        {
            return _v.sqrMagnitude > 1e-12f ? _v.normalized : _fallback.normalized;
        }

        /// <summary>수평면 기준 올라간 각. 위가 +.</summary>
        private static float ElevationDeg(Vector3 _dir, Vector3 _up)
        {
            Vector3 d = Safe(_dir, Vector3.forward);
            return Mathf.Asin(Mathf.Clamp(Vector3.Dot(d, _up), -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 코 쪽으로 돈 각(+가 안쪽). **어느 쪽 눈인지 인자로 안 받는다** — 쉬는 방향의
        /// 방위각 부호가 그것을 알려 준다. 그래서 좌우를 뒤바꿔 불러도 안 깨진다.
        /// </summary>
        private static float InwardDeg(Vector3 _rest, Vector3 _gaze,
                                       Vector3 _fwd, Vector3 _right, Vector3 _up)
        {
            float restAz = AzimuthDeg(_rest, _fwd, _right, _up);
            float gazeAz = AzimuthDeg(_gaze, _fwd, _right, _up);
            float side = restAz >= 0f ? 1f : -1f;
            return side * (restAz - gazeAz);
        }

        private static float AzimuthDeg(Vector3 _dir, Vector3 _fwd, Vector3 _right, Vector3 _up)
        {
            Vector3 p = _dir - _up * Vector3.Dot(_dir, _up);
            if (p.sqrMagnitude <= 1e-12f) return 0f;
            return Mathf.Atan2(Vector3.Dot(p, _right), Vector3.Dot(p, _fwd)) * Mathf.Rad2Deg;
        }
    }
}
```

- [ ] **4단계: 초록인지 확인**

`Tools/CTC/Run EditMode Tests`. 기대: 위 9개 전부 통과, 기존 테스트도 그대로.

- [ ] **5단계: 돌연변이 검사**

`Mathf.Clamp(h, -1f, 1f)`를 `h`로 되돌리고 다시 돌린다.
기대: `BeyondMaxAngle_StaysAtHundred`와 `Weights_AlwaysWithinUnityRange`가 **빨개진다**.
확인했으면 되돌린다. 빨개지지 않으면 테스트가 거짓이므로 테스트를 고친다.

- [ ] **6단계: 커밋**

```bash
git add CTC/Assets/Scripts/Player/EyeBlendShapeLogic.cs* CTC/Assets/Scripts/Tests/EditMode/EyeBlendShapeLogicTests.cs*
git commit -m "feat(eye): 시선을 셰이프키 가중치로 접는 순수 로직"
```

---

## 과제 2: 컴포넌트 `EyeBlendShapes`

**파일:**
- 생성: `Assets/Scripts/Player/EyeBlendShapes.cs`

**배경:** 셰이프키는 이름이 아니라 **인덱스**로 넣는다(`SetBlendShapeWeight(int, float)`).
이름→인덱스 조회(`sharedMesh.GetBlendShapeIndex`)는 매 프레임 하면 안 된다 — Awake에 한 번 푼다.
팩 메시에 이름이 하나라도 없으면 **에러 없이 눈이 굳는다.** 그래서 경고를 한 번 찍고 스스로 꺼진다.

- [ ] **1단계: 구현**

`Assets/Scripts/Player/EyeBlendShapes.cs`:

```csharp
using UnityEngine;

namespace CTC.Runtime.Player
{
    /// <summary>
    /// 팩 메시의 눈 셰이프키에 가중치를 넣는다 — **그것만** 한다(규칙 7).
    ///
    /// **왜 얹은 구가 아니라 이것인가**(2026-09-13 실측): 팩 메시에는 셰이프키가 30개 있고
    /// 그중 eyes.lookIn/Out/Up/Down이 동공을 2D로 옮긴다. Mouse_LOD0~3·Cat_LOD0 전부
    /// 갖고 있고 importBlendShapes도 이미 켜져 있다. 「팩 메시는 눈동자를 못 움직인다」는
    /// <see cref="EyeLook"/>의 옛 전제는 틀렸다.
    ///
    /// 무엇을 넣을지는 <see cref="EyeBlendShapeLogic"/>이 정한다. 이 클래스는 배선만 안다.
    /// </summary>
    public class EyeBlendShapes : MonoBehaviour
    {
        private static readonly string[] SHAPE_NAMES =
        {
            "eyes.lookIn", "eyes.lookOut", "eyes.lookUp", "eyes.lookDown", "eyes.blink"
        };

        private const int IN = 0, OUT = 1, UP = 2, DOWN = 3, BLINK = 4;

        private SkinnedMeshRenderer m_Skin;
        private readonly int[] m_Index = new int[SHAPE_NAMES.Length];
        private bool m_Ready;

        public bool Ready => m_Ready;

        private void Awake()
        {
            Resolve();
        }

        private void Resolve()
        {
            m_Ready = false;
            m_Skin = GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (m_Skin == null || m_Skin.sharedMesh == null)
            {
                Debug.LogWarning("[EyeBlendShapes] 스킨드 메시를 못 찾았다 — 눈 셰이프키를 끈다");
                return;
            }

            for (int i = 0; i < SHAPE_NAMES.Length; i++)
            {
                m_Index[i] = m_Skin.sharedMesh.GetBlendShapeIndex(SHAPE_NAMES[i]);
                if (m_Index[i] >= 0) continue;

                // 팩을 갈아 끼웠을 때 **에러 없이 눈이 굳는 것**을 막는 유일한 장치다.
                Debug.LogWarning("[EyeBlendShapes] 셰이프키가 없다 — 눈 셰이프키를 끈다: " + SHAPE_NAMES[i]);
                return;
            }

            m_Ready = true;
        }

        public void Apply(in EyeShapeWeights _weights)
        {
            if (!m_Ready || m_Skin == null) return;
            m_Skin.SetBlendShapeWeight(m_Index[IN], _weights.LookIn);
            m_Skin.SetBlendShapeWeight(m_Index[OUT], _weights.LookOut);
            m_Skin.SetBlendShapeWeight(m_Index[UP], _weights.LookUp);
            m_Skin.SetBlendShapeWeight(m_Index[DOWN], _weights.LookDown);
            m_Skin.SetBlendShapeWeight(m_Index[BLINK], _weights.Blink);
        }

        /// <summary>모드를 되돌릴 때 눈이 돌아간 채로 남지 않게 한다.</summary>
        public void ClearWeights()
        {
            Apply(new EyeShapeWeights(0f, 0f, 0f, 0f, 0f));
        }

        /// <summary>
        /// QA 전용 — **렌더러에서 되읽는다.** 계산한 값을 돌려주면 배선이 끊겨도 QA가 통과한다
        /// (2026-08-09 볼륨 사고와 같은 부류).
        /// </summary>
        public EyeShapeWeights DevWeights()
        {
            if (!m_Ready || m_Skin == null) return new EyeShapeWeights(0f, 0f, 0f, 0f, 0f);
            return new EyeShapeWeights(
                m_Skin.GetBlendShapeWeight(m_Index[IN]),
                m_Skin.GetBlendShapeWeight(m_Index[OUT]),
                m_Skin.GetBlendShapeWeight(m_Index[UP]),
                m_Skin.GetBlendShapeWeight(m_Index[DOWN]),
                m_Skin.GetBlendShapeWeight(m_Index[BLINK]));
        }
    }
}
```

- [ ] **2단계: 컴파일 확인**

`u_editor_asset(action: refresh)` 후 `u_console`로 에러 0인지 본다.

- [ ] **3단계: 커밋**

```bash
git add CTC/Assets/Scripts/Player/EyeBlendShapes.cs*
git commit -m "feat(eye): 셰이프키 가중치를 스킨드 메시에 넣는 컴포넌트"
```

---

## 과제 3: `EyeLook` 배선

**파일:**
- 수정: `Assets/Scripts/Player/EyeLook.cs`

**배경:** `Build()`가 두 가지를 같이 한다 — ① 머리 본·쉬는 방향·한계를 잡고 ② 구 6개를 만든다.
셰이프키 모드는 ①만 필요하다. 그래서 ①을 `SetupDirections()`로 뺀다.
**시선·깜빡임 계산(`LateUpdate`·`StepBlink`·`CurrentCover`)은 한 줄도 안 고친다.**

- [ ] **1단계: 필드와 Awake**

`m_HeadBoneName` 선언 **바로 위**에 넣는다:

```csharp
        /// <summary>
        /// 셰이프키로 굴린다(기본). **A/B 검수용 임시 손잡이다** — CSV에 안 내린다(밸런스가 아니다).
        /// 검수가 끝나면 이 필드와 진 쪽 경로를 같이 지운다(설계 §5).
        /// </summary>
        [SerializeField] private bool m_UseBlendShapes = true;
```

`m_Ready` 선언 아래에 넣는다:

```csharp
        private EyeBlendShapes m_Shapes;   // 셰이프키 모드일 때만 붙는다
```

`Awake()`의 `Build();`를 바꾼다:

```csharp
            if (m_UseBlendShapes) BuildBlendShapeMode();
            else Build();
```

- [ ] **2단계: `SetupDirections()`를 빼고 `Build()`를 그 위에 세운다**

`Build()`의 머리 본 탐색부터 `ReadLimits(); m_GazeR = m_RestR; m_GazeL = m_RestL;`까지를
아래 함수로 옮기고, `Build()`는 그것을 부른 뒤 나머지(구 만들기)를 한다:

```csharp
        /// <summary>
        /// 두 모드가 **함께 쓰는** 준비 — 머리 본·쉬는 방향·회전 한계.
        /// 여기까지는 구를 얹든 셰이프키를 밀든 똑같이 필요하다.
        /// </summary>
        private bool SetupDirections()
        {
            m_Head = FindDeep(transform, m_HeadBoneName);
            if (m_Head == null)
            {
                Debug.LogWarning("[EyeLook] 머리 본을 못 찾았다 — 눈을 굴리지 않는다: " + m_HeadBoneName);
                return false;
            }

            m_RestR = m_EyeAxisLocal.normalized;
            m_RestL = EyeLookLogic.Mirror(m_RestR);
            m_GazePivot = EyeLookLogic.GazePivot(m_HeadForwardLocal, m_RestR, m_RestL);
            ReadLimits();
            m_GazeR = m_RestR;
            m_GazeL = m_RestL;
            return true;
        }

        /// <summary>셰이프키 모드 — 새 오브젝트를 하나도 안 만든다.</summary>
        private void BuildBlendShapeMode()
        {
            if (!SetupDirections()) return;

            m_Shapes = GetComponent<EyeBlendShapes>();
            if (m_Shapes == null) m_Shapes = gameObject.AddComponent<EyeBlendShapes>();
            if (!m_Shapes.Ready)
            {
                // 컴포넌트가 이미 경고를 찍었다 — 여기서 또 찍지 않는다.
                m_Shapes = null;
                return;
            }

            Apply();
            m_Ready = true;
        }
```

`Build()`의 머리 본 탐색·쉬는 방향 계산 부분은 지우고 맨 앞을 이렇게 만든다:

```csharp
        private void Build()
        {
            if (!SetupDirections()) return;

            SkinnedMeshRenderer skin = GetComponentInChildren<SkinnedMeshRenderer>(true);
            // ...(이하 기존 그대로 — 머티리얼 확인, EnsureMeshes, m_EyeRotR 계산부터)
```

**주의:** `Build()`에 남아 있던 `m_RestR`·`m_RestL`·`m_GazePivot`·`ReadLimits()`·
`m_GazeR`·`m_GazeL` 대입 6줄을 **반드시 지운다.** 남겨 두면 두 곳에서 같은 값을 잡는다.
`AddComponent<EyeBlendShapes>()`는 그 자리에서 `Awake`가 돌아 `Ready`가 바로 정해진다.

- [ ] **3단계: `Apply()`를 두 갈래로**

`Apply()` 맨 앞에 넣는다:

```csharp
            if (m_Shapes != null)
            {
                m_Shapes.Apply(EyeBlendShapeLogic.Weights(
                    m_RestR, m_RestL, m_GazeR, m_GazeL, m_GazePivot, m_MaxAngleDeg, CurrentCover()));
                return;
            }
```

- [ ] **4단계: A/B 전환 훅**

`DevSetVisible` 옆에 넣는다:

```csharp
        /// <summary>
        /// QA 전용 — 구 방식 ↔ 셰이프키 방식을 런타임에 바꾼다. **A/B를 같은 몸·같은 카메라로
        /// 찍는 유일한 방법**이다(몸을 둘 세우면 조명·각도가 미세하게 갈린다).
        /// </summary>
        public void DevSetEyeMode(bool _useBlendShapes)
        {
            if (_useBlendShapes == m_UseBlendShapes && m_Ready) return;
            m_UseBlendShapes = _useBlendShapes;

            if (m_Shapes != null)
            {
                m_Shapes.ClearWeights();
                m_Shapes = null;
            }
            DestroySpheres();

            m_Ready = false;
            if (m_UseBlendShapes) BuildBlendShapeMode();
            else Build();
        }

        /// <summary>얹은 구를 지운다 — 컴포넌트만 꺼도 구는 그대로 남는다.</summary>
        private void DestroySpheres()
        {
            // 눈동자는 흰자의 자식이라 흰자를 지우면 같이 간다.
            Transform[] parts = { m_EyeR, m_EyeL, m_LidR, m_LidL };
            foreach (Transform t in parts)
            {
                if (t != null) Destroy(t.gameObject);
            }
            m_EyeR = m_EyeL = m_PupilR = m_PupilL = m_LidR = m_LidL = null;
            m_EyeRendererR = m_EyeRendererL = m_PupilRendererR = m_PupilRendererL = null;
            m_LidRendererR = m_LidRendererL = null;
            m_LidVisible = false;
        }
```

- [ ] **5단계: 컴파일·테스트**

`u_editor_asset(action: refresh)` → `u_console` 에러 0 → `Tools/CTC/Run EditMode Tests` 전부 초록.

- [ ] **6단계: 커밋**

```bash
git add CTC/Assets/Scripts/Player/EyeLook.cs
git commit -m "feat(eye): EyeLook에 셰이프키 경로 — 구 경로는 A/B 검수까지 남긴다"
```

---

## 과제 4: QA 훅과 플레이모드 확인

**파일:**
- 생성: `Assets/Scripts/Testing/TestManager.Eye.cs`

**배경:** `TestManager`는 **부분 클래스 하나**가 진입점이고 wesqa가 리플렉션으로 부른다 —
타입을 쪼개거나 Editor 어셈블리로 옮기면 QA가 전멸한다. 새 파일은 **같은 부분 클래스**여야 한다.
기존 `TestManager.Gun.cs`를 형식의 본보기로 삼는다(네임스페이스·클래스 이름을 거기서 확인한다).

- [ ] **1단계: 훅을 쓴다**

```csharp
using UnityEngine;

namespace CTC.Runtime.Testing
{
    /// <summary>
    /// 눈 QA 훅(2026-09-13). **렌더러에 실제로 들어간 셰이프키 값**을 읽는다 —
    /// 계산한 값을 보면 배선이 끊겨도 통과한다.
    /// </summary>
    public partial class TestManager
    {
        /// <summary>구 방식 ↔ 셰이프키 방식. A/B를 같은 카메라로 찍기 위한 것이다.</summary>
        public void TestSetEyeMode(int _useBlendShapes)
        {
            Player.EyeLook[] all = FindObjectsByType<Player.EyeLook>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Player.EyeLook eye in all)
            {
                eye.DevSetEyeMode(_useBlendShapes != 0);
            }
        }

        /// <summary>"in,out,up,down,blink" — 쉼표로 붙인 다섯 값. 없으면 빈 문자열.</summary>
        public string TestGetEyeShapeWeights()
        {
            var shapes = FindFirstObjectByType<Player.EyeBlendShapes>();
            if (shapes == null) return string.Empty;
            Player.EyeShapeWeights w = shapes.DevWeights();
            return string.Format("{0:0.##},{1:0.##},{2:0.##},{3:0.##},{4:0.##}",
                w.LookIn, w.LookOut, w.LookUp, w.LookDown, w.Blink);
        }
    }
}
```

- [ ] **2단계: 플레이모드로 값이 실제로 들어가는지 본다**

`u_play`로 진입 → `TestSetEyeMode` 인자 `1` → 캐릭터를 조금 돌린 뒤 `TestGetEyeShapeWeights`.
기대: 다섯 수 중 **최소 하나가 0이 아니다.** 눈이 쉬는 방향에 딱 붙어 있으면 전부 0일 수 있으니
반드시 돌린 뒤에 읽는다. 그래도 전부 0이면 배선이 끊긴 것이므로 과제 3으로 돌아간다.

**함정:** `u_play invoke`의 `args`는 **쉼표 구분 문자열**이다(`"1"`). JSON 배열로 주면
조용히 버려지고 0이 들어간다.

- [ ] **3단계: 커밋**

```bash
git add CTC/Assets/Scripts/Testing/TestManager.Eye.cs*
git commit -m "test(eye): 셰이프키 가중치를 렌더러에서 되읽는 QA 훅"
```

---

## 과제 5: A/B 그림을 찍어 웹으로 보여준다

**파일:** 스크래치패드의 렌더·페이지 스크립트(프로젝트에 안 넣는다)

- [ ] **1단계: 찍을 표를 정한다**

| 축 | 값 |
|---|---|
| 모드 | 구 · 셰이프키 |
| 카메라 | 정면 · 3/4 · 측면 |
| 시선 | 정면 조준 · 위 · 아래 · **옆 목표** |
| 종 | 쥐 · 고양이 |

**「옆 목표」 칸을 빼지 않는다** — 이 설계가 후퇴할 유일한 지점이다(설계 §2).

- [ ] **2단계: 찍는다**

`TestSetEyeMode`로 모드를 바꾸고 **같은 카메라**로 두 번 찍는다. 몸을 둘 세우지 않는다.
씬 뷰 스크린샷(`u_editor_sceneview`)은 갱신이 안 붙어 같은 그림을 반복하므로 **쓰지 않는다** —
임시 카메라로 직접 렌더한다.

- [ ] **3단계: 웹 검수 페이지로 만든다**

기본 검수 수단은 웹뷰다(사용자 지정, 2026-09-12). 모드를 **나란히** 놓아 같은 칸에서 비교되게 한다.

- [ ] **4단계: 사용자에게 보여주고 고르게 한다**

**여기서 멈춘다.** 진 쪽을 지우는 것은 다음 작업이다 — 고르기 전에 지우면 되돌릴 길이 없다.

---

## 과제 6: 다이어그램 갱신 (규칙 8)

- [ ] **1단계:** `docs/design-vault/코드 다이어그램.md`에 `EyeBlendShapes`·`EyeBlendShapeLogic`을
      넣고 `EyeLook`에서 나가는 화살표를 그린다. 구 경로는 「A/B 검수 대기」로 표시해 남긴다.
- [ ] **2단계: 커밋**

```bash
git add "CTC/docs/design-vault/코드 다이어그램.md"
git commit -m "docs: 코드 다이어그램에 눈 셰이프키 경로 추가"
```

---

## 안 하는 것

- 구 경로·CSV 5칸·`EyeAnchorBaker` 크기 굽기·`CharmTunerWindow` 슬라이더 **삭제** — 과제 5 이후.
- 표정 22개 배선 — 정보 설계 검토를 안 거쳤다(설계 §7).
- 「질끈 감기」 — 팩에 없다. 별건.
