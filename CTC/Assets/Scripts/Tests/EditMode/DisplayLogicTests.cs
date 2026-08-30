using System.Collections.Generic;
using CTC.Runtime.Core;
using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    public class DisplayLogicTests
    {
        private static List<Vector2Int> Res(params (int w, int h)[] _items)
        {
            var list = new List<Vector2Int>();
            foreach ((int w, int h) in _items) list.Add(new Vector2Int(w, h));
            return list;
        }

        /// <summary>같은 해상도가 주사율만 달라 여러 번 오는 게 정상이다 — 하나로 합쳐야 한다.</summary>
        [Test]
        public void Options_DeduplicatesSameSize()
        {
            List<Vector2Int> options = DisplayLogic.Options(Res((1920, 1080), (1920, 1080), (1280, 720)));
            Assert.AreEqual(2, options.Count);
        }

        [Test]
        public void Options_DropsTooSmall()
        {
            List<Vector2Int> options = DisplayLogic.Options(Res((800, 600), (1920, 1080)));
            CollectionAssert.AreEqual(Res((1920, 1080)), options);
        }

        /// <summary>큰 것이 앞이다 — 대부분은 최대 해상도를 고른다.</summary>
        [Test]
        public void Options_SortedLargestFirst()
        {
            List<Vector2Int> options = DisplayLogic.Options(Res((1280, 720), (3840, 2160), (1920, 1080)));
            Assert.AreEqual(new Vector2Int(3840, 2160), options[0]);
            Assert.AreEqual(new Vector2Int(1280, 720), options[2]);
        }

        /// <summary>전부 걸러지면 드롭다운이 비어 아무것도 못 고른다 — 하한을 어겨서라도 하나는 준다.</summary>
        [Test]
        public void Options_AllTooSmall_KeepsOne()
        {
            List<Vector2Int> options = DisplayLogic.Options(Res((640, 480)));
            Assert.AreEqual(1, options.Count);
        }

        [Test]
        public void Options_Null_Empty()
        {
            Assert.AreEqual(0, DisplayLogic.Options(null).Count);
        }

        [Test]
        public void NearestIndex_ExactMatch()
        {
            List<Vector2Int> options = Res((3840, 2160), (1920, 1080), (1280, 720));
            Assert.AreEqual(1, DisplayLogic.NearestIndex(options, 1920, 1080));
        }

        /// <summary>모니터를 바꾸면 저장된 해상도가 목록에 없다 — 0번(=최대)으로 튕기면 안 된다.</summary>
        [Test]
        public void NearestIndex_NoExact_PicksClosestByPixels()
        {
            List<Vector2Int> options = Res((3840, 2160), (1920, 1080), (1280, 720));
            Assert.AreEqual(2, DisplayLogic.NearestIndex(options, 1366, 768));
        }

        [Test]
        public void NearestIndex_Empty_Zero()
        {
            Assert.AreEqual(0, DisplayLogic.NearestIndex(new List<Vector2Int>(), 1920, 1080));
        }

        [Test]
        public void ClampIndex_OutOfRange()
        {
            Assert.AreEqual(0, DisplayLogic.ClampIndex(-3, 3));
            Assert.AreEqual(2, DisplayLogic.ClampIndex(9, 3));
            Assert.AreEqual(0, DisplayLogic.ClampIndex(1, 0));
        }

        /// <summary>유니티 모드와 왕복해야 저장·복원이 맞는다.</summary>
        [Test]
        public void WindowMode_RoundTrips()
        {
            foreach (WindowMode mode in System.Enum.GetValues(typeof(WindowMode)))
            {
                Assert.AreEqual(mode, DisplayLogic.FromUnity(DisplayLogic.ToUnity(mode)));
            }
        }

        [Test]
        public void ModeLabel_AllNamed()
        {
            foreach (WindowMode mode in System.Enum.GetValues(typeof(WindowMode)))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(DisplayLogic.ModeLabel(mode)));
            }
        }

        [Test]
        public void ResolutionLabel_Format()
        {
            Assert.AreEqual("1920 × 1080", DisplayLogic.ResolutionLabel(1920, 1080));
        }
    }
}
