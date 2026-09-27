using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace StageEditor
{
    /// <summary>
    /// Stands up a showcase stage in a scene: the objects, the asset
    /// references between them, and one apply to push the profile out.
    ///
    /// Everything it makes is an ordinary scene object holding ordinary asset
    /// references. Once this has run the stage is finished data - it can be
    /// saved, opened, hand-edited and shipped, and nothing has to assemble it
    /// on load. <see cref="StageRig"/> is only the thing that writes the
    /// profile into it; delete the rig afterwards and the lighting stands.
    /// </summary>
    public static class StageBuilder
    {
        private const string StageFolder = "Assets/GAME/Data/Stage";

        private const string ProfilePath = StageFolder + "/Stage_Showcase.asset";
        private const string SkyMaterialPath = StageFolder + "/Stage_Sky.mat";
        private const string GroundMaterialPath = StageFolder + "/Stage_Ground.mat";
        private const string PostProfilePath = StageFolder + "/Stage_Post.asset";

        private const string ScenePath = "Assets/Scenes/ShowcaseStage.unity";

        private const string RootName = "ShowcaseStage";

        [MenuItem("Tools/Stage/Add Stage To Active Scene", priority = 0)]
        public static void AddToActiveScene()
        {
            if (!tryLoadAssets(out Assets assets))
                return;

            Scene scene = SceneManager.GetActiveScene();
            StageRig rig = build(assets);

            Selection.activeGameObject = rig.gameObject;
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log(
                $"Showcase stage added to '{scene.name}' from " +
                $"{assets.Profile.name}. Existing directional lights were " +
                "switched off so they do not fight the rig; delete them once " +
                "the stage looks right.",
                rig);
        }

        [MenuItem("Tools/Stage/Create Showcase Scene", priority = 1)]
        public static void CreateShowcaseScene()
        {
            if (!tryLoadAssets(out Assets assets))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.DefaultGameObjects,
                NewSceneMode.Single);

            StageRig rig = build(assets);
            frameCamera();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = rig.gameObject;

            Debug.Log(
                $"Showcase scene written to '{ScenePath}'. Drop a character " +
                "in at the origin; the stage is built around it.",
                rig);
        }

        #region Build

        private static StageRig build(Assets assets)
        {
            GameObject root = GameObject.Find(RootName);

            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Create Showcase Stage");
            }

            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            StageRig rig = root.GetComponent<StageRig>();

            if (rig == null)
                rig = Undo.AddComponent<StageRig>(root);

            Light key = findOrCreateLight(root, "Key Light");
            Light fill = findOrCreateLight(root, "Fill Light");
            Light rim = findOrCreateLight(root, "Rim Light");
            Transform ground = findOrCreateGround(root, assets.GroundMaterial);
            Volume volume = findOrCreateVolume(root, assets.PostProfile);

            silenceForeignLights(root);

            // Through SerializedObject rather than public setters: the rig's
            // wiring is private on purpose - it is set up once, here - and
            // opening it up just so a tool can reach it would invite the scene
            // and the profile to disagree about who owns these objects.
            var serialized = new SerializedObject(rig);
            serialized.FindProperty("_profile").objectReferenceValue = assets.Profile;
            serialized.FindProperty("_skyMaterial").objectReferenceValue = assets.SkyMaterial;
            serialized.FindProperty("_groundMaterial").objectReferenceValue = assets.GroundMaterial;
            serialized.FindProperty("_postProfile").objectReferenceValue = assets.PostProfile;
            serialized.FindProperty("_key").objectReferenceValue = key;
            serialized.FindProperty("_fill").objectReferenceValue = fill;
            serialized.FindProperty("_rim").objectReferenceValue = rim;
            serialized.FindProperty("_ground").objectReferenceValue = ground;
            serialized.FindProperty("_volume").objectReferenceValue = volume;
            serialized.ApplyModifiedProperties();

            rig.Apply();

            return rig;
        }

        private static Light findOrCreateLight(GameObject root, string name)
        {
            Transform existing = root.transform.Find(name);

            if (existing != null && existing.TryGetComponent(out Light found))
                return found;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Stage Light");
            go.transform.SetParent(root.transform, false);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            return light;
        }

        private static Transform findOrCreateGround(GameObject root, Material material)
        {
            Transform existing = root.transform.Find("Ground");

            if (existing != null)
                return existing;

            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Ground";
            Undo.RegisterCreatedObjectUndo(plane, "Create Stage Ground");
            plane.transform.SetParent(root.transform, false);

            // Nothing walks on the backdrop; the gameplay collider belongs to
            // the level, not to the thing lighting it.
            if (plane.TryGetComponent(out Collider collider))
                Object.DestroyImmediate(collider);

            if (plane.TryGetComponent(out MeshRenderer renderer))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;

                if (material != null)
                    renderer.sharedMaterial = material;
            }

            return plane.transform;
        }

        private static Volume findOrCreateVolume(GameObject root, VolumeProfile profile)
        {
            if (!root.TryGetComponent(out Volume volume))
                volume = Undo.AddComponent<Volume>(root);

            volume.isGlobal = true;
            volume.priority = 100f;

            if (profile != null)
                volume.sharedProfile = profile;

            return volume;
        }

        /// <summary>
        /// Switches off directional lights the stage does not own.
        /// </summary>
        /// <remarks>
        /// A scene's default sun is the single most destructive thing for this
        /// stage. It is bright, white and aimed at nothing in particular, and
        /// with it in frame every careful number in the profile is being added
        /// to something that overwhelms it - the stage looks flat and the
        /// profile looks broken.
        ///
        /// Disabled rather than deleted, because they belong to the scene
        /// rather than to this tool, and a tool that quietly removes other
        /// people's objects is one nobody runs twice.
        /// </remarks>
        private static void silenceForeignLights(GameObject root)
        {
            Light[] lights = Object.FindObjectsByType<Light>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (Light light in lights)
            {
                if (light == null || light.type != LightType.Directional)
                    continue;

                if (light.transform.IsChildOf(root.transform))
                    continue;

                Undo.RecordObject(light.gameObject, "Silence Scene Light");
                light.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Puts the camera where the stage was composed to be seen from:
        /// roughly level with the subject and far enough back that the horizon
        /// band sits under it.
        /// </summary>
        private static void frameCamera()
        {
            Camera camera = Camera.main;

            if (camera == null)
                return;

            Undo.RecordObject(camera.transform, "Frame Stage Camera");

            camera.transform.SetPositionAndRotation(
                new Vector3(0f, 1.6f, -6.5f),
                Quaternion.Euler(6f, 0f, 0f));

            camera.fieldOfView = 45f;

            // The gradient is the backdrop; anything else would draw over it.
            camera.clearFlags = CameraClearFlags.Skybox;
        }

        #endregion

        #region Assets

        private readonly struct Assets
        {
            public readonly StageProfile Profile;
            public readonly Material SkyMaterial;
            public readonly Material GroundMaterial;
            public readonly VolumeProfile PostProfile;

            public Assets(
                StageProfile profile,
                Material skyMaterial,
                Material groundMaterial,
                VolumeProfile postProfile)
            {
                Profile = profile;
                SkyMaterial = skyMaterial;
                GroundMaterial = groundMaterial;
                PostProfile = postProfile;
            }
        }

        private static bool tryLoadAssets(out Assets assets)
        {
            assets = default;

            var profile = AssetDatabase.LoadAssetAtPath<StageProfile>(ProfilePath);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            var ground = AssetDatabase.LoadAssetAtPath<Material>(GroundMaterialPath);
            var post = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);

            if (profile == null)
            {
                Debug.LogError(
                    $"Showcase stage: no profile at '{ProfilePath}'. Create one " +
                    "from Assets > Create > ScriptableObjects > Stage > Stage " +
                    "Profile, or point StageBuilder at the one you want.");
                return false;
            }

            // The rest are reported but not fatal: a stage with no sky
            // material still lights a character, and saying which piece is
            // missing beats refusing to build over it.
            warnIfMissing(sky, SkyMaterialPath);
            warnIfMissing(ground, GroundMaterialPath);
            warnIfMissing(post, PostProfilePath);

            assets = new Assets(profile, sky, ground, post);
            return true;
        }

        private static void warnIfMissing(Object asset, string path)
        {
            if (asset == null)
                Debug.LogWarning($"Showcase stage: nothing at '{path}'.");
        }

        #endregion
    }
}
