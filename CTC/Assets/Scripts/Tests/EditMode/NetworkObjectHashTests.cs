using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CTC.Tests
{
    /// <summary>
    /// NGO GlobalObjectIdHash 베이크 검증 — SetupNetworkingScene이 프로그래매틱으로
    /// 생성한 NetworkObject의 해시가 0(미생성)이거나 서로 충돌하면 접속/스폰이 깨진다.
    /// (NetworkManager.cs:1475 "already contains the same GlobalObjectIdHash" Exception)
    /// 직렬화된 YAML을 직접 검사한다 — GlobalObjectIdHash는 internal이라 API 접근 불가.
    /// </summary>
    public class NetworkObjectHashTests
    {
        private static readonly string[] ASSET_PATHS =
        {
            "Assets/Scenes/Game.unity",
            "Assets/Prefabs/PlayerCat.prefab",
            "Assets/Prefabs/PlayerMouse.prefab",
        };

        private static readonly Regex HASH_PATTERN =
            new Regex(@"^\s*GlobalObjectIdHash:\s*(\d+)\s*$", RegexOptions.Multiline);

        private static List<(string asset, uint hash)> CollectHashes()
        {
            var result = new List<(string, uint)>();
            foreach (string path in ASSET_PATHS)
            {
                Assert.IsTrue(File.Exists(path), $"에셋 없음: {path}");
                foreach (Match m in HASH_PATTERN.Matches(File.ReadAllText(path)))
                    result.Add((path, uint.Parse(m.Groups[1].Value)));
            }
            return result;
        }

        [Test]
        public void AllNetworkObjects_HaveNonZeroHash()
        {
            List<(string asset, uint hash)> hashes = CollectHashes();
            Assert.IsNotEmpty(hashes, "NetworkObject를 하나도 못 찾음 — 경로/직렬화 포맷 확인");

            var zeros = hashes.Where(h => h.hash == 0).ToList();
            Assert.IsEmpty(zeros,
                "GlobalObjectIdHash=0 (베이크 누락): " + string.Join(", ", zeros.Select(z => z.asset)));
        }

        [Test]
        public void AllNetworkObjects_HaveUniqueHash()
        {
            List<(string asset, uint hash)> hashes = CollectHashes();
            var duplicates = hashes.GroupBy(h => h.hash).Where(g => g.Count() > 1).ToList();
            Assert.IsEmpty(duplicates, "GlobalObjectIdHash 충돌: " + string.Join("; ",
                duplicates.Select(g => $"{g.Key} ← {string.Join(", ", g.Select(h => h.asset))}")));
        }
    }
}
