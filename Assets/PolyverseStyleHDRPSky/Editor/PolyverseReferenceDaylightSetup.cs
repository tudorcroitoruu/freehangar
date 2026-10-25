using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace Liviu.Environment.HDRP.Editor
{
    internal static class PolyverseReferenceDaylightSetup
    {
        private const string ProfilePath = "Assets/Scenes/Gameplay/Polyverse Reference Daylight.asset";

        [MenuItem("Tools/Polyverse Style Sky/Enable Animated Procedural Clouds")]
        private static void EnableAnimatedClouds()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Configure the cloud profile outside Play Mode.");
            var volume = Object.FindObjectsByType<Volume>(FindObjectsSortMode.InstanceID)
                .FirstOrDefault(v => v.gameObject.scene == SceneManagerActive() && v.isGlobal
                                     && v.sharedProfile != null && v.sharedProfile.Has<PolyverseStyleSky>());
            if (volume == null) throw new InvalidOperationException("The active scene has no global Polyverse sky Volume.");
            var profile = volume.sharedProfile;
            var sky = Get<PolyverseStyleSky>(profile);
            sky.enableClouds.Override(true);
            sky.cloudsCubemap.Override(null);
            sky.cloudsCoverage.Override(0.45f);
            sky.cloudsScale.Override(3f);
            sky.cloudsSoftness.Override(0.065f);
            sky.cloudsDetail.Override(0.65f);
            sky.cloudsMaxHeight.Override(90f);
            sky.cloudsLayerAltitude.Override(1000f);
            sky.cloudsDistanceFade.Override(new Vector2(3000f, 8000f));
            sky.cloudsDistantScale.Override(0.35f);
            sky.cloudsBreakup.Override(0.8f);
            sky.cloudsHorizonFade.Override(0.1f);
            sky.cloudsWindVelocity.Override(new Vector2(0.003f, 0.001f));
            sky.cloudsEvolutionSpeed.Override(0.025f);
            sky.cloudsDeformation.Override(0.35f);
            sky.enableCloudsRotation.Override(false);
            sky.cloudsLightColor.Override(new Color(1f, 0.96f, 0.88f));
            sky.cloudsShadowColor.Override(new Color(0.55f, 0.65f, 0.82f));
            if (profile.TryGet<VolumetricClouds>(out var clouds))
            {
                Undo.RecordObject(clouds, "Use Polyverse procedural clouds");
                clouds.active = false;
                EditorUtility.SetDirty(clouds);
            }
            EditorUtility.SetDirty(sky);
            AssetDatabase.SaveAssets();
            if (SceneView.lastActiveSceneView != null)
                SceneView.lastActiveSceneView.sceneViewState.alwaysRefresh = true;
            SceneView.RepaintAll();
            Selection.activeGameObject = volume.gameObject;
            Debug.Log("[Polyverse Clouds] Enabled animated procedural clouds on " + AssetDatabase.GetAssetPath(profile));
        }

        [MenuItem("Tools/Polyverse Style Sky/Apply Reference Daylight")]
        private static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Apply the daylight preset outside Play Mode.");

            var scene = SceneManagerActive();
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.InstanceID)
                .Where(v => v.gameObject.scene == scene && v.isGlobal && v.sharedProfile != null).ToArray();
            var volume = volumes.FirstOrDefault(v => v.sharedProfile.Has<PolyverseStyleSky>())
                         ?? volumes.FirstOrDefault(v => v.sharedProfile.Has<VisualEnvironment>());
            if (volume == null)
                throw new InvalidOperationException("The active scene has no global sky Volume.");

            // Clone the complete profile, including its subassets, to preserve other scenes' lighting.
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath) == null)
            {
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(volume.sharedProfile), ProfilePath))
                    throw new InvalidOperationException("Could not copy the sky profile.");
            }
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            Undo.SetCurrentGroupName("Apply reference daylight sky");
            int undoGroup = Undo.GetCurrentGroup();
            Undo.RecordObject(volume, "Assign reference daylight profile");
            volume.sharedProfile = profile;

            var environment = Get<VisualEnvironment>(profile);
            environment.skyType.Override(26072026);
            var sky = Get<PolyverseStyleSky>(profile);
            sky.backgroundMode.Override(PolyverseBackgroundMode.Colors);
            sky.debugSolidColor.Override(false);
            sky.skyColor.Override(new Color(0.23f, 0.53f, 0.98f));
            sky.equatorColor.Override(new Color(0.61f, 0.79f, 0.94f));
            sky.groundColor.Override(new Color(0.36f, 0.40f, 0.43f));
            sky.equatorHeight.Override(0.65f);
            sky.equatorSmoothness.Override(0.32f);
            sky.exposure.Override(0f);
            sky.multiplier.Override(1f);
            sky.updateMode.Override(EnvironmentUpdateMode.OnChanged);
            sky.enableStars.Override(false);
            sky.enableMoon.Override(false);
            sky.enablePatternOverlay.Override(false);
            sky.enableSun.Override(false);
            // Animate cloud shapes directly in the Polyverse sky shader.
            sky.enableClouds.Override(true);
            sky.enableHorizonFog.Override(true);
            sky.horizonFogColor.Override(new Color(0.72f, 0.81f, 0.89f));
            sky.fogHeight.Override(0.08f);
            sky.fogSmoothness.Override(0.25f);
            sky.fogFill.Override(0f);

            sky.cloudsCubemap.Override(null);
            sky.cloudsCoverage.Override(0.45f);
            sky.cloudsScale.Override(3f);
            sky.cloudsSoftness.Override(0.065f);
            sky.cloudsDetail.Override(0.65f);
            sky.cloudsMaxHeight.Override(90f);
            sky.cloudsLayerAltitude.Override(1000f);
            sky.cloudsDistanceFade.Override(new Vector2(3000f, 8000f));
            sky.cloudsDistantScale.Override(0.35f);
            sky.cloudsBreakup.Override(0.8f);
            sky.cloudsWindVelocity.Override(new Vector2(0.003f, 0.001f));
            sky.cloudsEvolutionSpeed.Override(0.025f);
            sky.cloudsDeformation.Override(0.35f);
            sky.cloudsLightColor.Override(new Color(1f, 0.96f, 0.88f));
            sky.cloudsShadowColor.Override(new Color(0.55f, 0.65f, 0.82f));
            if (profile.TryGet<VolumetricClouds>(out var clouds))
            {
                Undo.RecordObject(clouds, "Use Polyverse procedural clouds");
                clouds.active = false;
            }
            var fog = Get<Fog>(profile);
            fog.enabled.Override(true);
            fog.colorMode.Override(FogColorMode.ConstantColor);
            fog.color.Override(new Color(0.65f, 0.76f, 0.83f));
            fog.meanFreePath.Override(1600f);
            fog.baseHeight.Override(0f);
            fog.maximumHeight.Override(65f);

            var sun = RenderSettings.sun;
            if (sun == null || sun.gameObject.scene != scene)
                sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.InstanceID)
                    .FirstOrDefault(l => l.gameObject.scene == scene && l.type == LightType.Directional);
            if (sun != null)
            {
                Undo.RecordObject(sun, "Warm reference sunlight");
                sun.useColorTemperature = false;
                sun.color = new Color(1f, 0.88f, 0.73f);
                EditorUtility.SetDirty(sun);
                sky.sunDirection.Override(-sun.transform.forward);
            }

            foreach (var controller in Object.FindObjectsByType<PolyverseStyleSkyTimeController>(FindObjectsSortMode.None))
            {
                if (controller.gameObject.scene != scene || controller.skyVolume != volume) continue;
                Undo.RecordObject(controller, "Keep reference daytime colors");
                controller.daySkyColor = sky.skyColor.value;
                controller.dayEquatorColor = sky.equatorColor.value;
                controller.dayGroundColor = sky.groundColor.value;
                EditorUtility.SetDirty(controller);
            }

            foreach (var component in profile.components) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(volume);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Undo.CollapseUndoOperations(undoGroup);
            (RenderPipelineManager.currentPipeline as HDRenderPipeline)?.RequestSkyEnvironmentUpdate();
            SceneView.RepaintAll();
            Selection.activeGameObject = volume.gameObject;
            Inspect();
            Debug.Log("[Polyverse Reference] Applied reference daylight to " + scene.path + ": " + ProfilePath);
        }

        private static T Get<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component))
            {
                component = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            Undo.RecordObject(component, "Configure reference daylight");
            component.active = true;
            return component;
        }

        private static UnityEngine.SceneManagement.Scene SceneManagerActive()
            => UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        [MenuItem("Tools/Polyverse Style Sky/Preview Reference Horizon")]
        private static void Preview()
        {
            var view = SceneView.lastActiveSceneView;
            if (view == null) return;
            var reference = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.gameObject.scene == SceneManagerActive() && c.name == "Exterior Review");
            var position = reference != null ? reference.transform.position : new Vector3(25f, 3f, 65f);
            // The authoring cameras can sit below the terrain/grass in an unrecovered checkout.
            position.y = Mathf.Max(position.y, 24f);
            var rotation = Quaternion.Euler(-8f, reference != null ? reference.transform.eulerAngles.y : 215f, 0f);
            var viewpoint = new GameObject("Sky preview viewpoint") { hideFlags = HideFlags.HideAndDontSave };
            viewpoint.transform.SetPositionAndRotation(position, rotation);
            view.AlignViewToObject(viewpoint.transform);
            Object.DestroyImmediate(viewpoint);
            view.sceneLighting = true;
            view.sceneViewState.showSkybox = true;
            view.sceneViewState.showFog = true;
            view.sceneViewState.showClouds = true;
            view.Repaint();
        }

        private static void Inspect()
        {
            var report = new StringBuilder("Active scene: " + SceneManagerActive().path + "\n");
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
                report.AppendLine($"Volume: {volume.name}; global={volume.isGlobal}; profile={AssetDatabase.GetAssetPath(volume.sharedProfile)}; priority={volume.priority}");
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional))
                report.AppendLine($"Sun: {light.name}; color={light.color}; intensity={light.intensity}; rotation={light.transform.eulerAngles}");
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                report.AppendLine($"Camera: {camera.name}; position={camera.transform.position}; rotation={camera.transform.eulerAngles}");
            File.WriteAllText("Library/PolyverseReferenceDaylight.txt", report.ToString());
        }
    }
}
