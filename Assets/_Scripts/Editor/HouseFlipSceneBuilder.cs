using System.Collections.Generic;
using HouseFlip.Art;
using HouseFlip.Audio;
using HouseFlip.Balance;
using HouseFlip.Building;
using HouseFlip.Cleaning;
using HouseFlip.Core;
using HouseFlip.Demolition;
using HouseFlip.Economy;
using HouseFlip.Events;
using HouseFlip.Furniture;
using HouseFlip.GameFlow;
using HouseFlip.Interaction;
using HouseFlip.Networking;
using HouseFlip.Painting;
using HouseFlip.PhysicsGrab;
using HouseFlip.Player;
using HouseFlip.Polish;
using HouseFlip.Repair;
using HouseFlip.UI;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HouseFlip.EditorTools
{
    /// <summary>
    /// Generates the entire MVP scene from code: the house, the props, the systems, the
    /// networking rig and the UI.
    ///
    /// The scene is a build artefact, not something to hand-edit — regenerating it is one
    /// menu click, which keeps the layout reproducible while the systems are still moving.
    /// Once the art pass starts, stop regenerating and edit the saved scene directly.
    /// </summary>
    public static class HouseFlipSceneBuilder
    {
        private const string ScenePath = "Assets/_Scenes/HouseFlip_MVP.unity";
        private const string PlayerPrefabPath = "Assets/_Prefabs/Player.prefab";

        private const float WallHeight = HouseDefinition.WallHeight;
        private const float WallThickness = HouseDefinition.WallThickness;
        private const float DoorWidth = HouseDefinition.DoorWidth;

        [MenuItem("House Flip/Build MVP Scene", priority = 0)]
        public static void BuildScene()
        {
            if (!EditorUtility.DisplayDialog("Build House Flip MVP Scene",
                    "This regenerates prefabs, data assets and the MVP scene.\n\n" +
                    "Any hand-edits to the generated scene will be lost.",
                    "Build it", "Cancel"))
            {
                return;
            }

            try
            {
                EditorUtility.DisplayProgressBar("House Flip", "Creating folders and data…", 0.05f);
                HouseFlipAssetBuilder.EnsureFolders();

                PlacementCatalog catalog = HouseFlipAssetBuilder.BuildCatalog();

                EditorUtility.DisplayProgressBar("House Flip", "Synthesising placeholder audio…", 0.15f);
                var audioClips = HouseFlipAudioBuilder.GenerateClips();

                EditorUtility.DisplayProgressBar("House Flip", "Building the player prefab…", 0.2f);
                GameObject playerPrefab = BuildPlayerPrefab(catalog);

                EditorUtility.DisplayProgressBar("House Flip", "Creating the scene…", 0.35f);
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

                BuildEnvironment();

                EditorUtility.DisplayProgressBar("House Flip", "Building the house…", 0.5f);
                var roomControllers = new Dictionary<string, RoomController>();
                Transform houseRoot = BuildHouse(roomControllers);

                EditorUtility.DisplayProgressBar("House Flip", "Dressing the rooms…", 0.65f);
                var pipes = new List<BurstPipe>();
                FuseBox fuseBox = DressRooms(houseRoot, roomControllers, pipes);

                EditorUtility.DisplayProgressBar("House Flip", "Wiring systems and networking…", 0.8f);
                HouseInspectorNPC inspector = BuildInspectorNPC();
                BuildSystems(catalog, pipes, fuseBox, inspector);
                BuildNetworking(playerPrefab, catalog);
                BuildUI(catalog, audioClips);

                EditorUtility.DisplayProgressBar("House Flip", "Saving…", 0.95f);
                EditorSceneManager.SaveScene(scene, ScenePath);
                RegisterSceneInBuildSettings();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[House Flip] MVP scene built at {ScenePath}. Press Play, then HOST GAME.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ==================================================================
        // Environment
        // ==================================================================

        private static void BuildEnvironment()
        {
            // Warm key light, cool ambient. That contrast is the whole look: flat-shaded
            // geometry has no texture detail to carry depth, so the light has to do it.
            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = ArtPalette.SunLight;
            light.shadows = LightShadows.Soft;

            // Steep enough to light interiors through the open roof, angled enough that
            // walls cast readable shadows rather than lying flat.
            lightObject.transform.rotation = Quaternion.Euler(52f, -38f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ArtPalette.AmbientSky;
            RenderSettings.ambientEquatorColor = ArtPalette.AmbientEquator;
            RenderSettings.ambientGroundColor = ArtPalette.AmbientGround;

            RenderSettings.skybox = BuildSkyMaterial();

            // Gentle distance fog pulls the yard back and stops the flat ground plane
            // running to a hard horizon line.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = ArtPalette.Fog;
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 95f;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(8f, 1f, 8f);
            ground.transform.position = new Vector3(0.5f, -0.02f, 2f);
            ground.GetComponent<Renderer>().sharedMaterial =
                HouseFlipAssetBuilder.GetMaterial("Mat_Grass", ArtPalette.Grass);
            ground.isStatic = true;
        }

        private static Material BuildSkyMaterial()
        {
            const string path = "Assets/_Art/Materials/Mat_Sky.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("HouseFlip/GradientSky");
            if (shader == null)
            {
                Debug.LogWarning("[House Flip] Gradient sky shader not found — leaving the default skybox.");
                return null;
            }

            var material = new Material(shader);
            material.SetColor("_TopColor", ArtPalette.SkyTop);
            material.SetColor("_HorizonColor", ArtPalette.SkyHorizon);
            material.SetColor("_BottomColor", ArtPalette.SkyBottom);

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ==================================================================
        // House shell
        // ==================================================================

        private static Transform BuildHouse(Dictionary<string, RoomController> roomControllers)
        {
            var houseRoot = new GameObject("House").transform;

            Material floorMaterial = HouseFlipAssetBuilder.GetMaterial("Mat_Floor", ArtPalette.FloorWood);

            foreach (RoomDefinition room in HouseDefinition.Rooms)
            {
                bool isYard = room.Name == "Yard";

                if (!isYard)
                {
                    GameObject floor = HouseFlipAssetBuilder.CreateBox(
                        $"Floor_{room.Name}",
                        new Vector3(room.Width, 0.2f, room.Depth),
                        floorMaterial,
                        houseRoot);

                    floor.transform.position = room.Center + new Vector3(0f, -0.2f, 0f);
                    floor.layer = GameLayers.HouseStructure;
                    SetLayerRecursive(floor.transform, GameLayers.HouseStructure);
                }

                roomControllers[room.Name] = CreateRoomVolume(room, houseRoot, isYard);
            }

            BuildWalls(houseRoot);
            return houseRoot;
        }

        private static RoomController CreateRoomVolume(RoomDefinition room, Transform parent, bool isYard)
        {
            var volumeObject = new GameObject($"Room_{room.Name}");
            volumeObject.transform.SetParent(parent, false);
            volumeObject.transform.position = room.Center + new Vector3(0f, WallHeight * 0.5f, 0f);

            var box = volumeObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(room.Width, WallHeight, room.Depth);

            // A room volume wraps every object in the room. On any raycasting layer it
            // would be the first thing every interaction ray hits, so park it on
            // Ignore Raycast — the room is queried by bounds, never by ray.
            volumeObject.layer = GameLayers.IgnoreRaycast;

            volumeObject.AddComponent<NetworkObject>();

            var controller = volumeObject.AddComponent<RoomController>();
            HouseFlipAssetBuilder.SetPrivateField(controller, "roomName", room.Name);

            HouseFlipAssetBuilder.SetPrivateField(controller, "scoreWeight", room.ScoreWeight);

            return controller;
        }

        private static void BuildWalls(Transform houseRoot)
        {
            var wallsRoot = new GameObject("Walls").transform;
            wallsRoot.SetParent(houseRoot, false);

            foreach (WallRunDefinition run in HouseDefinition.WallRuns)
            {
                AddWallRun(wallsRoot, run.Name, run.Start, run.End, run.Doors, run.Structural);
            }
        }

        /// <summary>
        /// Builds one wall run, split into segments around any doorways.
        /// <paramref name="doorPositions"/> are world coordinates along the run's axis.
        /// </summary>
        private static void AddWallRun(Transform parent, string name, Vector3 start, Vector3 end,
            float[] doorPositions, bool structural)
        {
            bool alongX = !Mathf.Approximately(start.x, end.x);

            float from = alongX ? start.x : start.z;
            float to = alongX ? end.x : end.z;
            float fixedAxis = alongX ? start.z : start.x;

            // Convert doorways into sorted gap intervals.
            var gaps = new List<Vector2>();
            if (doorPositions != null)
            {
                foreach (float door in doorPositions)
                {
                    gaps.Add(new Vector2(door - DoorWidth * 0.5f, door + DoorWidth * 0.5f));
                }

                gaps.Sort((a, b) => a.x.CompareTo(b.x));
            }

            float cursor = from;
            int segment = 0;

            foreach (Vector2 gap in gaps)
            {
                if (gap.x > cursor)
                {
                    CreateWallSegment($"{name}_{segment++}", cursor, gap.x, fixedAxis, alongX, parent, structural);
                }

                cursor = Mathf.Max(cursor, gap.y);
            }

            if (cursor < to)
            {
                CreateWallSegment($"{name}_{segment}", cursor, to, fixedAxis, alongX, parent, structural);
            }
        }

        private static void CreateWallSegment(string name, float from, float to, float fixedAxis,
            bool alongX, Transform parent, bool structural)
        {
            float length = to - from;
            if (length <= 0.05f)
            {
                return;
            }

            float mid = (from + to) * 0.5f;

            Vector3 size = alongX
                ? new Vector3(length, WallHeight, WallThickness)
                : new Vector3(WallThickness, WallHeight, length);

            Vector3 position = alongX
                ? new Vector3(mid, 0f, fixedAxis)
                : new Vector3(fixedAxis, 0f, mid);

            Material material = HouseFlipAssetBuilder.GetMaterial(
                "Mat_Wall", ArtPalette.Plaster);

            GameObject wall = HouseFlipAssetBuilder.CreateBox(name, size, material, parent);
            wall.transform.position = position;

            SetLayerRecursive(wall.transform, GameLayers.HouseStructure);

            wall.AddComponent<NetworkObject>();

            // Every wall takes paint (GDD 14).
            var paintable = wall.AddComponent<PaintableWall>();
            HouseFlipAssetBuilder.SetPrivateField(paintable, "targetRenderer",
                wall.GetComponentInChildren<Renderer>());

            // Every wall can come down, but the perimeter costs you (GDD 9, 16).
            var destructible = wall.AddComponent<Destructible>();
            HouseFlipAssetBuilder.SetPrivateField(destructible, "maxHitPoints", structural ? 8 : 4);
            HouseFlipAssetBuilder.SetPrivateField(destructible, "isStructural", structural);
            HouseFlipAssetBuilder.SetPrivateField(destructible, "intactVisual", wall.transform.GetChild(0).gameObject);
            HouseFlipAssetBuilder.SetPrivateField(destructible, "smashVerb", "Smash Wall");
        }

        // ==================================================================
        // Room dressing: dirt, broken fixtures, destructible junk, pipes
        // ==================================================================

        private static FuseBox DressRooms(Transform houseRoot,
            Dictionary<string, RoomController> rooms, List<BurstPipe> pipes)
        {
            var propsRoot = new GameObject("Props").transform;
            propsRoot.SetParent(houseRoot, false);

            foreach (RoomDefinition room in HouseDefinition.Rooms)
            {
                if (room.Name == "Yard")
                {
                    continue;
                }

                // Dirt: the cleaning system's raw material (GDD 12).
                int dirtCount = room.DirtCount;
                for (int i = 0; i < dirtCount; i++)
                {
                    Vector3 position = ScatterPoint(room, i, dirtCount);
                    CreateDirt($"Dirt_{room.Name}_{i}", position, propsRoot);
                }

                // A burst pipe per room, dormant until the water leak event picks one.
                pipes.Add(CreatePipe($"Pipe_{room.Name}", RoomCorner(room, 0.75f), propsRoot));

            }

            // Broken fixtures, straight from the house definition so the balance model
            // and the built scene can never disagree about how much work the house holds.
            foreach (FixtureDefinition fixture in HouseDefinition.Fixtures)
            {
                RoomDefinition room = HouseDefinition.Room(fixture.Room);
                CreateFixture(
                    $"Fixture_{fixture.Label.Replace(" ", string.Empty)}_{fixture.Room.Replace(" ", string.Empty)}",
                    room.Center + fixture.Offset,
                    fixture.Label, fixture.Tool, fixture.Cost, fixture.ValueBonus, propsRoot);
            }

            // A little loose junk to smash and shove around (GDD 9).
            CreateJunk("Junk_OldSofa", HouseDefinition.Room("Living Room").Center + new Vector3(-2f, 0f, -1.5f),
                new Vector3(2f, 0.8f, 0.9f), new Color(0.45f, 0.40f, 0.34f), propsRoot);
            CreateJunk("Junk_OldCabinet", HouseDefinition.Room("Kitchen").Center + new Vector3(2.5f, 0f, 1.5f),
                new Vector3(1.2f, 1.8f, 0.6f), new Color(0.48f, 0.36f, 0.24f), propsRoot);
            CreateJunk("Junk_OldWardrobe", HouseDefinition.Room("Bedroom").Center + new Vector3(-2.5f, 0f, 1.8f),
                new Vector3(1.4f, 2f, 0.7f), new Color(0.42f, 0.32f, 0.24f), propsRoot);
            CreateLooseProp("Prop_Box", HouseDefinition.Room("Hallway").Center + new Vector3(0f, 0.4f, 3f),
                new Vector3(0.6f, 0.6f, 0.6f), new Color(0.72f, 0.58f, 0.36f),
                MassCategory.Light, propsRoot);

            // The fuse box lives in the hallway so the blackout hunt has one obvious target.
            FuseBox fuseBox = CreateFuseBox(new Vector3(1.7f, 1.3f, 6.5f), propsRoot);

            return fuseBox;
        }

        private static Vector3 ScatterPoint(RoomDefinition room, int index, int count)
        {
            // Deterministic spread so a rebuild produces the same house.
            float t = (index + 1f) / (count + 1f);
            float x = Mathf.Lerp(room.MinX + 1.2f, room.MaxX - 1.2f, t);
            float z = Mathf.Lerp(room.MinZ + 1.2f, room.MaxZ - 1.2f, (index % 2 == 0) ? 0.32f : 0.72f);
            return new Vector3(x, 0.02f, z);
        }

        private static Vector3 RoomCorner(RoomDefinition room, float inset)
        {
            return new Vector3(room.MinX + inset, 0.05f, room.MaxZ - inset);
        }

        private static void CreateDirt(string name, Vector3 position, Transform parent)
        {
            GameObject dirt = HouseFlipAssetBuilder.CreateBox(name, new Vector3(0.8f, 0.12f, 0.8f),
                HouseFlipAssetBuilder.GetMaterial("Mat_Dirt", ArtPalette.Dirt), parent);

            dirt.transform.position = position;
            SetLayerRecursive(dirt.transform, GameLayers.Interactable);

            // Trigger collider: the player should aim at dirt, not trip over it.
            var collider = dirt.transform.GetChild(0).GetComponent<Collider>();
            collider.isTrigger = true;

            dirt.AddComponent<NetworkObject>();

            var source = dirt.AddComponent<DirtSource>();
            HouseFlipAssetBuilder.SetPrivateField(source, "maxDirt", 1f);
            HouseFlipAssetBuilder.SetPrivateField(source, "shrinkTarget", dirt.transform.GetChild(0));
        }

        private static void CreateJunk(string name, Vector3 position, Vector3 size, Color color, Transform parent)
        {
            GameObject junk = HouseFlipAssetBuilder.CreateBox(name, size,
                HouseFlipAssetBuilder.GetMaterial($"Mat_{name}", color), parent);

            junk.transform.position = position;
            SetLayerRecursive(junk.transform, GameLayers.Interactable);

            junk.AddComponent<NetworkObject>();

            var destructible = junk.AddComponent<Destructible>();
            HouseFlipAssetBuilder.SetPrivateField(destructible, "maxHitPoints", 3);
            HouseFlipAssetBuilder.SetPrivateField(destructible, "isStructural", false);
            HouseFlipAssetBuilder.SetPrivateField(destructible, "intactVisual", junk.transform.GetChild(0).gameObject);
        }

        private static void CreateFixture(string name, Vector3 position, string label,
            ToolType tool, float cost, float bonus, Transform parent)
        {
            GameObject fixture = HouseFlipAssetBuilder.CreateBox(name, new Vector3(0.45f, 0.45f, 0.45f),
                HouseFlipAssetBuilder.GetMaterial("Mat_Broken", ArtPalette.Broken), parent);

            fixture.transform.position = position;
            SetLayerRecursive(fixture.transform, GameLayers.Interactable);

            fixture.AddComponent<NetworkObject>();

            // Both visual states are the same box; the material swap comes with the art pass.
            GameObject broken = fixture.transform.GetChild(0).gameObject;
            GameObject fixedVisual = Object.Instantiate(broken, fixture.transform);
            fixedVisual.name = "FixedVisual";
            fixedVisual.GetComponent<Renderer>().sharedMaterial =
                HouseFlipAssetBuilder.GetMaterial("Mat_Fixed", ArtPalette.Repaired);
            fixedVisual.SetActive(false);

            var repairable = fixture.AddComponent<RepairableFixture>();
            HouseFlipAssetBuilder.SetPrivateField(repairable, "fixtureName", label);
            HouseFlipAssetBuilder.SetPrivateField(repairable, "requiredTool", (int)tool);
            HouseFlipAssetBuilder.SetPrivateField(repairable, "repairCost", cost);
            HouseFlipAssetBuilder.SetPrivateField(repairable, "houseValueBonus", bonus);
            HouseFlipAssetBuilder.SetPrivateField(repairable, "brokenVisual", broken);
            HouseFlipAssetBuilder.SetPrivateField(repairable, "fixedVisual", fixedVisual);
        }

        private static BurstPipe CreatePipe(string name, Vector3 position, Transform parent)
        {
            GameObject pipe = HouseFlipAssetBuilder.CreateBox(name, new Vector3(0.3f, 0.9f, 0.3f),
                HouseFlipAssetBuilder.GetMaterial("Mat_Pipe", ArtPalette.Pipe), parent);

            pipe.transform.position = position;
            SetLayerRecursive(pipe.transform, GameLayers.Interactable);

            pipe.AddComponent<NetworkObject>();

            // Flood plane, hidden until the pipe actually bursts.
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            water.name = "Water";
            water.transform.SetParent(pipe.transform, false);
            water.transform.localScale = new Vector3(12f, 0.02f, 12f);
            water.transform.localPosition = Vector3.zero;
            water.GetComponent<Renderer>().sharedMaterial =
                HouseFlipAssetBuilder.GetMaterial("Mat_Water", ArtPalette.Water);
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.layer = GameLayers.Water;
            water.SetActive(false);

            var burst = pipe.AddComponent<BurstPipe>();
            HouseFlipAssetBuilder.SetPrivateField(burst, "waterVisual", water.transform);

            return burst;
        }

        private static FuseBox CreateFuseBox(Vector3 position, Transform parent)
        {
            GameObject box = HouseFlipAssetBuilder.CreateBox("FuseBox", new Vector3(0.5f, 0.7f, 0.25f),
                HouseFlipAssetBuilder.GetMaterial("Mat_FuseBox", ArtPalette.FuseBox), parent);

            box.transform.position = position;
            SetLayerRecursive(box.transform, GameLayers.Interactable);

            box.AddComponent<NetworkObject>();

            GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            indicator.name = "Indicator";
            indicator.transform.SetParent(box.transform, false);
            indicator.transform.localScale = Vector3.one * 0.16f;
            indicator.transform.localPosition = new Vector3(0f, 0.78f, -0.18f);
            Object.DestroyImmediate(indicator.GetComponent<Collider>());
            indicator.GetComponent<Renderer>().sharedMaterial =
                HouseFlipAssetBuilder.GetMaterial("Mat_Indicator", new Color(0.35f, 0.9f, 0.4f), emissive: true);

            var fuseBox = box.AddComponent<FuseBox>();
            HouseFlipAssetBuilder.SetPrivateField(fuseBox, "indicatorRenderer", indicator.GetComponent<Renderer>());

            return fuseBox;
        }

        private static void CreateLooseProp(string name, Vector3 position, Vector3 size, Color color,
            MassCategory mass, Transform parent)
        {
            GameObject prop = HouseFlipAssetBuilder.CreateBox(name, size,
                HouseFlipAssetBuilder.GetMaterial($"Mat_{name}", color), parent);

            prop.transform.position = position;
            SetLayerRecursive(prop.transform, GameLayers.Grabbable);

            Object.DestroyImmediate(prop.transform.GetChild(0).GetComponent<Collider>());
            var collider = prop.AddComponent<BoxCollider>();
            collider.size = size;
            collider.center = new Vector3(0f, size.y * 0.5f, 0f);

            var body = prop.AddComponent<Rigidbody>();
            body.mass = mass.RigidbodyMass();
            body.interpolation = RigidbodyInterpolation.Interpolate;

            prop.AddComponent<NetworkObject>();
            prop.AddComponent<NetworkTransform>();
            prop.AddComponent<NetworkRigidbody>();

            var grabbable = prop.AddComponent<Grabbable>();
            HouseFlipAssetBuilder.SetPrivateField(grabbable, "category", (int)mass);
        }

        // ==================================================================
        // Systems, networking, UI
        // ==================================================================

        private static HouseInspectorNPC BuildInspectorNPC()
        {
            GameObject npc = HouseFlipAssetBuilder.CreateBox("HouseInspector", new Vector3(0.6f, 1.8f, 0.4f),
                HouseFlipAssetBuilder.GetMaterial("Mat_Inspector", new Color(0.20f, 0.24f, 0.40f)));

            npc.transform.position = new Vector3(0.5f, 0f, -4f);
            Object.DestroyImmediate(npc.transform.GetChild(0).GetComponent<Collider>());

            npc.AddComponent<NetworkObject>();
            npc.AddComponent<NetworkTransform>();

            var inspector = npc.AddComponent<HouseInspectorNPC>();

            // Hidden until the inspector event sends him in.
            npc.GetComponentInChildren<Renderer>().enabled = false;

            return inspector;
        }

        private static void BuildSystems(PlacementCatalog catalog, List<BurstPipe> pipes,
            FuseBox fuseBox, HouseInspectorNPC inspector)
        {
            var systems = new GameObject("GameSystems");
            systems.AddComponent<NetworkObject>();

            systems.AddComponent<BudgetManager>();
            systems.AddComponent<HouseValueManager>();
            systems.AddComponent<TimerManager>();
            systems.AddComponent<GameManager>();
            systems.AddComponent<LobbyManager>();

            var renovation = systems.AddComponent<RenovationService>();
            HouseFlipAssetBuilder.SetPrivateField(renovation, "catalog", catalog);

            var eventManager = systems.AddComponent<RandomEventManager>();

            // Event 1 — WATER LEAK
            var waterLeak = systems.AddComponent<WaterLeakEvent>();
            HouseFlipAssetBuilder.SetPrivateField(waterLeak, "definition",
                HouseFlipAssetBuilder.BuildEventDefinition("Event_WaterLeak", "WATER LEAK",
                    "💧 A pipe just burst! Find it and fix it with a wrench.", 120f, 1080f));

            var pipeObjects = new List<Object>();
            foreach (BurstPipe pipe in pipes)
            {
                pipeObjects.Add(pipe);
            }

            HouseFlipAssetBuilder.SetPrivateArray(waterLeak, "pipes", pipeObjects);

            // Event 2 — POWER OUTAGE
            var powerOutage = systems.AddComponent<PowerOutageEvent>();
            HouseFlipAssetBuilder.SetPrivateField(powerOutage, "definition",
                HouseFlipAssetBuilder.BuildEventDefinition("Event_PowerOutage", "POWER OUTAGE",
                    "⚡ The lights are out! Find the fuse box and use a screwdriver.", 180f, 1140f));
            HouseFlipAssetBuilder.SetPrivateField(powerOutage, "fuseBox", fuseBox);

            // Event 3 — INSPECTION WARNING
            var inspectionWarning = systems.AddComponent<InspectionWarningEvent>();
            HouseFlipAssetBuilder.SetPrivateField(inspectionWarning, "definition",
                HouseFlipAssetBuilder.BuildEventDefinition("Event_InspectionWarning", "INSPECTION WARNING",
                    "⚠️ INSPECTOR ARRIVING IN 3 MINUTES", 240f, 900f));
            HouseFlipAssetBuilder.SetPrivateField(inspectionWarning, "inspector", inspector);

            HouseFlipAssetBuilder.SetPrivateArray(eventManager, "handlers",
                new List<Object> { waterLeak, powerOutage, inspectionWarning });
        }

        private static void BuildNetworking(GameObject playerPrefab, PlacementCatalog catalog)
        {
            var networkObject = new GameObject("NetworkManager");

            var manager = networkObject.AddComponent<NetworkManager>();
            var transport = networkObject.AddComponent<UnityTransport>();

            manager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = playerPrefab,
                ConnectionApproval = true,
                EnableSceneManagement = true,
                TickRate = 30
            };

            networkObject.AddComponent<NetworkBootstrap>();

            // Spawn points in the yard, so players walk up to the house at the start.
            var spawnRoot = new GameObject("SpawnPoints").transform;
            var spawnPoints = new List<Object>();
            for (int i = 0; i < 4; i++)
            {
                var point = new GameObject($"Spawn_{i}").transform;
                point.SetParent(spawnRoot, false);
                point.position = new Vector3(-3f + i * 2.5f, 0.1f, -4f);
                point.rotation = Quaternion.Euler(0f, 0f, 0f);
                spawnPoints.Add(point);
            }

            var spawnService = networkObject.AddComponent<PlayerSpawnService>();
            HouseFlipAssetBuilder.SetPrivateArray(spawnService, "spawnPoints", spawnPoints);

            // Every prefab the game spawns at runtime has to be registered (GDD 22).
            var registrar = networkObject.AddComponent<NetworkPrefabRegistrar>();
            var runtimePrefabs = new List<Object>();

            foreach (BuildingData building in catalog.Buildings)
            {
                if (building != null && building.prefab != null)
                {
                    runtimePrefabs.Add(building.prefab);
                }
            }

            foreach (FurnitureData furniture in catalog.Furniture)
            {
                if (furniture != null && furniture.prefab != null)
                {
                    runtimePrefabs.Add(furniture.prefab);
                }
            }

            HouseFlipAssetBuilder.SetPrivateArray(registrar, "prefabs", runtimePrefabs);
        }

        private static void BuildUI(PlacementCatalog catalog,
            Dictionary<Core.SfxId, AudioClip> audioClips)
        {
            var camera = new GameObject("CameraRig");
            camera.AddComponent<PlayerCameraRig>();

            var cameraChild = new GameObject("MainCamera");
            cameraChild.transform.SetParent(camera.transform, false);
            cameraChild.tag = "MainCamera";
            var cam = cameraChild.AddComponent<Camera>();
            cam.fieldOfView = 65f;
            cam.nearClipPlane = 0.05f;
            cameraChild.AddComponent<AudioListener>();

            // Shake sits on the camera, not the rig, so it layers on top of the follow
            // logic instead of being overwritten by it every frame (GDD 18 — Polish).
            cameraChild.AddComponent<CameraShake>();

            camera.transform.position = new Vector3(0.5f, 4f, -8f);
            camera.transform.rotation = Quaternion.Euler(14f, 0f, 0f);

            var ui = new GameObject("UI");
            ui.AddComponent<HUDController>();
            ui.AddComponent<ToolbarUI>();
            ui.AddComponent<InteractionPromptUI>();
            ui.AddComponent<EventPopupUI>();
            ui.AddComponent<InspectionScreenUI>();
            ui.AddComponent<AwardsScreenUI>();
            ui.AddComponent<LobbyUI>();

            var catalogUI = ui.AddComponent<CatalogUI>();
            HouseFlipAssetBuilder.SetPrivateField(catalogUI, "catalog", catalog);

            var audio = ui.AddComponent<AudioManager>();
            HouseFlipAudioBuilder.WireInto(audio, audioClips);

            // uGUI needs an EventSystem to route clicks to the lobby buttons.
            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // ==================================================================
        // Player prefab
        // ==================================================================

        private static GameObject BuildPlayerPrefab(PlacementCatalog catalog)
        {
            var root = new GameObject("Player");

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.4f;

            // Slightly exaggerated proportions per GDD 6: big head, short limbs, wide
            // torso. It reads as a cartoon character at a distance, which matters when
            // four of them are crowded into one room.
            Material bodyMaterial = HouseFlipAssetBuilder.GetMaterial("Mat_PlayerBody", Color.white);
            Material skinMaterial = HouseFlipAssetBuilder.GetMaterial("Mat_PlayerSkin", ArtPalette.Skin);
            Material trimMaterial = HouseFlipAssetBuilder.GetMaterial("Mat_PlayerTrim", ArtPalette.Overalls);

            Transform body = AddRigPart(root.transform, "Body", new Vector3(0.62f, 0.72f, 0.42f),
                new Vector3(0f, 0.78f, 0f), bodyMaterial);

            Transform head = AddRigPart(root.transform, "Head", new Vector3(0.52f, 0.48f, 0.5f),
                new Vector3(0f, 1.5f, 0f), skinMaterial);

            // Limb pivots sit at the shoulder and hip, so a rotation swings the limb
            // rather than spinning it about its own middle.
            Transform armLeft = AddLimb(root.transform, "ArmLeft", new Vector3(0.16f, 0.5f, 0.16f),
                new Vector3(-0.4f, 1.28f, 0f), trimMaterial);
            Transform armRight = AddLimb(root.transform, "ArmRight", new Vector3(0.16f, 0.5f, 0.16f),
                new Vector3(0.4f, 1.28f, 0f), trimMaterial);

            Transform legLeft = AddLimb(root.transform, "LegLeft", new Vector3(0.2f, 0.46f, 0.2f),
                new Vector3(-0.16f, 0.46f, 0f), trimMaterial);
            Transform legRight = AddLimb(root.transform, "LegRight", new Vector3(0.2f, 0.46f, 0.2f),
                new Vector3(0.16f, 0.46f, 0f), trimMaterial);

            // A snout so players can tell which way a character faces without an animation
            // to read it from.
            Transform nose = AddRigPart(head, "Facing", new Vector3(0.12f, 0.1f, 0.14f),
                new Vector3(0f, -0.02f, 0.3f), skinMaterial);
            nose.GetComponentInChildren<Renderer>().sharedMaterial =
                HouseFlipAssetBuilder.GetMaterial("Mat_PlayerFace", new Color(0.15f, 0.13f, 0.12f));

            root.AddComponent<NetworkObject>();
            root.AddComponent<ClientNetworkTransform>();

            var playerController = root.AddComponent<PlayerController>();
            root.AddComponent<PlayerCarry>();
            var tools = root.AddComponent<PlayerToolController>();
            root.AddComponent<PlayerStatsTracker>();
            root.AddComponent<Interactor>();

            var animator = root.AddComponent<CharacterAnimator>();
            animator.SetRig(body, head, armLeft, armRight, legLeft, legRight);

            var placement = root.AddComponent<PlacementController>();
            HouseFlipAssetBuilder.SetPrivateField(placement, "catalog", catalog);

            // Only the body and head take the player colour; limbs stay in the shared
            // overalls tone so four players read as a team wearing different shirts.
            HouseFlipAssetBuilder.SetPrivateArray(playerController, "tintedRenderers",
                new List<Object> { body.GetComponentInChildren<Renderer>() });

            BuildToolVisuals(armRight, tools);

            SetLayerRecursive(root.transform, GameLayers.Player);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            Object.DestroyImmediate(root);

            return prefab;
        }

        /// <summary>A chamfered body part, pivoted at its own centre.</summary>
        private static Transform AddRigPart(Transform parent, string name, Vector3 size,
            Vector3 localPosition, Material material)
        {
            GameObject part = HouseFlipAssetBuilder.CreateBox(name, size, material, parent);
            part.transform.localPosition = localPosition;

            // CreateBox puts the visual above the pivot (floor-pivoted, for placement).
            // Body parts want to rotate about their own middle instead.
            Transform visual = part.transform.GetChild(0);
            visual.localPosition = Vector3.zero;

            Object.DestroyImmediate(visual.GetComponent<Collider>());
            return part.transform;
        }

        /// <summary>
        /// A limb whose pivot is at the top, so rotating it swings from the shoulder or
        /// hip. Getting this wrong makes arms rotate around their elbows, which looks
        /// like a puppet with broken strings.
        /// </summary>
        private static Transform AddLimb(Transform parent, string name, Vector3 size,
            Vector3 pivotPosition, Material material)
        {
            var pivot = new GameObject(name);
            pivot.transform.SetParent(parent, false);
            pivot.transform.localPosition = pivotPosition;

            GameObject limb = HouseFlipAssetBuilder.CreateBox($"{name}_Visual", size, material, pivot.transform);

            // Hang the limb below its pivot.
            Transform visual = limb.transform.GetChild(0);
            visual.localPosition = new Vector3(0f, -size.y * 0.5f, 0f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());

            return pivot.transform;
        }

        /// <summary>
        /// Tool models, indexed to match the ToolType enum so index 0 is "no tool".
        /// </summary>
        private static void BuildToolVisuals(Transform armRight, PlayerToolController tools)
        {
            // Parented to the swinging arm, so the hammer follows the swing for free.
            var hand = new GameObject("Hand").transform;
            hand.SetParent(armRight, false);
            hand.localPosition = new Vector3(0f, -0.52f, 0.06f);

            var visuals = new List<Object> { null };

            visuals.Add(CreateToolVisual(hand, "Hammer", new Vector3(0.1f, 0.34f, 0.1f),
                ArtPalette.ToolHandle));
            visuals.Add(CreateToolVisual(hand, "Screwdriver", new Vector3(0.06f, 0.30f, 0.06f),
                ArtPalette.ToolAccent));
            visuals.Add(CreateToolVisual(hand, "Wrench", new Vector3(0.08f, 0.32f, 0.08f),
                ArtPalette.ToolMetal));
            visuals.Add(CreateToolVisual(hand, "Vacuum", new Vector3(0.16f, 0.26f, 0.16f),
                new Color(0.30f, 0.62f, 0.85f)));
            visuals.Add(CreateToolVisual(hand, "PaintRoller", new Vector3(0.10f, 0.36f, 0.10f),
                ArtPalette.ToolMetal));

            HouseFlipAssetBuilder.SetPrivateArray(tools, "toolVisuals", visuals);
        }

        private static GameObject CreateToolVisual(Transform parent, string name, Vector3 size, Color color)
        {
            GameObject tool = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tool.name = $"Tool_{name}";
            tool.transform.SetParent(parent, false);
            tool.transform.localScale = size;
            Object.DestroyImmediate(tool.GetComponent<Collider>());
            tool.GetComponent<Renderer>().sharedMaterial =
                HouseFlipAssetBuilder.GetMaterial($"Mat_Tool{name}", color);
            tool.SetActive(false);
            return tool;
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        private static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayerRecursive(root.GetChild(i), layer);
            }
        }

        private static void RegisterSceneInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            foreach (EditorBuildSettingsScene existing in scenes)
            {
                if (existing.path == ScenePath)
                {
                    return;
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
