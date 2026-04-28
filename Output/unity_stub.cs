// Minimal UnityEngine stub providing only the APIs touched by ScenarioGeneration.
// Compiled into the dry-run console exe in place of the real UnityEngine.dll.
using System;
using System.Globalization;

namespace UnityEngine
{
    [Serializable]
    public struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public Vector3 normalized {
            get {
                float m = magnitude;
                return m < 1e-9f ? zero : new Vector3(x / m, y / m, z / m);
            }
        }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float s)   => new Vector3(a.x * s, a.y * s, a.z * s);
        public static Vector3 operator *(float s, Vector3 a)   => a * s;
        public bool Equals(Vector3 o) => x == o.x && y == o.y && z == o.z;
        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "({0:F2}, {1:F2}, {2:F2})", x, y, z);
    }

    [Serializable]
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
    }

    public static class Mathf
    {
        public static float Abs(float v) => Math.Abs(v);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
    }

    public static class Debug
    {
        public static void Log(object msg)        { /* silenced */ }
        public static void LogWarning(object msg) { /* silenced */ }
        public static void LogError(object msg)   { Console.Error.WriteLine("[ERR] " + msg); }
    }

    public static class Application
    {
        public static string dataPath => System.IO.Path.Combine(Environment.CurrentDirectory, "Assets");
    }

    // Used only by ScenarioConfigLoader.LoadFromTextAsset and ScenarioExporter.LoadFromTextAsset,
    // neither of which the dry-run touches. Stubbed for compile-only support.
    public class TextAsset
    {
        public string text { get; set; }
    }

    public class Object { }
}
