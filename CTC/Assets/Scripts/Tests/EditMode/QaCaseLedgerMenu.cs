using UnityEditor;
using UnityEngine;

namespace CTC.Tests
{
    /// <summary>
    /// 대장 문서를 다시 만든다. **문서는 생성물이라 손으로 고치지 않는다** —
    /// 케이스를 더하거나 ID를 붙인 뒤 이 메뉴를 누르면 된다.
    /// 안 눌러도 `QaCaseRegistryTests.Ledger_IsUpToDate`가 빨개져 알려 준다.
    /// </summary>
    public static class QaCaseLedgerMenu
    {
        [MenuItem("Tools/CTC/Generate QA Case Ledger")]
        public static void Generate()
        {
            string text = QaCaseLedger.Render(QaCaseLedger.ScanAttachments());
            QaCaseLedger.WriteLedger(text);
            AssetDatabase.Refresh();
            Debug.Log("[QaCases] 대장 갱신: " + QaCaseLedger.RepoRoot() + "/" + QaCaseLedger.LEDGER_PATH);
        }
    }
}
