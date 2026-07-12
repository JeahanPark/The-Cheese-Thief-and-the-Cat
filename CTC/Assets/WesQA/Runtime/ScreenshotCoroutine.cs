using System.Collections;
using System.Net.Sockets;
using UnityEngine;

namespace WesQA
{
    /// <summary>end-of-frame까지 기다린 뒤 합성 프레임(Overlay UI 포함)을 캡처해 응답한다.</summary>
    internal static class ScreenshotCoroutine
    {
        public static IEnumerator Run(int width, string id, NetworkStream stream, object streamLock)
        {
            yield return new WaitForEndOfFrame();
            string resp;
            try
            {
                var shot = Screenshotter.Capture(width); // object[]{ base64, "jpg" }
                resp = RpcResponse.Result(id, shot);
            }
            catch (System.Exception e)
            {
                resp = RpcResponse.Error(id, e.Message);
            }
            WesPocoServer.Send(stream, streamLock, resp);
        }
    }
}
