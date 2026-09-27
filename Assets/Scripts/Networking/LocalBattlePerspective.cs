using UnityEngine;

namespace Mishi.Networking
{
    // Rotate only the local camera. Board IDs, colliders and network coordinates stay fixed.
    public sealed class LocalBattlePerspective
    {
        private readonly Camera camera;
        private readonly Vector3 position, pivot;
        private readonly Quaternion rotation;
        public LocalBattlePerspective(Camera camera, Vector3 pivot)
        {
            this.camera = camera; this.pivot = pivot;
            position = camera.transform.position; rotation = camera.transform.rotation;
        }
        public void SetPlayer(int player)
        {
            if (camera == null) return;
            var turn = Quaternion.AngleAxis(player == 1 ? 180 : 0, Vector3.up);
            camera.transform.SetPositionAndRotation(pivot + turn * (position - pivot), turn * rotation);
        }
        public void Restore()
        {
            if (camera != null) camera.transform.SetPositionAndRotation(position, rotation);
        }
    }
}
