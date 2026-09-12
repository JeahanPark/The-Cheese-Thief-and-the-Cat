using System.Collections.Generic;
using System.IO;
using CTC.Runtime.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 쥐 사격 두 클립이 **실제로 걸리는지**를 묶는다.
    ///
    /// 여기서 지키는 것은 전부 "틀려도 에러가 안 나는" 종류다 —
    /// 철자가 어긋나면 <c>CrossFadeInFixedTime</c>은 조용히 아무 일도 안 하고,
    /// 상태가 컨트롤러에 없어도 마찬가지다. 화면을 보기 전에는 알 수 없으므로 여기서 막는다.
    /// </summary>
    public class MouseGunVisualLogicTests
    {
        private const string CONTROLLER_DIR =
            "Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Animations";

        private const string CLIPS_FBX = "Assets/Animations/Pets/CTC_Mouse_Clips.fbx";

        // ===== 클립 선택 규칙 =====

        [Test]
        public void GunNotOut_DrawsFirst()
        {
            Assert.AreEqual(AnimationStateLogic.ShootDraw, MouseGunVisualLogic.ClipForShot(false));
        }

        [Test]
        public void GunAlreadyOut_RecoilOnly()
        {
            Assert.AreEqual(AnimationStateLogic.Shoot, MouseGunVisualLogic.ClipForShot(true));
        }

        // ===== 뽑기 바꿔치기 시점 =====
        //
        // 허리춤 총 → 든 총으로 넘어가는 순간. 클립은 24fps 10프레임이고, 앞발이 허리를 떠나
        // 위로 휘어 나오는 것이 4프레임째라 3프레임(=3/24초)에서 바꾼다.

        [Test]
        public void DrawSwap_IsThreeFramesAt24Fps()
        {
            Assert.AreEqual(3f / 24f, MouseGunVisualLogic.DRAW_SWAP_SECONDS, 1e-6f);
        }

        [Test]
        public void BeforeSwap_HolsteredGunShows()
        {
            Assert.IsTrue(MouseGunVisualLogic.HolsteredAt(0f));
            Assert.IsTrue(MouseGunVisualLogic.HolsteredAt(MouseGunVisualLogic.DRAW_SWAP_SECONDS - 0.001f));
        }

        [Test]
        public void AtAndAfterSwap_HeldGunShows()
        {
            Assert.IsFalse(MouseGunVisualLogic.HolsteredAt(MouseGunVisualLogic.DRAW_SWAP_SECONDS));
            Assert.IsFalse(MouseGunVisualLogic.HolsteredAt(1f));
        }

        /// <summary>
        /// 바꿔치기는 클립이 끝나기 **한참 전에** 일어나야 한다 — 클립이 끝난 뒤에 바뀌면
        /// 총이 허리에 붙어 있는 채로 발사 포즈가 지나간다. 클립 길이(10프레임 @24fps)의
        /// 절반을 넘으면 그건 더 이상 "뽑는 순간"이 아니다.
        /// </summary>
        [Test]
        public void DrawSwap_HappensWellInsideTheClip()
        {
            const float CLIP_SECONDS = 10f / 24f;
            Assert.Less(MouseGunVisualLogic.DRAW_SWAP_SECONDS, CLIP_SECONDS * 0.5f);
            Assert.Greater(MouseGunVisualLogic.DRAW_SWAP_SECONDS, 0f);
        }

        // ===== 철자 =====
        //
        // 상수 값이 곧 애니메이터 상태 이름이자 FBX 클립 이름이다. 세 곳이 같은 글자여야
        // 하는데 어긋나도 아무도 안 알려 준다.

        [Test]
        public void ShootStateNames_AreSpelledExactly()
        {
            Assert.AreEqual("Shoot", AnimationStateLogic.Shoot);
            Assert.AreEqual("ShootDraw", AnimationStateLogic.ShootDraw);
        }

        // ===== 컨트롤러 =====

        [Test]
        public void ShootStates_ExistInMouseController()
        {
            HashSet<string> states = StatesIn("AC_Mouse.controller");
            foreach (string name in AnimationStateLogic.MouseOnly)
            {
                Assert.IsTrue(states.Contains(name),
                    $"AC_Mouse에 '{name}' 상태가 없다 — 이름이 없으면 원샷은 조용히 죽는다");
            }
        }

        /// <summary>
        /// 고양이는 총을 쏘지 않는다. 상태가 생겼다면 누가 두 컨트롤러를 같이 손댄 것이고,
        /// 그건 「고양이도 쏠 수 있다」로 오해될 배선이다.
        /// </summary>
        [Test]
        public void ShootStates_DoNotExistInCatController()
        {
            HashSet<string> states = StatesIn("AC_Cat.controller");
            foreach (string name in AnimationStateLogic.MouseOnly)
            {
                Assert.IsFalse(states.Contains(name),
                    $"AC_Cat에 '{name}' 상태가 생겼다 — 고양이는 총을 쏘지 않는다");
            }
        }

        // ===== FBX =====

        /// <summary>
        /// 왕복 FBX 안에 **그 이름 그대로** 클립이 있는지.
        ///
        /// 유니티는 임포트 중에 <c>__preview__Rig|Shoot</c> 같은 **내부 복제본**을 같은 파일에
        /// 들고 있다. 그것들은 FBX의 원본 테이크 이름(<c>Rig|…</c>)을 달고 있어서, 이름을
        /// 대충 훑는 검사는 클립 분리가 실패했어도 "있다"고 대답한다 — 그래서 먼저 걸러 낸다.
        /// </summary>
        [Test]
        public void RoundTripFbx_ContainsBothShootClips()
        {
            if (AssetDatabase.LoadMainAssetAtPath(CLIPS_FBX) == null)
            {
                Assert.Ignore($"왕복 FBX가 없다(아직 안 만들었거나 지워짐): {CLIPS_FBX}");
            }

            var names = new List<string>();
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(CLIPS_FBX))
            {
                var clip = o as AnimationClip;
                if (clip == null) continue;
                if (clip.name.StartsWith("__preview__")) continue; // 유니티 내부 복제본
                names.Add(clip.name);
            }

            Assert.IsNotEmpty(names, "클립을 하나도 못 읽었다 — 리그 인식 실패일 수 있다");
            foreach (string name in AnimationStateLogic.MouseOnly)
            {
                CollectionAssert.Contains(names, name,
                    $"{CLIPS_FBX}에 '{name}' 클립이 없다. 읽힌 것: {string.Join(", ", names)}");
            }
        }

        /// <summary>
        /// 걸러 낸 뒤의 이름에 테이크 구분자(<c>|</c>)가 남아 있으면 임포터가 테이크를
        /// 안 쪼갠 것이다 — 그때는 위 검사도 같이 의심해야 한다.
        /// </summary>
        [Test]
        public void RoundTripFbx_ClipNamesAreNotRawTakeNames()
        {
            if (AssetDatabase.LoadMainAssetAtPath(CLIPS_FBX) == null)
            {
                Assert.Ignore($"왕복 FBX가 없다: {CLIPS_FBX}");
            }

            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(CLIPS_FBX))
            {
                var clip = o as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__")) continue;
                Assert.IsFalse(clip.name.Contains("|"),
                    $"'{clip.name}'은 원본 테이크 이름 그대로다 — 클립 분리가 안 됐다");
            }
        }

        // ===== 총 모델은 하나뿐이다 =====

        /// <summary>
        /// **티어마다 다른 총을 보여 주면 고양이가 「저 쥐가 어느 티어인가」를 알게 된다 —
        /// 없던 정보다.** 소리는 거리로 줄지만 보이는 모델은 안 줄어든다.
        ///
        /// 지금까지 이 규칙을 지키는 건 주석뿐이었다. 티어별 총 메시를 하나 추가해도
        /// 아무것도 빨개지지 않았다 — 그래서 여기서 **개수를 센다.**
        /// </summary>
        [Test]
        public void OnlyOneGunMeshExists_NoPerTierVariants()
        {
            const string PROPS_DIR = "Assets/Models/Props";
            const string GUN_MESH = PROPS_DIR + "/Mesh_MouseGun.asset";

            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Mesh>(GUN_MESH),
                $"총 메시가 없다: {GUN_MESH} — Tools/CTC/Build Gun Mesh로 굽는다");

            string dir = Path.Combine(
                Application.dataPath, PROPS_DIR.Substring("Assets/".Length));
            if (!Directory.Exists(dir)) Assert.Ignore($"폴더가 없다: {dir}");

            var found = new List<string>();
            foreach (string path in Directory.GetFiles(dir, "*.asset", SearchOption.AllDirectories))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.IndexOf("Gun", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                found.Add(name);
            }

            CollectionAssert.AreEqual(new[] { "Mesh_MouseGun" }, found,
                "총 메시가 하나가 아니다. 티어(새총·기관총·긴 총열)는 **같은 모델을 나눠 써야 한다** — " +
                "티어마다 다른 총을 보여 주면 고양이가 「저 쥐가 어느 티어인가」를 알게 된다. " +
                "없던 정보다. 찾은 것: " + string.Join(", ", found));
        }

        /// <summary>
        /// 프리팹도 하나여야 한다 — 메시를 공유하면서 프리팹만 늘려도 재질·크기로 티어가 샌다.
        /// </summary>
        [Test]
        public void OnlyOneGunPrefabExists()
        {
            string dir = Path.Combine(Application.dataPath, "Prefabs/Props");
            if (!Directory.Exists(dir)) Assert.Ignore($"폴더가 없다: {dir}");

            var found = new List<string>();
            foreach (string path in Directory.GetFiles(dir, "*.prefab", SearchOption.AllDirectories))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                if (name.IndexOf("Gun", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                found.Add(name);
            }

            CollectionAssert.AreEqual(new[] { "MouseGun" }, found,
                "총 프리팹이 하나가 아니다 — 티어별 총은 「저 쥐가 어느 티어인가」를 새게 한다. " +
                "찾은 것: " + string.Join(", ", found));
        }

        /// <summary>
        /// 컨트롤러 원본에서 상태 이름 집합을 읽는다. 줄 단위로 정확히 견준다 —
        /// Shapekey 레이어의 <c>Eyes_Spin</c> 같은 이름에 부분 일치로 섞여 들지 않게.
        /// </summary>
        private static HashSet<string> StatesIn(string _controllerFile)
        {
            string path = Path.Combine(Application.dataPath, CONTROLLER_DIR, _controllerFile);
            if (!File.Exists(path)) Assert.Ignore($"컨트롤러 원본이 없다: {path}");

            var names = new HashSet<string>();
            foreach (string line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (!trimmed.StartsWith("m_Name: ")) continue;
                names.Add(trimmed.Substring("m_Name: ".Length));
            }
            return names;
        }
    }
}
