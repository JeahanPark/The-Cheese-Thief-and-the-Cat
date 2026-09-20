using System.Collections.Generic;
using UnityEngine;

namespace CTC.Runtime.Cameras
{
    /// <summary>
    /// 방해물 페이드의 판정 수학 — 어떤 렌더러를 이번에 새로 흐리게 하고 어떤 것을 되돌릴지.
    /// MonoBehaviour와 분리된 순수 함수라 EditMode 테스트가 가능하다(CameraOrbitLogic과 같은 관례).
    /// </summary>
    public static class OcclusionFadeLogic
    {
        /// <summary>한 번에 흐릴 묶음의 렌더러 상한 — 넘으면 맵·방 통째일 수 있어 안 흐린다(상점 부스는 20개 안쪽).</summary>
        public const int MAX_GROUP_RENDERERS = 48;

        /// <summary>카메라가 겨누는 점 — 발에서 <paramref name="_height"/> 위. 발끝만 보면 윗몸을 가린 것을 놓친다.</summary>
        public static Vector3 AimPoint(Vector3 _feet, float _height)
        {
            return _feet + Vector3.up * Mathf.Max(0f, _height);
        }

        /// <summary>이 크기의 묶음을 흐려도 되는가 — 비었거나 너무 크면(맵 통째) 안 된다.</summary>
        public static bool IsFadeableGroup(int _rendererCount)
        {
            return _rendererCount > 0 && _rendererCount <= MAX_GROUP_RENDERERS;
        }

        /// <summary>
        /// 스피어캐스트 시작점 — 카메라 위치에서 대상 쪽으로 캐스트 반경만큼 전진시킨다.
        /// 카메라에 딱 붙은 면이 자기 자신을 가린 것으로 잡히는 것을 막는다.
        /// </summary>
        public static Vector3 CastOrigin(Vector3 _cameraPosition, Vector3 _targetPosition, float _radius)
        {
            Vector3 delta = _targetPosition - _cameraPosition;
            float distance = delta.magnitude;
            if (distance <= Mathf.Epsilon) return _cameraPosition;
            return _cameraPosition + delta / distance * Mathf.Min(_radius, distance);
        }

        /// <summary>대상 표면 직전까지만 캐스트 — 대상 자신을 방해물로 잡지 않는다.</summary>
        public static float CastDistance(Vector3 _cameraPosition, Vector3 _targetPosition, float _radius)
        {
            float distance = Vector3.Distance(_cameraPosition, _targetPosition);
            return Mathf.Max(0f, distance - _radius * 2f);
        }

        /// <summary>
        /// 이전 프레임 페이드 집합과 이번 프레임 히트 집합을 비교해, 원래대로 되돌릴 것들을 채운다.
        /// 계속 히트 중인 것은 건드리지 않는다 — 매 프레임 머티리얼을 다시 스왑하면 낭비다.
        /// </summary>
        public static void CollectRestored<T>(
            IEnumerable<T> _previouslyFaded, ICollection<T> _currentHits, ICollection<T> _restoredOut)
        {
            _restoredOut.Clear();
            foreach (T item in _previouslyFaded)
            {
                if (!_currentHits.Contains(item)) _restoredOut.Add(item);
            }
        }

        /// <summary>이번 프레임에 새로 흐려져야 하는 것들(이미 흐린 것은 제외).</summary>
        public static void CollectNewlyFaded<T>(
            IEnumerable<T> _currentHits, ICollection<T> _alreadyFaded, ICollection<T> _newlyFadedOut)
        {
            _newlyFadedOut.Clear();
            foreach (T item in _currentHits)
            {
                if (!_alreadyFaded.Contains(item)) _newlyFadedOut.Add(item);
            }
        }
    }
}
