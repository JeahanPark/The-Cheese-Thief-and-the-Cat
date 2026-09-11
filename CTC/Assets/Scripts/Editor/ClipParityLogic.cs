using System.Collections.Generic;
using System.Text;

namespace CTC.Editor
{
    /// <summary>
    /// 클립 하나에서 뽑은 **커브 한 줄** — 키(경로:프로퍼티)와 같은 간격으로 잰 값들.
    /// </summary>
    public readonly struct CurveSample
    {
        public CurveSample(string _key, float[] _values)
        {
            Key = _key;
            Values = _values;
        }

        /// <summary>`Rig/root/body/head:m_LocalRotation.x` 꼴. **이 문자열이 곧 유니티가 뼈를 찾는 방법이다.**</summary>
        public string Key { get; }

        public float[] Values { get; }
    }

    /// <summary>두 클립의 차이. 비어 있고 **커브가 1개 이상일 때만** "같다"고 말한다.</summary>
    public sealed class ParityReport
    {
        public List<string> MissingInB { get; } = new List<string>();
        public List<string> ExtraInB { get; } = new List<string>();
        public List<string> ValueMismatches { get; } = new List<string>();

        /// <summary>비교에 쓴 커브 수(양쪽에 다 있는 키). 0이면 잰 것이 없다는 뜻이다.</summary>
        public int ComparedCurves { get; set; }

        public bool IsIdentical =>
            ComparedCurves > 0 &&
            MissingInB.Count == 0 && ExtraInB.Count == 0 && ValueMismatches.Count == 0;

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"견준 커브: {ComparedCurves}");
            Section(sb, "A에만 있음(사라진 뼈·프로퍼티)", MissingInB);
            Section(sb, "B에만 있음(늘어난 뼈·말단 본)", ExtraInB);
            Section(sb, "값 어긋남", ValueMismatches);
            sb.AppendLine(IsIdentical ? "판정: 같다" : "판정: 다르다");
            return sb.ToString();
        }

        private static void Section(StringBuilder _sb, string _title, List<string> _items)
        {
            if (_items.Count == 0) return;
            _sb.AppendLine($"[{_title}] {_items.Count}건");
            foreach (string s in _items) _sb.AppendLine("  " + s);
        }
    }

    /// <summary>
    /// 클립 두 벌을 견준다. **Unity 애니메이션 API를 모른다** — 채취는 ClipParityProbe(Task 2)가 한다.
    ///
    /// 왜 키 집합을 값보다 먼저 보는가(2026-09-12): 블렌더 FBX 왕복이 깨지는 세 가지가
    /// 전부 **키 집합의 사고**다 — 뼈 이름이 `아마추어이름+뼈이름`으로 바뀌거나, 계층에
    /// Armature가 한 겹 끼거나, Add Leaf Bones가 없던 말단을 만든다. 셋 다 값을 재기 전에
    /// **이름에서 죽는다.** 그래서 집합 차이가 곧 진단명이다.
    /// </summary>
    public static class ClipParityLogic
    {
        public static ParityReport Compare(
            IReadOnlyList<CurveSample> _a, IReadOnlyList<CurveSample> _b, float _epsilon)
        {
            var report = new ParityReport();

            var byKeyA = new Dictionary<string, float[]>();
            foreach (CurveSample s in _a) byKeyA[s.Key] = s.Values;

            var byKeyB = new Dictionary<string, float[]>();
            foreach (CurveSample s in _b) byKeyB[s.Key] = s.Values;

            foreach (CurveSample s in _a)
            {
                if (!byKeyB.ContainsKey(s.Key)) report.MissingInB.Add(s.Key);
            }

            foreach (CurveSample s in _b)
            {
                if (!byKeyA.ContainsKey(s.Key)) report.ExtraInB.Add(s.Key);
            }

            foreach (CurveSample s in _a)
            {
                if (!byKeyB.TryGetValue(s.Key, out float[] other)) continue;
                report.ComparedCurves++;

                if (s.Values.Length != other.Length)
                {
                    report.ValueMismatches.Add(
                        $"{s.Key}: 샘플 수가 다르다 {s.Values.Length} vs {other.Length}");
                    continue;
                }

                for (int i = 0; i < s.Values.Length; i++)
                {
                    float delta = s.Values[i] - other[i];
                    if (delta < 0f) delta = -delta;
                    if (delta <= _epsilon) continue;

                    report.ValueMismatches.Add(
                        $"{s.Key}[{i}]: {s.Values[i]:F5} vs {other[i]:F5} (Δ{delta:F5})");
                    break; // 커브당 한 건만 — 보고서가 수천 줄이 되면 안 읽는다
                }
            }

            return report;
        }
    }
}
