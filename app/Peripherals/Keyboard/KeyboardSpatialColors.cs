using GHelper.Helpers;

namespace GHelper.Peripherals.Keyboard
{
    internal static class KeyboardSpatialColors
    {
        // Same left-to-right order as the laptop's four keyboard zones (not its lightbar).
        internal static Color[] Gradient(Color left, Color right) =>
            Enumerable.Range(0, 4).Select(i => ColorUtils.GetWeightedAverage(left, right, i / 3f)).ToArray();

        internal static Color[] MapToKeys(List<KeyDef[]> rows, Color[] zones)
        {
            var centers = new List<float>();
            foreach (var row in rows)
            {
                float x = 0;
                foreach (var key in row)
                {
                    x += key.Gap;
                    centers.Add(x + key.Width / 2);
                    x += key.Width;
                }
            }

            if (zones.Length == 0 || centers.Count == 0) return Array.Empty<Color>();
            int zoneCount = Math.Min(4, zones.Length);
            float left = centers.Min(), span = centers.Max() - left;
            return centers.Select(x =>
            {
                float position = span > 0 ? (x - left) / span * (zoneCount - 1) : 0;
                int a = Math.Clamp((int)position, 0, zoneCount - 1);
                return ColorUtils.GetWeightedAverage(zones[a], zones[Math.Min(a + 1, zoneCount - 1)], position - a);
            }).ToArray();
        }
    }
}
