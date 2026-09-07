using CTC.Runtime.Map;
using CTC.Runtime.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CTC.Tests
{
    public class MinimapPainterTests
    {
        // 손 배치 창고 맵 — Rooms·Corridors가 비어 있고 Props만 77개다.
        // 「방이 없으면 아무것도 안 그려진다」는 회귀를 잡는 게 이 테스트의 목적이다.
        private const string WAREHOUSE = "Assets/Prefabs/Maps/Layout_Warehouse01.asset";
        private const int PX = 256;

        private static MapLayout LoadWarehouse()
        {
            var layout = AssetDatabase.LoadAssetAtPath<MapLayout>(WAREHOUSE);
            Assert.IsNotNull(layout, $"레이아웃 에셋이 없다: {WAREHOUSE}");
            return layout;
        }

        /// <summary>바닥색과 눈에 띄게 다른 픽셀 수. Color32로 재야 텍스처 양자화에 안 걸린다.</summary>
        private static int CountNonFloor(Texture2D _tex)
        {
            Color32 floor = UiPalette.Slot;
            Color32[] pixels = _tex.GetPixels32();
            int count = 0;
            foreach (Color32 p in pixels)
            {
                if (Mathf.Abs(p.r - floor.r) > 4 || Mathf.Abs(p.g - floor.g) > 4 || Mathf.Abs(p.b - floor.b) > 4)
                    count++;
            }
            return count;
        }

        [Test]
        public void Paint_TextureIsSquareOfRequestedSize()
        {
            Texture2D tex = MinimapPainter.Paint(LoadWarehouse(), PX);
            Assert.AreEqual(PX, tex.width);
            Assert.AreEqual(PX, tex.height);
            Assert.AreEqual(FilterMode.Point, tex.filterMode);
            Assert.AreEqual(TextureWrapMode.Clamp, tex.wrapMode);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void Paint_DrawsSomethingOnTopOfTheFloor()
        {
            Texture2D tex = MinimapPainter.Paint(LoadWarehouse(), PX);
            Assert.Greater(CountNonFloor(tex), 0, "지형이 한 픽셀도 안 그려졌다");
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void Paint_HandPlacedMapHasNoRoomsButStillShowsTerrain()
        {
            MapLayout layout = LoadWarehouse();
            Assert.AreEqual(0, layout.Rooms.Count, "손 배치 맵은 방이 없다 — 전제가 깨졌다");
            Assert.AreEqual(0, layout.Corridors.Count, "손 배치 맵은 복도가 없다 — 전제가 깨졌다");
            Assert.Greater(layout.Props.Count, 0);

            Texture2D tex = MinimapPainter.Paint(layout, PX);
            Texture2D empty = MinimapPainter.Paint(null, PX);
            // 경계 테두리만으로도 0은 아니므로, Props가 실제로 더 칠했는지를 본다
            Assert.Greater(CountNonFloor(tex), CountNonFloor(empty));
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(empty);
        }

        [Test]
        public void Paint_NullLayoutIsFloorOnly()
        {
            Texture2D tex = MinimapPainter.Paint(null, PX);
            Assert.AreEqual(PX, tex.width);
            Assert.AreEqual(0, CountNonFloor(tex), "레이아웃이 없으면 경계도 없다 — 바닥뿐이어야 한다");
            Object.DestroyImmediate(tex);
        }
    }
}
