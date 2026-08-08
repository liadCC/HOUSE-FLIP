// Stubs for the UnityEditor surface used by the scene/asset generators.
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityEditor
{
    [AttributeUsage(AttributeTargets.Method)]
    public class MenuItem : Attribute
    {
        public MenuItem(string itemName) { }
        public MenuItem(string itemName, bool isValidateFunction) { }
        public MenuItem(string itemName, bool isValidateFunction, int priority) { }
        public int priority { get; set; }
    }

    public static class AssetDatabase
    {
        public static bool IsValidFolder(string path) => false;
        public static string CreateFolder(string parent, string name) => "";
        public static void CreateAsset(UnityEngine.Object asset, string path) { }
        public static void ImportAsset(string path) { }
        public static T LoadAssetAtPath<T>(string path) where T : UnityEngine.Object => default;
        public static void SaveAssets() { }
        public static void Refresh() { }
    }

    public static class PrefabUtility
    {
        public static GameObject SaveAsPrefabAsset(GameObject instance, string path) => null;
        public static GameObject SaveAsPrefabAsset(GameObject instance, string path, out bool success)
        { success = true; return null; }
    }

    public static class EditorUtility
    {
        public static void SetDirty(UnityEngine.Object o) { }
        public static bool DisplayDialog(string title, string message, string ok) => false;
        public static bool DisplayDialog(string title, string message, string ok, string cancel) => false;
        public static void DisplayProgressBar(string title, string info, float progress) { }
        public static void ClearProgressBar() { }
    }

    public class SerializedObject
    {
        public SerializedObject(UnityEngine.Object target) { }
        public SerializedProperty FindProperty(string path) => null;
        public bool ApplyModifiedProperties() => false;
        public void ApplyModifiedPropertiesWithoutUndo() { }
        public void Update() { }
    }

    public class SerializedProperty
    {
        public int intValue { get; set; }
        public float floatValue { get; set; }
        public bool boolValue { get; set; }
        public string stringValue { get; set; }
        public Color colorValue { get; set; }
        public Vector2 vector2Value { get; set; }
        public Vector3 vector3Value { get; set; }
        public int enumValueIndex { get; set; }
        public UnityEngine.Object objectReferenceValue { get; set; }
        public bool isArray => false;
        public int arraySize { get; set; }
        public SerializedProperty GetArrayElementAtIndex(int i) => null;
        public SerializedProperty FindPropertyRelative(string name) => null;
    }

    public class EditorBuildSettingsScene
    {
        public EditorBuildSettingsScene(string path, bool enabled) { this.path = path; this.enabled = enabled; }
        public string path { get; set; }
        public bool enabled { get; set; }
    }

    public static class EditorBuildSettings
    {
        public static EditorBuildSettingsScene[] scenes { get; set; } = new EditorBuildSettingsScene[0];
    }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }
    public enum NewSceneMode { Single, Additive }

    public static class EditorSceneManager
    {
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => default;
        public static bool SaveScene(Scene scene, string path) => false;
        public static bool SaveScene(Scene scene) => false;
    }
}
