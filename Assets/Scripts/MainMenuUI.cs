using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Leaderboard UI")]
    [SerializeField] private GameObject leaderboardPanel;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private Transform leaderboardEntriesContainer; // Parent object to hold score entries
    [SerializeField] private GameObject leaderboardEntryPrefab; // Optional: prefab for each entry

    [Header("Leaderboard Text (if not using prefab)")]
    [SerializeField] private TextMeshProUGUI[] leaderboardScoreTexts; // For rank/score display
    [SerializeField] private TextMeshProUGUI[] leaderboardComboTexts; // For combo display

    private List<GameObject> _instantiatedEntries = new List<GameObject>();

    void Start()
    {
        // Ensure leaderboard panel is hidden on start
        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(false);

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
    }

    public void OnStartGame()
    {
        SceneManager.LoadScene("gameplay_scene");
    }

    public void OnStartSandbox()
    {
        SceneManager.LoadScene("sandbox");
    }

    public void OnLeaderboard()
    {
        // Hide main menu and show leaderboard
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(true);

        // Populate leaderboard with high scores
        PopulateLeaderboard();
    }

    public void OnCloseLeaderboard()
    {
        // Hide leaderboard and show main menu
        if (leaderboardPanel != null)
            leaderboardPanel.SetActive(false);

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        // Clear any instantiated entries
        ClearInstantiatedEntries();
    }

    private void PopulateLeaderboard()
    {
        if (HighScoreManager.Instance == null)
        {
            Debug.LogWarning("HighScoreManager not found! Cannot display leaderboard.");
            return;
        }

        // Get the high scores
        List<HighScoreEntry> highScores = HighScoreManager.Instance.GetHighScores();

        // Method 1: Using prefab (dynamic instantiation)
        if (leaderboardEntryPrefab != null && leaderboardEntriesContainer != null)
        {
            PopulateWithPrefab(highScores);
        }
        // Method 2: Using pre-existing text fields
        else if (leaderboardScoreTexts != null && leaderboardScoreTexts.Length > 0)
        {
            PopulateWithTextFields(highScores);
        }
        else
        {
            Debug.LogWarning("No leaderboard display method configured! Please assign either prefab or text fields.");
        }
    }

    private void PopulateWithPrefab(List<HighScoreEntry> highScores)
    {
        // Clear previous entries
        ClearInstantiatedEntries();

        // Create an entry for each high score
        for (int i = 0; i < highScores.Count; i++)
        {
            GameObject entryObj = Instantiate(leaderboardEntryPrefab, leaderboardEntriesContainer);
            _instantiatedEntries.Add(entryObj);

            // Find text components in the prefab (assumes specific naming or components)
            TextMeshProUGUI rankText = entryObj.transform.Find("RankText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI scoreText = entryObj.transform.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI comboText = entryObj.transform.Find("ComboText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI speedLevelText = entryObj.transform.Find("SpeedLevelText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI dateText = entryObj.transform.Find("DateText")?.GetComponent<TextMeshProUGUI>();

            // Populate the text fields
            if (rankText != null)
                rankText.text = $"#{i + 1}";

            if (scoreText != null)
                scoreText.text = highScores[i].score.ToString("N0");

            if (comboText != null)
                comboText.text = $"Combo x{highScores[i].highestCombo}";

            if (speedLevelText != null)
            {
                // Handle old entries that might have speedLevel = 0
                int displayLevel = highScores[i].speedLevel > 0 ? highScores[i].speedLevel : 1;
                speedLevelText.text = $"Level {displayLevel}";
            }

            if (dateText != null)
                dateText.text = highScores[i].date;
        }

        // Fill remaining slots with empty entries if you want to show "empty" slots
        int maxSlots = 10;
        for (int i = highScores.Count; i < maxSlots; i++)
        {
            GameObject entryObj = Instantiate(leaderboardEntryPrefab, leaderboardEntriesContainer);
            _instantiatedEntries.Add(entryObj);

            TextMeshProUGUI rankText = entryObj.transform.Find("RankText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI scoreText = entryObj.transform.Find("ScoreText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI comboText = entryObj.transform.Find("ComboText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI speedLevelText = entryObj.transform.Find("SpeedLevelText")?.GetComponent<TextMeshProUGUI>();
            TextMeshProUGUI dateText = entryObj.transform.Find("DateText")?.GetComponent<TextMeshProUGUI>();

            if (rankText != null)
                rankText.text = $"#{i + 1}";

            if (scoreText != null)
                scoreText.text = "---";

            if (comboText != null)
                comboText.text = "---";

            if (speedLevelText != null)
                speedLevelText.text = "---";

            if (dateText != null)
                dateText.text = "---";
        }
    }

    private void PopulateWithTextFields(List<HighScoreEntry> highScores)
    {
        // Populate existing text fields with high scores
        for (int i = 0; i < leaderboardScoreTexts.Length; i++)
        {
            if (i < highScores.Count)
            {
                // Handle old entries that might have speedLevel = 0
                int displayLevel = highScores[i].speedLevel > 0 ? highScores[i].speedLevel : 1;

                // Build detailed info string with score, combo, level, and date
                string scoreInfo = $"#{i + 1}  {highScores[i].score.ToString("N0")}";
                string detailsInfo = $"Combo x{highScores[i].highestCombo} | Level {displayLevel} | {highScores[i].date}";

                // Display main score line
                if (leaderboardScoreTexts[i] != null)
                    leaderboardScoreTexts[i].text = scoreInfo;

                // Display details if combo text array exists, otherwise append to score text
                if (leaderboardComboTexts != null && i < leaderboardComboTexts.Length && leaderboardComboTexts[i] != null)
                {
                    leaderboardComboTexts[i].text = detailsInfo;
                }
                else if (leaderboardScoreTexts[i] != null)
                {
                    // If no separate combo text, add details to main text
                    leaderboardScoreTexts[i].text = $"{scoreInfo}\n{detailsInfo}";
                }
            }
            else
            {
                // Empty slot
                if (leaderboardScoreTexts[i] != null)
                    leaderboardScoreTexts[i].text = $"#{i + 1}  ---";

                if (leaderboardComboTexts != null && i < leaderboardComboTexts.Length && leaderboardComboTexts[i] != null)
                    leaderboardComboTexts[i].text = "---";
            }
        }
    }

    private void ClearInstantiatedEntries()
    {
        foreach (GameObject entry in _instantiatedEntries)
        {
            if (entry != null)
                Destroy(entry);
        }
        _instantiatedEntries.Clear();
    }

    public void OnQuitGame()
    {
        Application.Quit();
    }
}
