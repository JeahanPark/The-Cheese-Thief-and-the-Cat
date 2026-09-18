using System.Collections.Generic;
using CTC.Runtime.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CTC.Editor
{
    /// <summary>
    /// 상점 부스(2026-09-19, 기획 `2026-09-19-shop-booth-design.md` §1) — 나무 상자 가게 · 창구 · 판매대 · 줄무늬 차양 ·
    /// 지붕 위 치즈 간판 · **철문**(셔터 상자에서 말려 내려온다) · 안에 **쥐 점원**. 상점 자리 프리팹(<see cref="ShopSpotPrefabFactory"/>)에 단다.
    ///
    /// - 모양은 **상자 메시**로 짓는다(부스 하나에 정점 수백 개) — 네모난 궤짝이라 SDF로 굴리면 정점만 수만 개가 된다.
    /// - 나무는 **맵과 같은 삼면 투영 셰이더**(<c>CTC/MapTriplanar</c> · 판자 텍스처)라 UV 없이 창고 결과 맞는다.
    /// - 철문 판은 **윗변이 원점**이라 <see cref="ShopShutter"/>가 세로 배율로 말았다 내린다.
    /// - 점원은 팩 쥐 모델을 **밤색 팔레트**(<c>T_ShopKeeper</c>)로 칠한다. 콜라이더는 걷어 낸다.
    /// - 몸통만 막는다(상자 콜라이더 + 격자 <c>NavStructureKind.Solid</c>) — 차양·판매대·간판은 콜라이더가 없다.
    /// 좌표계: 부스 로컬, 원점 = 바닥 가운데, +Z = 창구(앞), 단위 m.
    /// </summary>
    public static class ShopBoothBuilder
    {
        public const string BOOTH_NAME = "Booth";
        public const string KEEPER_NAME = "ShopKeeper";

        private const string MESH_DIR = PropAssets.PROP_FOLDER;
        private const string MAT_DIR = "Assets/Materials/Props";

        private const string BODY_MESH = MESH_DIR + "/Mesh_BoothBody.asset";
        private const string TRIM_MESH = MESH_DIR + "/Mesh_BoothTrim.asset";
        private const string AWNING_A_MESH = MESH_DIR + "/Mesh_BoothAwningA.asset";
        private const string AWNING_B_MESH = MESH_DIR + "/Mesh_BoothAwningB.asset";
        private const string STEEL_MESH = MESH_DIR + "/Mesh_BoothShutterBox.asset";
        private const string SHUTTER_MESH = MESH_DIR + "/Mesh_BoothShutter.asset";

        private const string WOOD_MAT = MAT_DIR + "/M_BoothWood.mat";
        private const string TRIM_MAT = MAT_DIR + "/M_BoothTrim.mat";
        private const string AWNING_A_MAT = MAT_DIR + "/M_BoothAwningA.mat";
        private const string AWNING_B_MAT = MAT_DIR + "/M_BoothAwningB.mat";
        private const string STEEL_MAT = MAT_DIR + "/M_BoothSteel.mat";
        private const string KEEPER_MAT = MAT_DIR + "/M_ShopKeeper.mat";
        private const string BULB_MAT = MAT_DIR + "/M_BoothBulb.mat";

        private const string KEEPER_TEX = MAT_DIR + "/T_ShopKeeper.png";
        private const string MOUSE_MAT = "Assets/Materials/M_Mouse_URP.mat";
        private const string MOUSE_TEX = "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Textures/T_Mouse.png";
        private const string PACK_MOUSE = "Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Pets Vol.1/Prefabs/Mouse.prefab";
        private const string CHEESE_MESH = "Assets/Models/Cheese/Mesh_CheeseWedge.asset";
        private const string CHEESE_MAT = "Assets/Materials/Props/M_Cheese.mat";
        private const string WOOD_TEX = "Assets/Materials/Map/Textures/T_PantryRoomFloor.png";
        private const string TRIM_TEX = "Assets/Materials/Map/Textures/T_PantryWall.png";

        // ── 치수(m) ──
        private const float HALF_W = 0.70f;       // 몸통 가로 반
        private const float HALF_D = 0.50f;       // 몸통 깊이 반
        private const float HEIGHT = 1.00f;       // 몸통 높이(지붕 판 밑까지)
        private const float WALL = 0.06f;         // 벽 두께
        private const float WIN_HALF_W = 0.475f;  // 창구 가로 반
        private const float WIN_Y0 = 0.30f;       // 창구 아랫변 = 판매대 높이
        private const float WIN_Y1 = 0.72f;       // 창구 윗변 = 셔터 상자 밑
        private const float HOUSING_TOP = 0.81f;
        private const float SHUTTER_OUT = 0.03f;  // 철문 판이 벽 앞으로 떨어진 거리
        // 차양은 창구 위로 높이 — 첫 판(0.90→0.78, 0.36m)은 위에서 내려다보는 게임 카메라에 창구와 점원을 가렸다(인게임 촬영).
        private const float AWNING_BACK_Y = 0.97f;
        private const float AWNING_FRONT_Y = 0.88f;
        private const float AWNING_OUT = 0.28f;   // 차양이 벽 앞으로 나온 길이
        private const int AWNING_STRIPS = 7;
        private const float KEEPER_FLOOR = 0.12f; // 점원 발판 — 이만큼 올라서야 얼굴이 창구 위로 온다

        // 가게 전등 — 밤 맵에서 부스가 어둠에 묻혔다(인게임 촬영). 창구 앞을 따뜻하게 비추고, 철문이 닫히면 꺼진다(ShopShutter).
        private static readonly Color LAMP_COLOR = new Color(1f, 0.78f, 0.45f);
        private const float LAMP_INTENSITY = 2.2f;
        private const float LAMP_RANGE = 3.2f;

        [MenuItem("Tools/CTC/Dev/Bake Shop Booth (상점 부스)")]
        public static void BakeIntoPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ShopSpotPrefabFactory.PATH);
            if (root == null)
            {
                Debug.LogError($"[ShopBoothBuilder] 프리팹 없음: {ShopSpotPrefabFactory.PATH}");
                return;
            }
            try
            {
                Attach(root);
                PrefabUtility.SaveAsPrefabAsset(root, ShopSpotPrefabFactory.PATH);
                Debug.Log($"[ShopBoothBuilder] 부스를 다시 달았다: {ShopSpotPrefabFactory.PATH}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>상점 자리 루트에 부스를 단다(있으면 지우고 다시). 철문(<see cref="ShopShutter"/>)도 여기서 붙인다.</summary>
        public static void Attach(GameObject _root)
        {
            if (_root == null) return;
            Transform old = _root.transform.Find(BOOTH_NAME);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            if (_root.GetComponent<ShopShutter>() == null) _root.AddComponent<ShopShutter>();

            var booth = new GameObject(BOOTH_NAME);
            booth.transform.SetParent(_root.transform, false);

            Material wood = SaveTriplanar(WOOD_MAT, "M_BoothWood", WOOD_TEX, new Color(1.0f, 0.93f, 0.84f), 0.9f);
            Material trim = SaveTriplanar(TRIM_MAT, "M_BoothTrim", TRIM_TEX, new Color(0.78f, 0.66f, 0.54f), 0.7f);
            Material awningA = SdfMesher.SaveMaterial(AWNING_A_MAT, "M_BoothAwningA", new Color(0.98f, 0.80f, 0.30f), 0.25f);
            Material awningB = SdfMesher.SaveMaterial(AWNING_B_MAT, "M_BoothAwningB", new Color(0.82f, 0.24f, 0.20f), 0.25f);
            Material steel = SdfMesher.SaveMaterial(STEEL_MAT, "M_BoothSteel", new Color(0.46f, 0.50f, 0.55f), 0.55f);
            if (steel.HasProperty("_Metallic")) steel.SetFloat("_Metallic", 0.55f);
            if (wood == null || trim == null) return;

            AddPart(booth.transform, "Body", SaveBoxes(BODY_MESH, "Mesh_BoothBody", BodyBoxes()), wood);
            AddPart(booth.transform, "Trim", SaveBoxes(TRIM_MESH, "Mesh_BoothTrim", TrimBoxes()), trim);
            AddPart(booth.transform, "AwningA", SaveBoxes(AWNING_A_MESH, "Mesh_BoothAwningA", AwningBoxes(0)), awningA);
            AddPart(booth.transform, "AwningB", SaveBoxes(AWNING_B_MESH, "Mesh_BoothAwningB", AwningBoxes(1)), awningB);
            AddPart(booth.transform, "ShutterBox", SaveBoxes(STEEL_MESH, "Mesh_BoothShutterBox", HousingBoxes()), steel);

            GameObject panel = AddPart(booth.transform, ShopShutter.PANEL_NAME,
                SaveBoxes(SHUTTER_MESH, "Mesh_BoothShutter", ShutterBoxes()), steel);
            panel.transform.localPosition = new Vector3(0f, WIN_Y1, HALF_D + SHUTTER_OUT);
            panel.transform.localScale = new Vector3(1f, 0.04f, 1f);   // 말린 채로 굽는다 — 판이 열린 채 시작한다

            AddLamp(booth.transform);

            AddCheese(booth.transform, "Sign", new Vector3(0.05f, HEIGHT + 0.09f, 0.02f), 0.42f, 20f);
            AddCheese(booth.transform, "ShelfCheeseA", new Vector3(-0.34f, 0.655f, -0.37f), 0.12f, 60f);
            AddCheese(booth.transform, "ShelfCheeseB", new Vector3(0.30f, 0.655f, -0.37f), 0.10f, -30f);

            // 몸통만 막는다 — 걸어서 창구 앞에 선다. 격자에도 「아무도 못 지나가는 칸」으로 찍는다(봇이 돌아간다).
            var body = new GameObject("BodyCollider");
            body.transform.SetParent(booth.transform, false);
            var box = body.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, HEIGHT * 0.5f, 0f);
            box.size = new Vector3(HALF_W * 2f, HEIGHT, HALF_D * 2f);
            var blocker = booth.AddComponent<Runtime.Ai.NavGridBlocker>();
            var so = new SerializedObject(blocker);
            so.FindProperty("m_Kind").intValue = (int)Runtime.Ai.NavStructureKind.Solid;
            so.ApplyModifiedPropertiesWithoutUndo();

            AddKeeper(booth.transform, _root.GetComponent<ShopShutter>());
            SdfMesher.Commit(BODY_MESH, TRIM_MESH, AWNING_A_MESH, AWNING_B_MESH, STEEL_MESH, SHUTTER_MESH);
        }

        // ── 부품 ─────────────────────────────────────────────

        private struct Piece
        {
            public Vector3 Center;
            public Vector3 Half;
            public Quaternion Rotation;

            public Piece(Vector3 _center, Vector3 _half) : this(_center, _half, Quaternion.identity) { }

            public Piece(Vector3 _center, Vector3 _half, Quaternion _rotation)
            {
                Center = _center;
                Half = _half;
                Rotation = _rotation;
            }
        }

        /// <summary>몸통 — 바닥 · 뒷벽 · 옆벽 둘 · 앞벽(창구 아래 · 위 · 좌 · 우) · 천장.</summary>
        private static List<Piece> BodyBoxes()
        {
            float h = HEIGHT;
            var list = new List<Piece>
            {
                new Piece(new Vector3(0f, WALL * 0.5f, 0f), new Vector3(HALF_W, WALL * 0.5f, HALF_D)),                        // 바닥
                new Piece(new Vector3(0f, h - WALL * 0.5f, 0f), new Vector3(HALF_W, WALL * 0.5f, HALF_D)),                    // 천장
                new Piece(new Vector3(0f, h * 0.5f, -HALF_D + WALL * 0.5f), new Vector3(HALF_W, h * 0.5f, WALL * 0.5f)),      // 뒷벽
                new Piece(new Vector3(-HALF_W + WALL * 0.5f, h * 0.5f, 0f), new Vector3(WALL * 0.5f, h * 0.5f, HALF_D)),      // 왼벽
                new Piece(new Vector3(HALF_W - WALL * 0.5f, h * 0.5f, 0f), new Vector3(WALL * 0.5f, h * 0.5f, HALF_D)),       // 오른벽
            };
            float fz = HALF_D - WALL * 0.5f;
            float sideHalf = (HALF_W - WIN_HALF_W) * 0.5f;
            list.Add(new Piece(new Vector3(0f, WIN_Y0 * 0.5f, fz), new Vector3(HALF_W, WIN_Y0 * 0.5f, WALL * 0.5f)));                 // 창구 아래
            list.Add(new Piece(new Vector3(0f, (WIN_Y1 + h) * 0.5f, fz), new Vector3(HALF_W, (h - WIN_Y1) * 0.5f, WALL * 0.5f)));      // 창구 위
            list.Add(new Piece(new Vector3(-WIN_HALF_W - sideHalf, (WIN_Y0 + WIN_Y1) * 0.5f, fz),
                new Vector3(sideHalf, (WIN_Y1 - WIN_Y0) * 0.5f, WALL * 0.5f)));                                                     // 창구 왼
            list.Add(new Piece(new Vector3(WIN_HALF_W + sideHalf, (WIN_Y0 + WIN_Y1) * 0.5f, fz),
                new Vector3(sideHalf, (WIN_Y1 - WIN_Y0) * 0.5f, WALL * 0.5f)));                                                     // 창구 오른
            return list;
        }

        /// <summary>테두리 나무 — 지붕 판 · 모서리 기둥 넷 · 판매대 · 안쪽 선반 · 점원 발판.</summary>
        private static List<Piece> TrimBoxes()
        {
            float post = 0.045f;
            var list = new List<Piece>
            {
                new Piece(new Vector3(0f, HEIGHT + 0.03f, 0f), new Vector3(HALF_W + 0.07f, 0.03f, HALF_D + 0.07f)),          // 지붕 판(처마)
                new Piece(new Vector3(0f, HEIGHT + 0.075f, 0f), new Vector3(HALF_W - 0.02f, 0.015f, HALF_D - 0.02f)),        // 지붕 턱
                new Piece(new Vector3(0f, WIN_Y0 - 0.015f, HALF_D + 0.06f), new Vector3(WIN_HALF_W + 0.08f, 0.018f, 0.12f)), // 판매대
                new Piece(new Vector3(0f, WIN_Y0 - 0.06f, HALF_D + 0.13f), new Vector3(WIN_HALF_W + 0.05f, 0.03f, 0.012f)),  // 판매대 앞 판
                new Piece(new Vector3(0f, 0.62f, -HALF_D + WALL + 0.07f), new Vector3(HALF_W - WALL - 0.02f, 0.012f, 0.07f)), // 안쪽 선반
                new Piece(new Vector3(0f, WALL + (KEEPER_FLOOR - WALL) * 0.5f, 0.02f), new Vector3(0.30f, (KEEPER_FLOOR - WALL) * 0.5f, 0.30f)), // 발판
            };
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    list.Add(new Piece(new Vector3(sx * (HALF_W + 0.005f), HEIGHT * 0.5f, sz * (HALF_D + 0.005f)),
                        new Vector3(post, HEIGHT * 0.5f, post)));
                }
            }
            return list;
        }

        /// <summary>줄무늬 차양 — 벽에서 앞으로 기운 판을 세로 띠로 나눈다. <paramref name="_parity"/> 0 = 노랑 띠, 1 = 빨강 띠. 앞에 드리운 술 한 줄.</summary>
        private static List<Piece> AwningBoxes(int _parity)
        {
            var list = new List<Piece>();
            float halfSpan = WIN_HALF_W + 0.14f;
            float stripW = halfSpan * 2f / AWNING_STRIPS;
            float drop = AWNING_BACK_Y - AWNING_FRONT_Y;
            float slope = Mathf.Atan2(drop, AWNING_OUT) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(drop * drop + AWNING_OUT * AWNING_OUT);
            Quaternion tilt = Quaternion.Euler(slope, 0f, 0f);   // +X 축으로 숙인다 — 앞(+Z)이 내려간다
            Vector3 mid = new Vector3(0f, (AWNING_BACK_Y + AWNING_FRONT_Y) * 0.5f, HALF_D + AWNING_OUT * 0.5f);
            for (int i = 0; i < AWNING_STRIPS; i++)
            {
                if (i % 2 != _parity) continue;
                float x = -halfSpan + stripW * (i + 0.5f);
                list.Add(new Piece(new Vector3(x, mid.y, mid.z), new Vector3(stripW * 0.5f, 0.012f, length * 0.5f + 0.01f), tilt));
                // 앞 가장자리 술 — 차양 끝에 드리운 짧은 천
                list.Add(new Piece(new Vector3(x, AWNING_FRONT_Y - 0.045f, HALF_D + AWNING_OUT + 0.004f),
                    new Vector3(stripW * 0.5f, 0.045f, 0.008f)));
            }
            return list;
        }

        /// <summary>셔터 상자 — 창구 위 철제 상자(철문이 말려 들어가는 곳)와 양옆 레일.</summary>
        private static List<Piece> HousingBoxes()
        {
            float zc = HALF_D + 0.045f;
            return new List<Piece>
            {
                new Piece(new Vector3(0f, (WIN_Y1 + HOUSING_TOP) * 0.5f, zc), new Vector3(WIN_HALF_W + 0.05f, (HOUSING_TOP - WIN_Y1) * 0.5f, 0.045f)),
                new Piece(new Vector3(-WIN_HALF_W - 0.025f, (WIN_Y0 + WIN_Y1) * 0.5f, HALF_D + 0.02f), new Vector3(0.018f, (WIN_Y1 - WIN_Y0) * 0.5f, 0.022f)),
                new Piece(new Vector3(WIN_HALF_W + 0.025f, (WIN_Y0 + WIN_Y1) * 0.5f, HALF_D + 0.02f), new Vector3(0.018f, (WIN_Y1 - WIN_Y0) * 0.5f, 0.022f)),
            };
        }

        /// <summary>철문 판 — **윗변이 원점**이고 아래로 창구 높이만큼. 골판(앞뒤로 엇갈린 가로 살)과 아래 손잡이 막대.</summary>
        private static List<Piece> ShutterBoxes()
        {
            var list = new List<Piece>();
            float height = WIN_Y1 - WIN_Y0 + 0.01f;
            const float SLAT = 0.035f;
            int slats = Mathf.CeilToInt(height / SLAT);
            for (int i = 0; i < slats; i++)
            {
                float y = -(i + 0.5f) * SLAT;
                float z = (i % 2 == 0) ? 0.004f : -0.004f;
                list.Add(new Piece(new Vector3(0f, y, z), new Vector3(WIN_HALF_W + 0.01f, SLAT * 0.5f, 0.008f)));
            }
            list.Add(new Piece(new Vector3(0f, -height - 0.012f, 0.004f), new Vector3(WIN_HALF_W + 0.012f, 0.014f, 0.014f)));   // 아래 막대
            list.Add(new Piece(new Vector3(0f, -height - 0.03f, 0.02f), new Vector3(0.06f, 0.008f, 0.012f)));                   // 손잡이
            return list;
        }

        // ── 메시·머티리얼 ─────────────────────────────────────

        /// <summary>
        /// 상자 여럿을 한 메시로 — 면마다 법선을 따로 둔다(각진 궤짝). UV는 면의 미터 좌표(박스 투영 — 함정 5 규약).
        /// </summary>
        private static Mesh SaveBoxes(string _path, string _name, List<Piece> _pieces)
        {
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            Vector3[] axes = { Vector3.right, Vector3.up, Vector3.forward };
            foreach (Piece p in _pieces)
            {
                for (int a = 0; a < 3; a++)
                {
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Vector3 n = axes[a] * sign;
                        Vector3 u = axes[(a + 1) % 3];
                        Vector3 v = axes[(a + 2) % 3];
                        float hn = p.Half[a], hu = p.Half[(a + 1) % 3], hv = p.Half[(a + 2) % 3];
                        int start = verts.Count;
                        for (int k = 0; k < 4; k++)
                        {
                            float su = (k == 0 || k == 3) ? -1f : 1f;
                            float sv = (k < 2) ? -1f : 1f;
                            Vector3 local = n * hn + u * (su * hu) + v * (sv * hv);
                            Vector3 world = p.Center + p.Rotation * local;
                            verts.Add(world);
                            normals.Add(p.Rotation * n);
                            uvs.Add(new Vector2(Vector3.Dot(world, p.Rotation * u), Vector3.Dot(world, p.Rotation * v)));
                        }
                        // 감는 순서 = 면 법선 쪽이 앞면. u × v = +축이라 (0,1,2)의 외적이 +n이다 — 음의 면은 뒤집는다.
                        if (sign > 0)
                        {
                            tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                            tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
                        }
                        else
                        {
                            tris.Add(start); tris.Add(start + 2); tris.Add(start + 1);
                            tris.Add(start); tris.Add(start + 3); tris.Add(start + 2);
                        }
                    }
                }
            }
            var mesh = new Mesh { name = _name };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return SdfMesher.SaveMesh(mesh, _path);
        }

        /// <summary>맵과 같은 삼면 투영 나무 — **셰이더 로드를 널 검사한다**(함정 6).</summary>
        private static Material SaveTriplanar(string _path, string _name, string _texPath, Color _tint, float _tileMeters)
        {
            Shader shader = Shader.Find(MapAssets.SHADER_NAME);
            if (shader == null)
            {
                Debug.LogError($"[ShopBoothBuilder] '{MapAssets.SHADER_NAME}' 셰이더가 없다 — 리임포트가 끝난 뒤 다시");
                return null;
            }
            SdfMesher.EnsureFolders(_path);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(_path);
            if (mat == null)
            {
                mat = new Material(shader) { name = _name };
                AssetDatabase.CreateAsset(mat, _path);
            }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(_texPath));
            mat.SetColor("_BaseColor", _tint);
            mat.SetFloat("_TileMeters", _tileMeters);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject AddPart(Transform _parent, string _name, Mesh _mesh, Material _mat)
        {
            var go = new GameObject(_name);
            go.transform.SetParent(_parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        /// <summary>
        /// 가게 전등 — 차양 밑 전구(빛나는 작은 공) + 점광원. 전구는 켜진 채로 굽고 <see cref="ShopShutter"/>가 철문을 따라 끈다.
        /// 그림자는 안 드리운다(점광원 그림자는 비싸고, 부스 넷이 동시에 켜진다).
        /// </summary>
        private static void AddLamp(Transform _booth)
        {
            var bulbMat = SdfMesher.SaveMaterial(BULB_MAT, "M_BoothBulb", LAMP_COLOR, 0.6f);
            bulbMat.EnableKeyword("_EMISSION");
            if (bulbMat.HasProperty("_EmissionColor")) bulbMat.SetColor("_EmissionColor", LAMP_COLOR * 2.5f);
            bulbMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(bulbMat);

            var lamp = new GameObject(ShopShutter.LAMP_NAME);
            lamp.transform.SetParent(_booth, false);
            lamp.transform.localPosition = new Vector3(0f, AWNING_FRONT_Y - 0.06f, HALF_D + AWNING_OUT - 0.06f);
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = LAMP_COLOR;
            light.intensity = LAMP_INTENSITY;
            light.range = LAMP_RANGE;
            light.shadows = LightShadows.None;

            GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bulb";
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.transform.SetParent(lamp.transform, false);
            bulb.transform.localScale = Vector3.one * 0.06f;
            var mr = bulb.GetComponent<MeshRenderer>();
            mr.sharedMaterial = bulbMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static void AddCheese(Transform _parent, string _name, Vector3 _position, float _scale, float _yaw)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(CHEESE_MESH);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(CHEESE_MAT);
            if (mesh == null || mat == null)
            {
                Debug.LogWarning($"[ShopBoothBuilder] 치즈 메시·머티리얼이 없다 — {_name} 없이 간다");
                return;
            }
            GameObject go = AddPart(_parent, _name, mesh, mat);
            go.transform.localPosition = _position;
            go.transform.localRotation = Quaternion.Euler(0f, _yaw, 0f);
            go.transform.localScale = Vector3.one * _scale;
        }

        /// <summary>점원 — 팩 쥐를 밤색으로 칠해 발판 위에 세운다. 창구를 본다. 콜라이더는 걷는다(부스 몸통이 막는다).</summary>
        private static void AddKeeper(Transform _booth, ShopShutter _shutter)
        {
            var packMouse = AssetDatabase.LoadAssetAtPath<GameObject>(PACK_MOUSE);
            if (packMouse == null)
            {
                Debug.LogWarning($"[ShopBoothBuilder] 팩 쥐가 없다 — 점원 없이 간다: {PACK_MOUSE}");
                return;
            }
            var keeper = (GameObject)PrefabUtility.InstantiatePrefab(packMouse);
            keeper.name = KEEPER_NAME;
            keeper.transform.SetParent(_booth, false);
            keeper.transform.localPosition = new Vector3(0f, KEEPER_FLOOR, 0.06f);
            keeper.transform.localRotation = Quaternion.identity;

            foreach (Collider col in keeper.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);

            Material skin = SaveKeeperMaterial();
            if (skin != null)
            {
                foreach (Renderer r in keeper.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = skin;
                    r.sharedMaterials = mats;
                }
            }

            var shopKeeper = keeper.AddComponent<ShopKeeper>();
            var so = new SerializedObject(shopKeeper);
            so.FindProperty("m_Shutter").objectReferenceValue = _shutter;
            so.FindProperty("m_Animator").objectReferenceValue = keeper.GetComponentInChildren<Animator>(true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>플레이어 쥐 머티리얼을 본떠 팔레트만 밤색으로 — 셰이더·속성은 그대로라 조명이 같다.</summary>
        private static Material SaveKeeperMaterial()
        {
            var mouse = AssetDatabase.LoadAssetAtPath<Material>(MOUSE_MAT);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(KEEPER_TEX);
            var mouseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(MOUSE_TEX);
            if (mouse == null || tex == null)
            {
                Debug.LogWarning("[ShopBoothBuilder] 쥐 머티리얼·점원 팔레트가 없다 — 점원이 팩 색 그대로다");
                return null;
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(KEEPER_MAT);
            if (mat == null)
            {
                mat = new Material(mouse) { name = "M_ShopKeeper" };
                AssetDatabase.CreateAsset(mat, KEEPER_MAT);
            }
            else
            {
                mat.CopyPropertiesFromMaterial(mouse);
            }
            foreach (string prop in mat.GetTexturePropertyNames())
            {
                if (mat.GetTexture(prop) == mouseTex) mat.SetTexture(prop, tex);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
