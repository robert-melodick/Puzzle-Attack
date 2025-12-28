#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using PuzzleAttack.Grid.AI;

namespace PuzzleAttack.Editor
{
    /// <summary>
    /// Editor utility to create AI difficulty preset ScriptableObjects.
    /// </summary>
    public static class AIDifficultyPresetCreator
    {
        private const string PRESET_PATH = "Assets/ScriptableObjects/AI";

        [MenuItem("Puzzle Attack/Create AI Difficulty Presets")]
        public static void CreatePresets()
        {
            // Ensure directory exists
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }
            if (!AssetDatabase.IsValidFolder(PRESET_PATH))
            {
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "AI");
            }

            // Create presets
            CreatePreset(AIDifficultySettings.CreateEasy(), $"{PRESET_PATH}/AIDifficulty_Easy.asset");
            CreatePreset(AIDifficultySettings.CreateMedium(), $"{PRESET_PATH}/AIDifficulty_Medium.asset");
            CreatePreset(AIDifficultySettings.CreateHard(), $"{PRESET_PATH}/AIDifficulty_Hard.asset");
            CreatePreset(AIDifficultySettings.CreateExpert(), $"{PRESET_PATH}/AIDifficulty_Expert.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"AI Difficulty presets created in {PRESET_PATH}");
            
            // Select the folder in the Project window
            var folder = AssetDatabase.LoadAssetAtPath<Object>(PRESET_PATH);
            Selection.activeObject = folder;
            EditorGUIUtility.PingObject(folder);
        }

        private static void CreatePreset(AIDifficultySettings settings, string path)
        {
            // Check if asset already exists
            var existing = AssetDatabase.LoadAssetAtPath<AIDifficultySettings>(path);
            if (existing != null)
            {
                // Update existing asset
                EditorUtility.CopySerialized(settings, existing);
                EditorUtility.SetDirty(existing);
                Debug.Log($"Updated existing preset: {path}");
            }
            else
            {
                // Create new asset
                AssetDatabase.CreateAsset(settings, path);
                Debug.Log($"Created new preset: {path}");
            }
        }

        [MenuItem("Puzzle Attack/Select AI Presets Folder")]
        public static void SelectPresetsFolder()
        {
            if (AssetDatabase.IsValidFolder(PRESET_PATH))
            {
                var folder = AssetDatabase.LoadAssetAtPath<Object>(PRESET_PATH);
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }
            else
            {
                Debug.LogWarning($"Presets folder not found. Use 'Puzzle Attack/Create AI Difficulty Presets' first.");
            }
        }
    }
}
#endif
