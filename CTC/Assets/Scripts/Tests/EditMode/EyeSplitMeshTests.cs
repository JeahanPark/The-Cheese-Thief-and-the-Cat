using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 플레이어 프리팹이 **좌우로 쪼갠 눈 셰이프키를 가진 메시**를 무는지 묶는다(2026-09-13).
    ///
    /// 조용히 깨지는 자리다: 프리팹이 팩 원본 메시로 돌아가도 에러가 없고, <see cref="EyeBlendShapes"/>가
    /// 경고 한 줄 찍고 꺼질 뿐이라 **눈이 그냥 굳는다.** 씬 재생성이 매번 프리팹을 새로 굽고
    /// <c>PlayerPrefabFactory</c>가 굽기를 부르는데, 그 한 줄이 빠지면 여기가 빨개진다.
    /// </summary>
    public class EyeSplitMeshTests
    {
        private static readonly string[] PREFABS =
        {
            "Assets/Prefabs/PlayerMouse.prefab",
            "Assets/Prefabs/PlayerCat.prefab",
        };

        private static readonly string[] SPLIT_KEYS =
        {
            "eyes.lookIn.R", "eyes.lookIn.L", "eyes.lookOut.R", "eyes.lookOut.L",
        };

        /// <summary>
        /// **LOD 렌더러 전부**가 쪼갠 키를 가져야 한다. 팩 프리팹은 LODGroup에 `Mesh_LOD0..3` 넷이라,
        /// 첫 렌더러만 보면 멀어졌을 때 눈이 멈추는 사고를 못 잡는다(2026-09-13에 실제로 그 상태였다).
        /// </summary>
        [Test]
        public void PlayerPrefabs_EveryLodUsesMeshWithSplitEyeKeys()
        {
            foreach (string path in PREFABS)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) Assert.Ignore($"프리팹이 없다(씬을 아직 안 구웠다): {path}");

                int withEyes = 0;
                var meshes = new System.Collections.Generic.HashSet<Mesh>();
                foreach (SkinnedMeshRenderer skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Mesh mesh = skin.sharedMesh;
                    Assert.IsNotNull(mesh, $"{path}의 '{skin.name}'에 메시가 비었다");
                    if (mesh.GetBlendShapeIndex("eyes.lookIn") < 0) continue;   // 눈이 없는 렌더러(총 등)

                    withEyes++;
                    meshes.Add(mesh);
                    foreach (string key in SPLIT_KEYS)
                    {
                        Assert.GreaterOrEqual(mesh.GetBlendShapeIndex(key), 0,
                            $"{path}의 '{skin.name}'(메시 '{mesh.name}')에 '{key}'가 없다 — 이 LOD에서 눈이 굳는다. " +
                            "Tools/CTC/Dev/Bake Eye Split Meshes를 돌려라");
                    }
                }

                Assert.Greater(withEyes, 0, $"{path}에 눈 셰이프키를 가진 렌더러가 하나도 없다");
                // LOD마다 메시가 따로여야 한다 — 전부 한 메시를 물면 이름 충돌로 한 파일에 겹친 것이다.
                Assert.AreEqual(withEyes, meshes.Count,
                    $"{path}: 눈 렌더러 {withEyes}개가 메시 {meshes.Count}개를 나눠 문다 — 파생 메시가 한 파일로 겹쳤다");
            }
        }

        /// <summary>
        /// **쥐와 고양이가 같은 파생 메시를 물면 안 된다.** 팩 FBX 메시 이름이 전부 "Mesh"라,
        /// 이름으로 파일을 만들면 두 종이 한 파일로 겹친다(2026-09-13에 실제로 겹쳤다).
        /// </summary>
        [Test]
        public void MouseAndCat_DoNotShareEyeMeshes()
        {
            var mouse = AssetDatabase.LoadAssetAtPath<GameObject>(PREFABS[0]);
            var cat = AssetDatabase.LoadAssetAtPath<GameObject>(PREFABS[1]);
            if (mouse == null || cat == null) Assert.Ignore("프리팹이 없다");

            var mouseMeshes = new System.Collections.Generic.HashSet<Mesh>();
            foreach (SkinnedMeshRenderer s in mouse.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (s.sharedMesh != null) mouseMeshes.Add(s.sharedMesh);
            }
            foreach (SkinnedMeshRenderer s in cat.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (s.sharedMesh == null) continue;
                Assert.IsFalse(mouseMeshes.Contains(s.sharedMesh),
                    $"고양이 '{s.name}'가 쥐와 같은 메시 '{s.sharedMesh.name}'를 문다 — 파생 메시 파일이 겹쳤다");
            }
        }

        /// <summary>
        /// **팩 컨트롤러가 쓰는 표정 키를 지우면 안 된다.** AC_Mouse·AC_Cat의 Shapekey 레이어가 기본으로
        /// `Eyes_Annoyed`를 틀어 `eyes.annoyed`를 매 프레임 100으로 쓴다 — 지금 게임의 기본 얼굴이 그것이다.
        /// 파생 메시에서 그 키가 빠지면 애니메이터 커브가 **에러 없이 허공을 가리켜** 얼굴이 조용히 바뀐다.
        /// (2026-09-13에 크기를 줄이려고 안 쓰는 표정을 지우는 안이 나왔다가 이걸 보고 폐기했다.)
        /// </summary>
        [Test]
        public void DerivedMeshes_KeepThePackDefaultExpressionKey()
        {
            foreach (string path in PREFABS)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) Assert.Ignore($"프리팹이 없다: {path}");

                foreach (SkinnedMeshRenderer skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    Mesh mesh = skin.sharedMesh;
                    if (mesh == null || mesh.GetBlendShapeIndex("eyes.lookIn.R") < 0) continue;
                    Assert.GreaterOrEqual(mesh.GetBlendShapeIndex("eyes.annoyed"), 0,
                        $"{path}의 '{skin.name}'에서 eyes.annoyed가 빠졌다 — 기본 얼굴이 바뀐다");
                }
            }
        }

        /// <summary>
        /// **파생 메시가 다시 부풀면 빨개진다.** 셰이프키를 그대로 복제하면 정점이 거의 다 저장돼
        /// 8개가 **54MB**였다(레포 전체가 24MB). 안 움직이는 정점을 0으로 눌러 저장에서 빼는 것이
        /// `EyeSplitMeshBaker.SnapNoise`인데, 그게 빠지면 여기서 드러난다.
        /// </summary>
        [Test]
        public void DerivedMeshAssets_StaySmall()
        {
            const long LIMIT_BYTES = 12L * 1024 * 1024;
            string[] guids = AssetDatabase.FindAssets("_EyeSplit t:Mesh", new[] { "Assets/Models/Characters" });
            if (guids.Length == 0) Assert.Ignore("파생 메시가 아직 없다");

            long total = 0;
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                total += new System.IO.FileInfo(assetPath).Length;
            }
            Assert.Less(total, LIMIT_BYTES,
                $"눈 파생 메시 {guids.Length}개가 {total / 1024 / 1024}MB다 — 안 움직이는 정점까지 저장되고 있다");
        }

        /// <summary>
        /// 쪼갠 두 키를 더하면 **원본 키와 같아야** 한다 — 좌우 판정이 틀려 한 정점이 양쪽에 들어가거나
        /// 어느 쪽에도 안 들어가면 여기서 드러난다. 쪼개기가 「새로 그리기」가 아니라
        /// 「나누기」라는 약속을 붙드는 테스트다.
        /// </summary>
        [Test]
        public void SplitKeys_SumBackToTheOriginal()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFABS[0]);
            if (prefab == null) Assert.Ignore("쥐 프리팹이 없다");
            Mesh mesh = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMesh;

            foreach (string source in new[] { "eyes.lookIn", "eyes.lookOut" })
            {
                int o = mesh.GetBlendShapeIndex(source);
                int r = mesh.GetBlendShapeIndex(source + ".R");
                int l = mesh.GetBlendShapeIndex(source + ".L");
                if (o < 0 || r < 0 || l < 0) Assert.Fail($"'{source}' 계열 키가 빠졌다");

                int n = mesh.vertexCount;
                var ov = new Vector3[n]; var rv = new Vector3[n]; var lv = new Vector3[n];
                var tmpN = new Vector3[n]; var tmpT = new Vector3[n];
                mesh.GetBlendShapeFrameVertices(o, 0, ov, tmpN, tmpT);
                mesh.GetBlendShapeFrameVertices(r, 0, rv, tmpN, tmpT);
                mesh.GetBlendShapeFrameVertices(l, 0, lv, tmpN, tmpT);

                int movedR = 0, movedL = 0;
                for (int i = 0; i < n; i++)
                {
                    Assert.Less((rv[i] + lv[i] - ov[i]).magnitude, 1e-6f,
                        $"{source} 정점 {i}: R+L이 원본과 다르다 — 좌우 나누기가 새거나 겹쳤다");
                    if (rv[i].sqrMagnitude > 1e-12f) movedR++;
                    if (lv[i].sqrMagnitude > 1e-12f) movedL++;
                    Assert.IsFalse(rv[i].sqrMagnitude > 1e-12f && lv[i].sqrMagnitude > 1e-12f,
                        $"{source} 정점 {i}이 양쪽 키에 다 들어갔다");
                }
                Assert.Greater(movedR, 0, $"{source}.R이 아무 정점도 안 움직인다");
                Assert.Greater(movedL, 0, $"{source}.L이 아무 정점도 안 움직인다");
            }
        }
    }
}
