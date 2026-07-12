using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WesQA
{
    /// <summary>요청 {method, params, jsonrpc, id} 파싱 + 응답 {jsonrpc, result, id} 직렬화.</summary>
    public class RpcRequest
    {
        public string Method;
        public JToken Params;   // 배열(args) 또는 객체(kwargs)
        public string Id;

        public static RpcRequest Parse(string json)
        {
            var o = JObject.Parse(json);
            return new RpcRequest
            {
                Method = (string)o["method"],
                Params = o["params"],
                Id = (string)o["id"],
            };
        }

        /// <summary>위치 인자 리스트로 정규화(없으면 빈 리스트).</summary>
        public List<JToken> Args()
        {
            var list = new List<JToken>();
            if (Params is JArray arr) foreach (var t in arr) list.Add(t);
            return list;
        }
    }

    public static class RpcResponse
    {
        public static string Result(string id, object result)
        {
            return JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["jsonrpc"] = "2.0",
                ["result"] = result,
                ["id"] = id,
            });
        }

        public static string Error(string id, string message)
        {
            return JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["jsonrpc"] = "2.0",
                ["error"] = new Dictionary<string, object>
                {
                    ["code"] = -32603,
                    ["message"] = message,
                },
                ["id"] = id,
            });
        }
    }
}
