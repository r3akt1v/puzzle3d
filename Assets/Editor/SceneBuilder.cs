using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Puzzle3D.EditorTools
{
    /// <summary>Builds the main scene and the macOS player. Run from batch mode.</summary>
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("Puzzle3D/Build Main Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("Game");
            go.AddComponent<Puzzle3D.GameManager>();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EnsureShaderIncluded("Standard");
            Debug.Log("[SceneBuilder] Scene saved: " + ScenePath);
        }

        /// <summary>Shader.Find only works in players for shaders referenced by assets.
        /// Force-include ours via GraphicsSettings "Always Included Shaders".</summary>
        static void EnsureShaderIncluded(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogWarning("[SceneBuilder] shader not found: " + shaderName); return; }
            var gsAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (gsAsset == null || gsAsset.Length == 0) return;
            var so = new SerializedObject(gsAsset[0]);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");
            if (arr == null) return;
            for (int i = 0; i < arr.arraySize; i++)
            {
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
            }
            arr.InsertArrayElementAtIndex(arr.arraySize);
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[SceneBuilder] Added shader to always-included: " + shaderName);
        }

        /// <summary>Batch entry: build scene, then optionally quit.</summary>
        public static void BuildSceneAndQuit()
        {
            BuildScene();
            EditorApplication.Exit(0);
        }

        /// <summary>Batch entry: build macOS player.</summary>
        public static void BuildMac()
        {
            BuildScene();
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Mac/Puzzle3D.app",
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            };
            var report = UnityEditor.Build.Reporting.BuildReport.GetLatestReport();
            var result = BuildPipeline.BuildPlayer(opts);
            if (result.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.Log("[SceneBuilder] Build succeeded: " + opts.locationPathName);
            else
                Debug.LogError("[SceneBuilder] Build failed: " + result.summary.result);
            EditorApplication.Exit(result.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
    }
}
