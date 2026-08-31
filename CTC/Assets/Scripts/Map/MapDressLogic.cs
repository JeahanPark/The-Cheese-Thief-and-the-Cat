using UnityEngine;

namespace CTC.Runtime.Map
{
    /// <summary>벽 세그먼트에 가구를 몇 개, 어디에, 어떤 변종으로 놓는가의 순수 수학. 시드 결정적 — 전 클라이언트 동일.</summary>
    public static class MapDressLogic
    {
        public static int TileCount(float _length, float _unitWidth)
            => _unitWidth <= 0f ? 0 : Mathf.Max(0, Mathf.FloorToInt(_length / _unitWidth));

        /// <summary>자투리를 양끝에 반씩 — 가운데 정렬 시작 오프셋.</summary>
        public static float TileOffset(float _length, float _unitWidth, int _count)
            => (_length - _count * _unitWidth) * 0.5f;

        /// <summary>슬롯 번호 → 변종 인덱스. PantryTextureLogic.Hash 재사용으로 플랫폼 무관.</summary>
        public static int Pick(int _slot, int _variants, int _seed)
        {
            if (_variants <= 0) return 0;
            return (int)(PantryTextureLogic.Hash(_slot, 17, _seed) * _variants) % _variants;
        }
    }
}
