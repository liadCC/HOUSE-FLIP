// Minimal stubs of the UnityEngine surface the House Flip scripts touch.
// Used only to type-check the game code outside the editor. Signatures mirror
// the real API; anything not used by the game is omitted.
using System;
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; } = "";
        public int GetInstanceID() => 0;
        public override string ToString() => name;

        public static bool operator ==(Object a, Object b) => ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !ReferenceEquals(a, b);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => 0;
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);

        public static void Destroy(Object o) { }
        public static void Destroy(Object o, float t) { }
        public static void DestroyImmediate(Object o) { }
        public static T Instantiate<T>(T original) where T : Object => original;
        public static T Instantiate<T>(T original, Transform parent) where T : Object => original;
        public static T Instantiate<T>(T original, Vector3 pos, Quaternion rot) where T : Object => original;
        public static T FindFirstObjectByType<T>() where T : Object => default;
        public static T[] FindObjectsByType<T>(FindObjectsSortMode mode) where T : Object => new T[0];
    }

    public enum FindObjectsSortMode { None, InstanceID }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public static Vector2 operator *(Vector2 a, float b) => new Vector2(a.x * b, a.y * b);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
    }

    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static Vector2Int zero => new Vector2Int(0, 0);
        public static Vector2Int one => new Vector2Int(1, 1);
        public override string ToString() => $"({x}, {y})";
    }

    // Faithfully implemented: the logic tests depend on these producing Unity's answers.
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default;
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 back => new Vector3(0, 0, -1);
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 right => new Vector3(1, 0, 0);

        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);

        public Vector3 normalized
        {
            get
            {
                float m = magnitude;
                return m > 1e-5f ? new Vector3(x / m, y / m, z / m) : zero;
            }
        }

        public void Normalize() { this = normalized; }

        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;

        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x);

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }

        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float d)
        {
            Vector3 delta = b - a;
            float m = delta.magnitude;
            return m <= d || m < 1e-5f ? b : a + delta / m * d;
        }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float b) => new Vector3(a.x * b, a.y * b, a.z * b);
        public static Vector3 operator *(float b, Vector3 a) => a * b;
        public static Vector3 operator /(Vector3 a, float b) => new Vector3(a.x / b, a.y / b, a.z / b);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);

        public override string ToString() => $"({x:0.###}, {y:0.###}, {z:0.###})";
    }

    /// <summary>
    /// Real quaternion arithmetic, with one documented limitation: <see cref="eulerAngles"/>
    /// is a yaw/pitch/roll extraction, which is exact for the single-axis rotations this
    /// project builds via <see cref="Euler"/> but is not a general ZXY decomposition.
    /// Do not write tests that depend on decomposing an arbitrary composed rotation.
    /// </summary>
    public struct Quaternion
    {
        public float x, y, z, w;

        public Quaternion(float x, float y, float z, float w)
        { this.x = x; this.y = y; this.z = z; this.w = w; }

        public static Quaternion identity => new Quaternion(0, 0, 0, 1);

        private const double Deg2Rad = Math.PI / 180.0;
        private const double Rad2Deg = 180.0 / Math.PI;

        public static Quaternion Euler(float x, float y, float z)
        {
            double hx = x * Deg2Rad * 0.5, hy = y * Deg2Rad * 0.5, hz = z * Deg2Rad * 0.5;

            var qx = new Quaternion((float)Math.Sin(hx), 0, 0, (float)Math.Cos(hx));
            var qy = new Quaternion(0, (float)Math.Sin(hy), 0, (float)Math.Cos(hy));
            var qz = new Quaternion(0, 0, (float)Math.Sin(hz), (float)Math.Cos(hz));

            // Unity applies Euler rotations in Z, then X, then Y.
            return qy * qx * qz;
        }

        public static Quaternion Euler(Vector3 v) => Euler(v.x, v.y, v.z);

        public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.up);

        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            Vector3 f = forward.normalized;
            if (f.sqrMagnitude < 1e-8f)
            {
                return identity;
            }

            // Yaw/pitch only, which is all this project's look rotations ever need.
            float yaw = (float)(Math.Atan2(f.x, f.z) * Rad2Deg);
            float pitch = (float)(-Math.Asin(Math.Max(-1.0, Math.Min(1.0, f.y))) * Rad2Deg);
            return Euler(pitch, yaw, 0f);
        }

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => t < 0.5f ? a : b;

        public Vector3 eulerAngles
        {
            get
            {
                double sinPitch = 2.0 * (w * x - y * z);
                sinPitch = Math.Max(-1.0, Math.Min(1.0, sinPitch));

                double pitch = Math.Asin(sinPitch);
                double yaw = Math.Atan2(2.0 * (w * y + x * z), 1.0 - 2.0 * (x * x + y * y));
                double roll = Math.Atan2(2.0 * (w * z + x * y), 1.0 - 2.0 * (x * x + z * z));

                return new Vector3(
                    Normalise360((float)(pitch * Rad2Deg)),
                    Normalise360((float)(yaw * Rad2Deg)),
                    Normalise360((float)(roll * Rad2Deg)));
            }
        }

        // Unity reports Euler angles in [0, 360).
        private static float Normalise360(float degrees)
        {
            float d = degrees % 360f;
            if (d < 0f) d += 360f;
            if (Math.Abs(d - 360f) < 1e-4f) d = 0f;
            return d;
        }

        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            // v' = v + 2 * cross(q.xyz, cross(q.xyz, v) + q.w * v)
            float tx = 2f * (q.y * v.z - q.z * v.y);
            float ty = 2f * (q.z * v.x - q.x * v.z);
            float tz = 2f * (q.x * v.y - q.y * v.x);

            return new Vector3(
                v.x + q.w * tx + (q.y * tz - q.z * ty),
                v.y + q.w * ty + (q.z * tx - q.x * tz),
                v.z + q.w * tz + (q.x * ty - q.y * tx));
        }

        public static Quaternion operator *(Quaternion a, Quaternion b)
        {
            return new Quaternion(
                a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
                a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
                a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
                a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        }

        public override string ToString() => $"({x:0.###}, {y:0.###}, {z:0.###}, {w:0.###})";
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color black => new Color(0, 0, 0);

        public static Color Lerp(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t,
                a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }

        // Unity scales alpha too; the tests only care that it is componentwise.
        public static Color operator *(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a * f);

        public override string ToString() => $"RGBA({r:0.###}, {g:0.###}, {b:0.###}, {a:0.###})";
    }

    public static class ColorUtility
    {
        public static string ToHtmlStringRGB(Color c) => "FFFFFF";
    }

    public struct Bounds
    {
        public Vector3 center, size, min, max, extents;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; this.size = size; min = center; max = center; extents = size; }
        public bool Contains(Vector3 p) => false;
        public float SqrDistance(Vector3 p) => 0f;
    }

    public struct Ray
    {
        public Vector3 origin, direction;
        public Ray(Vector3 origin, Vector3 direction) { this.origin = origin; this.direction = direction; }
    }

    public struct RaycastHit
    {
        public Collider collider => null;
        public Vector3 point => default;
        public Vector3 normal => default;
        public float distance => 0f;
        public Transform transform => null;
    }

    public struct Matrix4x4 { }

    public static class Mathf
    {
        public const float Epsilon = 1e-5f;
        public const float PI = 3.14159265358979f;

        // Unity's Mathf.Round delegates to Math.Round, which is banker's rounding
        // (half-to-even). Grid snapping depends on this, so the stub must match.
        public static float Round(float f) => (float)Math.Round(f, MidpointRounding.ToEven);

        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        public static int Clamp(int v, int a, int b) => v < a ? a : (v > b ? b : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float MoveTowards(float a, float b, float d)
            => Math.Abs(b - a) <= d ? b : a + Math.Sign(b - a) * d;
        public static bool Approximately(float a, float b)
            => Math.Abs(b - a) < Math.Max(1e-6f * Math.Max(Math.Abs(a), Math.Abs(b)), Epsilon * 8f);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int i) => Math.Abs(i);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static int RoundToInt(float f) => (int)Math.Round(f, MidpointRounding.ToEven);
        public static float Exp(float f) => (float)Math.Exp(f);
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);

        // Not real Perlin noise, but bounded to [0,1] and continuous, which is all the
        // camera shake needs from it.
        public static float PerlinNoise(float x, float y)
            => (float)((Math.Sin(x * 12.9898 + y * 78.233) + 1.0) * 0.5);
    }

    public static class Random
    {
        public static float Range(float min, float max) => min;
        public static int Range(int min, int max) => min;
        public static Vector3 insideUnitSphere => default;
        public static float value => 0f;
    }

    public static class Debug
    {
        public static void Log(object m) { }
        public static void LogWarning(object m) { }
        public static void LogError(object m) { }
    }

    public static class Time
    {
        public static float deltaTime => 0f;
        public static float fixedDeltaTime => 0f;
        public static float time => 0f;
    }

    public static class Screen
    {
        public static int width => 1920;
        public static int height => 1080;
    }

    public static class Cursor
    {
        public static CursorLockMode lockState { get; set; }
        public static bool visible { get; set; }
    }

    public enum CursorLockMode { None, Locked, Confined }

    public static class Resources
    {
        public static T GetBuiltinResource<T>(string path) where T : Object => default;
    }

    // ---------------- Components ----------------

    public class Component : Object
    {
        public Transform transform => null;
        public GameObject gameObject => null;
        public string tag { get; set; } = "";
        public T GetComponent<T>() => default;
        public Component GetComponent(Type t) => null;
        public T[] GetComponents<T>() => new T[0];
        public T GetComponentInChildren<T>() => default;
        public T GetComponentInChildren<T>(bool includeInactive) => default;
        public T[] GetComponentsInChildren<T>() => new T[0];
        public T[] GetComponentsInChildren<T>(bool includeInactive) => new T[0];
        public T GetComponentInParent<T>() => default;
        public T[] GetComponentsInParent<T>() => new T[0];
    }

    public class Behaviour : Component
    {
        public bool enabled { get; set; } = true;
        public bool isActiveAndEnabled => enabled;
    }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) => null;
        public void StopCoroutine(Coroutine c) { }
        public void CancelInvoke() { }
    }

    public class Coroutine { }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject => default;
    }

    public class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { this.name = name; }
        public GameObject(string name, params Type[] components) { this.name = name; }
        public Transform transform => null;
        public int layer { get; set; }
        public string tag { get; set; } = "";
        public bool activeSelf => true;
        public bool activeInHierarchy => true;
        public bool isStatic { get; set; }
        public void SetActive(bool value) { }
        public T AddComponent<T>() where T : Component => default;
        public Component AddComponent(Type t) => null;
        public T GetComponent<T>() => default;
        public T[] GetComponents<T>() => new T[0];
        public T GetComponentInChildren<T>() => default;
        public T GetComponentInChildren<T>(bool includeInactive) => default;
        public T[] GetComponentsInChildren<T>() => new T[0];
        public T[] GetComponentsInChildren<T>(bool includeInactive) => new T[0];
        public T GetComponentInParent<T>() => default;
        public static GameObject Find(string name) => null;
        public static GameObject CreatePrimitive(PrimitiveType type) => null;
    }

    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    public class Transform : Component, IEnumerable
    {
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public Vector3 localPosition { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 localScale { get; set; }
        public Vector3 eulerAngles { get; set; }
        public Vector3 localEulerAngles { get; set; }
        public Vector3 forward => default;
        public Vector3 right => default;
        public Vector3 up => default;
        public Transform parent { get; set; }
        public Transform root => this;
        public int childCount => 0;
        public Matrix4x4 localToWorldMatrix => default;
        public Transform GetChild(int i) => null;
        public void SetParent(Transform p) { }
        public void SetParent(Transform p, bool worldPositionStays) { }
        public bool IsChildOf(Transform t) => false;
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { }
        public IEnumerator GetEnumerator() => null;
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
    }

    // ---------------- Physics ----------------

    public class Collider : Component
    {
        public bool enabled { get; set; } = true;
        public bool isTrigger { get; set; }
        public Bounds bounds => default;
        public Rigidbody attachedRigidbody => null;
    }

    public class BoxCollider : Collider
    {
        public Vector3 size { get; set; }
        public Vector3 center { get; set; }
    }

    public class SphereCollider : Collider { public float radius { get; set; } }
    public class CapsuleCollider : Collider { }

    public class Rigidbody : Component
    {
        public float mass { get; set; }
        public bool useGravity { get; set; }
        public bool isKinematic { get; set; }
        public bool detectCollisions { get; set; }
        public Vector3 linearVelocity { get; set; }
        public Vector3 angularVelocity { get; set; }
        public Vector3 position { get; set; }
        public Quaternion rotation { get; set; }
        public RigidbodyInterpolation interpolation { get; set; }
        public CollisionDetectionMode collisionDetectionMode { get; set; }
        public void MovePosition(Vector3 p) { }
        public void MoveRotation(Quaternion q) { }
        public void AddForce(Vector3 f, ForceMode m) { }
        public void AddTorque(Vector3 t, ForceMode m) { }
        public void AddExplosionForce(float force, Vector3 pos, float radius, float upMod, ForceMode m) { }
    }

    public enum RigidbodyInterpolation { None, Interpolate, Extrapolate }
    public enum CollisionDetectionMode { Discrete, Continuous, ContinuousDynamic, ContinuousSpeculative }
    public enum ForceMode { Force, Acceleration, Impulse, VelocityChange }
    public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }

    public class CharacterController : Collider
    {
        public float height { get; set; }
        public float radius { get; set; }
        public Vector3 center { get; set; }
        public float slopeLimit { get; set; }
        public float stepOffset { get; set; }
        public bool isGrounded => false;
        public void Move(Vector3 motion) { }
    }

    public static class Physics
    {
        public static bool Raycast(Ray r, out RaycastHit hit, float d, int mask, QueryTriggerInteraction q)
        { hit = default; return false; }
        public static bool Raycast(Ray r, out RaycastHit hit, float d, int mask) { hit = default; return false; }
        public static bool SphereCast(Ray r, float radius, out RaycastHit hit, float d, int mask, QueryTriggerInteraction q)
        { hit = default; return false; }
        public static bool SphereCast(Vector3 o, float radius, Vector3 dir, out RaycastHit hit, float d, int mask, QueryTriggerInteraction q)
        { hit = default; return false; }
        public static Collider[] OverlapSphere(Vector3 p, float r) => new Collider[0];
        public static int OverlapBoxNonAlloc(Vector3 center, Vector3 half, Collider[] results, Quaternion rot, int mask, QueryTriggerInteraction q) => 0;
    }

    public struct LayerMask
    {
        public int value;
        public static implicit operator int(LayerMask m) => m.value;
        public static implicit operator LayerMask(int i) => new LayerMask { value = i };
        public static int NameToLayer(string n) => 0;
    }

    // ---------------- Rendering ----------------

    public class Renderer : Component
    {
        public bool enabled { get; set; } = true;
        public Material material { get; set; }
        public Material sharedMaterial { get; set; }
        public Material[] materials { get; set; }
        public Material[] sharedMaterials { get; set; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
        public void GetPropertyBlock(MaterialPropertyBlock b) { }
        public void SetPropertyBlock(MaterialPropertyBlock b) { }
    }

    public class MeshRenderer : Renderer { }

    public class MeshFilter : Component
    {
        public Mesh mesh { get; set; }
        public Mesh sharedMesh { get; set; }
    }

    public class Skybox : Behaviour { public Material material { get; set; } }
    public class SkinnedMeshRenderer : Renderer { }

    public class Material : Object
    {
        public Material(Shader s) { }
        public Color color { get; set; }
        public int renderQueue { get; set; }
        public bool HasProperty(string n) => true;
        public void SetFloat(string n, float v) { }
        public void SetInt(string n, int v) { }
        public void SetColor(string n, Color c) { }
        public void EnableKeyword(string k) { }
        public void DisableKeyword(string k) { }
    }

    public class Shader : Object
    {
        public static Shader Find(string name) => null;
    }

    public class MaterialPropertyBlock
    {
        public void SetColor(string n, Color c) { }
        public void SetFloat(string n, float f) { }
    }

    /// <summary>Functional enough to validate generated geometry in tests.</summary>
    public class Mesh : Object
    {
        public Vector3[] vertices { get; set; } = new Vector3[0];
        public Vector3[] normals { get; set; } = new Vector3[0];
        public int[] triangles { get; set; } = new int[0];
        public Vector2[] uv { get; set; } = new Vector2[0];
        public Bounds bounds { get; private set; }

        public int vertexCount => vertices.Length;
        public void Clear() { vertices = new Vector3[0]; normals = new Vector3[0]; triangles = new int[0]; }

        public void RecalculateBounds()
        {
            if (vertices.Length == 0) { bounds = new Bounds(Vector3.zero, Vector3.zero); return; }

            Vector3 min = vertices[0], max = vertices[0];
            foreach (Vector3 v in vertices)
            {
                min = new Vector3(Math.Min(min.x, v.x), Math.Min(min.y, v.y), Math.Min(min.z, v.z));
                max = new Vector3(Math.Max(max.x, v.x), Math.Max(max.y, v.y), Math.Max(max.z, v.z));
            }

            Vector3 size = max - min;
            Vector3 centre = (min + max) * 0.5f;
            bounds = new Bounds(centre, size) { min = min, max = max, extents = size * 0.5f };
        }

        public void RecalculateNormals() { }
    }
    public class Sprite : Object { }
    public class Font : Object { }

    public class Light : Behaviour
    {
        public LightType type { get; set; }
        public float intensity { get; set; }
        public Color color { get; set; }
        public LightShadows shadows { get; set; }
    }

    public enum LightType { Spot, Directional, Point, Area, Rectangle, Disc }
    public enum FogMode { Linear = 1, Exponential = 2, ExponentialSquared = 3 }
    public enum LightShadows { None, Hard, Soft }

    public static class RenderSettings
    {
        public static Color ambientLight { get; set; }
        public static float ambientIntensity { get; set; }
        public static Color ambientSkyColor { get; set; }
        public static Color ambientEquatorColor { get; set; }
        public static Color ambientGroundColor { get; set; }
        public static Rendering.AmbientMode ambientMode { get; set; }
        public static Material skybox { get; set; }
        public static bool fog { get; set; }
        public static Color fogColor { get; set; }
        public static FogMode fogMode { get; set; }
        public static float fogStartDistance { get; set; }
        public static float fogEndDistance { get; set; }
        public static float fogDensity { get; set; }
    }

    public class Camera : Behaviour
    {
        public static Camera main => null;
        public float fieldOfView { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
    }

    public class ParticleSystem : Component
    {
        public bool isPlaying => false;
        public void Play() { }
        public void Stop() { }
    }

    public class AudioClip : Object { public float length => 0f; }

    public class AudioSource : Behaviour
    {
        public AudioClip clip { get; set; }
        public bool loop { get; set; }
        public bool playOnAwake { get; set; }
        public float volume { get; set; }
        public float pitch { get; set; }
        public float spatialBlend { get; set; }
        public void Play() { }
        public void Stop() { }
        public void PlayOneShot(AudioClip c) { }
        public void PlayOneShot(AudioClip c, float volumeScale) { }
        public static void PlayClipAtPoint(AudioClip c, Vector3 p) { }
        public static void PlayClipAtPoint(AudioClip c, Vector3 p, float volume) { }
    }

    public class AudioListener : Behaviour { }

    public static class Gizmos
    {
        public static Color color { get; set; }
        public static Matrix4x4 matrix { get; set; }
        public static void DrawCube(Vector3 c, Vector3 s) { }
        public static void DrawWireCube(Vector3 c, Vector3 s) { }
    }

    public enum TextAnchor
    {
        UpperLeft, UpperCenter, UpperRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        LowerLeft, LowerCenter, LowerRight
    }

    public class RectOffset
    {
        public RectOffset() { }
        public RectOffset(int l, int r, int t, int b) { }
    }

    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
    }

    // ---------------- Input ----------------

    public static class Input
    {
        public static float GetAxis(string n) => 0f;
        public static float GetAxisRaw(string n) => 0f;
        public static bool GetKey(KeyCode k) => false;
        public static bool GetKeyDown(KeyCode k) => false;
        public static bool GetKeyUp(KeyCode k) => false;
        public static bool GetButton(string n) => false;
        public static bool GetButtonDown(string n) => false;
        public static bool GetMouseButton(int b) => false;
        public static bool GetMouseButtonDown(int b) => false;
    }

    public enum KeyCode
    {
        None, Space, LeftShift, E, F, B, C, G, R, X, Escape,
        Alpha0, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5
    }

    // ---------------- Attributes ----------------

    public enum RuntimeInitializeLoadType { AfterAssembliesLoaded, BeforeSplashScreen, BeforeSceneLoad, AfterSceneLoad, SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)] public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute() { }
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { }
    }

    [AttributeUsage(AttributeTargets.Field)] public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class HideInInspector : Attribute { }
    [AttributeUsage(AttributeTargets.Field)] public class HeaderAttribute : Attribute { public HeaderAttribute(string h) { } }
    [AttributeUsage(AttributeTargets.Field)] public class TooltipAttribute : Attribute { public TooltipAttribute(string t) { } }
    [AttributeUsage(AttributeTargets.Field)] public class RangeAttribute : Attribute { public RangeAttribute(float a, float b) { } }
    [AttributeUsage(AttributeTargets.Field)] public class TextAreaAttribute : Attribute { public TextAreaAttribute() { } public TextAreaAttribute(int a, int b) { } }
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)] public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
    [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponent : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public class CreateAssetMenu : Attribute
    {
        public string menuName { get; set; }
        public string fileName { get; set; }
        public int order { get; set; }
    }
    [AttributeUsage(AttributeTargets.All)] public class SerializableAttribute2 : Attribute { }
}

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
    public enum BlendMode { Zero, One, SrcAlpha, OneMinusSrcAlpha }
    public enum AmbientMode { Skybox, Trilight, Flat, Custom }
}

namespace UnityEngine.Events
{
    public class UnityEventBase { }
    public class UnityEvent : UnityEventBase
    {
        public void Invoke() { }
        public void AddListener(Action a) { }
        public void RemoveListener(Action a) { }
    }
    public class UnityEvent<T> : UnityEventBase
    {
        public void Invoke(T arg) { }
        public void AddListener(Action<T> a) { }
    }
}

namespace UnityEngine.EventSystems
{
    public class EventSystem : UnityEngine.Behaviour { }
    public class StandaloneInputModule : UnityEngine.Behaviour { }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene { public string name => ""; public string path => ""; }
    public static class SceneManager { }
}
