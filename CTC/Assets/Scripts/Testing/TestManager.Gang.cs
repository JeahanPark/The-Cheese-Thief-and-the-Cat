using CTC.Runtime.Core;
using UnityEngine;

namespace CTC.Runtime.Testing
{
    /// <summary>
    /// 깡패 쥐 모습 QA 훅(2026-09-14). **이 인스턴스의 화면에서** 그 쥐가 어떻게 보이는지를 한 줄로 —
    /// 복제값 구독으로 켜지는 기능이라 다른 인스턴스에서 읽어야 전파가 검증된다.
    /// </summary>
    public partial class TestManager
    {
        /// <summary>"active=True biped=True club=True hat=True rabid=55 state=BipedIdle" 꼴. 몸이 없으면 "missing".</summary>
        public string TestGetGangLook(long _clientId)
        {
            var body = NetUtil.FindPlayerObject((ulong)_clientId);
            if (body == null) return "missing";

            var look = body.GetComponent<Player.GangLook>();
            var driver = body.GetComponent<Player.PlayerAnimationDriver>();
            var visual = body.GetComponent<Player.GangLookVisual>();
            bool club = false, hat = false;
            int brows = 0;
            foreach (Transform t in body.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == Player.GangLook.CLUB_NAME) club = t.gameObject.activeInHierarchy;
                else if (t.name == Player.GangLook.HAT_NAME) hat = t.gameObject.activeInHierarchy;
                else if (t.name.StartsWith(Player.GangLook.BROW_PREFIX) && t.gameObject.activeInHierarchy) brows++;
            }

            float rabid = -1f;
            foreach (SkinnedMeshRenderer skin in body.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (skin.sharedMesh == null) continue;
                int i = skin.sharedMesh.GetBlendShapeIndex("eyes.rabid");
                if (i < 0) continue;
                rabid = skin.GetBlendShapeWeight(i);
                break;
            }

            return $"active={(look != null && look.Active)} biped={(driver != null && driver.Biped)} " +
                   $"club={club} hat={hat} brows={brows} rabid={rabid:0} state={(driver != null ? driver.PlayingState : "")} " +
                   $"clubA={(visual != null ? visual.ClubAlpha : -1f):0.##} hatA={(visual != null ? visual.HatAlpha : -1f):0.##} " +
                   $"transforming={(visual != null && visual.Transforming)}";
        }

        /// <summary>
        /// 깡패 소품의 **월드 크기** — "size=(x,y,z) lossy=s boneLossy=s fromBone=m". 몸이 없으면 "missing".
        /// 모자가 프리팹에선 27cm인데 화면에선 1/3로 보였다(2026-09-14) — 뼈 스케일을 애니메이션이 바꾸는지 잰다.
        /// </summary>
        public string TestGetGangPropTransform(long _clientId, string _name)
        {
            var body = NetUtil.FindPlayerObject((ulong)_clientId);
            if (body == null) return "missing";
            foreach (Transform t in body.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != _name) continue;
                var r = t.GetComponent<Renderer>();
                Bounds b = r != null ? r.bounds : new Bounds();
                Transform bone = t.parent;
                return $"size=({b.size.x:F3},{b.size.y:F3},{b.size.z:F3}) lossy={t.lossyScale.x:F3} " +
                       $"boneLossy={(bone != null ? bone.lossyScale.x : 0f):F2} bone={(bone != null ? bone.name : "?")} " +
                       $"boneLocal={(bone != null ? bone.localScale.x : 0f):F3} active={t.gameObject.activeInHierarchy}";
            }
            return "missing";
        }
    }
}
