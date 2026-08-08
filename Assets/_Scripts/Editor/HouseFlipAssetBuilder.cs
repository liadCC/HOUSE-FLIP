using System.Collections.Generic;
using System.IO;
using HouseFlip.Balance;
using HouseFlip.Building;
using HouseFlip.Core;
using HouseFlip.Events;
using HouseFlip.Furniture;
using HouseFlip.PhysicsGrab;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace HouseFlip.EditorTools
{
    /// <summary>
    /// Generates the placeholder art, prefabs and ScriptableObjects the MVP needs.
    ///
    /// Everything here is programmer art on purpose: coloured boxes standing in for the
    /// low-poly cartoon assets in GDD 1. Replacing a placeholder means swapping the mesh
    /// inside the generated prefab — no code changes, because all the gameplay data lives
    /// in the ScriptableObjects rather than the models.
    /// </summary>
    public static class HouseFlipAssetBuilder
    {
        public const string PrefabRoot = "Assets/_Prefabs";
        public const string DataRoot = "Assets/_ScriptableObjects";
        public const string MaterialRoot = "Assets/_Art/Materials";

        // ------------------------------------------------------------------
        // Folders and materials
        // ------------------------------------------------------------------

        public static void EnsureFolders()
        {
            EnsureFolder("Assets/_Prefabs");
            EnsureFolder("Assets/_Prefabs/Placeables");
            EnsureFolder("Assets/_Prefabs/House");
            EnsureFolder("Assets/_ScriptableObjects");
            EnsureFolder("Assets/_ScriptableObjects/Building");
            EnsureFolder("Assets/_ScriptableObjects/Furniture");
            EnsureFolder("Assets/_ScriptableObjects/Events");
            EnsureFolder("Assets/_Art");
            EnsureFolder("Assets/_Art/Materials");
            EnsureFolder("Assets/_Scenes");
            EnsureFolder("Assets/_Audio");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }

        public static Material GetMaterial(string name, Color color, bool emissive = false)
        {
            string path = $"{MaterialRoot}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.color = color;
                return existing;
            }

            Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            var material = new Material(shader) { color = color };

            // Flat, low-spec look to match the cartoon target in GDD 1.
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.08f);
            }

            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.7f);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ------------------------------------------------------------------
        // Primitive helpers
        // ------------------------------------------------------------------

        /// <summary>A coloured box with its pivot on the floor, which is what every placement assumes.</summary>
        public static GameObject CreateBox(string name, Vector3 size, Material material, Transform parent = null)
        {
            var root = new GameObject(name);

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = size;
            visual.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);

            var renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;

            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            return root;
        }

        public static GameObject SavePrefab(GameObject instance, string folder)
        {
            string path = $"{folder}/{instance.name}.prefab";
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return saved;
        }

        // ------------------------------------------------------------------
        // Placeable prefabs
        // ------------------------------------------------------------------

        /// <summary>
        /// Builds a networked, grabbable placeable. Furniture is deliberately physical:
        /// GDD 28's chaos scenario needs a fridge that can go through a window.
        /// </summary>
        public static GameObject BuildPlaceablePrefab(string name, Vector3 size, Color color, MassCategory mass)
        {
            GameObject root = CreateBox(name, size, GetMaterial($"Mat_{name}", color));

            var body = root.AddComponent<Rigidbody>();
            body.mass = mass.RigidbodyMass();
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // A box collider on the root keeps the grab raycast simple; the visual child
            // carries its own collider from CreatePrimitive, so drop that one.
            Object.DestroyImmediate(root.transform.GetChild(0).GetComponent<Collider>());

            var collider = root.AddComponent<BoxCollider>();
            collider.size = size;
            collider.center = new Vector3(0f, size.y * 0.5f, 0f);

            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();
            root.AddComponent<NetworkRigidbody>();
            root.AddComponent<Grabbable>();
            root.AddComponent<PlacedFurniture>();

            SetPrivateField(root.GetComponent<Grabbable>(), "category", (int)mass);

            root.layer = GameLayers.Grabbable;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = GameLayers.Grabbable;
            }

            return SavePrefab(root, $"{PrefabRoot}/Placeables");
        }

        // ------------------------------------------------------------------
        // ScriptableObject data
        // ------------------------------------------------------------------

        public static T CreateData<T>(string folder, string fileName) where T : ScriptableObject
        {
            string path = $"{folder}/{fileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>
        /// Turns <see cref="CatalogDefinition"/> into the ScriptableObjects the game loads.
        ///
        /// The numbers deliberately live in that plain data table rather than here: the
        /// balance model reads the same table, so the economy cannot be tuned in one place
        /// and generated from another.
        /// </summary>
        public static PlacementCatalog BuildCatalog()
        {
            var buildings = new List<BuildingData>();
            foreach (PlaceableDefinition definition in CatalogDefinition.Buildings)
            {
                BuildingData data = CreateData<BuildingData>($"{DataRoot}/Building", definition.AssetName);
                ApplyCommon(data, definition);
                data.requiresFloorContact = definition.RequiresFloorContact;
                EditorUtility.SetDirty(data);
                buildings.Add(data);
            }

            var furniture = new List<FurnitureData>();
            foreach (PlaceableDefinition definition in CatalogDefinition.Furniture)
            {
                FurnitureData data = CreateData<FurnitureData>($"{DataRoot}/Furniture", definition.AssetName);
                ApplyCommon(data, definition);
                data.category = definition.Category;

                // Decoration suits any room; everything else is judged on where it lands.
                data.preferredRooms = definition.Category == FurnitureCategory.Decoration
                    ? new FurnitureCategory[0]
                    : new[] { definition.Category };

                EditorUtility.SetDirty(data);
                furniture.Add(data);
            }

            PlacementCatalog catalog = CreateData<PlacementCatalog>(DataRoot, "PlacementCatalog");
            catalog.EditorSetContents(buildings, furniture);
            EditorUtility.SetDirty(catalog);

            return catalog;
        }

        private static void ApplyCommon(PlaceableData data, PlaceableDefinition definition)
        {
            data.itemName = definition.DisplayName;
            data.cost = definition.Cost;
            data.valueContribution = definition.ValueContribution;
            data.designPoints = definition.DesignPoints;
            data.size = definition.Size;
            data.height = definition.Height;
            data.prefab = BuildPlaceablePrefab(
                definition.AssetName, definition.PrefabSize, definition.Color, definition.Mass);
        }

        // ------------------------------------------------------------------
        // Random event definitions
        // ------------------------------------------------------------------

        public static GameEventDefinition BuildEventDefinition(string assetName, string eventName,
            string popup, float earliest, float latest)
        {
            GameEventDefinition definition = CreateData<GameEventDefinition>($"{DataRoot}/Events", assetName);
            definition.eventName = eventName;
            definition.popupMessage = popup;
            definition.earliestTime = earliest;
            definition.latestTime = latest;
            definition.weight = 1f;
            definition.oncePerRound = true;
            EditorUtility.SetDirty(definition);
            return definition;
        }

        // ------------------------------------------------------------------
        // Serialized field helpers
        // ------------------------------------------------------------------

        public static void SetPrivateField(Object target, string fieldName, object value)
        {
            if (target == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null)
            {
                Debug.LogWarning($"[HouseFlipAssetBuilder] '{target.GetType().Name}' has no field '{fieldName}'.");
                return;
            }

            switch (value)
            {
                case int intValue:
                    property.intValue = intValue;
                    break;
                case float floatValue:
                    property.floatValue = floatValue;
                    break;
                case bool boolValue:
                    property.boolValue = boolValue;
                    break;
                case string stringValue:
                    property.stringValue = stringValue;
                    break;
                case Color colorValue:
                    property.colorValue = colorValue;
                    break;
                case Vector3 vector3Value:
                    property.vector3Value = vector3Value;
                    break;
                case Vector2 vector2Value:
                    property.vector2Value = vector2Value;
                    break;
                case Object objectValue:
                    property.objectReferenceValue = objectValue;
                    break;
                default:
                    Debug.LogWarning($"[HouseFlipAssetBuilder] Unsupported field type for '{fieldName}'.");
                    return;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Assigns an array/list of object references to a private serialized field.</summary>
        public static void SetPrivateArray(Object target, string fieldName, IList<Object> values)
        {
            if (target == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(fieldName);

            if (property == null || !property.isArray)
            {
                Debug.LogWarning($"[HouseFlipAssetBuilder] '{target.GetType().Name}' has no array field '{fieldName}'.");
                return;
            }

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
