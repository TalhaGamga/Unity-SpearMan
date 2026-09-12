using System.Collections.Generic;
using System.Linq;
using Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MovementEditor
{
    /// <summary>
    /// Builds the traversal sandbox straight out of <see cref="MovementDesignSO"/>.
    ///
    /// The geometry is not hand-placed: every ledge and every gap is sized from
    /// the design asset's own gate table, so the room is always an honest
    /// statement of what the current numbers can and cannot reach. Retune the
    /// asset, rebuild, and the level re-states the new envelope.
    ///
    /// Nothing here runs at play time. The sandbox measures by construction -
    /// a ledge you cannot mount is a ledge that is too tall, no readout needed.
    /// </summary>
    public static class MovementSandboxBuilder
    {
        private const string DesignAssetPath =
            "Assets/GAME/Data/Movement/MovementDesign_Hero.asset";

        private const string PlatformPrefabPath =
            "Assets/GAME/Prefabs/Sandbox/SandboxPlatform.prefab";

        private const string SourceScenePath =
            "Assets/Scenes/EnemyTest_Sword.unity";

        private const string SandboxScenePath =
            "Assets/Scenes/MovementSandbox.unity";

        private const string RootName = "MovementSandbox";

        /// <summary>Lane width in X. The player is X-frozen, so this is purely visual.</summary>
        private const float LaneWidth = 4f;

        /// <summary>How far a pillar extends below its own top face.</summary>
        private const float PillarDepthTiles = 2f;

        private static readonly string[] StrippedRootNames =
        {
            "Level3D",
            "Environment"
        };

        #region Menu

        [MenuItem("Tools/Movement/Sandbox/Rebuild Geometry In Active Scene", priority = 0)]
        public static void RebuildInActiveScene()
        {
            if (!TryLoadDependencies(out var design, out var platform))
                return;

            Scene scene = SceneManager.GetActiveScene();
            GameObject root = Build(scene, design, platform);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                $"Movement sandbox rebuilt in '{scene.name}' from " +
                $"{design.name}.\n{Summary(design)}",
                root);
        }

        [MenuItem("Tools/Movement/Sandbox/Create Sandbox Scene", priority = 1)]
        public static void CreateSandboxScene()
        {
            if (!TryLoadDependencies(out var design, out var platform))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            if (!System.IO.File.Exists(SourceScenePath))
            {
                Debug.LogError(
                    $"Movement sandbox: source scene not found at " +
                    $"'{SourceScenePath}'. It supplies the camera, UI and " +
                    "manager rig. Point SourceScenePath at a working scene.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(
                SourceScenePath,
                OpenSceneMode.Single);

            int stripped = StripExistingLevel(scene);
            GameObject root = Build(scene, design, platform);
            PlacePlayerAtSpawn(scene, design);

            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene, SandboxScenePath))
            {
                Debug.LogError(
                    $"Movement sandbox: could not save to '{SandboxScenePath}'.");
                return;
            }

            AssetDatabase.Refresh();
            Selection.activeGameObject = root;

            Debug.Log(
                $"Movement sandbox scene written to '{SandboxScenePath}'.\n" +
                $"Stripped {stripped} object(s) of old level geometry.\n" +
                Summary(design),
                root);
        }

        #endregion

        #region Build

        private static GameObject Build(
            Scene scene,
            MovementDesignSO design,
            GameObject platform)
        {
            DestroyExistingRoot(scene);

            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = Vector3.zero;

            // Retrying a tune should cost one key press, not a play-mode cycle.
            root.AddComponent<SandboxRestarter>();

            var ctx = new BuildContext(root.transform, platform, design);

            // Sections run head to tail along +Z, because X is frozen on the
            // player: the sandbox has to be one continuous lane, not a grid.
            // The run-up, the ruler and the ledge ladder all sit on the same
            // floor, so they get ONE slab rather than a row of butted boxes.
            // Every seam between two colliders is a place a capsule can catch,
            // and a sandbox that snags is measuring the geometry, not the
            // traversal numbers.
            float z = 0f;
            z = BuildApexRuler(ctx, z);
            z = BuildLedgeLadder(ctx, z);

            ctx.Section("A - Ground");
            ctx.Platform("Ground_Continuous", 0f, z, topTiles: 0f);

            // Start the gap ladder past a real gap rather than butted against
            // the slab, so the two sections never share a collider edge.
            z = BuildGapLadder(ctx, z + 2f);
            BuildDropTower(ctx, z);

            return root;
        }

        /// <summary>Flat run-up. Long enough to reach full ground speed before the first test.</summary>
        /// <summary>
        /// A vertical ruler beside the lane, one marker per tile, with the two
        /// apex heights called out. Stand next to it and jump: where the head
        /// stops is the number the design asset promised.
        /// </summary>
        private static float BuildApexRuler(BuildContext ctx, float zTiles)
        {
            // Long enough to reach full ground speed before the first test.
            const float LengthTiles = 22f;
            const int TopMarkerTiles = 9;

            ctx.Section("B - Apex Ruler");

            float rulerZ = zTiles + LengthTiles * 0.6f;

            for (int t = 1; t <= TopMarkerTiles; t++)
            {
                ctx.Marker(
                    $"Tick_{t}T",
                    rulerZ,
                    t,
                    widthTiles: 0.6f,
                    lengthTiles: LengthTiles * 0.8f);
            }

            ctx.Marker(
                $"APEX_SingleJump_{ctx.Design.JumpHeight / ctx.Design.TileUnit:0.00}T",
                rulerZ,
                ctx.Design.JumpHeight / ctx.Design.TileUnit,
                widthTiles: 1.6f,
                lengthTiles: LengthTiles * 0.9f);

            ctx.Marker(
                $"APEX_DoubleJump_{ctx.Design.DoubleJumpHeight / ctx.Design.TileUnit:0.00}T",
                rulerZ,
                ctx.Design.DoubleJumpHeight / ctx.Design.TileUnit,
                widthTiles: 1.6f,
                lengthTiles: LengthTiles * 0.9f);

            return zTiles + LengthTiles;
        }

        /// <summary>
        /// Pillars of rising height, each approached from flat ground. The
        /// first one you cannot mount is the single-jump ledge gate; the first
        /// one you cannot mount with the air jump is the double-jump gate.
        /// </summary>
        private static float BuildLedgeLadder(BuildContext ctx, float zTiles)
        {
            const float ApproachTiles = 5f;
            const float PillarTiles = 3f;

            int tallest = ctx.Design.DoubleJumpLedgeTiles + 2;
            float z = zTiles;

            ctx.Section("C - Ledge Ladder");

            for (int height = 1; height <= tallest; height++)
            {
                // No approach pad is emitted: the continuous ground slab runs
                // underneath the whole section. Each pillar sits ON that slab,
                // so the only edge in the lane is the step the test is about.
                z += ApproachTiles;

                ctx.Platform(
                    $"Ledge_{height}T{GateTag(ctx, height, isLedge: true)}",
                    z,
                    PillarTiles,
                    topTiles: height,
                    depthTiles: height);
                z += PillarTiles;
            }

            return z + ApproachTiles;
        }

        /// <summary>
        /// Flat pads separated by widening gaps. The first gap you cannot clear
        /// is the gate for whichever traversal move you are testing with.
        /// </summary>
        private static float BuildGapLadder(BuildContext ctx, float zTiles)
        {
            const float PadTiles = 7f;

            int widest = ctx.Design.DashJumpGapTiles + 2;
            float z = zTiles;

            ctx.Section("D - Gap Ladder");

            for (int gap = 2; gap <= widest; gap++)
            {
                ctx.Platform(
                    $"Gap_Pad_before_{gap}T",
                    z,
                    PadTiles,
                    topTiles: 0f);
                z += PadTiles;

                // The gap itself is the absence of geometry. Name the marker
                // that follows it so the hierarchy still reads as a ruler.
                z += gap;
            }

            ctx.Platform("Gap_RunOut", z, PadTiles, topTiles: 0f);
            return z + PadTiles;
        }

        /// <summary>
        /// A staircase of single-jump-legal steps up to a height well past the
        /// hard-land threshold, then nothing. Tests terminal velocity and the
        /// landing read on a genuinely long drop.
        /// </summary>
        private static float BuildDropTower(BuildContext ctx, float zTiles)
        {
            const float StepTiles = 4f;
            const float TowerTopTiles = 6f;

            int stepHeight = Mathf.Max(1, ctx.Design.SingleJumpLedgeTiles);
            int steps = Mathf.CeilToInt(
                (ctx.Design.HardLandDistance / ctx.Design.TileUnit + 6f) /
                stepHeight);

            float z = zTiles;
            int top = 0;

            ctx.Section("E - Drop Tower");

            for (int i = 0; i < steps; i++)
            {
                top += stepHeight;
                ctx.Platform(
                    $"Tower_Step_{top}T",
                    z,
                    StepTiles,
                    topTiles: top,
                    depthTiles: top + PillarDepthTiles);
                z += StepTiles;
            }

            ctx.Platform(
                $"Tower_Ledge_{top}T",
                z,
                TowerTopTiles,
                topTiles: top,
                depthTiles: top + PillarDepthTiles);
            z += TowerTopTiles;

            ctx.Platform(
                $"Tower_LandingPad_drop_{top}T" +
                $"_hardLandAt_{ctx.Design.HardLandDistance / ctx.Design.TileUnit:0.0}T",
                z,
                StepTiles * 4f,
                topTiles: 0f);

            return z + StepTiles * 4f;
        }

        private static string GateTag(BuildContext ctx, int height, bool isLedge)
        {
            if (!isLedge)
                return string.Empty;

            if (height == ctx.Design.SingleJumpLedgeTiles)
                return "_GATE_singleJump";

            if (height == ctx.Design.SingleJumpLedgeTiles + 1)
                return "_needsDoubleJump";

            if (height == ctx.Design.DoubleJumpLedgeTiles)
                return "_GATE_doubleJump";

            if (height == ctx.Design.DoubleJumpLedgeTiles + 1)
                return "_unreachable";

            return string.Empty;
        }

        #endregion

        #region Scene Surgery

        private static void DestroyExistingRoot(Scene scene)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == RootName)
                    Object.DestroyImmediate(go);
            }
        }

        private static int StripExistingLevel(Scene scene)
        {
            var doomed = new List<GameObject>();

            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == RootName)
                    continue;

                if (StrippedRootNames.Contains(go.name) ||
                    go.name.StartsWith("Fracture Object") ||
                    IsPlatformCubeInstance(go))
                {
                    doomed.Add(go);
                }
            }

            foreach (GameObject go in doomed)
                Object.DestroyImmediate(go);

            return doomed.Count;
        }

        private static bool IsPlatformCubeInstance(GameObject go)
        {
            string path =
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);

            return !string.IsNullOrEmpty(path) &&
                path.EndsWith("/PlatformCube.prefab");
        }

        private static void PlacePlayerAtSpawn(Scene scene, MovementDesignSO design)
        {
            MovementManager player = scene
                .GetRootGameObjects()
                .Select(go => go.GetComponentInChildren<MovementManager>(true))
                .FirstOrDefault(m => m != null);

            if (player == null)
            {
                Debug.LogWarning(
                    "Movement sandbox: no MovementManager in the scene, so the " +
                    "player was not moved to the spawn pad. Drag the Character " +
                    "prefab in and place it at the start of the lane.");
                return;
            }

            Transform root = player.transform.root;
            root.position = new Vector3(0f, design.BodyHeight, 3f * design.TileUnit);
        }

        #endregion

        #region Plumbing

        private static bool TryLoadDependencies(
            out MovementDesignSO design,
            out GameObject platform)
        {
            design = AssetDatabase.LoadAssetAtPath<MovementDesignSO>(DesignAssetPath);
            platform = AssetDatabase.LoadAssetAtPath<GameObject>(PlatformPrefabPath);

            if (design == null)
            {
                Debug.LogError(
                    $"Movement sandbox: no MovementDesignSO at " +
                    $"'{DesignAssetPath}'. The sandbox is generated from it, " +
                    "so there is nothing to build.");
                return false;
            }

            if (platform == null)
            {
                Debug.LogError(
                    $"Movement sandbox: no platform prefab at " +
                    $"'{PlatformPrefabPath}'.");
                return false;
            }

            return true;
        }

        private static string Summary(MovementDesignSO d)
        {
            return
                $"  jump apex        {d.JumpHeight:0.00} u ({d.JumpHeight / d.TileUnit:0.00} T)\n" +
                $"  double jump apex {d.DoubleJumpHeight:0.00} u ({d.DoubleJumpHeight / d.TileUnit:0.00} T)\n" +
                $"  airtime          {d.SingleJumpAirTime:0.000} s\n" +
                $"  gates            single {d.SingleJumpLedgeTiles}T ledge / {d.SingleJumpGapTiles}T gap, " +
                $"double {d.DoubleJumpLedgeTiles}T ledge / {d.DoubleJumpGapTiles}T gap, " +
                $"dash-jump {d.DashJumpGapTiles}T gap";
        }

        private sealed class BuildContext
        {
            public readonly MovementDesignSO Design;

            private readonly Transform _root;
            private readonly GameObject _platformPrefab;

            private Transform _section;

            public BuildContext(
                Transform root,
                GameObject platformPrefab,
                MovementDesignSO design)
            {
                _root = root;
                _platformPrefab = platformPrefab;
                Design = design;
                _section = root;
            }

            public void Section(string name)
            {
                var go = new GameObject(name);
                go.transform.SetParent(_root, false);
                _section = go.transform;
            }

            /// <summary>
            /// A slab whose top face sits at <paramref name="topTiles"/> and
            /// which spans [zStartTiles, zStartTiles + lengthTiles) along the lane.
            /// </summary>
            public void Platform(
                string name,
                float zStartTiles,
                float lengthTiles,
                float topTiles,
                float depthTiles = 1f)
            {
                float t = Design.TileUnit;

                Spawn(
                    name,
                    position: new Vector3(
                        0f,
                        (topTiles - depthTiles * 0.5f) * t,
                        (zStartTiles + lengthTiles * 0.5f) * t),
                    scale: new Vector3(
                        LaneWidth,
                        depthTiles * t,
                        lengthTiles * t),
                    solid: true);
            }

            /// <summary>
            /// A height tick beside the lane. Collider stripped, because a
            /// ruler the player can stand on is not a ruler.
            /// </summary>
            public void Marker(
                string name,
                float zCenterTiles,
                float heightTiles,
                float widthTiles,
                float lengthTiles)
            {
                float t = Design.TileUnit;

                Spawn(
                    name,
                    position: new Vector3(
                        LaneWidth * 0.5f + widthTiles * t,
                        heightTiles * t,
                        zCenterTiles * t),
                    scale: new Vector3(
                        widthTiles * t,
                        0.12f * t,
                        lengthTiles * t),
                    solid: false);
            }

            private void Spawn(
                string name,
                Vector3 position,
                Vector3 scale,
                bool solid)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(
                    _platformPrefab,
                    _section);

                go.name = name;
                go.transform.localPosition = position;
                go.transform.localScale = scale;

                if (solid)
                    return;

                // Disabled rather than removed: a property override on a
                // prefab instance is always legal, a removed component is not.
                var collider = go.GetComponent<BoxCollider>();
                if (collider != null)
                    collider.enabled = false;
            }
        }

        #endregion
    }
}
