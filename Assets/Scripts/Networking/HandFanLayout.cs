using UnityEngine;

namespace Mishi.Networking
{
    // Screen-space geometry shared by rendering and stable input; independent of camera perspective.
    public static class HandFanLayout
    {
        public static float CardHeight(Rect area) => Mathf.Max(1, Mathf.Min(area.height * .23f, 180f, area.width * .65f));
        public static int Capacity(Rect area)
        {
            float height = CardHeight(area);
            float margin = height * .62f;
            return Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0, area.width - margin * 2) / 32f) + 1, 1, 18);
        }
        public static Vector2 Center(Rect area, int index, int count)
        {
            float height = CardHeight(area), margin = height * .62f;
            float width = Mathf.Min(Mathf.Max(0, area.width - margin * 2), Mathf.Max(0, count - 1) * height * .43f);
            float t = count <= 1 ? 0 : index / (float)(count - 1) * 2 - 1;
            // Rest around the screen edge: approximately half of each card stays off-screen.
            return new Vector2(area.center.x + t * width * .5f, area.yMin - height * .06f * t * t);
        }
        public static Vector2 RaisedCenter(Rect area, int index, int count)
        {
            var center = Center(area, index, count);
            center.y = area.yMin + CardHeight(area) * (.58f + .05f);
            return center;
        }
        public static float Angle(int index, int count) => count <= 1 ? 0 : (1 - 2 * index / (float)(count - 1)) * Mathf.Min(12, (count - 1) * 2);
        public static Rect HitRect(Rect area, int index, int count)
        {
            var center = Center(area, index, count);
            float height = CardHeight(area);
            float angle = Angle(index, count) * Mathf.Deg2Rad;
            float width = height * .7154f;
            float halfWidth = (width * Mathf.Abs(Mathf.Cos(angle)) + height * Mathf.Abs(Mathf.Sin(angle))) * .5f;
            float halfHeight = (height * Mathf.Abs(Mathf.Cos(angle)) + width * Mathf.Abs(Mathf.Sin(angle))) * .5f;
            // Acquisition is limited to the visible resting card, never its future raised height.
            return Rect.MinMaxRect(Mathf.Max(area.xMin, center.x - halfWidth), Mathf.Max(area.yMin, center.y - halfHeight),
                Mathf.Min(area.xMax, center.x + halfWidth), Mathf.Min(area.yMax, center.y + halfHeight));
        }
    }
}
