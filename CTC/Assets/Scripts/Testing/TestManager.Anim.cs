// TestManager의 일부 — **애니메이터가 실제로 무엇을 돌리고 있는가** (2026-09-09 감사 B-04·C-23).
//
// 이 프로젝트의 QA에서 세 번째로 되풀이된 병이 "Model만 보고 몸을 안 본다"였다.
// 원샷(대시 Dash · 끈끈이 Sticky · 요새 Fortress · 창고 Warehouse · 광분 Frenzy · 전직 CatSkill · 구매 Clicked · 채굴 Eat)은
// 지금까지 **컨트롤러에 이름이 있다**까지만 검사했다 — 이름이 맞아도 배선이 끊기면 아무 일도
// 안 일어나고, `CrossFadeInFixedTime`은 그때 에러도 경고도 안 낸다.
//
// wesqa가 리플렉션으로 Instance.<method>를 부르므로 **타입을 쪼개지 않는다**(CLAUDE.md).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using CTC.Runtime.Core;
using CTC.Runtime.Player;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        private static PlayerAnimationDriver DriverOf(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            return p != null ? p.GetComponent<PlayerAnimationDriver>() : null;
        }

        /// <summary>
        /// QA: 그 몸의 애니메이터가 **지금 실제로 돌리는** 상태 이름(전이 중이면 목적지).
        ///
        /// 못 찾으면 "NoDriver", Animator가 없으면 빈 문자열, 아는 이름이 아니면 "?해시".
        /// **없는 것을 정상값으로 돌려주지 않는다**(감사 T9).
        ///
        /// 원샷은 0.35~0.6초만 산다 — 한 번 물어보는 방식은 왕복 지연에 놓치므로,
        /// 실제 판정에는 <see cref="TestGetAnimObserved"/>를 쓴다.
        /// </summary>
        public string TestGetAnimState(long _clientId)
        {
            PlayerAnimationDriver d = DriverOf(_clientId);
            return d != null ? d.PlayingState : "NoDriver";
        }

        /// <summary>
        /// QA: 그 몸의 드라이버가 **시킨** 상태 이름. `TestGetAnimState`와 다르면
        /// 「시켰는데 안 걸렸다」 — 철자 오타로 조용히 죽은 원샷의 지문이다.
        /// </summary>
        public string TestGetAnimStateRequested(long _clientId)
        {
            PlayerAnimationDriver d = DriverOf(_clientId);
            return d != null ? d.RequestedState : "NoDriver";
        }

        /// <summary>QA: 로컬 플레이어의 현재 상태 — clientId를 모르는 쪽이 쓴다.</summary>
        public string TestGetAnimStateLocal()
        {
            PlayerAnimationDriver d = LocalComponent<PlayerAnimationDriver>();
            return d != null ? d.PlayingState : "NoDriver";
        }

        /// <summary>
        /// QA: 관측 창을 연다 — 그 몸이 지금까지 지나온 상태 기록을 지운다.
        /// 동작을 일으키기 **직전에** 부르고, 끝나면 <see cref="TestGetAnimObserved"/>로 읽는다.
        /// false = 그 몸을 못 찾았다(「안 걸렸다」와 구분된다).
        /// </summary>
        public bool TestResetAnimObserved(long _clientId)
        {
            PlayerAnimationDriver d = DriverOf(_clientId);
            if (d == null)
            {
                GameDebug.LogWarning($"[TestManager] TestResetAnimObserved — {_clientId} 드라이버 없음");
                return false;
            }
            d.ClearObservedStates();
            FaceExpression face = d.GetComponent<FaceExpression>();
            if (face != null) face.DevClearObservedFaces();
            return true;
        }

        /// <summary>
        /// QA: 리셋 이후 애니메이터가 **실제로 들어갔던** 상태들(쉼표 구분, 연속 중복은 접힘).
        /// 여기에 원샷 이름이 없으면 그 원샷은 재생되지 않았다.
        /// </summary>
        public string TestGetAnimObserved(long _clientId)
        {
            PlayerAnimationDriver d = DriverOf(_clientId);
            if (d == null) return "NoDriver";
            IReadOnlyList<string> seen = d.ObservedStates;
            return string.Join(",", seen);
        }

        /// <summary>
        /// QA: 그 몸의 **표정 레이어가 실제로 들어갔던** 표정들(쉼표 구분, 연속 중복은 접힘) — 2026-09-13.
        /// <see cref="TestResetAnimObserved"/>가 같이 지운다. 표정 컴포넌트가 없으면 "NoFace".
        /// </summary>
        public string TestGetFaceObserved(long _clientId)
        {
            GameObject p = FindPlayerGo(_clientId);
            FaceExpression f = p != null ? p.GetComponent<FaceExpression>() : null;
            if (f == null) return "NoFace";
            return string.Join(",", f.DevObservedFaces);
        }

        /// <summary>
        /// QA: 그 몸의 뼈 하나의 **지금** 로컬 회전 "x,y,z"(오일러, 도) — 2026-09-13 무릎 검증용.
        /// 애니메이터가 새로 넣은 뼈(<c>shin.B.L</c>)를 실제로 굴리는지는 이걸로만 안다 — 경로가 어긋나면
        /// 에러 없이 쉬는 값에 머문다. 가만히 있을 때와 쏠 때를 견준다. 뼈를 못 찾으면 "NoBone".
        /// </summary>
        public string TestGetBoneLocalEuler(long _clientId, string _bone)
        {
            GameObject p = FindPlayerGo(_clientId);
            if (p == null) return "NoPlayer";
            foreach (Transform t in p.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != _bone) continue;
                Vector3 e = t.localEulerAngles;
                return $"{e.x:0.#},{e.y:0.#},{e.z:0.#}";
            }
            return "NoBone";
        }

        /// <summary>QA: 리셋 이후 그 상태가 한 번이라도 실제로 걸렸는가.</summary>
        public bool TestSawAnimState(long _clientId, string _state)
        {
            PlayerAnimationDriver d = DriverOf(_clientId);
            if (d == null) return false;
            foreach (string s in d.ObservedStates)
            {
                if (s == _state) return true;
            }
            return false;
        }
    }
}
#endif
