using System.Collections.Generic;
using CTC.Runtime.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CTC.Editor
{
    /// <summary>
    /// 깡패 쥐 소품(2026-09-14) — **몽둥이·모자** 메시를 SDF로 굽고 쥐 프리팹에 꺼진 채로 단다.
    /// 메뉴 `Tools/CTC/Dev/Bake Gang Props (몽둥이·모자)`. 설계: docs/superpowers/specs/2026-09-14-gang-mouse-look-design.md
    ///
    /// - 몽둥이: 총과 **같은 본·같은 자리·같은 회전**(<see cref="MouseGunBaker.HeldPosition"/>) — 총열이 +Z로 뻗듯 몽둥이도 +Z로 뻗는다.
    /// - 모자: 머리 꼭대기를 **두 귀 뼈에서 잰다** — 그것도 **BipedIdle 자세로 뼈를 돌린 채로**. 손으로 좌표를 박으면 리그가 바뀔 때 뜬다.
    /// - <see cref="GangLook"/>·<see cref="GangAngryFace"/> 컴포넌트도 여기서 붙인다.
    /// </summary>
    public static class GangPropBaker
    {
        private const string MOUSE_PREFAB = "Assets/Prefabs/PlayerMouse.prefab";
        private const string CLUB_MESH = PropAssets.PROP_FOLDER + "/Mesh_GangClub.asset";
        private const string HAT_MESH = PropAssets.PROP_FOLDER + "/Mesh_GangHat.asset";
        private const string CLUB_MAT = "Assets/Materials/Props/M_GangClub.mat";
        private const string HAT_MAT = "Assets/Materials/Props/M_GangHat.mat";

        private static readonly Color CLUB_COLOR = new Color(0.46f, 0.30f, 0.17f);   // 나무
        private static readonly Color HAT_COLOR = new Color(0.22f, 0.19f, 0.18f);    // 짙은 갈회색 중절모 — 순흑은 어두운 맵에서 파랗게 묻혔다

        // 몽둥이: 손잡이(가늘다) → 머리(굵다), +Z로 0.28m. 총(0.17m)보다 길어야 "무기를 바꿨다"가 읽힌다.
        private const float CLUB_LENGTH = 0.28f;
        private const float CLUB_HANDLE_R = 0.014f;
        private const float CLUB_HEAD_R = 0.036f;

        // 모자: 챙 반지름·크라운은 머리 폭에 비례해 굽는 시점에 정한다 — 이건 1 m 머리폭 기준 비율이다.
        // 크라운을 높게 — 귀 두 장이 챙 양옆을 가려서, 낮은 모자는 위에서 봐도 귀 사이 얼룩으로만 읽혔다(2026-09-14 촬영).
        private const float BRIM_RATIO = 0.58f;
        private const float CROWN_RATIO = 0.38f;
        private const float CROWN_HEIGHT_RATIO = 0.55f;
        private const float HAT_LIFT_RATIO = 0.10f;   // 챙 밑면을 두 귀 뿌리보다 위에 — 두피 속에 묻히지 않게

        // 화난 눈썹(2026-09-14 사용자 스케치). 비율은 눈 반지름 기준.
        private const string BROW_MESH = PropAssets.PROP_FOLDER + "/Mesh_GangBrow.asset";
        private const string BROW_MAT = "Assets/Materials/Props/M_GangBrow.mat";
        private static readonly Color BROW_COLOR = new Color(0.07f, 0.06f, 0.07f);
        private const string EYE_REGION_SHAPE = "eyes.shrink";
        private const float BROW_TILT_DEGREES = 18f;   // 코 쪽 끝이 내려가는 각 — 스케치의 기울기
        private const float BROW_FACE_TURN = 0.45f;    // 눈썹 판을 눈 법선에서 얼굴 앞쪽으로 돌리는 비율
        private const float BROW_LENGTH_RATIO = 1.05f; // 반길이 = 눈 반지름 × 이것
        private const float BROW_THICK_RATIO = 0.34f;  // 0.26은 멀리서 실선으로만 읽혔다
        private const float BROW_LIFT_RATIO = 0.95f;   // 눈 윗단(반지름 1.0)에 살짝 걸친다 — 누르는 느낌
        private const float BROW_OUT_RATIO = 0.30f;    // 얼굴 밖으로 — 판을 돌리면 코 쪽 끝이 머리 속으로 들어가므로 넉넉히

        // 모자를 얹는 기준 자세 — 깡패일 때만 보이는 소품이라 두 발 서기 자세에서 잰다.
        private const string BIPED_CLIPS_FBX = "Assets/Animations/Pets/CTC_Mouse_Clips.fbx";
        private const string BIPED_POSE_CLIP = "BipedIdle";

        [MenuItem("Tools/CTC/Dev/Bake Gang Props (몽둥이·모자)")]
        public static void BakeAll()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(MOUSE_PREFAB);
            if (root == null)
            {
                Debug.LogError($"[GangPropBaker] 프리팹 없음: {MOUSE_PREFAB}");
                return;
            }
            try
            {
                Attach(root);
                PrefabUtility.SaveAsPrefabAsset(root, MOUSE_PREFAB);
                Debug.Log($"[GangPropBaker] 몽둥이·모자·GangLook을 달았다: {MOUSE_PREFAB}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>프리팹 계층에 단다 — 생성기(<c>PlayerPrefabFactory</c>)가 갓 만든 계층에도 같이 쓴다. 멱등.</summary>
        public static void Attach(GameObject _root)
        {
            if (_root == null) return;

            Mesh club = BuildClubMesh();
            Material clubMat = SdfMesher.SaveMaterial(CLUB_MAT, "M_GangClub", CLUB_COLOR, 0.25f);
            // 변신 때 알파로 나타난다(GangLookVisual) — URP/Lit은 불투명이면 알파를 조용히 무시하므로 반투명으로 굽는다(총과 같다).
            GunMeshBuilder.MakeTransparent(clubMat);

            Transform paw = MouseGunBaker.FindBone(_root.transform, MouseGunBaker.GUN_BONE);
            if (paw == null)
            {
                Debug.LogError($"[GangPropBaker] '{MouseGunBaker.GUN_BONE}' 본이 없다");
                return;
            }
            GameObject clubGo = Replace(paw, GangLook.CLUB_NAME, club, clubMat);
            MouseGunBaker.Place(clubGo.transform, paw, MouseGunBaker.HeldPosition, MouseGunBaker.HeldEuler);
            clubGo.SetActive(false);

            AttachHat(_root);

            if (_root.GetComponent<GangAngryFace>() == null) _root.AddComponent<GangAngryFace>();
            if (_root.GetComponent<GangLookVisual>() == null) _root.AddComponent<GangLookVisual>();
            if (_root.GetComponent<GangLook>() == null) _root.AddComponent<GangLook>();
        }

        private static void AttachHat(GameObject _root)
        {
            Transform head = MouseGunBaker.FindBone(_root.transform, "head");
            SkinnedMeshRenderer skin = null;
            foreach (SkinnedMeshRenderer s in _root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (s.sharedMesh != null && (skin == null || s.sharedMesh.vertexCount > skin.sharedMesh.vertexCount)) skin = s;
            }
            if (head == null || skin == null)
            {
                Debug.LogError("[GangPropBaker] 머리 본이나 스킨 메시가 없다 — 모자를 못 단다");
                return;
            }

            // **모자를 쓰는 자세에서** 잰다. 프리팹의 쉬는 자세는 네 발이라 거기서 수평으로 얹으면,
            // 몸을 48도 젖히는 BipedIdle에서 모자가 뒤로 누워 머리 속에 파묻힌다(2026-09-14 촬영에서 크라운만 삐죽 보였다).
            // 뼈를 잠깐 그 자세로 돌리고, 다 달고 나면 되돌린다 — 프리팹이 두 발 자세로 저장되면 안 된다.
            Transform model = _root.transform.Find("Model");
            AnimationClip pose = ClipParityProbe.FindClip(BIPED_CLIPS_FBX, BIPED_POSE_CLIP);
            var saved = new List<(Transform t, Vector3 p, Quaternion r, Vector3 s)>();
            if (model != null && pose != null)
            {
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true)) saved.Add((t, t.localPosition, t.localRotation, t.localScale));
                pose.SampleAnimation(model.gameObject, 0f);   // 커브 경로 기준은 Animator가 있는 Model이다(함정 3)
            }
            else
            {
                Debug.LogWarning($"[GangPropBaker] '{BIPED_POSE_CLIP}' 클립이나 Model이 없다 — 네 발 자세 기준으로 얹는다(두 발에서 기울어 보일 수 있다)");
            }

            try
            {
                if (!MeasureHeadTop(_root.transform, head, out Vector3 top, out Vector3 up, out float width))
                {
                    Debug.LogError("[GangPropBaker] 귀 뼈를 못 찾았다 — 모자를 못 단다");
                    return;
                }

                Mesh hat = BuildHatMesh(width);
                Material hatMat = SdfMesher.SaveMaterial(HAT_MAT, "M_GangHat", HAT_COLOR, 0.35f);
                GunMeshBuilder.MakeTransparent(hatMat);
                GameObject hatGo = Replace(head, GangLook.HAT_NAME, hat, hatMat);

                // 월드에서 자리를 잡고 머리에 붙인다 — 뼈의 스케일(100)·축은 SetParent(worldPositionStays)가 되받는다.
                Vector3 forward = Vector3.ProjectOnPlane(_root.transform.forward, up).normalized;
                hatGo.transform.SetParent(null, true);
                hatGo.transform.position = top + up * (width * HAT_LIFT_RATIO);
                hatGo.transform.rotation = Quaternion.LookRotation(forward, up)
                                           * Quaternion.Euler(-8f, 0f, 6f);   // 살짝 뒤로·옆으로 삐딱하게 — 깡패 모자
                hatGo.transform.localScale = Vector3.one;
                hatGo.transform.SetParent(head, true);
                hatGo.SetActive(false);

                Debug.Log($"[GangPropBaker] 모자: 머리 폭 {width * 1000f:0}mm · 꼭대기 {top} · 위 {up} · 자세 {(pose != null ? BIPED_POSE_CLIP : "쉬는 자세")}");

                // 눈썹도 같은 자세에서 잰다 — 눈 자리는 스키닝된 메시에서만 나온다.
                AttachBrows(_root.transform, head, skin, up);
            }
            finally
            {
                foreach (var s in saved)
                {
                    if (s.t == null) continue;
                    s.t.localPosition = s.p;
                    s.t.localRotation = s.r;
                    s.t.localScale = s.s;
                }
            }
        }

        /// <summary>
        /// 화난 눈썹 둘(2026-09-14 사용자 스케치 — 콧등 쪽 끝이 내려간 사선 막대가 눈 윗단을 누른다).
        /// 팩에 「화남」 셰이프키가 없어서 소품으로 만든다. 머리 뼈 자식이라 고개를 따라가고, 모자처럼 꺼진 채 굽는다.
        ///
        /// 눈 자리는 **셰이프키 `eyes.shrink`가 움직이는 정점**이다 — 흰자·눈동자 전부가 줄어드는 키라 눈 영역을 정확히 짚는다.
        /// 좌우는 바인드 공간 x 부호로 가르고, 월드 좌표는 **머리 뼈 행렬 × 바인드포즈**로 직접 스키닝한다(BakeMesh는 샘플한 자세를 안 따랐다).
        /// 눈이 보는 쪽은 눈 정점 법선의 평균이다.
        /// </summary>
        private static void AttachBrows(Transform _root, Transform _head, SkinnedMeshRenderer _skin, Vector3 _up)
        {
            Mesh shared = _skin.sharedMesh;
            int shrink = shared.GetBlendShapeIndex(EYE_REGION_SHAPE);
            if (shrink < 0)
            {
                Debug.LogError($"[GangPropBaker] '{EYE_REGION_SHAPE}' 셰이프키가 없다 — 눈썹을 못 단다");
                return;
            }

            int count = shared.vertexCount;
            var deltas = new Vector3[count];
            shared.GetBlendShapeFrameVertices(shrink, shared.GetBlendShapeFrameCount(shrink) - 1, deltas, null, null);
            Vector3[] bind = shared.vertices;
            Vector3[] bindNormals = shared.normals;

            // 눈 정점은 머리 뼈에 100% 붙어 있다 → **머리 뼈 행렬 × 바인드포즈**가 곧 스키닝 결과다.
            // BakeMesh로 재 봤더니 막 샘플한 두 발 자세가 아니라 **쉬는 자세의 눈 자리**를 줘서(에디터 프리팹 씬에선 스키닝이 안 돈다 —
            // CLAUDE.md 「본을 코드로 돌려도 에디터에선 다시 스키닝되지 않는다」), 눈썹이 눈 가장자리에 세로로 섰다(2026-09-14).
            int headIndex = System.Array.IndexOf(_skin.bones, _head);
            if (headIndex < 0 || headIndex >= shared.bindposes.Length)
            {
                Debug.LogError("[GangPropBaker] 스킨 뼈 목록에 head가 없다 — 눈썹을 못 단다");
                return;
            }
            Matrix4x4 headSkin = _head.localToWorldMatrix * shared.bindposes[headIndex];

            Material browMat = SdfMesher.SaveMaterial(BROW_MAT, "M_GangBrow", BROW_COLOR, 0.2f);
            foreach (int side in new[] { -1, 1 })
            {
                var eye = new List<Vector3>();
                Vector3 normalSum = Vector3.zero;
                for (int i = 0; i < count; i++)
                {
                    if (deltas[i].sqrMagnitude < 1e-12f || Mathf.Sign(bind[i].x) != side) continue;
                    eye.Add(headSkin.MultiplyPoint3x4(bind[i]));
                    if (bindNormals.Length == count) normalSum += headSkin.MultiplyVector(bindNormals[i]).normalized;
                }
                string name = side < 0 ? GangLook.BROW_PREFIX + "R" : GangLook.BROW_PREFIX + "L";
                if (eye.Count < 8)
                {
                    Debug.LogError($"[GangPropBaker] {name}: 눈 정점이 {eye.Count}개뿐이다 — 건너뛴다");
                    continue;
                }

                Vector3 center = Vector3.zero;
                foreach (Vector3 p in eye) center += p;
                center /= eye.Count;

                // 눈이 보는 쪽 = 눈 정점 법선의 평균. 「머리 뼈 → 눈」으로 잡았더니 뼈가 목 뒤(169mm)에 있어 앞쪽을 가리켰고,
                // 그 평면에 코 방향을 투영하니 막대가 거의 세로로 서서 눈을 가로질렀다(2026-09-14 인게임 촬영).
                Vector3 normal = normalSum.sqrMagnitude > 1e-6f ? normalSum.normalized : (center - _head.position).normalized;
                // 눈썹 판은 **눈 표면이 아니라 얼굴 앞쪽으로 반쯤 돌린 면**에 둔다. 쥐 눈은 옆을 보고 있어서, 눈 표면에 그대로 누이면
                // 앞·앞옆에서 막대가 세로로 곧추서 보였다(2026-09-14 인게임 촬영). 앞으로 돌리면 정면에서 「\ /」로 읽힌다.
                normal = (normal * (1f - BROW_FACE_TURN) + _root.forward * BROW_FACE_TURN).normalized;
                Vector3 alongUp = Vector3.ProjectOnPlane(_up, normal).normalized;              // 눈썹 판 위 방향
                Vector3 toNose = Vector3.ProjectOnPlane(_root.forward, normal).normalized;     // 눈썹 판에서 코(얼굴 가운데) 쪽
                float radius = 0f;
                foreach (Vector3 p in eye) radius = Mathf.Max(radius, Vector3.Dot(p - center, alongUp));

                // 코 쪽 끝이 내려간다 — 막대 축을 코 방향에서 아래로 BROW_TILT만큼 돌린다.
                float tilt = BROW_TILT_DEGREES * Mathf.Deg2Rad;
                Vector3 axis = (toNose * Mathf.Cos(tilt) - alongUp * Mathf.Sin(tilt)).normalized;

                Mesh brow = BuildBrowMesh(radius);
                GameObject go = Replace(_head, name, brow, browMat);
                go.transform.SetParent(null, true);
                go.transform.position = center + alongUp * (radius * BROW_LIFT_RATIO) + normal * (radius * BROW_OUT_RATIO);
                go.transform.rotation = Quaternion.LookRotation(axis, normal);   // 메시: +Z가 막대 길이, +Y가 얼굴 밖
                go.transform.localScale = Vector3.one;
                go.transform.SetParent(_head, true);
                go.SetActive(false);

                Debug.Log($"[GangPropBaker] {name}: 눈 정점 {eye.Count} · 반지름 {radius * 1000f:0}mm · 법선 {normal} · 코쪽 {toNose} · 축 {axis}");
            }
        }

        /// <summary>눈썹 막대 — 길이 +Z(눈 지름보다 약간 길게), 두께(보이는 굵기) X, 얼굴 밖 두께 Y. 코 쪽(+Z) 끝을 조금 굵게.</summary>
        private static Mesh BuildBrowMesh(float _eyeRadius)
        {
            float half = _eyeRadius * BROW_LENGTH_RATIO;
            float thick = _eyeRadius * BROW_THICK_RATIO;
            float depth = thick * 0.6f;
            string path = BROW_MESH;
            System.Func<Vector3, float> field = _p =>
            {
                // 코 쪽으로 갈수록 굵어지는 둥근 막대 — 두 구를 부드럽게 잇는다
                float t = Mathf.Clamp01((_p.z + half) / (2f * half));
                float r = Mathf.Lerp(thick * 0.35f, thick * 0.55f, t);
                Vector3 onAxis = new Vector3(0f, 0f, Mathf.Clamp(_p.z, -half, half));
                Vector3 q = _p - onAxis;
                // 얼굴 밖(Y) 쪽을 납작하게 — Y를 늘려 재고, 늘린 만큼 거리를 되줄인다(보수적인 근사 거리).
                float squash = thick / depth;
                return (new Vector3(q.x, q.y * squash, q.z).magnitude - r) / squash;
            };
            float pad = thick;
            Mesh built = SdfMesher.Build("Mesh_GangBrow", field, new Vector3(-pad, -pad, -half - pad), new Vector3(pad, pad, half + pad),
                Mathf.Max(0.0015f, thick * 0.12f), 4, 0.5f, 2);
            Mesh mesh = SdfMesher.SaveMesh(built, path);
            SdfMesher.Commit(path);
            return mesh;
        }

        /// <summary>
        /// 머리 꼭대기와 폭 — **두 귀 뼈의 뿌리**로 잰다. 귀는 머리 꼭대기 양옆에 붙어 있어 가운데가 곧 정수리,
        /// 두 귀 사이 거리가 곧 머리 윗면 폭이다.
        /// 처음엔 스킨 메시 정점으로 쟀는데 BakeMesh의 스케일(FBX 0.01·뼈 100)이 꼬여 **머리 폭 7mm**가 나왔다(2026-09-14).
        /// 뼈 월드 좌표는 그런 꼬임이 없다.
        /// </summary>
        private static bool MeasureHeadTop(Transform _root, Transform _head, out Vector3 _top, out Vector3 _up, out float _width)
        {
            _top = Vector3.zero;
            _up = Vector3.up;
            _width = 0f;
            Transform earL = MouseGunBaker.FindBone(_root, "ear.L");
            Transform earR = MouseGunBaker.FindBone(_root, "ear.R");
            if (earL == null || earR == null) return false;

            _top = (earL.position + earR.position) * 0.5f;
            _width = Vector3.Distance(earL.position, earR.position) * 1.15f;
            // 정수리 방향 = 머리 뼈 → 두 귀 가운데. 자세를 따라 기울기 때문에 루트의 위(up)보다 맞다.
            Vector3 toTop = _top - _head.position;
            if (toTop.sqrMagnitude > 1e-8f) _up = toTop.normalized;
            return _width > 1e-3f;
        }

        private static GameObject Replace(Transform _parent, string _name, Mesh _mesh, Material _mat)
        {
            Transform old = _parent.Find(_name);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            var go = new GameObject(_name);
            go.transform.SetParent(_parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            go.tag = _parent.gameObject.tag;
            go.layer = _parent.gameObject.layer;
            return go;
        }

        // ── 메시 ─────────────────────────────────────────────

        private static Mesh BuildClubMesh()
        {
            // 원기둥을 Z축으로 쓰려고 샘플 지점을 X로 90도 돌려 Y 원기둥에 넣는다.
            System.Func<Vector3, float> field = _p =>
            {
                Vector3 q = new Vector3(_p.x, _p.z, -_p.y);   // Z → Y
                float handle = SdfMesher.RoundCylinderY(q, new Vector3(0f, 0.07f, 0f), CLUB_HANDLE_R, 0.08f, 0.006f);
                // 머리는 끝으로 갈수록 굵다 — 반지름이 다른 둥근 원기둥 둘을 부드럽게 잇는다
                float neck = SdfMesher.RoundCylinderY(q, new Vector3(0f, 0.17f, 0f), (CLUB_HANDLE_R + CLUB_HEAD_R) * 0.5f, 0.03f, 0.01f);
                float head = SdfMesher.RoundCylinderY(q, new Vector3(0f, 0.235f, 0f), CLUB_HEAD_R, 0.045f, 0.02f);
                float knob = SdfMesher.Sphere(q, new Vector3(0f, -0.012f, 0f), CLUB_HANDLE_R * 1.35f);   // 손잡이 끝 뭉치
                float d = SdfMesher.SmoothUnion(handle, neck, 0.02f);
                d = SdfMesher.SmoothUnion(d, head, 0.02f);
                d = SdfMesher.SmoothUnion(d, knob, 0.008f);
                // 옹이 둘 — 나무 방망이로 읽히게
                d = SdfMesher.SmoothUnion(d, SdfMesher.Sphere(q, new Vector3(0.03f, 0.22f, 0.012f), 0.012f), 0.01f);
                d = SdfMesher.SmoothUnion(d, SdfMesher.Sphere(q, new Vector3(-0.022f, 0.255f, -0.02f), 0.010f), 0.01f);
                return d;
            };
            Mesh built = SdfMesher.Build("Mesh_GangClub", field, new Vector3(-0.06f, -0.06f, -0.04f), new Vector3(0.06f, 0.06f, CLUB_LENGTH + 0.03f),
                0.004f, 6, 0.5f, 3);
            Mesh mesh = SdfMesher.SaveMesh(built, CLUB_MESH);
            SdfMesher.Commit(CLUB_MESH);
            return mesh;
        }

        private static Mesh BuildHatMesh(float _headWidth)
        {
            float brim = _headWidth * BRIM_RATIO;
            float crown = _headWidth * CROWN_RATIO;
            float crownH = _headWidth * CROWN_HEIGHT_RATIO;
            float t = Mathf.Max(0.004f, _headWidth * 0.03f);

            System.Func<Vector3, float> field = _p =>
            {
                float brimD = SdfMesher.RoundCylinderY(_p, new Vector3(0f, t, 0f), brim, t, t * 0.8f);
                float crownD = SdfMesher.RoundCylinderY(_p, new Vector3(0f, t * 2f + crownH * 0.5f, 0f), crown, crownH * 0.5f, crown * 0.25f);
                // 크라운 윗면을 앞뒤로 눌러 중절모 주름
                float dent = SdfMesher.Box(_p, new Vector3(0f, t * 2f + crownH + crown * 0.05f, 0f), new Vector3(crown * 0.12f, crown * 0.18f, crown * 0.8f));
                float d = SdfMesher.SmoothUnion(brimD, crownD, t * 1.5f);
                return SdfMesher.SmoothSubtract(d, dent, t);
            };
            float r = brim + t * 2f;
            Mesh built = SdfMesher.Build("Mesh_GangHat", field, new Vector3(-r, -t, -r), new Vector3(r, t * 3f + crownH + crown * 0.3f, r),
                Mathf.Max(0.0025f, _headWidth * 0.02f), 6, 0.5f, 3);
            Mesh mesh = SdfMesher.SaveMesh(built, HAT_MESH);
            SdfMesher.Commit(HAT_MESH);
            return mesh;
        }
    }
}
