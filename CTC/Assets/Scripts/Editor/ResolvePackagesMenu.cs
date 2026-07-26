using UnityEditor;

namespace CTC.Editor
{
    /// <summary>UPM 강제 리졸브 — manifest.json 외부 수정 후 패키지 반영용.</summary>
    public static class ResolvePackagesMenu
    {
        [MenuItem("Tools/CTC/Dev/Resolve Packages")]
        public static void Resolve()
        {
            UnityEditor.PackageManager.Client.Resolve();
        }
    }
}
