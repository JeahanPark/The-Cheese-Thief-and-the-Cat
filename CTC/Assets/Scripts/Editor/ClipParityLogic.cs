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

        /// <summary>같은 리스트 안에서 키가 두 번 이상 나온 경우. 채취(추출) 쪽이 잘못됐다는 신호다.</summary>
        public List<string> DuplicateKeys { get; } = new List<string>();

        /// <summary>
        /// 커브 밖의 클립 설정(길이·프레임레이트·반복) 어긋남. 커브 비교는 이걸 못 본다 —
        /// 길이가 늘어나도 정규화된 지점마다 같은 값이 찍히기 때문이다.
        /// </summary>
        public List<string> SettingMismatches { get; } = new List<string>();

        /// <summary>비교에 쓴 커브 수(양쪽에 다 있는 고유 키). 0이면 잰 것이 없다는 뜻이다.</summary>
        public int ComparedCurves { get; set; }

        public bool IsIdentical =>
            ComparedCurves > 0 &&
            SettingMismatches.Count == 0 &&
            MissingInB.Count == 0 && ExtraInB.Count == 0 &&
            ValueMismatches.Count == 0 && DuplicateKeys.Count == 0;

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"견준 커브: {ComparedCurves}");
            // 길이가 다르면 아래 커브 비교 자체가 의미 없어진다 — 맨 앞에 둔다.
            Section(sb, "클립 설정 어긋남", SettingMismatches);
            Section(sb, "중복 키(채취가 잘못됐다)", DuplicateKeys);
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
    /// 클립 두 벌을 견준다. **Unity 애니메이션 API를 모른다** — 채취는 <see cref="ClipParityProbe"/>가 한다.
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

            Dictionary<string, float[]> byKeyA = BuildIndex(_a, "A", report.DuplicateKeys);
            Dictionary<string, float[]> byKeyB = BuildIndex(_b, "B", report.DuplicateKeys);

            // 이후 루프는 전부 딕셔너리(고유 키) 기준으로 돈다 — 원본 리스트를 다시 돌면
            // 중복 키가 있을 때 같은 키를 여러 번 세거나 서로 다른 판정을 두 번 내게 된다.
            foreach (string key in byKeyA.Keys)
            {
                if (!byKeyB.ContainsKey(key)) report.MissingInB.Add(key);
            }

            foreach (string key in byKeyB.Keys)
            {
                if (!byKeyA.ContainsKey(key)) report.ExtraInB.Add(key);
            }

            foreach (KeyValuePair<string, float[]> kv in byKeyA)
            {
                if (!byKeyB.TryGetValue(kv.Key, out float[] other)) continue;
                report.ComparedCurves++;

                float[] mine = kv.Value;
                if (mine.Length != other.Length)
                {
                    report.ValueMismatches.Add(
                        $"{kv.Key}: 샘플 수가 다르다 {mine.Length} vs {other.Length}");
                    continue;
                }

                for (int i = 0; i < mine.Length; i++)
                {
                    // 무한대끼리 빼면 NaN이고, NaN은 어떤 비교(<=)도 통과하지 못한다.
                    // 그래서 같은 값(무한대 포함)은 뺄셈 전에 먼저 걸러야 오탐이 안 난다.
                    if (mine[i] == other[i]) continue;

                    float delta = mine[i] - other[i];
                    if (delta < 0f) delta = -delta;
                    if (delta <= _epsilon) continue;

                    report.ValueMismatches.Add(
                        $"{kv.Key}[{i}]: {mine[i]:F5} vs {other[i]:F5} (Δ{delta:F5})");
                    break; // 커브당 한 건만 — 보고서가 수천 줄이 되면 안 읽는다
                }
            }

            return report;
        }

        /// <summary>
        /// 키 → 값 배열 인덱스를 만든다. 같은 키가 두 번 나오면 채취가 잘못된 것이므로
        /// <paramref name="_duplicates"/>에 적어 두고 **먼저 들어온 값을 그대로 둔다**(먼저 값이 이긴다).
        /// </summary>
        private static Dictionary<string, float[]> BuildIndex(
            IReadOnlyList<CurveSample> _samples, string _side, List<string> _duplicates)
        {
            var byKey = new Dictionary<string, float[]>();
            foreach (CurveSample s in _samples)
            {
                if (byKey.ContainsKey(s.Key))
                {
                    _duplicates.Add($"{_side}: {s.Key}");
                    continue;
                }

                byKey[s.Key] = s.Values;
            }

            return byKey;
        }
    }
}
