// UiBuildRunner.cs — synchronous "layout JSON → uGUI prefab" builder.
// Walks the ComponentNode tree using the real Unity API (LayoutGroup auto-arrangement)
// and saves a Canvas-less prefab. Caller MUST be on the main thread (the McpBridge
// queue processes requests on EditorApplication.update, so this is satisfied).
using System.IO;
using UnityEditor;
using UnityEngine;

namespace McpUiGen
{
    public static class UiBuildRunner
    {
        // Returns the saved prefab asset path (project-relative, under Assets/).
        public static string BuildAndSave(string layoutJson, string outputPrefabPath, Vector2 resolution)
        {
            if (string.IsNullOrWhiteSpace(layoutJson))
                throw new System.Exception("layoutJson is empty");
            if (string.IsNullOrWhiteSpace(outputPrefabPath))
                throw new System.Exception("outputPrefabPath is empty");

            outputPrefabPath = outputPrefabPath.Replace('\\', '/');
            if (!outputPrefabPath.EndsWith(".prefab"))
                outputPrefabPath += ".prefab";

            var tree = LayoutParser.Parse(layoutJson);

            // Build under a temp parent so anchors resolve, then detach.
            var temp = new GameObject("_McpUiTempParent");
            temp.AddComponent<RectTransform>();
            GameObject root = null;
            try
            {
                root = BuildNode(tree, temp.transform, resolution);
                root.transform.SetParent(null, false);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }

            EnsureFolderExists(outputPrefabPath);

            root.name = Path.GetFileNameWithoutExtension(outputPrefabPath);
            PrefabUtility.SaveAsPrefabAsset(root, outputPrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return outputPrefabPath;
        }

        private static GameObject BuildNode(ComponentNode node, Transform parent, Vector2 resolution)
        {
            var go = CreateElementTool.Create(node, parent);

            SetAnchorTool.Apply(go, node.anchor, resolution);
            SetSizeTool.Apply(go, node.size, resolution);
            SetStyleTool.Apply(go, node);

            if (node.position != null && (node.position.x != 0 || node.position.y != 0))
            {
                var rt = go.GetComponent<RectTransform>();
                if (rt != null) rt.anchoredPosition = new Vector2(node.position.x, node.position.y);
            }

            if (node.layout != LayoutType.None)
                SetLayoutTool.Apply(go, node);

            foreach (var child in node.children)
            {
                var childGo = BuildNode(child, go.transform, resolution);

                // LayoutGroup children MUST be top-left anchored. Unity's
                // HorizontalOrVerticalLayoutGroup computes child anchoredPosition assuming
                // (0,1) anchors; a child's declared anchor (e.g. middle-center) otherwise
                // offsets it by ~half the parent size (the "renders in bottom half" bug).
                // Override the child anchor here whenever this node drives a LayoutGroup.
                if (node.layout != LayoutType.None && childGo != null)
                {
                    var crt = childGo.GetComponent<RectTransform>();
                    if (crt != null)
                    {
                        crt.anchorMin = new Vector2(0, 1);
                        crt.anchorMax = new Vector2(0, 1);
                        crt.pivot     = new Vector2(0, 1);
                    }
                }
            }

            return go;
        }

        // Creates the asset folder on disk if missing (e.g. "Assets/GameResource/UI/HUD").
        private static void EnsureFolderExists(string assetPath)
        {
            var folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            var projectRoot = Path.GetDirectoryName(Application.dataPath); // parent of Assets
            Directory.CreateDirectory(Path.Combine(projectRoot, folder));
            AssetDatabase.Refresh();
        }
    }
}
