using System.Collections.Generic;
using System.IO;
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
        /// The full MVP catalog from GDD 10 and 11.
        /// Costs and value contributions are first-pass balance: a full renovation of every
        /// room lands a little under the $20,000 budget, so a careful team turns a profit
        /// and a chaotic one does not.
        /// </summary>
        public static PlacementCatalog BuildCatalog()
        {
            var buildings = new List<BuildingData>
            {
                MakeBuilding("Build_WallSegment", "Wall Segment", 350f, 900f, new Vector2Int(4, 1), 3f,
                    new Vector3(2f, 3f, 0.2f), new Color(0.90f, 0.88f, 0.83f), MassCategory.Heavy),
                MakeBuilding("Build_Door", "Door", 450f, 1100f, new Vector2Int(2, 1), 2.4f,
                    new Vector3(1f, 2.4f, 0.14f), new Color(0.55f, 0.36f, 0.22f), MassCategory.Medium),
                MakeBuilding("Build_Window", "Window", 500f, 1300f, new Vector2Int(2, 1), 1.4f,
                    new Vector3(1.2f, 1.2f, 0.12f), new Color(0.62f, 0.82f, 0.92f), MassCategory.Medium),
                MakeBuilding("Build_FloorTile", "Floor Tile", 90f, 260f, new Vector2Int(2, 2), 0.12f,
                    new Vector3(1f, 0.1f, 1f), new Color(0.82f, 0.79f, 0.72f), MassCategory.Light),
                MakeBuilding("Build_Cabinet", "Cabinet", 620f, 1500f, new Vector2Int(2, 1), 1.9f,
                    new Vector3(1f, 1.9f, 0.55f), new Color(0.72f, 0.55f, 0.35f), MassCategory.Heavy),
                MakeBuilding("Build_Sink", "Sink", 540f, 1400f, new Vector2Int(2, 1), 0.95f,
                    new Vector3(0.9f, 0.9f, 0.5f), new Color(0.92f, 0.94f, 0.95f), MassCategory.Medium)
            };

            var furniture = new List<FurnitureData>
            {
                // Living Room
                MakeFurniture("Furniture_Sofa", "Sofa", FurnitureCategory.LivingRoom, 1400f, 2600f, 22f,
                    new Vector2Int(4, 2), 0.85f, new Vector3(2f, 0.85f, 1f),
                    new Color(0.36f, 0.52f, 0.78f), MassCategory.Heavy),
                MakeFurniture("Furniture_TV", "TV", FurnitureCategory.LivingRoom, 900f, 1900f, 16f,
                    new Vector2Int(3, 1), 0.7f, new Vector3(1.4f, 0.7f, 0.12f),
                    new Color(0.14f, 0.14f, 0.17f), MassCategory.Medium),
                MakeFurniture("Furniture_CoffeeTable", "Coffee Table", FurnitureCategory.LivingRoom, 380f, 800f, 10f,
                    new Vector2Int(2, 2), 0.45f, new Vector3(1f, 0.45f, 0.6f),
                    new Color(0.62f, 0.42f, 0.26f), MassCategory.Medium),
                MakeFurniture("Furniture_Chair", "Chair", FurnitureCategory.LivingRoom, 220f, 480f, 7f,
                    new Vector2Int(1, 1), 0.9f, new Vector3(0.5f, 0.9f, 0.5f),
                    new Color(0.78f, 0.42f, 0.34f), MassCategory.Light),
                MakeFurniture("Furniture_FloorLamp", "Floor Lamp", FurnitureCategory.LivingRoom, 260f, 560f, 12f,
                    new Vector2Int(1, 1), 1.6f, new Vector3(0.28f, 1.6f, 0.28f),
                    new Color(0.95f, 0.88f, 0.62f), MassCategory.Light),

                // Bedroom
                MakeFurniture("Furniture_Bed", "Bed", FurnitureCategory.Bedroom, 1600f, 3000f, 24f,
                    new Vector2Int(4, 5), 0.6f, new Vector3(2f, 0.6f, 2.4f),
                    new Color(0.85f, 0.80f, 0.72f), MassCategory.Heavy),
                MakeFurniture("Furniture_Wardrobe", "Wardrobe", FurnitureCategory.Bedroom, 1100f, 2200f, 18f,
                    new Vector2Int(3, 2), 2.1f, new Vector3(1.4f, 2.1f, 0.7f),
                    new Color(0.58f, 0.40f, 0.26f), MassCategory.Heavy),
                MakeFurniture("Furniture_Desk", "Desk", FurnitureCategory.Bedroom, 520f, 1050f, 11f,
                    new Vector2Int(3, 2), 0.78f, new Vector3(1.4f, 0.78f, 0.7f),
                    new Color(0.68f, 0.50f, 0.32f), MassCategory.Medium),
                MakeFurniture("Furniture_BedsideLamp", "Bedside Lamp", FurnitureCategory.Bedroom, 180f, 380f, 9f,
                    new Vector2Int(1, 1), 0.5f, new Vector3(0.3f, 0.5f, 0.3f),
                    new Color(0.96f, 0.86f, 0.55f), MassCategory.Light),

                // Kitchen
                MakeFurniture("Furniture_Fridge", "Fridge", FurnitureCategory.Kitchen, 1500f, 2900f, 20f,
                    new Vector2Int(2, 2), 1.9f, new Vector3(0.9f, 1.9f, 0.8f),
                    new Color(0.90f, 0.92f, 0.94f), MassCategory.Heavy),
                MakeFurniture("Furniture_Oven", "Oven", FurnitureCategory.Kitchen, 1150f, 2300f, 17f,
                    new Vector2Int(2, 2), 0.95f, new Vector3(0.85f, 0.95f, 0.7f),
                    new Color(0.28f, 0.28f, 0.32f), MassCategory.Heavy),
                MakeFurniture("Furniture_Counter", "Counter", FurnitureCategory.Kitchen, 700f, 1500f, 13f,
                    new Vector2Int(4, 2), 0.95f, new Vector3(2f, 0.95f, 0.7f),
                    new Color(0.80f, 0.74f, 0.62f), MassCategory.Heavy),
                MakeFurniture("Furniture_KitchenSink", "Kitchen Sink", FurnitureCategory.Kitchen, 620f, 1350f, 12f,
                    new Vector2Int(2, 2), 0.95f, new Vector3(0.9f, 0.95f, 0.65f),
                    new Color(0.88f, 0.90f, 0.92f), MassCategory.Medium),

                // Bathroom
                MakeFurniture("Furniture_Toilet", "Toilet", FurnitureCategory.Bathroom, 480f, 1200f, 10f,
                    new Vector2Int(2, 2), 0.8f, new Vector3(0.6f, 0.8f, 0.75f),
                    new Color(0.95f, 0.96f, 0.97f), MassCategory.Medium),
                MakeFurniture("Furniture_Shower", "Shower", FurnitureCategory.Bathroom, 1250f, 2500f, 19f,
                    new Vector2Int(3, 3), 2.1f, new Vector3(1.2f, 2.1f, 1.2f),
                    new Color(0.72f, 0.86f, 0.90f), MassCategory.Heavy),
                MakeFurniture("Furniture_BathroomSink", "Bathroom Sink", FurnitureCategory.Bathroom, 420f, 950f, 10f,
                    new Vector2Int(2, 1), 0.9f, new Vector3(0.8f, 0.9f, 0.5f),
                    new Color(0.93f, 0.95f, 0.96f), MassCategory.Medium),
                MakeFurniture("Furniture_Mirror", "Mirror", FurnitureCategory.Bathroom, 240f, 620f, 11f,
                    new Vector2Int(2, 1), 0.9f, new Vector3(0.8f, 0.9f, 0.08f),
                    new Color(0.80f, 0.88f, 0.92f), MassCategory.Light),

                // Decoration — no preferred room, so it earns full design points anywhere.
                MakeFurniture("Furniture_Plant", "Plant", FurnitureCategory.Decoration, 150f, 340f, 12f,
                    new Vector2Int(1, 1), 1.1f, new Vector3(0.45f, 1.1f, 0.45f),
                    new Color(0.34f, 0.68f, 0.36f), MassCategory.Light),
                MakeFurniture("Furniture_Painting", "Painting", FurnitureCategory.Decoration, 320f, 780f, 15f,
                    new Vector2Int(2, 1), 0.8f, new Vector3(0.9f, 0.8f, 0.07f),
                    new Color(0.85f, 0.62f, 0.30f), MassCategory.Light),
                MakeFurniture("Furniture_Rug", "Rug", FurnitureCategory.Decoration, 280f, 640f, 13f,
                    new Vector2Int(4, 3), 0.06f, new Vector3(2f, 0.06f, 1.5f),
                    new Color(0.72f, 0.30f, 0.32f), MassCategory.Light),
                MakeFurniture("Furniture_Clock", "Clock", FurnitureCategory.Decoration, 130f, 300f, 8f,
                    new Vector2Int(1, 1), 0.4f, new Vector3(0.4f, 0.4f, 0.08f),
                    new Color(0.94f, 0.90f, 0.80f), MassCategory.Light)
            };

            PlacementCatalog catalog = CreateData<PlacementCatalog>(DataRoot, "PlacementCatalog");
            catalog.EditorSetContents(buildings, furniture);
            EditorUtility.SetDirty(catalog);

            return catalog;
        }

        private static BuildingData MakeBuilding(string assetName, string displayName, float cost,
            float value, Vector2Int size, float height, Vector3 prefabSize, Color color, MassCategory mass)
        {
            BuildingData data = CreateData<BuildingData>($"{DataRoot}/Building", assetName);
            data.itemName = displayName;
            data.cost = cost;
            data.valueContribution = value;
            data.size = size;
            data.height = height;
            data.designPoints = 4f;
            data.prefab = BuildPlaceablePrefab(assetName, prefabSize, color, mass);
            EditorUtility.SetDirty(data);
            return data;
        }

        private static FurnitureData MakeFurniture(string assetName, string displayName,
            FurnitureCategory category, float cost, float value, float designPoints,
            Vector2Int size, float height, Vector3 prefabSize, Color color, MassCategory mass)
        {
            FurnitureData data = CreateData<FurnitureData>($"{DataRoot}/Furniture", assetName);
            data.itemName = displayName;
            data.category = category;
            data.cost = cost;
            data.valueContribution = value;
            data.designPoints = designPoints;
            data.size = size;
            data.height = height;

            // Decoration suits any room; everything else is judged on where it lands.
            data.preferredRooms = category == FurnitureCategory.Decoration
                ? new FurnitureCategory[0]
                : new[] { category };

            data.prefab = BuildPlaceablePrefab(assetName, prefabSize, color, mass);
            EditorUtility.SetDirty(data);
            return data;
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
