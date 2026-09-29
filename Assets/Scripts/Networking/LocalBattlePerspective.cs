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
        private bool centered;
        public void SetCentered(bool value) { centered = value; }
        public void SetPlayer(int player)
        {
            if (camera == null) return;
            var turn = Quaternion.AngleAxis(player == 1 ? 180 : 0, Vector3.up);
            // Remove the old preview-side horizontal offset, preserving distance and table pitch.
            var right = rotation * Vector3.right;
            var viewPosition = centered ? position + right * Vector3.Dot(pivot - position, right) : position;
            camera.transform.SetPositionAndRotation(pivot + turn * (viewPosition - pivot), turn * rotation);
        }
        public void Restore()
        {
            if (camera != null) camera.transform.SetPositionAndRotation(position, rotation);
        }
    }
}
