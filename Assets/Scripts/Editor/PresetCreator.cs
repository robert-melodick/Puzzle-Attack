#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using PuzzleAttack;
using PuzzleAttack.Grid.AI;

/// <summary>
/// Editor utility to create default preset ScriptableObjects.
/// Access via menu: Puzzle Attack > Create Presets
/// </summary>
public static class PresetCreator
{
    private const string PresetPath = "Assets/ScriptableObjects";
    private const string ModesPath = PresetPath + "/GameModes";
    private const string GridDifficultyPath = PresetPath + "/GridDifficulty";
    private const string AIDifficultyPath = PresetPath + "/AIDifficulty";

    [MenuItem("Puzzle Attack/Create All Presets")]
    public static void CreateAllPresets()
    {
        CreateFolders();
        CreateGameModePresets();
        CreateGridDifficultyPresets();
        CreateAIDifficultyPresets();
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log("All presets created successfully!");
    }

    [MenuItem("Puzzle Attack/Create Game Mode Presets")]
    public static void CreateGameModePresets()
    {
        CreateFolders();
        
        // Marathon Mode
        var marathon = ScriptableObject.CreateInstance<GameModeConfig>();
        marathon.displayName = "Marathon";
        marathon.description = "Classic endless mode. Survive as long as you can and aim for the high score!";
        marathon.modeType = GameModeType.Marathon;
        marathon.minPlayers = 1;
        marathon.maxPlayers = 1;
        marathon.defaultPlayers = 1;
        marathon.allowAI = false;
        marathon.allowDifficultySelection = false;
        marathon.allowSpeedSelection = true;
        marathon.allowGridDifficultySelection = true;
        marathon.enableGarbageSending = false;
        marathon.targetScene = "gameplay_scene";
        CreateAsset(marathon, ModesPath + "/Marathon.asset");

        // VS CPU Mode
        var vsCpu = ScriptableObject.CreateInstance<GameModeConfig>();
        vsCpu.displayName = "VS CPU";
        vsCpu.description = "Battle against AI opponents. Send garbage blocks to defeat them!";
        vsCpu.modeType = GameModeType.VsCPU;
        vsCpu.minPlayers = 2;
        vsCpu.maxPlayers = 4;
        vsCpu.defaultPlayers = 2;
        vsCpu.allowAI = true;
        vsCpu.allowDifficultySelection = true;
        vsCpu.allowSpeedSelection = true;
        vsCpu.allowGridDifficultySelection = true;
        vsCpu.enableGarbageSending = true;
        vsCpu.garbageMultiplier = 1f;
        vsCpu.eliminationMode = true;
        vsCpu.targetScene = "gameplay_scene";
        CreateAsset(vsCpu, ModesPath + "/VsCPU.asset");

        // VS Human Mode
        var vsHuman = ScriptableObject.CreateInstance<GameModeConfig>();
        vsHuman.displayName = "VS Human";
        vsHuman.description = "Local multiplayer battle! Challenge your friends!";
        vsHuman.modeType = GameModeType.VsHuman;
        vsHuman.minPlayers = 2;
        vsHuman.maxPlayers = 4;
        vsHuman.defaultPlayers = 2;
        vsHuman.allowAI = false;
        vsHuman.allowDifficultySelection = false;
        vsHuman.allowSpeedSelection = true;
        vsHuman.allowGridDifficultySelection = true;
        vsHuman.enableGarbageSending = true;
        vsHuman.garbageMultiplier = 1f;
        vsHuman.eliminationMode = true;
        vsHuman.targetScene = "gameplay_scene";
        CreateAsset(vsHuman, ModesPath + "/VsHuman.asset");

        // Mixed Mode (Humans + AI)
        var mixed = ScriptableObject.CreateInstance<GameModeConfig>();
        mixed.displayName = "Custom Battle";
        mixed.description = "Mix human and AI players for custom battles!";
        mixed.modeType = GameModeType.Mixed;
        mixed.minPlayers = 2;
        mixed.maxPlayers = 4;
        mixed.defaultPlayers = 2;
        mixed.allowAI = true;
        mixed.allowDifficultySelection = true;
        mixed.allowSpeedSelection = true;
        mixed.allowGridDifficultySelection = true;
        mixed.enableGarbageSending = true;
        mixed.garbageMultiplier = 1f;
        mixed.eliminationMode = true;
        mixed.targetScene = "gameplay_scene";
        CreateAsset(mixed, ModesPath + "/CustomBattle.asset");

        Debug.Log("Game mode presets created!");
    }

    [MenuItem("Puzzle Attack/Create Grid Difficulty Presets")]
    public static void CreateGridDifficultyPresets()
    {
        CreateFolders();

        // Easy
        var easy = GridDifficultySettings.CreateEasy();
        CreateAsset(easy, GridDifficultyPath + "/Easy.asset");

        // Normal
        var normal = GridDifficultySettings.CreateNormal();
        CreateAsset(normal, GridDifficultyPath + "/Normal.asset");

        // Hard
        var hard = GridDifficultySettings.CreateHard();
        CreateAsset(hard, GridDifficultyPath + "/Hard.asset");

        // Very Hard
        var veryHard = GridDifficultySettings.CreateVeryHard();
        CreateAsset(veryHard, GridDifficultyPath + "/VeryHard.asset");

        // Super Hard
        var superHard = GridDifficultySettings.CreateSuperHard();
        CreateAsset(superHard, GridDifficultyPath + "/SuperHard.asset");

        Debug.Log("Grid difficulty presets created!");
    }

    [MenuItem("Puzzle Attack/Create AI Difficulty Presets")]
    public static void CreateAIDifficultyPresets()
    {
        CreateFolders();

        // Easy
        var easy = AIDifficultySettings.CreateEasy();
        CreateAsset(easy, AIDifficultyPath + "/AI_Easy.asset");

        // Medium
        var medium = AIDifficultySettings.CreateMedium();
        CreateAsset(medium, AIDifficultyPath + "/AI_Medium.asset");

        // Hard
        var hard = AIDifficultySettings.CreateHard();
        CreateAsset(hard, AIDifficultyPath + "/AI_Hard.asset");

        // Expert
        var expert = AIDifficultySettings.CreateExpert();
        CreateAsset(expert, AIDifficultyPath + "/AI_Expert.asset");

        Debug.Log("AI difficulty presets created!");
    }

    private static void CreateFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        
        if (!AssetDatabase.IsValidFolder(ModesPath))
            AssetDatabase.CreateFolder(PresetPath, "GameModes");
        
        if (!AssetDatabase.IsValidFolder(GridDifficultyPath))
            AssetDatabase.CreateFolder(PresetPath, "GridDifficulty");
        
        if (!AssetDatabase.IsValidFolder(AIDifficultyPath))
            AssetDatabase.CreateFolder(PresetPath, "AIDifficulty");
    }

    private static void CreateAsset(ScriptableObject asset, string path)
    {
        // Check if asset already exists
        var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        if (existing != null)
        {
            Debug.Log($"Asset already exists at {path}, skipping...");
            return;
        }

        AssetDatabase.CreateAsset(asset, path);
        Debug.Log($"Created asset: {path}");
    }
}
#endif