// 실제로 소리가 난 횟수 (2026-09-09 감사 C-18·C-19).
//
// **`SfxPlayer.Played` 이벤트로는 이걸 못 잰다.** 그 이벤트는 클립을 꺼내기 **전에** 울리므로
// 클립이 안 붙어 무음인 소리도 "났다"고 센다 — 봇 청각에는 그게 맞지만(설계값을 봐야 한다)
// "들렸나"를 묻는 QA에는 거짓말이다. 여기서는 `AudioSource.Play()`가 실제로 불린 자리만 센다.
//
// 개발 빌드에도 실린다 — wesqa가 빌드에서도 같은 수를 읽어야 한다. 배포 빌드에선 통째로 빠진다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.Text;

namespace CTC.Runtime.Audio
{
    /// <summary>
    /// SfxId별 실제 재생 횟수. QA가 재기 직전에 <see cref="Reset"/>하고 창을 열어 센다.
    ///
    /// 숨은 창고 Lv3의 "무음"처럼 **안 나는 것이 사양인 소리**는 이 수가 0이어야 증명된다.
    /// 그때 "안 났다"와 "아예 안 돌았다"를 구별하려면 틱 수(<c>TestGetWarehouseTicks</c>)를
    /// 같이 봐야 한다 — 둘 다 0이면 검사가 아무것도 안 본 것이다.
    /// </summary>
    public static class SfxPlayLog
    {
        private static readonly Dictionary<SfxId, int> s_Counts = new Dictionary<SfxId, int>();

        /// <summary>`SfxPlayer`가 소스를 실제로 Play()한 직후에만 부른다.</summary>
        public static void Record(SfxId _id)
        {
            s_Counts.TryGetValue(_id, out int n);
            s_Counts[_id] = n + 1;
        }

        public static int CountOf(SfxId _id) => s_Counts.TryGetValue(_id, out int n) ? n : 0;

        public static void Reset() => s_Counts.Clear();

        /// <summary>0이 아닌 것만 "이름=횟수"로 잇는다 — 무엇이 대신 울렸는지 보려고.</summary>
        public static string Dump()
        {
            var sb = new StringBuilder();
            foreach (KeyValuePair<SfxId, int> pair in s_Counts)
            {
                if (pair.Value <= 0) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(pair.Key).Append('=').Append(pair.Value);
            }
            return sb.ToString();
        }
    }
}
#endif
