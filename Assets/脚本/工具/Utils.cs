using UnityEngine;

namespace TowerDefense.Utils
{
    public static class MathUtils
    {
        public static float Remap(float value, float fromMin, float fromMax, float toMin, float toMax)
        {
            return toMin + (value - fromMin) * (toMax - toMin) / (fromMax - fromMin);
        }

        public static float ClampAngle(float angle)
        {
            if (angle < 0) angle += 360f;
            if (angle >= 360) angle -= 360f;
            return angle;
        }

        public static Vector2 RotateVector2(Vector2 v, float degrees)
        {
            float sin = Mathf.Sin(degrees * Mathf.Deg2Rad);
            float cos = Mathf.Cos(degrees * Mathf.Deg2Rad);
            return new Vector2(cos * v.x - sin * v.y, sin * v.x + cos * v.y);
        }

        public static float DistanceToLine(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
        {
            Vector3 line = lineEnd - lineStart;
            float len = line.magnitude;
            line.Normalize();

            Vector3 v = point - lineStart;
            float d = Vector3.Dot(v, line);
            d = Mathf.Clamp(d, 0f, len);

            Vector3 closestPoint = lineStart + line * d;
            return Vector3.Distance(point, closestPoint);
        }
    }

    public static class TransformExtensions
    {
        public static void DestroyChildren(this Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(transform.GetChild(i).gameObject);
            }
        }

        public static void SetLayerRecursively(this Transform transform, int layer)
        {
            transform.gameObject.layer = layer;
            foreach (Transform child in transform)
            {
                child.SetLayerRecursively(layer);
            }
        }
    }

    public static class ColorUtils
    {
        public static Color WithAlpha(this Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        public static string ColorToHex(Color color)
        {
            return $"#{(int)(color.r * 255):X2}{(int)(color.g * 255):X2}{(int)(color.b * 255):X2}";
        }

        public static Color HexToColor(string hex)
        {
            if (hex.StartsWith("#"))
                hex = hex.Substring(1);

            byte r = byte.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);

            return new Color32(r, g, b, 255);
        }
    }

    public static class ArrayUtils
    {
        public static T GetRandomElement<T>(T[] array)
        {
            if (array == null || array.Length == 0)
                return default;
            return array[Random.Range(0, array.Length)];
        }

        public static T[] Shuffle<T>(T[] array)
        {
            T[] result = (T[])array.Clone();
            for (int i = result.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T temp = result[i];
                result[i] = result[j];
                result[j] = temp;
            }
            return result;
        }
    }
}
