using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleAttack.Grid;
using System.Collections.Generic;

#if UNITY_EDITOR || DEVELOPMENT_BUILD

namespace PuzzleAttack.RuntimeDebug
{
    /// <summary>
    /// Unity UI-based debug panel - replacement for ImGui version.
    /// Creates a canvas-based interface with tabs and controls.
    /// </summary>
    public class UnityUIDebugPanel : MonoBehaviour
    {
        private bool _isVisible = false;
        private GridDebugAPI _api;
        
        // UI state
        private int _selectedTileType = 0;
        private string[] _tileTypeNames;
        private bool _clickToSpawnMode = false;
        
        // Grid interaction
        private Camera _mainCamera;
        private Vector2Int? _hoveredCell;
        
        // Timescale controls
        private Slider _timescaleSlider;
        private TMP_InputField _timescaleInput;
        private TextMeshProUGUI _timescaleLabel;
        
        // UI References
        private Canvas _canvas;
        private GameObject _mainPanel;
        private TMP_Dropdown _tileTypeDropdown;
        private Toggle _clickToSpawnToggle;
        private Toggle _gridOverlayToggle;
        private TextMeshProUGUI _hoveredCellText;
        
        // Tab system
        private GameObject[] _tabPanels;
        private Button[] _tabButtons;
        private int _currentTab = 0;
        
        // Grid info display
        private TextMeshProUGUI _gridInfoText;
        
        // Grid controls
        private Slider _speedLevelSlider;
        private TextMeshProUGUI _speedLevelLabel;
        private Slider _baseRiseSpeedSlider;
        private TextMeshProUGUI _baseRiseSpeedLabel;
        private Slider _fastRiseMultiplierSlider;
        private TextMeshProUGUI _fastRiseMultiplierLabel;
        private Toggle _breathingRoomToggle;
        private Slider _breathingRoomPerTileSlider;
        private TextMeshProUGUI _breathingRoomPerTileLabel;

        void Start()
        {
            _api = DebugManager.Instance.gridDebugAPI;
            _mainCamera = Camera.main;
            
            // Get tile type names from API
            if (_api != null)
            {
                _tileTypeNames = _api.GetTileTypeNames();
            }
            else
            {
                _tileTypeNames = new string[] { "red", "blue", "green", "yellow", "purple", "orange" };
            }

            CreateUI();
            _canvas.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!_isVisible) return;

            HandleGridClick();
            UpdateGridInfo();
            UpdateGridControls();
            UpdateTimescaleDisplay();
        }

        private void CreateUI()
        {
            // Create Canvas
            GameObject canvasGO = new GameObject("DebugPanelCanvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;
            
            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasGO.AddComponent<GraphicRaycaster>();

            // Create main panel
            _mainPanel = CreatePanel(canvasGO.transform, "MainPanel", new Vector2(450, 700), new Vector2(10, -10), new Vector2(0, 1));
            
            // Create header
            CreateHeader();
            
            // Create tab buttons
            CreateTabButtons();
            
            // Create tab content panels
            CreateTabPanels();
            
            // Populate each tab
            PopulateTileSpawnerTab();
            PopulateGridOperationsTab();
            PopulateGridControlsTab();
            PopulateGameSettingsTab();
            PopulateQuickActionsTab();
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 size, Vector2 position, Vector2 pivot)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            
            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            rt.pivot = pivot;
            rt.anchorMin = pivot;
            rt.anchorMax = pivot;
            
            Image img = panel.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            
            return panel;
        }

        private void CreateHeader()
        {
            GameObject header = new GameObject("Header");
            header.transform.SetParent(_mainPanel.transform, false);
            
            RectTransform rt = header.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(0, 40);
            
            Image img = header.AddComponent<Image>();
            img.color = new Color(0.05f, 0.4f, 0.6f, 1f);
            
            // Title text
            GameObject titleGO = new GameObject("Title");
            titleGO.transform.SetParent(header.transform, false);
            
            TextMeshProUGUI title = titleGO.AddComponent<TextMeshProUGUI>();
            title.text = "Grid Debugger";
            title.fontSize = 20;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Center;
            title.color = Color.white;
            
            RectTransform titleRT = titleGO.GetComponent<RectTransform>();
            titleRT.anchorMin = Vector2.zero;
            titleRT.anchorMax = Vector2.one;
            titleRT.sizeDelta = Vector2.zero;
        }

