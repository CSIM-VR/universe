using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace SpaceBetweenUs.EditorTools
{
    /// <summary>
    /// Menu: Space Between Us > Build Core Prototype Scene.
    /// Generates placeholder materials + prefabs (fragments, island, asteroid) and a fully wired scene
    /// (Assets/_Project/Scenes/Prototype_Core.unity) that plays the whole 6-step loop with grey-box art.
    /// Safe to re-run: it overwrites only the generated "Prototype" assets and that scene.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/Prototype_Core.unity";
        const string MatDir = "Assets/_Project/Materials/Prototype";
        const string PrefabDir = "Assets/_Project/Prefabs/Prototype";

        const float DestroyerScale = 20f;
        static readonly Vector3 DestroyerAnchorPos = new(0f, 0f, -14f);      // step 2: the asteroid field
        static readonly Vector3 DestroyerLateAnchorPos = new(0f, -17f, -10f); // step 4: right next to the islands
        static readonly Vector3 DriftTargetPos = new(0f, 1.25f, 0.45f);       // where fragments gather for the Builder
        // asteroids sit at arm's reach of the scaled-up Destroyer (rig-local position x 20)
        static readonly Vector3 AsteroidLocalLeft = new(-0.25f, 1.35f, 0.55f);
        static readonly Vector3 AsteroidLocalRight = new(0.25f, 1.35f, 0.55f);
        static readonly Vector3 AsteroidLocalCareful = new(0f, 1.35f, 0.55f);
        static readonly Vector3 LakeLocal = new(0.12f, 0f, 0.1f);

        // ------------------------------------------------------------------ entry points

        [MenuItem("Space Between Us/Build Core Prototype Scene", priority = 0)]
        public static void BuildMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
            Debug.Log($"[Prototype] Built {ScenePath}. Press Play (no headset needed: desktop controls are shown in the view).");
        }

        public static void Build()
        {
            EnsureFolder(MatDir);
            EnsureFolder(PrefabDir);
            EnsureFolder(Path.GetDirectoryName(ScenePath)!.Replace('\\', '/'));
            EnsureTagsAndLayers();

            var m = new Mats();
            var rock = MakeFragmentPrefab("Fragment_Rock", MaterialType.Rock, m);
            var ice = MakeFragmentPrefab("Fragment_Ice", MaterialType.Ice, m);
            var crystal = MakeFragmentPrefab("Fragment_Crystal", MaterialType.Crystal, m);
            var islandPrefab = MakeIslandPrefab(m);
            var asteroidPrefab = MakeAsteroidPrefab(m, rock, ice, crystal);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildScene(m, islandPrefab, asteroidPrefab);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ scene

        static void BuildScene(Mats m, GameObject islandPrefab, GameObject asteroidPrefab)
        {
            // space look
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.34f);
            var sun = new GameObject("Starlight").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.3f;
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;
            MakeStars(m);

            // anchors
            var anchors = new GameObject("Anchors").transform;
            var builderAnchor = Empty("Anchor_Builder", anchors, Vector3.zero);
            var destroyerAnchor = Empty("Anchor_Destroyer_Asteroids", anchors, DestroyerAnchorPos);
            var destroyerLateAnchor = Empty("Anchor_Destroyer_NextToIslands", anchors, DestroyerLateAnchorPos);
            var driftTarget = Empty("IslandsDriftTarget", anchors, DriftTargetPos);

            // islands
            var islandsRoot = new GameObject("Islands").transform;
            var islands = new Island[3];
            float[] angles = { -55f, 0f, 55f };
            for (int i = 0; i < 3; i++)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(islandPrefab, islandsRoot);
                go.name = $"Island_{i + 1}";
                float a = angles[i] * Mathf.Deg2Rad;
                go.transform.SetPositionAndRotation(new Vector3(Mathf.Sin(a) * 0.8f, 0.95f, Mathf.Cos(a) * 0.8f),
                    Quaternion.Euler(0f, angles[i] + 180f, 0f));
                islands[i] = go.GetComponent<Island>();
            }

            // asteroids
            var asteroidsRoot = new GameObject("Asteroids").transform;
            Asteroid MakeAsteroid(string name, Transform anchor, Vector3 rigLocal)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(asteroidPrefab, asteroidsRoot);
                go.name = name;
                go.transform.position = anchor.TransformPoint(rigLocal * DestroyerScale);
                go.transform.rotation = Random.rotation;
                var ast = go.GetComponent<Asteroid>();
                ast.islandsDriftTarget = driftTarget;
                return ast;
            }
            var astA = MakeAsteroid("Asteroid_1", destroyerAnchor, AsteroidLocalLeft);
            var astB = MakeAsteroid("Asteroid_2", destroyerAnchor, AsteroidLocalRight);
            var astC = MakeAsteroid("Asteroid_Last_Careful", destroyerLateAnchor, AsteroidLocalCareful);
            astC.carefulMode = true;
            astC.violentTargets = System.Array.ConvertAll(islands, x => x.transform);
            astC.gameObject.SetActive(false);   // appears in step 4

            // XR rig
            var rig = MakeRig(out var head, out var leftAnchor, out var rightAnchor, out var cam);
            rig.transform.SetPositionAndRotation(builderAnchor.position, builderAnchor.rotation);

            var builderL = MakeBuilderHand("BuilderHand_Left", leftAnchor, Hand.Left, m);
            var builderR = MakeBuilderHand("BuilderHand_Right", rightAnchor, Hand.Right, m);
            var destroyerL = MakeDestroyerHand("DestroyerHand_Left", leftAnchor, m);
            var destroyerR = MakeDestroyerHand("DestroyerHand_Right", rightAnchor, m);
            destroyerL.SetActive(false);
            destroyerR.SetActive(false);

            // the artifact rides with the rig so it's reachable in both scales
            var artifactGo = new GameObject("CentralArtifact");
            artifactGo.transform.SetParent(rig.transform, false);
            artifactGo.transform.localPosition = new Vector3(0.3f, 0.85f, 0.3f);
            var artCol = artifactGo.AddComponent<SphereCollider>();
            artCol.isTrigger = true;
            artCol.radius = 0.07f;
            var gem = Prim(PrimitiveType.Cube, "Gem", artifactGo.transform, Vector3.zero, Vector3.one * 0.06f, m.artifact);
            gem.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            var artifact = artifactGo.AddComponent<CentralArtifact>();
            artifact.glowRenderer = gem.GetComponent<Renderer>();

            // systems
            var systems = new GameObject("GameSystems");
            var flow = systems.AddComponent<GameFlowManager>();
            var switcher = systems.AddComponent<PerspectiveSwitcher>();
            artifact.flow = flow;
            flow.switcher = switcher;
            flow.normalAsteroids = 2;
            switcher.xrOrigin = rig.transform;
            switcher.xrCamera = cam;
            switcher.builderAnchor = builderAnchor;
            switcher.destroyerAnchor = destroyerAnchor;
            switcher.destroyerScale = DestroyerScale;
            switcher.builderOnly = new[] { builderL, builderR };
            switcher.destroyerOnly = new[] { destroyerL, destroyerR };
            switcher.fadeOverlay = MakeFade(cam);

            // reveal (step 6)
            var reveal = new GameObject("Reveal").AddComponent<RevealThreads>();
            reveal.islands = System.Array.ConvertAll(islands, x => x.transform);
            reveal.asteroids = new[] { astA.transform, astB.transform, astC.transform };
            reveal.threadMaterial = m.thread;
            reveal.enableOnReveal = new GameObject[0];

            // event wiring
            flow.onStepStarted = new UnityEvent[7];
            for (int i = 0; i < flow.onStepStarted.Length; i++) flow.onStepStarted[i] = new UnityEvent();
            UnityEventTools.AddBoolPersistentListener(flow.onStepStarted[4], astC.gameObject.SetActive, true);
            UnityEventTools.AddObjectPersistentListener(flow.onStepStarted[4], switcher.SetDestroyerAnchor, destroyerLateAnchor);
            flow.onReveal = new UnityEvent();
            UnityEventTools.AddPersistentListener(flow.onReveal, reveal.Play);

            foreach (var ast in new[] { astA, astB, astC })
            {
                UnityEventTools.AddPersistentListener(ast.onOpened, flow.AsteroidOpened);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ast);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ast.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ast.gameObject);
            }
            foreach (var island in islands)
            {
                UnityEventTools.AddPersistentListener(island.onAlive, flow.IslandAlive);
                PrefabUtility.RecordPrefabInstancePropertyModifications(island);
                PrefabUtility.RecordPrefabInstancePropertyModifications(island.transform);
            }

            // desktop simulator + HUD
            var sim = rig.AddComponent<DesktopRigSimulator>();
            sim.head = head;
            sim.leftHand = leftAnchor;
            sim.rightHand = rightAnchor;
            sim.switcher = switcher;
            sim.artifact = artifact;
            MakeHints(head, flow, switcher, sim);
        }

        // ------------------------------------------------------------------ rig

        static GameObject MakeRig(out Transform head, out Transform left, out Transform right, out Camera cam)
        {
            GameObject rig = null;
            foreach (var guid in AssetDatabase.FindAssets("OVRCameraRig t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith("/OVRCameraRig.prefab")) continue;
                rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                break;
            }

            if (rig != null)
            {
                head = FindDeep(rig.transform, "CenterEyeAnchor");
                left = FindDeep(rig.transform, "LeftHandAnchor");
                right = FindDeep(rig.transform, "RightHandAnchor");
                var mgr = rig.GetComponentInChildren<OVRManager>(true);
                if (mgr == null) mgr = rig.AddComponent<OVRManager>();
                mgr.trackingOriginType = OVRManager.TrackingOrigin.FloorLevel;
            }
            else
            {
                Debug.LogWarning("[Prototype] OVRCameraRig prefab not found - building a plain rig (desktop only).");
                rig = new GameObject("OVRCameraRig");
                var space = Empty("TrackingSpace", rig.transform, Vector3.zero);
                head = Empty("CenterEyeAnchor", space, new Vector3(0f, 1.6f, 0f));
                head.gameObject.AddComponent<Camera>();
                head.gameObject.AddComponent<AudioListener>();
                left = Empty("LeftHandAnchor", space, Vector3.zero);
                right = Empty("RightHandAnchor", space, Vector3.zero);
            }

            rig.name = "XR Rig (OVRCameraRig)";
            cam = head.GetComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.015f, 0.04f);
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 3000f;
            return rig;
        }

        static GameObject MakeBuilderHand(string name, Transform anchor, Hand hand, Mats m)
        {
            var go = HandBase(name, anchor, "PlayerHand", 0.04f);
            Prim(PrimitiveType.Sphere, "Visual", go.transform, Vector3.zero, Vector3.one * 0.06f, m.builderHand);
            var beam = MakeParticles("AttractGlow", go.transform, Vector3.zero, m.goldParticle, 0.12f, 0.008f, 40f, 0.5f, false);
            var attractor = go.AddComponent<FragmentAttractor>();
            attractor.hand = hand;
            attractor.radius = 1.2f;
            attractor.beamEffect = beam;
            go.AddComponent<FragmentGrabber>().hand = hand;
            return go;
        }

        static GameObject MakeDestroyerHand(string name, Transform anchor, Mats m)
        {
            var go = HandBase(name, anchor, "DestroyerHand", 0.06f);
            Prim(PrimitiveType.Sphere, "Visual", go.transform, Vector3.zero, Vector3.one * 0.11f, m.destroyerHand);
            return go;
        }

        static GameObject HandBase(string name, Transform anchor, string tag, float radius)
        {
            var go = new GameObject(name) { tag = tag };
            go.transform.SetParent(anchor, false);
            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = radius;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            go.AddComponent<HandVelocityTracker>();
            return go;
        }

        static CanvasGroup MakeFade(Camera cam)
        {
            var go = new GameObject("FadeCanvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.04f;
            canvas.sortingOrder = 100;
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var img = new GameObject("Black").AddComponent<Image>();
            img.transform.SetParent(go.transform, false);
            img.color = Color.black;
            img.raycastTarget = false;
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return group;
        }

        static void MakeHints(Transform head, GameFlowManager flow, PerspectiveSwitcher switcher, DesktopRigSimulator sim)
        {
            var go = new GameObject("HintCanvas");
            go.transform.SetParent(head, false);
            go.transform.localPosition = new Vector3(0f, -0.16f, 0.8f);
            go.transform.localScale = Vector3.one * 0.001f;   // 1 unit = 1 mm
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            ((RectTransform)go.transform).sizeDelta = new Vector2(900f, 320f);

            var text = new GameObject("Hint").AddComponent<Text>();
            text.transform.SetParent(go.transform, false);
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.alignment = TextAnchor.LowerCenter;
            text.color = new Color(1f, 1f, 1f, 0.9f);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var rt = text.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var hints = text.gameObject.AddComponent<FlowHints>();
            hints.flow = flow;
            hints.switcher = switcher;
            hints.simulator = sim;
        }

        static void MakeStars(Mats m)
        {
            var go = new GameObject("Stars");
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = 1f;
            main.startLifetime = 1e6f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 2.2f);
            main.maxParticles = 2500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2500) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 600f;
            shape.radiusThickness = 0f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = m.starParticle;
        }

        // ------------------------------------------------------------------ prefabs

        static MaterialFragment MakeFragmentPrefab(string name, MaterialType type, Mats m)
        {
            GameObject go;
            switch (type)
            {
                case MaterialType.Rock:   // rough, heavy lump
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.transform.localScale = new Vector3(0.1f, 0.08f, 0.09f);
                    go.GetComponent<Renderer>().sharedMaterial = m.rock;
                    Prim(PrimitiveType.Sphere, "Lump", go.transform, new Vector3(0.3f, 0.2f, 0.1f), Vector3.one * 0.6f, m.rock);
                    Prim(PrimitiveType.Sphere, "Lump2", go.transform, new Vector3(-0.25f, -0.2f, 0.2f), Vector3.one * 0.5f, m.rock);
                    break;
                case MaterialType.Ice:    // frosted translucent block
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.localScale = new Vector3(0.08f, 0.08f, 0.08f);
                    go.GetComponent<Renderer>().sharedMaterial = m.ice;
                    break;
                default:                  // long glowing prism
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.transform.localScale = new Vector3(0.035f, 0.065f, 0.035f);
                    go.GetComponent<Renderer>().sharedMaterial = m.crystal;
                    var shard = Prim(PrimitiveType.Capsule, "Shard", go.transform, new Vector3(0.5f, -0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), m.crystal);
                    shard.transform.localRotation = Quaternion.Euler(0f, 0f, -30f);
                    break;
            }
            go.name = name;
            var rb = go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = 0.2f;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            go.AddComponent<MaterialFragment>().type = type;
            return SavePrefab(go, name).GetComponent<MaterialFragment>();
        }

        static GameObject MakeIslandPrefab(Mats m)
        {
            var root = new GameObject("Island");
            var island = root.AddComponent<Island>();
            var life = root.AddComponent<LifeGrowth>();
            island.life = life;
            island.waterLifeDelay = 3f;

            // body: flat top at y = 0, rocky underside
            var body = Prim(PrimitiveType.Cylinder, "Body", root.transform, new Vector3(0f, -0.06f, 0f), new Vector3(0.7f, 0.06f, 0.7f), m.islandTop);
            var mc = body.AddComponent<MeshCollider>();
            mc.sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = true;
            Prim(PrimitiveType.Sphere, "Underside", root.transform, new Vector3(0f, -0.15f, 0f), new Vector3(0.62f, 0.32f, 0.62f), m.islandRock);
            Prim(PrimitiveType.Sphere, "Underside2", root.transform, new Vector3(0.05f, -0.3f, -0.03f), new Vector3(0.3f, 0.28f, 0.3f), m.islandRock);
            Prim(PrimitiveType.Cylinder, "LakeBed", root.transform, LakeLocal + new Vector3(0f, 0.002f, 0f), new Vector3(0.25f, 0.002f, 0.25f), m.lakeBed);

            // --- slots
            var ground = Slot("Slot_Ground_Rock", root.transform, new Vector3(-0.06f, 0.1f, -0.04f), new Vector3(0.42f, 0.22f, 0.42f),
                MaterialType.Rock, IslandSlot.Mode.Place);
            ground.glowWhileEmpty = Prim(PrimitiveType.Cylinder, "Glow", ground.transform, new Vector3(0f, -0.095f, 0f), new Vector3(0.24f, 0.003f, 0.24f), m.goldGlow);

            var water = Slot("Slot_Lake_Ice", root.transform, LakeLocal + new Vector3(0f, 0.12f, 0f), new Vector3(0.26f, 0.26f, 0.26f),
                MaterialType.Ice, IslandSlot.Mode.Pour);
            water.pourTime = 2.5f;
            water.glowWhileEmpty = Prim(PrimitiveType.Cylinder, "Glow", water.transform, new Vector3(0f, -0.114f, 0f), new Vector3(0.2f, 0.003f, 0.2f), m.iceGlow);
            water.pourEffect = MakeParticles("PourDrops", water.transform, Vector3.zero, m.waterParticle, 0.01f, 0.012f, 40f, 0.5f, false, gravity: 0.6f);
            var waterSurface = Prim(PrimitiveType.Cylinder, "Water", root.transform, LakeLocal + new Vector3(0f, 0.012f, 0f), new Vector3(0.24f, 0.01f, 0.24f), m.water);
            var waterScaler = waterSurface.AddComponent<ProgressScaler>();
            UnityEventTools.AddPersistentListener(water.onProgress, waterScaler.SetProgress);

            var lotus = Slot("Slot_Lotus_Crystal", root.transform, LakeLocal + new Vector3(0f, 0.1f, 0f), new Vector3(0.14f, 0.2f, 0.14f),
                MaterialType.Crystal, IslandSlot.Mode.HoldStill);
            lotus.holdTime = 2f;
            lotus.glowWhileEmpty = Prim(PrimitiveType.Sphere, "Glow", lotus.transform, new Vector3(0f, -0.07f, 0f), Vector3.one * 0.035f, m.crystalGlow);
            var ring = Prim(PrimitiveType.Cylinder, "HoldRing", lotus.transform, new Vector3(0f, -0.075f, 0f), new Vector3(0.15f, 0.002f, 0.15f), m.crystalGlow);
            var ringScaler = ring.AddComponent<ProgressScaler>();
            UnityEventTools.AddPersistentListener(lotus.onProgress, ringScaler.SetProgress);

            island.groundSlot = ground;
            island.waterSlot = water;
            island.lifeSlot = lotus;

            // --- life stages (objects start hidden, LifeGrowth scales them up)
            var rnd = new System.Random(7);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            Vector3 OnGround()
            {
                for (int tries = 0; tries < 30; tries++)
                {
                    var p = new Vector3(R(-0.28f, 0.28f), 0f, R(-0.28f, 0.28f));
                    if (p.magnitude < 0.3f && (p - LakeLocal).magnitude > 0.17f) return p;
                }
                return new Vector3(-0.15f, 0f, -0.15f);
            }

            var stages = new GameObject("Life").transform;
            stages.SetParent(root.transform, false);

            // Moss: soil + moss cushions
            var mossList = new System.Collections.Generic.List<Transform>
            {
                Prim(PrimitiveType.Cylinder, "Soil", stages, new Vector3(0f, 0.005f, 0f), new Vector3(0.66f, 0.004f, 0.66f), m.soil).transform
            };
            for (int i = 0; i < 8; i++)
                mossList.Add(Prim(PrimitiveType.Sphere, $"Moss_{i}", stages, OnGround() + new Vector3(0f, 0.01f, 0f),
                    new Vector3(R(0.06f, 0.11f), 0.025f, R(0.06f, 0.11f)), m.moss).transform);

            // Plants: reeds around the lake + bushes
            var plantList = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                plantList.Add(Prim(PrimitiveType.Cylinder, $"Reed_{i}", stages,
                    LakeLocal + new Vector3(Mathf.Cos(a) * 0.135f, 0.04f, Mathf.Sin(a) * 0.135f), new Vector3(0.008f, 0.04f, 0.008f), m.grass).transform);
            }
            Transform bush0 = null;
            for (int i = 0; i < 3; i++)
            {
                var b = Prim(PrimitiveType.Sphere, $"Bush_{i}", stages, OnGround() + new Vector3(0f, 0.03f, 0f), Vector3.one * R(0.05f, 0.08f), m.grass).transform;
                bush0 ??= b;
                plantList.Add(b);
            }

            // WaterLife: fish + frog
            var waterLife = new System.Collections.Generic.List<Transform>();
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                var fish = Prim(PrimitiveType.Capsule, $"Fish_{i}", stages, LakeLocal + new Vector3(Mathf.Cos(a) * 0.07f, 0.022f, Mathf.Sin(a) * 0.07f),
                    new Vector3(0.01f, 0.018f, 0.01f), m.fish);
                fish.transform.localRotation = Quaternion.Euler(90f, a * Mathf.Rad2Deg, 0f);
                waterLife.Add(fish.transform);
            }
            waterLife.Add(Prim(PrimitiveType.Sphere, "Frog", stages, LakeLocal + new Vector3(-0.12f, 0.015f, -0.06f), new Vector3(0.025f, 0.018f, 0.025f), m.moss).transform);

            // Lotus: flower in the lake + a tree + fireflies
            var pad = Prim(PrimitiveType.Cylinder, "LotusPad", stages, LakeLocal + new Vector3(0f, 0.024f, 0f), new Vector3(0.07f, 0.002f, 0.07f), m.moss).transform;
            var flower = Prim(PrimitiveType.Sphere, "LotusFlower", stages, LakeLocal + new Vector3(0f, 0.035f, 0f), new Vector3(0.05f, 0.03f, 0.05f), m.lotus).transform;
            var tree = new GameObject("Tree").transform;
            tree.SetParent(stages, false);
            tree.localPosition = new Vector3(-0.16f, 0f, -0.12f);
            Prim(PrimitiveType.Cylinder, "Trunk", tree, new Vector3(0f, 0.08f, 0f), new Vector3(0.025f, 0.08f, 0.025f), m.trunk);
            Prim(PrimitiveType.Sphere, "Canopy", tree, new Vector3(0f, 0.2f, 0f), new Vector3(0.17f, 0.13f, 0.17f), m.grass);
            var fireflies = MakeParticles("Fireflies", stages, new Vector3(-0.05f, 0.15f, 0f), m.goldParticle, 0.25f, 0.008f, 10f, 3f, false);

            foreach (var t in mossList) t.gameObject.SetActive(false);
            foreach (var t in plantList) t.gameObject.SetActive(false);
            foreach (var t in waterLife) t.gameObject.SetActive(false);
            pad.gameObject.SetActive(false);
            flower.gameObject.SetActive(false);
            tree.gameObject.SetActive(false);

            life.stages = new[]
            {
                new LifeGrowth.Stage { name = "Moss", growObjects = mossList.ToArray(), effects = new ParticleSystem[0], delayBetween = 0.12f, growDuration = 1.2f },
                new LifeGrowth.Stage { name = "Plants", growObjects = plantList.ToArray(), effects = new ParticleSystem[0], delayBetween = 0.15f, growDuration = 1.2f },
                new LifeGrowth.Stage { name = "WaterLife", growObjects = waterLife.ToArray(), effects = new ParticleSystem[0], delayBetween = 0.3f, growDuration = 0.8f },
                new LifeGrowth.Stage { name = "Lotus", growObjects = new[] { pad, flower, tree }, effects = new[] { fireflies }, delayBetween = 0.4f, growDuration = 1.8f },
            };

            island.breakableParts = new[] { tree.gameObject, flower.gameObject, bush0.gameObject };
            island.damageEffect = MakeParticles("DamageBurst", root.transform, Vector3.zero, m.dustParticle, 0.05f, 0.02f, 0f, 0.8f, false, burst: 25);
            island.onAlive = new UnityEvent();

            return SavePrefab(root, "Island");
        }

        static IslandSlot Slot(string name, Transform parent, Vector3 localPos, Vector3 size, MaterialType accepts, IslandSlot.Mode mode)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = size;
            var slot = go.AddComponent<IslandSlot>();
            slot.accepts = accepts;
            slot.mode = mode;
            slot.onProgress = new UnityEvent<float>();
            slot.onFilled = new UnityEvent();
            return slot;
        }

        static GameObject MakeAsteroidPrefab(Mats m, MaterialFragment rock, MaterialFragment ice, MaterialFragment crystal)
        {
            var root = new GameObject("Asteroid");
            root.transform.localScale = Vector3.one * 4.8f;   // ~fist-sized for the x20 Destroyer
            var col = root.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.5f;
            var ast = root.AddComponent<Asteroid>();
            ast.rockPrefab = rock;
            ast.icePrefab = ice;
            ast.crystalPrefab = crystal;
            ast.fragmentsPerLayer = 3;
            ast.spawnSpread = 0.8f;
            ast.onOpened = new UnityEvent();
            ast.onViolentBreak = new UnityEvent();
            ast.onTap = new UnityEvent<int>();

            var rnd = new System.Random(3);
            Vector3 Dir() => new Vector3((float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f, (float)rnd.NextDouble() - 0.5f).normalized;

            var shell = Group("Shell", root.transform);
            Prim(PrimitiveType.Sphere, "Rock", shell, Vector3.zero, Vector3.one, m.asteroidShell);
            for (int i = 0; i < 5; i++) Prim(PrimitiveType.Sphere, $"Lump_{i}", shell, Dir() * 0.4f, Vector3.one * 0.42f, m.asteroidShell);
            int xray = LayerMask.NameToLayer("XRay");
            for (int i = 0; i < 4; i++)
            {
                var vein = Prim(PrimitiveType.Capsule, $"Vein_{i}", shell, Dir() * 0.47f, new Vector3(0.03f, 0.3f, 0.03f), m.vein);
                vein.transform.localRotation = Quaternion.LookRotation(Dir()) * Quaternion.Euler(90f, 0f, 0f);
                if (xray >= 0) vein.layer = xray;   // only the Destroyer sees the veins
            }

            var inside = Group("Inside", root.transform);
            Prim(PrimitiveType.Sphere, "Ice", inside, Vector3.zero, Vector3.one * 0.78f, m.asteroidInside);
            for (int i = 0; i < 5; i++) Prim(PrimitiveType.Cube, $"ShellBit_{i}", inside, Dir() * 0.42f, Vector3.one * 0.16f, m.asteroidShell).transform.localRotation = Random.rotation;

            var core = Group("Core", root.transform);
            Prim(PrimitiveType.Capsule, "Crystal", core, Vector3.zero, new Vector3(0.22f, 0.3f, 0.22f), m.crystal);
            for (int i = 0; i < 4; i++) Prim(PrimitiveType.Cube, $"IceBit_{i}", core, Dir() * 0.3f, Vector3.one * 0.13f, m.asteroidInside).transform.localRotation = Random.rotation;

            var broken = Group("Broken", root.transform);
            for (int i = 0; i < 7; i++)
                Prim(PrimitiveType.Sphere, $"Chunk_{i}", broken, Dir() * (0.5f + (float)rnd.NextDouble() * 0.3f), Vector3.one * (0.1f + (float)rnd.NextDouble() * 0.1f), m.asteroidBroken);

            ast.shellVisual = shell.gameObject;
            ast.insideVisual = inside.gameObject;
            ast.coreVisual = core.gameObject;
            ast.brokenVisual = broken.gameObject;
            inside.gameObject.SetActive(false);
            core.gameObject.SetActive(false);
            broken.gameObject.SetActive(false);

            return SavePrefab(root, "Asteroid");
        }

        // ------------------------------------------------------------------ materials

        class Mats
        {
            public readonly Material rock, ice, crystal, islandTop, islandRock, lakeBed, soil, moss, grass, trunk, fish, lotus, water;
            public readonly Material goldGlow, iceGlow, crystalGlow, builderHand, destroyerHand, artifact;
            public readonly Material asteroidShell, asteroidInside, asteroidBroken, vein;
            public readonly Material starParticle, goldParticle, waterParticle, dustParticle, thread;

            public Mats()
            {
                rock = Lit("Rock", new Color(0.55f, 0.42f, 0.28f), smooth: 0.05f);
                ice = Lit("Ice", new Color(0.7f, 0.9f, 1f, 0.6f), new Color(0.1f, 0.25f, 0.35f), 0.9f, transparent: true);
                crystal = Lit("Crystal", new Color(0.7f, 0.45f, 1f), new Color(0.8f, 0.35f, 1.6f), 0.9f);
                islandTop = Lit("IslandTop", new Color(0.42f, 0.4f, 0.38f), smooth: 0.1f);
                islandRock = Lit("IslandRock", new Color(0.3f, 0.28f, 0.3f), smooth: 0.1f);
                lakeBed = Lit("LakeBed", new Color(0.2f, 0.2f, 0.24f), smooth: 0.2f);
                soil = Lit("Soil", new Color(0.36f, 0.26f, 0.16f), smooth: 0.05f);
                moss = Lit("Moss", new Color(0.35f, 0.65f, 0.25f), smooth: 0.1f);
                grass = Lit("Grass", new Color(0.25f, 0.55f, 0.2f), smooth: 0.15f);
                trunk = Lit("Trunk", new Color(0.4f, 0.27f, 0.15f), smooth: 0.1f);
                fish = Lit("Fish", new Color(1f, 0.5f, 0.15f), new Color(0.4f, 0.15f, 0f));
                lotus = Lit("Lotus", new Color(1f, 0.6f, 0.85f), new Color(1f, 0.4f, 0.8f));
                water = Lit("Water", new Color(0.2f, 0.55f, 0.9f, 0.75f), new Color(0.02f, 0.1f, 0.2f), 0.95f, transparent: true);
                goldGlow = Lit("GoldGlow", new Color(1f, 0.8f, 0.3f), new Color(1.2f, 0.8f, 0.2f));
                iceGlow = Lit("IceGlow", new Color(0.5f, 0.85f, 1f), new Color(0.3f, 0.8f, 1.4f));
                crystalGlow = Lit("CrystalGlow", new Color(0.75f, 0.5f, 1f), new Color(0.9f, 0.4f, 1.8f));
                builderHand = Lit("BuilderHand", new Color(1f, 0.8f, 0.35f), new Color(0.8f, 0.55f, 0.1f));
                destroyerHand = Lit("DestroyerHand", new Color(0.3f, 0.55f, 1f), new Color(0.2f, 0.45f, 1.5f));
                artifact = Lit("Artifact", new Color(1f, 0.9f, 0.6f), new Color(1f, 0.85f, 0.4f), 0.9f);
                asteroidShell = Lit("AsteroidShell", new Color(0.3f, 0.27f, 0.25f), smooth: 0.05f);
                asteroidInside = Lit("AsteroidInside", new Color(0.55f, 0.8f, 0.95f), new Color(0.05f, 0.2f, 0.3f), 0.8f);
                asteroidBroken = Lit("AsteroidBroken", new Color(0.18f, 0.16f, 0.15f), smooth: 0.05f);
                vein = Lit("XRayVein", new Color(0.8f, 0.4f, 1f), new Color(1.5f, 0.6f, 2.5f));
                starParticle = Particle("Stars", new Color(0.9f, 0.92f, 1f));
                goldParticle = Particle("GoldSpark", new Color(1f, 0.85f, 0.35f));
                waterParticle = Particle("WaterDrop", new Color(0.5f, 0.8f, 1f));
                dustParticle = Particle("Dust", new Color(0.5f, 0.4f, 0.3f));
                thread = Particle("LightThread", new Color(1f, 0.9f, 0.6f));
            }

            static Material Lit(string name, Color baseColor, Color? emission = null, float smooth = 0.4f, bool transparent = false)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = GetOrCreate(name, shader);
                mat.SetColor("_BaseColor", baseColor);
                mat.SetColor("_Color", baseColor);
                mat.SetFloat("_Smoothness", smooth);
                if (emission.HasValue)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission.Value);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                if (transparent)
                {
                    mat.SetFloat("_Surface", 1f);
                    mat.SetFloat("_Blend", 0f);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                    mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0f);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)RenderQueue.Transparent;
                }
                EditorUtility.SetDirty(mat);
                return mat;
            }

            static Material Particle(string name, Color color)
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                var mat = GetOrCreate("P_" + name, shader);
                mat.SetColor("_BaseColor", color);
                mat.SetColor("_Color", color);
                EditorUtility.SetDirty(mat);
                return mat;
            }

            static Material GetOrCreate(string name, Shader shader)
            {
                string path = $"{MatDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                else if (mat.shader != shader) mat.shader = shader;
                return mat;
            }
        }

        // ------------------------------------------------------------------ helpers

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            // small details (glows, rings, life bits) don't need shadows; big shapes do, for depth
            bool big = localScale.x >= 0.1f || localScale.y >= 0.1f || parent == null || parent.lossyScale.x > 2f;
            r.shadowCastingMode = big ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static Transform Empty(string name, Transform parent, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = pos;
            return t;
        }

        static ParticleSystem MakeParticles(string name, Transform parent, Vector3 localPos, Material mat, float radius, float size,
            float rate, float lifetime, bool playOnAwake, float gravity = 0f, int burst = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = playOnAwake;
            main.loop = burst == 0;
            main.startLifetime = lifetime;
            main.startSpeed = burst > 0 ? 0.4f : 0.02f;
            main.startSize = size;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            if (burst > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burst) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius;
            var noise = ps.noise;
            noise.enabled = burst == 0;
            noise.strength = 0.05f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat;
            return ps;
        }

        static GameObject SavePrefab(GameObject go, string name)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root)
            {
                var r = FindDeep(c, name);
                if (r) return r;
            }
            return null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void EnsureTagsAndLayers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tags = tm.FindProperty("tags");
            foreach (var tag in new[] { "DestroyerHand", "PlayerHand" })
            {
                bool found = false;
                for (int i = 0; i < tags.arraySize; i++) if (tags.GetArrayElementAtIndex(i).stringValue == tag) found = true;
                if (found) continue;
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
            }
            var layers = tm.FindProperty("layers");
            bool hasXray = false;
            for (int i = 0; i < layers.arraySize; i++) if (layers.GetArrayElementAtIndex(i).stringValue == "XRay") hasXray = true;
            if (!hasXray)
                for (int i = 8; i < layers.arraySize; i++)
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    {
                        layers.GetArrayElementAtIndex(i).stringValue = "XRay";
                        break;
                    }
            tm.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddToBuildSettings()
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
