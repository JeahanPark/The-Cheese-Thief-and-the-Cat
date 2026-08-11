using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace CTC.Runtime.Map
{
    /// <summary>
    /// **판 중에 생긴 구조물이 이동 폴리곤에 구멍을 판다** (2026-08-12).
    ///
    /// 폴리곤은 `MapBuilder`가 지형을 지을 때 한 번 굽는다. 쥐가 그 뒤에 세운 창고·요새는
    /// 그 폴리곤에 없어서, 길찾기가 "거기로 걸어가면 된다"고 답하고 걷는 쪽이 창고를 민다
    /// (2026-08-11 실측: 고양이 둘이 창고 앞에서 8초 동안 1mm도 못 갔다).
    ///
    /// 다시 굽는 건 비싸고 프레임을 끊는다. `NavMeshObstacle`이 제 자리만 도려내면 된다 —
    /// 값도 훨씬 싸고, 부서져 사라지면 구멍도 같이 닫힌다.
    ///
    /// 크기는 콜라이더에서 읽는다. 손으로 적어 두면 프리팹을 고칠 때마다 어긋난다.
    /// </summary>
    [DisallowMultipleComponent]
    public class NavMeshCarver : MonoBehaviour
    {
        private void Start()
        {
            Collider col = GetComponentInChildren<Collider>();
            if (col == null) return;

            var obstacle = gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;

            // 서 있는 동안만 도려낸다 — 움직이는 것에 카빙을 켜면 폴리곤을 매 프레임 다시 만든다
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;

            Bounds world = col.bounds;
            Vector3 scale = transform.lossyScale;
            obstacle.center = transform.InverseTransformPoint(world.center);
            obstacle.size = new Vector3(
                world.size.x / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
                world.size.y / Mathf.Max(Mathf.Abs(scale.y), 0.0001f),
                world.size.z / Mathf.Max(Mathf.Abs(scale.z), 0.0001f));
        }
    }
}
