// TestManager의 일부 — **소리가 실제로 났는가** (2026-09-09 감사 C-18·C-19).
//
// 기존 `TestGetWarehouseSfxClip`·`TestGetUnwiredSfxIds`는 "배선이 있다"까지만 본다.
// "숨은 창고 Lv3은 무음"처럼 **안 나는 것이 사양**인 소리는 그걸로 증명이 안 된다 —
// 실제 재생 횟수를 세야 하고, 그래서 `qa_v47` ⑦-b가 SKIP으로 남아 있었다.
//
// wesqa가 리플렉션으로 Instance.<method>를 부르므로 **타입을 쪼개지 않는다**(CLAUDE.md).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CTC.Runtime.Audio;
using CTC.Runtime.Core;

namespace CTC.Runtime.Testing
{
    public partial class TestManager
    {
        /// <summary>QA: 재기 직전에 창을 연다 — 그때까지의 재생 횟수를 전부 지운다.</summary>
        public void TestResetSfxPlayCounts() => SfxPlayLog.Reset();

        /// <summary>
        /// QA: 리셋 이후 그 SfxId가 **실제로 재생된** 횟수. 이름이 SfxId에 없으면 -1이다
        /// (0이 아니다 — 오타를 "안 났다"로 읽으면 무음 검사가 항진명제가 된다).
        /// </summary>
        public int TestGetSfxPlayCount(string _id)
        {
            if (!System.Enum.TryParse(_id, out SfxId id) || !System.Enum.IsDefined(typeof(SfxId), id))
            {
                GameDebug.LogWarning($"[TestManager] TestGetSfxPlayCount — '{_id}'는 SfxId가 아니다");
                return -1;
            }
            return SfxPlayLog.CountOf(id);
        }

        /// <summary>QA: 리셋 이후 실제로 난 소리 전부("이름=횟수" 쉼표 구분) — 무엇이 대신 울렸는지 본다.</summary>
        public string TestGetSfxPlayCounts() => SfxPlayLog.Dump();
    }
}
#endif
