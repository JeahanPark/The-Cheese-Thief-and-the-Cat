using NUnit.Framework;
using UnityEngine;

namespace CTC.Tests
{
    [NUnit.Framework.Category("Asset")]
    public class MapTriplanarShaderTests
    {
        [Test]
        public void Shader_Exists_And_Has_Required_Properties()
        {
            Shader shader = Shader.Find("CTC/MapTriplanar");
            Assert.IsNotNull(shader, "CTC/MapTriplanar 셰이더가 없다 — 임포트 실패거나 이름이 틀렸다");

            var mat = new Material(shader);
            Assert.IsTrue(mat.HasProperty("_BaseMap"));
            Assert.IsTrue(mat.HasProperty("_TileMeters"));
            Assert.IsTrue(mat.HasProperty("_SkirtMeters"));
            Object.DestroyImmediate(mat);
        }
    }
}