        private void CreateTabButtons()
        {
            GameObject tabButtonContainer = new GameObject("TabButtons");
            tabButtonContainer.transform.SetParent(_mainPanel.transform, false);
            
            RectTransform rt = tabButtonContainer.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -40);
            rt.sizeDelta = new Vector2(0, 35);
            
            HorizontalLayoutGroup layout = tabButtonContainer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 2;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.padding = new RectOffset(5, 5, 2, 2);
            
            string[] tabNames = { "Spawner", "Operations", "Controls", "Game", "Actions" };
            _tabButtons = new Button[tabNames.Length];
            
            for (int i = 0; i < tabNames.Length; i++)
            {
                int tabIndex = i; // Capture for lambda
                _tabButtons[i] = CreateTabButton(tabButtonContainer.transform, tabNames[i], () => SwitchTab(tabIndex));
            }
            
            // Set first tab as active
            _tabButtons[0].image.color = new Color(0.05f, 0.4f, 0.6f, 1f);
        }

        private Button CreateTabButton(Transform parent, string text, System.Action onClick)
        {
            GameObject buttonGO = new GameObject("Tab_" + text);
            buttonGO.transform.SetParent(parent, false);
            
            Button button = buttonGO.AddComponent<Button>();
            Image img = buttonGO.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick());
            
            // Button colors
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            colors.pressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            colors.selectedColor = new Color(0.05f, 0.4f, 0.6f, 1f);
            button.colors = colors;
            
            // Text
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform, false);
            
            TextMeshProUGUI textComp = textGO.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.fontSize = 14;
            textComp.alignment = TextAlignmentOptions.Center;
            textComp.color = Color.white;
            
            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
            
            return button;
        }

        private void CreateTabPanels()
        {
            _tabPanels = new GameObject[5];
            
            for (int i = 0; i < 5; i++)
            {
                GameObject tabPanel = new GameObject("TabPanel_" + i);
                tabPanel.transform.SetParent(_mainPanel.transform, false);
                
                RectTransform rt = tabPanel.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.offsetMin = new Vector2(5, 5);
                rt.offsetMax = new Vector2(-5, -80);
                
                // Add scroll view
                GameObject scrollView = CreateScrollView(tabPanel.transform);
                
                _tabPanels[i] = scrollView;
                tabPanel.SetActive(i == 0); // Only first tab active
            }
        }

        private GameObject CreateScrollView(Transform parent)
        {
            GameObject scrollViewGO = new GameObject("ScrollView");
            scrollViewGO.transform.SetParent(parent, false);
            
            RectTransform rt = scrollViewGO.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            
            ScrollRect scrollRect = scrollViewGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            
            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollViewGO.transform, false);
            
            RectTransform viewportRT = viewport.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.sizeDelta = Vector2.zero;
            
            Image viewportImg = viewport.AddComponent<Image>();
            viewportImg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = true;
            
            // Content
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            
            RectTransform contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0, 1);
            contentRT.anchorMax = Vector2.one;
            contentRT.pivot = new Vector2(0.5f, 1);
            contentRT.sizeDelta = new Vector2(0, 0);
            
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10;
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = false;
            
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            scrollRect.viewport = viewportRT;
            scrollRect.content = contentRT;
            
            return content;
        }

        private void PopulateTileSpawnerTab()
        {
            Transform content = _tabPanels[0].transform;
            
            // Title
            CreateLabel(content, "Tile Spawner", 18, FontStyles.Bold);
            CreateSeparator(content);
            
            // Tile type dropdown
            CreateLabel(content, "Tile Type:", 14);
            _tileTypeDropdown = CreateDropdown(content, _tileTypeNames, (index) => { _selectedTileType = index; });
            
            // Click to spawn mode
            _clickToSpawnToggle = CreateToggle(content, "Click to Spawn Mode", false, (value) => { _clickToSpawnMode = value; });
            _gridOverlayToggle = CreateToggle(content, "Show Grid Overlay", true, null);
            
            // Hovered cell display
            _hoveredCellText = CreateLabel(content, "Hover over grid to spawn", 12);
            _hoveredCellText.color = new Color(0.3f, 1f, 0.5f, 1f);
            
            CreateSeparator(content);
            
            // Manual coordinate input
            CreateLabel(content, "Manual Coordinate Spawn:", 14);
            
            GameObject coordPanel = CreateHorizontalGroup(content);
            TMP_InputField spawnXInput = CreateInputField(coordPanel.transform, "0", "X");
            TMP_InputField spawnYInput = CreateInputField(coordPanel.transform, "0", "Y");
            
            CreateButton(content, "Spawn at Coordinates", () =>
            {
                if (int.TryParse(spawnXInput.text, out int x) && int.TryParse(spawnYInput.text, out int y))
                {
                    _api?.SpawnTile(_tileTypeNames[_selectedTileType], x, y);
                }
            });
        }

        private void PopulateGridOperationsTab()
        {
            Transform content = _tabPanels[1].transform;
            
            CreateLabel(content, "Grid Operations", 18, FontStyles.Bold);
            CreateSeparator(content);
            
            // Clear single tile
            CreateLabel(content, "Clear Single Tile:", 14);
            GameObject clearPanel = CreateHorizontalGroup(content);
            TMP_InputField clearXInput = CreateInputField(clearPanel.transform, "0", "X");
            TMP_InputField clearYInput = CreateInputField(clearPanel.transform, "0", "Y");
            
            CreateButton(content, "Clear Tile", () =>
            {
                if (int.TryParse(clearXInput.text, out int x) && int.TryParse(clearYInput.text, out int y))
                {
                    _api?.ClearTile(x, y);
                }
            });
            
            CreateSeparator(content);
            
            // Clear area
            CreateLabel(content, "Clear Area:", 14);
            GameObject areaPanel1 = CreateHorizontalGroup(content);
            TMP_InputField areaX1Input = CreateInputField(areaPanel1.transform, "0", "X1");
            TMP_InputField areaY1Input = CreateInputField(areaPanel1.transform, "0", "Y1");
            
            GameObject areaPanel2 = CreateHorizontalGroup(content);
            TMP_InputField areaX2Input = CreateInputField(areaPanel2.transform, "7", "X2");
            TMP_InputField areaY2Input = CreateInputField(areaPanel2.transform, "7", "Y2");
            
            CreateButton(content, "Clear Area", () =>
            {
                if (int.TryParse(areaX1Input.text, out int x1) && int.TryParse(areaY1Input.text, out int y1) &&
                    int.TryParse(areaX2Input.text, out int x2) && int.TryParse(areaY2Input.text, out int y2))
                {
                    _api?.ClearArea(x1, y1, x2, y2);
                }
            });
            
            CreateSeparator(content);
            
            // Quick clear buttons
            CreateButton(content, "Clear All Matches", () => _api?.ClearMatches());
            CreateButton(content, "Clear Entire Grid (Hold CTRL)", () =>
            {
                if (Input.GetKey(KeyCode.LeftControl))
                {
                    _api?.ClearAll();
                }
            });
        }

        private void PopulateGridControlsTab()
        {
            Transform content = _tabPanels[2].transform;
            
            CreateLabel(content, "Grid Controls", 18, FontStyles.Bold);
            CreateSeparator(content);
            
            // Speed Level
            _speedLevelLabel = CreateLabel(content, "Speed Level: 1 / 99", 14);
            _speedLevelSlider = CreateSlider(content, 1, 99, 1, (value) =>
            {
                var gm = _api?.GetGridManager();
                if (gm?.gridRiser != null)
                {
                    gm.gridRiser.speedLevel = Mathf.RoundToInt(value);
                }
            });
            
            // Base Rise Speed
            _baseRiseSpeedLabel = CreateLabel(content, "Base Rise Speed: 0.00", 14);
            _baseRiseSpeedSlider = CreateSlider(content, 0f, 1f, 0.1f, (value) =>
            {
                var gm = _api?.GetGridManager();
                if (gm?.gridRiser != null)
                {
                    gm.gridRiser.baseRiseSpeed = value;
                }
            });
            
            // Fast Rise Multiplier
            _fastRiseMultiplierLabel = CreateLabel(content, "Fast Rise Multiplier: 1.0x", 14);
            _fastRiseMultiplierSlider = CreateSlider(content, 1f, 10f, 2f, (value) =>
            {
                var gm = _api?.GetGridManager();
                if (gm?.gridRiser != null)
                {
                    gm.gridRiser.fastRiseMultiplier = value;
                }
            });
            
            CreateSeparator(content);
            
            // Breathing Room
            _breathingRoomToggle = CreateToggle(content, "Enable Breathing Room", true, (value) =>
            {
                var gm = _api?.GetGridManager();
                if (gm?.gridRiser != null)
                {
                    gm.gridRiser.enableBreathingRoom = value;
                }
            });
            
            _breathingRoomPerTileLabel = CreateLabel(content, "Breathing Room Per Tile: 0.00s", 14);
            _breathingRoomPerTileSlider = CreateSlider(content, 0f, 2f, 0.5f, (value) =>
            {
                var gm = _api?.GetGridManager();
                if (gm?.gridRiser != null)
                {
                    gm.gridRiser.breathingRoomPerTile = value;
                }
            });
            
            CreateSeparator(content);
            
            CreateButton(content, "Force Drop All Tiles", () => _api?.ForceDrop());
            
            // Grid info display
            _gridInfoText = CreateLabel(content, "Loading grid info...", 12);
            _gridInfoText.alignment = TextAlignmentOptions.TopLeft;
        }

        private void PopulateQuickActionsTab()
        {
            Transform content = _tabPanels[4].transform;
            
            CreateLabel(content, "Quick Actions", 18, FontStyles.Bold);
            CreateSeparator(content);
            
            CreateLabel(content, "Quick Fill Patterns:", 14);
            
            CreateButton(content, "Fill Random (All)", () =>
            {
                var gm = _api?.GetGridManager();
                if (gm != null)
                {
                    _api.SpawnRandomArea(0, 0, gm.gridWidth - 1, gm.gridHeight - 1);
                }
            });
            
            CreateButton(content, "Fill Bottom Half", () =>
            {
                var gm = _api?.GetGridManager();
                if (gm != null)
                {
                    _api.SpawnRandomArea(0, 0, gm.gridWidth - 1, gm.gridHeight / 2);
                }
            });
            
            CreateButton(content, "Fill Top Half", () =>
            {
                var gm = _api?.GetGridManager();
                if (gm != null)
                {
                    _api.SpawnRandomArea(0, gm.gridHeight / 2, gm.gridWidth - 1, gm.gridHeight - 1);
                }
            });
            
            CreateSeparator(content);
            
            CreateLabel(content, "Debug Commands:", 14);
            
            CreateButton(content, "Show Grid Info", () => _api?.GridInfo());
            CreateButton(content, "Toggle Breathing Room", () => _api?.ToggleBreathingRoom());
        }

        private void PopulateGameSettingsTab()
        {
            Transform content = _tabPanels[3].transform;
            
            CreateLabel(content, "Game Settings", 18, FontStyles.Bold);
            CreateSeparator(content);
            
            // Timescale Section
            CreateLabel(content, "Time Scale", 16, FontStyles.Bold);
            
            // Current timescale display
            _timescaleLabel = CreateLabel(content, $"Current: {Time.timeScale:F2}x", 14);
            _timescaleLabel.color = new Color(0.3f, 1f, 0.5f, 1f);
            
            // Timescale slider
            CreateLabel(content, "Adjust with Slider:", 12);
            _timescaleSlider = CreateSlider(content, 0f, 5f, Time.timeScale, (value) =>
            {
                Time.timeScale = value;
                UpdateTimescaleDisplay();
            });
            
            // Timescale input field
            CreateLabel(content, "Or Type Value:", 12);
            GameObject inputPanel = CreateHorizontalGroup(content);
            _timescaleInput = CreateInputField(inputPanel.transform, Time.timeScale.ToString("F2"), "Timescale");
            
            Button applyButton = CreateButton(inputPanel.transform, "Apply", () =>
            {
                if (float.TryParse(_timescaleInput.text, out float value))
                {
                    value = Mathf.Clamp(value, 0f, 10f);
                    Time.timeScale = value;
                    _timescaleSlider.value = Mathf.Min(value, 5f); // Slider max is 5
                    UpdateTimescaleDisplay();
                }
            });
            
            // Quick timescale buttons
            CreateSeparator(content);
            CreateLabel(content, "Quick Presets:", 14);
            
            GameObject presetsPanel = CreateHorizontalGroup(content);
            CreateButton(presetsPanel.transform, "0.25x", () => SetTimescale(0.25f));
            CreateButton(presetsPanel.transform, "0.5x", () => SetTimescale(0.5f));
            CreateButton(presetsPanel.transform, "1.0x", () => SetTimescale(1.0f));
            
            GameObject presetsPanel2 = CreateHorizontalGroup(content);
            CreateButton(presetsPanel2.transform, "2.0x", () => SetTimescale(2.0f));
            CreateButton(presetsPanel2.transform, "5.0x", () => SetTimescale(5.0f));
            CreateButton(presetsPanel2.transform, "Pause", () => SetTimescale(0f));
        }

        // UI Helper Methods
        private TextMeshProUGUI CreateLabel(Transform parent, string text, int fontSize = 14, FontStyles style = FontStyles.Normal)
        {
            GameObject labelGO = new GameObject("Label_" + text);
            labelGO.transform.SetParent(parent, false);
            
            TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Left;
            
            LayoutElement layout = labelGO.AddComponent<LayoutElement>();
            layout.preferredHeight = fontSize + 5;
            
            return label;
        }

        private void CreateSeparator(Transform parent)
        {
            GameObject separator = new GameObject("Separator");
            separator.transform.SetParent(parent, false);
            
            Image img = separator.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            
            LayoutElement layout = separator.AddComponent<LayoutElement>();
            layout.preferredHeight = 2;
            layout.minHeight = 2;
        }

        private Button CreateButton(Transform parent, string text, System.Action onClick)
        {
            GameObject buttonGO = new GameObject("Button_" + text);
            buttonGO.transform.SetParent(parent, false);
            
            Button button = buttonGO.AddComponent<Button>();
            Image img = buttonGO.AddComponent<Image>();
            img.color = new Color(0.05f, 0.4f, 0.6f, 1f);
            
            button.targetGraphic = img;
            button.onClick.AddListener(() => onClick());
            
            // Text
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(buttonGO.transform, false);
            
            TextMeshProUGUI textComp = textGO.AddComponent<TextMeshProUGUI>();
            textComp.text = text;
            textComp.fontSize = 14;
            textComp.alignment = TextAlignmentOptions.Center;
            textComp.color = Color.white;
            
            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
            
            LayoutElement layout = buttonGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 30;
            
            return button;
        }

        private TMP_Dropdown CreateDropdown(Transform parent, string[] options, System.Action<int> onValueChanged)
        {
            GameObject dropdownGO = new GameObject("Dropdown");
            dropdownGO.transform.SetParent(parent, false);
            
            TMP_Dropdown dropdown = dropdownGO.AddComponent<TMP_Dropdown>();
            
            Image img = dropdownGO.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            dropdown.ClearOptions();
            dropdown.AddOptions(new List<string>(options));
            dropdown.onValueChanged.AddListener((index) => onValueChanged?.Invoke(index));
            
            LayoutElement layout = dropdownGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 30;
            
            // Create label
            GameObject labelGO = new GameObject("Label");
            labelGO.transform.SetParent(dropdownGO.transform, false);
            
            TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = options[0];
            label.fontSize = 14;
            label.alignment = TextAlignmentOptions.Left;
            label.color = Color.white;
            
            RectTransform labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero;
            labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = new Vector2(10, 0);
            labelRT.offsetMax = new Vector2(-25, 0);
            
            dropdown.captionText = label;
            
            return dropdown;
        }

        private Toggle CreateToggle(Transform parent, string text, bool defaultValue, System.Action<bool> onValueChanged)
        {
            GameObject toggleGO = new GameObject("Toggle_" + text);
            toggleGO.transform.SetParent(parent, false);
            
            Toggle toggle = toggleGO.AddComponent<Toggle>();
            toggle.isOn = defaultValue;
            if (onValueChanged != null)
                toggle.onValueChanged.AddListener((value) => onValueChanged(value));
            
            // Background
            GameObject backgroundGO = new GameObject("Background");
            backgroundGO.transform.SetParent(toggleGO.transform, false);
            
            RectTransform bgRT = backgroundGO.AddComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0.5f);
            bgRT.anchorMax = new Vector2(0, 0.5f);
            bgRT.pivot = new Vector2(0, 0.5f);
            bgRT.anchoredPosition = Vector2.zero;
            bgRT.sizeDelta = new Vector2(40, 20);
            
            Image bgImg = backgroundGO.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            // Checkmark
            GameObject checkmarkGO = new GameObject("Checkmark");
            checkmarkGO.transform.SetParent(backgroundGO.transform, false);
            
            RectTransform checkRT = checkmarkGO.AddComponent<RectTransform>();
            checkRT.anchorMin = Vector2.zero;
            checkRT.anchorMax = Vector2.one;
            checkRT.sizeDelta = Vector2.zero;
            
            Image checkImg = checkmarkGO.AddComponent<Image>();
            checkImg.color = new Color(0.05f, 0.8f, 0.4f, 1f);
            
            toggle.graphic = checkImg;
            
            // Label
            GameObject labelGO = new GameObject("Label");
            labelGO.transform.SetParent(toggleGO.transform, false);
            
            RectTransform labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0, 0);
            labelRT.anchorMax = new Vector2(1, 1);
            labelRT.offsetMin = new Vector2(50, 0);
            labelRT.offsetMax = new Vector2(0, 0);
            
            TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 14;
            label.alignment = TextAlignmentOptions.Left;
            label.color = Color.white;
            
            LayoutElement layout = toggleGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 25;
            
            return toggle;
        }

        private Slider CreateSlider(Transform parent, float min, float max, float defaultValue, System.Action<float> onValueChanged)
        {
            GameObject sliderGO = new GameObject("Slider");
            sliderGO.transform.SetParent(parent, false);
            
            Slider slider = sliderGO.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defaultValue;
            slider.wholeNumbers = false;
            slider.onValueChanged.AddListener((value) => onValueChanged?.Invoke(value));
            
            // Background
            GameObject backgroundGO = new GameObject("Background");
            backgroundGO.transform.SetParent(sliderGO.transform, false);
            
            RectTransform bgRT = backgroundGO.AddComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero;
            bgRT.anchorMax = Vector2.one;
            bgRT.sizeDelta = Vector2.zero;
            
            Image bgImg = backgroundGO.AddComponent<Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            // Fill Area
            GameObject fillAreaGO = new GameObject("Fill Area");
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            
            RectTransform fillAreaRT = fillAreaGO.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0, 0.25f);
            fillAreaRT.anchorMax = new Vector2(1, 0.75f);
            fillAreaRT.offsetMin = new Vector2(10, 0);
            fillAreaRT.offsetMax = new Vector2(-10, 0);
            
            // Fill
            GameObject fillGO = new GameObject("Fill");
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            
            RectTransform fillRT = fillGO.AddComponent<RectTransform>();
            fillRT.sizeDelta = Vector2.zero;
            
            Image fillImg = fillGO.AddComponent<Image>();
            fillImg.color = new Color(0.05f, 0.6f, 0.8f, 1f);
            
            slider.fillRect = fillRT;
            
            // Handle Slide Area
            GameObject handleAreaGO = new GameObject("Handle Slide Area");
            handleAreaGO.transform.SetParent(sliderGO.transform, false);
            
            RectTransform handleAreaRT = handleAreaGO.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = new Vector2(10, 0);
            handleAreaRT.offsetMax = new Vector2(-10, 0);
            
            // Handle
            GameObject handleGO = new GameObject("Handle");
            handleGO.transform.SetParent(handleAreaGO.transform, false);
            
            RectTransform handleRT = handleGO.AddComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(20, 20);
            
            Image handleImg = handleGO.AddComponent<Image>();
            handleImg.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImg;
            
            LayoutElement layout = sliderGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 25;
            
            return slider;
        }

        private TMP_InputField CreateInputField(Transform parent, string placeholder, string label = "")
        {
            GameObject inputGO = new GameObject("InputField_" + label);
            inputGO.transform.SetParent(parent, false);
            
            TMP_InputField inputField = inputGO.AddComponent<TMP_InputField>();
            
            Image img = inputGO.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            // Text Area
            GameObject textAreaGO = new GameObject("TextArea");
            textAreaGO.transform.SetParent(inputGO.transform, false);
            
            RectTransform textAreaRT = textAreaGO.AddComponent<RectTransform>();
            textAreaRT.anchorMin = Vector2.zero;
            textAreaRT.anchorMax = Vector2.one;
            textAreaRT.offsetMin = new Vector2(5, 0);
            textAreaRT.offsetMax = new Vector2(-5, 0);
            
            RectMask2D mask = textAreaGO.AddComponent<RectMask2D>();
            
            // Text
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(textAreaGO.transform, false);
            
            TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
            text.fontSize = 14;
            text.color = Color.white;
            
            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
            
            inputField.textComponent = text;
            inputField.text = placeholder;
            
            LayoutElement layout = inputGO.AddComponent<LayoutElement>();
            layout.preferredHeight = 25;
            layout.flexibleWidth = 1;
            
            return inputField;
        }

        private GameObject CreateHorizontalGroup(Transform parent)
        {
            GameObject groupGO = new GameObject("HorizontalGroup");
            groupGO.transform.SetParent(parent, false);
            
            HorizontalLayoutGroup layout = groupGO.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 5;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            
            LayoutElement layoutElement = groupGO.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 30;
            
            return groupGO;
        }

        private void SwitchTab(int tabIndex)
        {
            if (_currentTab == tabIndex) return;
            
            _tabPanels[_currentTab].transform.parent.gameObject.SetActive(false);
            _tabPanels[tabIndex].transform.parent.gameObject.SetActive(true);
            
            // Update button colors
            _tabButtons[_currentTab].image.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            _tabButtons[tabIndex].image.color = new Color(0.05f, 0.4f, 0.6f, 1f);
            
            _currentTab = tabIndex;
        }

        private void HandleGridClick()
        {
            if (!_clickToSpawnMode || _api == null) return;

            var gridManager = _api.GetGridManager();
            if (gridManager == null || _mainCamera == null) return;

            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorldPos.z = 0;

            int gridX = Mathf.FloorToInt(mouseWorldPos.x / gridManager.tileSize);
            int gridY = Mathf.FloorToInt((mouseWorldPos.y - gridManager.gridRiser.CurrentGridOffset) / gridManager.tileSize);

            if (gridX >= 0 && gridX < gridManager.gridWidth && gridY >= 0 && gridY < gridManager.gridHeight)
            {
                _hoveredCell = new Vector2Int(gridX, gridY);
                _hoveredCellText.text = $"Hovered: ({gridX}, {gridY}) - Click to spawn!";

                if (Input.GetMouseButtonDown(0))
                {
                    _api.SpawnTile(_tileTypeNames[_selectedTileType], gridX, gridY);
                }
            }
            else
            {
                _hoveredCell = null;
                _hoveredCellText.text = "Hover over grid to spawn";
            }
        }

        private void UpdateGridInfo()
        {
            if (_gridInfoText == null) return;
            
            var gridManager = _api?.GetGridManager();
            if (gridManager == null)
            {
                _gridInfoText.text = "Grid not found - enter gameplay scene";
                _gridInfoText.color = new Color(1f, 0.3f, 0.3f, 1f);
                return;
            }

            System.Text.StringBuilder info = new System.Text.StringBuilder();
            info.AppendLine($"Grid Size: {gridManager.gridWidth} x {gridManager.gridHeight}");
            info.AppendLine($"Tile Size: {gridManager.tileSize}");
            
            if (gridManager.gridRiser != null)
            {
                info.AppendLine($"Grid Offset: {gridManager.gridRiser.CurrentGridOffset:F2}");
                info.AppendLine($"Speed Level: {gridManager.gridRiser.speedLevel}");
                info.AppendLine($"Is Swapping: {gridManager.IsSwapping}");
            }
            
            if (gridManager.matchProcessor != null)
            {
                info.AppendLine($"Processing: {gridManager.matchProcessor.IsProcessingMatches}");
            }

            int tileCount = 0;
            var grid = gridManager.GetGrid();
            if (grid != null)
            {
                for (int x = 0; x < gridManager.gridWidth; x++)
                {
                    for (int y = 0; y < gridManager.gridHeight; y++)
                    {
                        if (grid[x, y] != null) tileCount++;
                    }
                }
            }
            info.AppendLine($"Active Tiles: {tileCount}");

            _gridInfoText.text = info.ToString();
            _gridInfoText.color = Color.white;
        }

        private void UpdateGridControls()
        {
            var gridManager = _api?.GetGridManager();
            if (gridManager?.gridRiser == null) return;

            // Update slider values and labels
            if (_speedLevelSlider != null)
            {
                _speedLevelSlider.value = gridManager.gridRiser.speedLevel;
                _speedLevelLabel.text = $"Speed Level: {gridManager.gridRiser.speedLevel} / {gridManager.gridRiser.maxSpeedLevel}";
            }

            if (_baseRiseSpeedSlider != null)
            {
                _baseRiseSpeedSlider.value = gridManager.gridRiser.baseRiseSpeed;
                _baseRiseSpeedLabel.text = $"Base Rise Speed: {gridManager.gridRiser.baseRiseSpeed:F3}";
            }

            if (_fastRiseMultiplierSlider != null)
            {
                _fastRiseMultiplierSlider.value = gridManager.gridRiser.fastRiseMultiplier;
                _fastRiseMultiplierLabel.text = $"Fast Rise Multiplier: {gridManager.gridRiser.fastRiseMultiplier:F1}x";
            }

            if (_breathingRoomToggle != null)
            {
                _breathingRoomToggle.isOn = gridManager.gridRiser.enableBreathingRoom;
            }

            if (_breathingRoomPerTileSlider != null)
            {
                _breathingRoomPerTileSlider.value = gridManager.gridRiser.breathingRoomPerTile;
                _breathingRoomPerTileLabel.text = $"Breathing Room Per Tile: {gridManager.gridRiser.breathingRoomPerTile:F2}s";
            }
        }

        private void UpdateTimescaleDisplay()
        {
            if (_timescaleLabel != null)
            {
                _timescaleLabel.text = $"Current: {Time.timeScale:F2}x";
            }
            
            if (_timescaleInput != null && !_timescaleInput.isFocused)
            {
                _timescaleInput.text = Time.timeScale.ToString("F2");
            }
        }

        private void SetTimescale(float value)
        {
            Time.timeScale = value;
            if (_timescaleSlider != null)
            {
                _timescaleSlider.value = Mathf.Min(value, 5f);
            }
            UpdateTimescaleDisplay();
        }

        public void SetVisible(bool visible)
        {
            _isVisible = visible;
            if (_canvas != null)
            {
                _canvas.gameObject.SetActive(visible);
            }
            
            if (!visible)
            {
                _clickToSpawnMode = false;
                _hoveredCell = null;
            }
        }
    }
}

#endif