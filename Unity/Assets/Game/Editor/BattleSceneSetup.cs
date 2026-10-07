// Creates the battle scene the first time the project is opened, adds it to the build,
// and sets the phone to portrait. Also available from the menu: Shattered Pantheon > Rebuild Battle Scene.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ShatteredPantheon.Game.Editor
{
    [InitializeOnLoad]
    public static class BattleSceneSetup
    {
        const string ScenePath = "Assets/Scenes/Battle.unity";

        static BattleSceneSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath)) CreateScene();
            };
        }

        [MenuItem("Shattered Pantheon/Rebuild Battle Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.08f, 0.07f, 0.1f);
            }
            new GameObject("Battle Screen").AddComponent<BattleScreen>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.companyName = "Shattered Pantheon";
            PlayerSettings.productName = "Shattered Pantheon";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            AssetDatabase.SaveAssets();
            Debug.Log("Shattered Pantheon: created " + ScenePath + ". Press Play to watch a fight.");
        }
    }
}
