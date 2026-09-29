using UnityEngine;

namespace Mishi.Networking
{
    public sealed partial class BattleNetworkSession
    {
        private BattleCardPreviewOverlay battlePreview;
        private BattleLogWindow battleLog;
        private void EnsureBattlePreview()
        {
            if (battlePreview == null)
                battlePreview = FindFirstObjectByType<BattleCardPreviewOverlay>();
            if (battleLog == null)
                battleLog = FindFirstObjectByType<BattleLogWindow>();
            perspective?.SetCentered(battlePreview != null);
        }
        private void OpenBattleLog()
        {
            if (!sessionOpen || !snapshot.HasValue) { status = "开始对局后可查看操作记录。"; return; }
            EnsureBattlePreview();
            if (battleLog != null) battleLog.Toggle();
        }
        private void ClearBattlePreview()
        {
            if (battlePreview != null) battlePreview.Clear();
        }
    }
}
