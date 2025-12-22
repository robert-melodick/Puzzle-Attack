using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PuzzleAttack.Grid;

/// <summary>
/// Visual debug overlay for BlockSlip mechanics.
/// Shows timeline visualization for each dropping tile per column,
/// with color-coded swap outcome indicators.
/// </summary>
public class BlockSlipDebugOverlay : MonoBehaviour
{
    #region Inspector Fields

    [Header("References")]
    [SerializeField] private GridManager gridManager;
    [SerializeField] private BlockSlipManager blockSlipManager;
    [SerializeField] private GridRiser gridRiser;
    [SerializeField] private CursorController cursorController;
    [SerializeField] private GameObject debugPanel; // Reference to Debugger's panel for visibility sync

    [Header("Layout Settings")]
    [SerializeField] private float timelineWidth = 260f;
    [SerializeField] private float timelineHeight = 28f;
    [SerializeField] private float timelineSpacing = 7f;
    [SerializeField] private float columnSpacing = 35f;
    [SerializeField] private float closestTileScale = 1.3f;
    [SerializeField] private Vector2 panelOffset = new Vector2(-10f, -10f);

    [Header("Colors")]
    [SerializeField] private Color whiteSlideUnder = Color.white;
    [SerializeField] private Color redBlocked = new Color(1f, 0.3f, 0.3f);
    [SerializeField] private Color greenNudge = new Color(0.3f, 1f, 0.3f);
    [SerializeField] private Color cyanSwapWithFalling = new Color(0.3f, 1f, 1f);
    [SerializeField] private Color grayNoBlockSlip = new Color(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color backgroundColor = new Color(0f, 0f, 0f, 0.8f);
    [SerializeField] private Color timelineBackground = new Color(0.2f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private Color sliderColor = Color.yellow;
    [SerializeField] private Color lastSwapMarkerColor = new Color(1f, 0.5f, 0f, 0.7f);

    #endregion

    #region Private Fields

    private Canvas _canvas;
    private GameObject _overlayPanel;
    private RectTransform _overlayRect;
    
    // Per-column UI containers
    private Dictionary<int, GameObject> _columnContainers = new();
    private Dictionary<int, List<TimelineEntry>> _columnTimelines = new();
    
    // Last swap attempt markers (persist until next swap)
    private Dictionary<int, List<LastSwapMarker>> _lastSwapMarkers = new();
    
    private bool _isVisible;
    private int _gridWidth;
    private float _tileSize;

    // Cached references for reflection access to BlockSlipManager internals
    private System.Reflection.FieldInfo _droppingTilesField;
    private System.Reflection.FieldInfo _dropTargetsField;
    private System.Reflection.FieldInfo _dropProgressField;
    private System.Reflection.FieldInfo _swapWithFallingThresholdField;
    private System.Reflection.FieldInfo _slideUnderBlockedThresholdField;
    private System.Reflection.FieldInfo _quickNudgeThresholdField;

    #endregion

    #region Data Structures

    private class TimelineEntry
    {
        public GameObject Container;
        public Image BackgroundImage;
        public Image ProgressFill;
        public Image SliderMarker;
        public TextMeshProUGUI Label;
        public GameObject TileRef;
        public bool IsClosestToSwapRow;
    }

    private class LastSwapMarker
    {
        public float NormalizedPosition;
        public BlockSlipOutcome Outcome;
        public Image MarkerImage;
    }

    private enum BlockSlipOutcome
    {
        NoBlockSlip,        // Gray - no falling blocks involved
        SlideUnder,         // White - normal slide under
        Blocked,            // Red - past threshold, swap blocked
        QuickNudge,         // Green - cascade/nudge will occur
        SwapWithFalling     // Cyan - can swap directly with falling tile
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        // Auto-find references
        if (gridManager == null) gridManager = FindObjectOfType<GridManager>();
        if (blockSlipManager == null) blockSlipManager = FindObjectOfType<BlockSlipManager>();
        if (gridRiser == null) gridRiser = FindObjectOfType<GridRiser>();
        if (cursorController == null) cursorController = FindObjectOfType<CursorController>();

        // Cache reflection fields for BlockSlipManager internals
        CacheReflectionFields();
    }

    private void Start()
    {
        _gridWidth = gridManager.Width;
        _tileSize = gridManager.TileSize;
        
        CreateOverlayUI();
        
        // Initialize column containers
        for (int x = 0; x < _gridWidth; x++)
        {
            _columnContainers[x] = null;
            _columnTimelines[x] = new List<TimelineEntry>();
            _lastSwapMarkers[x] = new List<LastSwapMarker>();
        }
    }

    private void Update()
    {
        // Sync visibility with debug panel
        bool shouldBeVisible = debugPanel != null && debugPanel.activeSelf;
        
        if (shouldBeVisible != _isVisible)
        {
            _isVisible = shouldBeVisible;
            if (_overlayPanel != null)
                _overlayPanel.SetActive(_isVisible);
        }

        if (!_isVisible) return;

        UpdateTimelines();
    }

    #endregion

    #region Initialization

    private void CacheReflectionFields()
    {
        var type = typeof(BlockSlipManager);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        
        _droppingTilesField = type.GetField("_droppingTiles", flags);
        _dropTargetsField = type.GetField("_dropTargets", flags);
        _dropProgressField = type.GetField("_dropProgress", flags);
        _swapWithFallingThresholdField = type.GetField("_swapWithFallingThreshold", flags);
        _slideUnderBlockedThresholdField = type.GetField("_slideUnderBlockedThreshold", flags);
        _quickNudgeThresholdField = type.GetField("_quickNudgeThreshold", flags);
    }

    private void CreateOverlayUI()
    {
        // Find or create canvas
        _canvas = FindObjectOfType<Canvas>();
        if (_canvas == null)
        {
            var canvasObj = new GameObject("BlockSlipDebugCanvas");
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100; // Above other UI
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Create main overlay panel (right side of screen)
        _overlayPanel = new GameObject("BlockSlipOverlay");
        _overlayPanel.transform.SetParent(_canvas.transform, false);

        _overlayRect = _overlayPanel.AddComponent<RectTransform>();
        _overlayRect.anchorMin = new Vector2(1, 0.5f);
        _overlayRect.anchorMax = new Vector2(1, 1);
        _overlayRect.pivot = new Vector2(1, 1);
        _overlayRect.anchoredPosition = panelOffset;
        _overlayRect.sizeDelta = new Vector2(
            timelineWidth + 100f,
            450f
        );

        // Background
        var bgImage = _overlayPanel.AddComponent<Image>();
        bgImage.color = backgroundColor;
        bgImage.raycastTarget = false;

        // Title
        var titleObj = new GameObject("Title");
        titleObj.transform.SetParent(_overlayPanel.transform, false);
        var titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -5);
        titleRect.sizeDelta = new Vector2(0, 30);

        var titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "BlockSlip Debug";
        titleText.fontSize = 20;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.Center;

        // Legend
        CreateLegend();

        _overlayPanel.SetActive(false);
    }

    private void CreateLegend()
    {
        var legendObj = new GameObject("Legend");
        legendObj.transform.SetParent(_overlayPanel.transform, false);
        var legendRect = legendObj.AddComponent<RectTransform>();
        legendRect.anchorMin = new Vector2(0, 1);
        legendRect.anchorMax = new Vector2(1, 1);
        legendRect.pivot = new Vector2(0, 1);
        legendRect.anchoredPosition = new Vector2(10, -35);
        legendRect.sizeDelta = new Vector2(-20, 70);

        var legendText = legendObj.AddComponent<TextMeshProUGUI>();
        legendText.fontSize = 13;
        legendText.color = Color.white;
        legendText.alignment = TextAlignmentOptions.TopLeft;
        legendText.text = 
            "<color=#FFFFFF>■</color> Slide Under  " +
            "<color=#4CFF4C>■</color> Quick Nudge  " +
            "<color=#4CFFFF>■</color> Swap w/Fall\n" +
            "<color=#FF4C4C>■</color> Blocked  " +
            "<color=#808080>■</color> No BlockSlip  " +
            "<color=#FFFF00>|</color> Current  " +
            "<color=#FF8000>|</color> Last Swap";
    }

    #endregion

    #region Timeline Updates

    private void UpdateTimelines()
    {
        // Get dropping tiles data via reflection
        var droppingTiles = GetDroppingTiles();
        var dropTargets = GetDropTargets();
        var dropProgress = GetDropProgress();

        if (droppingTiles == null) return;

        var cursorPos = cursorController.CursorPosition;
        var cursorLeftX = cursorPos.x;
        var cursorRightX = cursorPos.x + 1;
        var cursorY = cursorPos.y;

        // Group dropping tiles by column
        var tilesByColumn = new Dictionary<int, List<(GameObject tile, Vector2Int target, float progress)>>();
        
        for (int x = 0; x < _gridWidth; x++)
            tilesByColumn[x] = new List<(GameObject, Vector2Int, float)>();

        foreach (var tile in droppingTiles)
        {
            if (tile == null) continue;
            if (!dropTargets.TryGetValue(tile, out var target)) continue;
            
            var progress = dropProgress.TryGetValue(tile, out var p) ? p : 0f;
            tilesByColumn[target.x].Add((tile, target, progress));
        }

        // Update each column
        float yOffset = -110f; // Start below legend
        
        for (int x = 0; x < _gridWidth; x++)
        {
            var tilesInColumn = tilesByColumn[x];
            bool isCursorColumn = (x == cursorLeftX || x == cursorRightX);
            
            // Sort by world Y position (highest first for display, lowest first for "closest")
            tilesInColumn = tilesInColumn
                .OrderByDescending(t => t.tile.transform.position.y)
                .ToList();

            // Find closest to swap row
            GameObject closestTile = null;
            float closestDistance = float.MaxValue;
            
            foreach (var (tile, target, progress) in tilesInColumn)
            {
                if (target.y > cursorY) continue; // Target is above cursor row
                
                var tileWorldY = tile.transform.position.y;
                var cursorWorldY = cursorY * _tileSize + (gridRiser?.CurrentGridOffset ?? 0f);
                var distance = Mathf.Abs(tileWorldY - cursorWorldY);
                
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTile = tile;
                }
            }

            // Ensure we have enough timeline entries for this column
            EnsureTimelineEntries(x, tilesInColumn.Count);

            // Update timeline entries
            for (int i = 0; i < tilesInColumn.Count; i++)
            {
                var (tile, target, progress) = tilesInColumn[i];
                var entry = _columnTimelines[x][i];
                var isClosest = (tile == closestTile);
                
                UpdateTimelineEntry(entry, tile, target, progress, cursorY, isClosest, isCursorColumn);
            }

            // Hide unused entries
            for (int i = tilesInColumn.Count; i < _columnTimelines[x].Count; i++)
            {
                _columnTimelines[x][i].Container.SetActive(false);
            }

            // Position column container
            if (_columnContainers[x] != null)
            {
                var containerRect = _columnContainers[x].GetComponent<RectTransform>();
                containerRect.anchoredPosition = new Vector2(10, yOffset);
            }
        }
    }

    private void EnsureTimelineEntries(int column, int needed)
    {
        // Create column container if needed
        if (_columnContainers[column] == null)
        {
            var container = new GameObject($"Column_{column}");
            container.transform.SetParent(_overlayPanel.transform, false);
            
            var rect = container.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(-20, 200);

            // Column label
            var labelObj = new GameObject("ColumnLabel");
            labelObj.transform.SetParent(container.transform, false);
            var labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 1);
            labelRect.anchorMax = new Vector2(0, 1);
            labelRect.pivot = new Vector2(0, 1);
            labelRect.anchoredPosition = new Vector2(0, 0);
            labelRect.sizeDelta = new Vector2(30, 20);

            var labelText = labelObj.AddComponent<TextMeshProUGUI>();
            labelText.text = $"C{column}";
            labelText.fontSize = 14;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.MidlineLeft;

            _columnContainers[column] = container;
        }

        // Create additional timeline entries if needed
        while (_columnTimelines[column].Count < needed)
        {
            var entry = CreateTimelineEntry(column, _columnTimelines[column].Count);
            _columnTimelines[column].Add(entry);
        }
    }

    private TimelineEntry CreateTimelineEntry(int column, int index)
    {
        var entry = new TimelineEntry();
        
        // Container
        entry.Container = new GameObject($"Timeline_{index}");
        entry.Container.transform.SetParent(_columnContainers[column].transform, false);
        
        var containerRect = entry.Container.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 1);
        containerRect.anchorMax = new Vector2(1, 1);
        containerRect.pivot = new Vector2(0, 1);
        containerRect.anchoredPosition = new Vector2(35, -index * (timelineHeight + timelineSpacing));
        containerRect.sizeDelta = new Vector2(-45, timelineHeight);

        // Background bar
        var bgObj = new GameObject("Background");
        bgObj.transform.SetParent(entry.Container.transform, false);
        var bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        entry.BackgroundImage = bgObj.AddComponent<Image>();
        entry.BackgroundImage.color = timelineBackground;

        // Progress fill (shows how far along the drop animation is)
        var fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(entry.Container.transform, false);
        var fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0, 1);
        fillRect.pivot = new Vector2(0, 0.5f);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = new Vector2(0, 0);
        entry.ProgressFill = fillObj.AddComponent<Image>();

        // Current position slider
        var sliderObj = new GameObject("Slider");
        sliderObj.transform.SetParent(entry.Container.transform, false);
        var sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0, 0);
        sliderRect.anchorMax = new Vector2(0, 1);
        sliderRect.pivot = new Vector2(0.5f, 0.5f);
        sliderRect.sizeDelta = new Vector2(4, 0);
        entry.SliderMarker = sliderObj.AddComponent<Image>();
        entry.SliderMarker.color = sliderColor;

        // Label showing tile info
        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(entry.Container.transform, false);
        var labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(1, 0);
        labelRect.anchorMax = new Vector2(1, 1);
        labelRect.pivot = new Vector2(0, 0.5f);
        labelRect.anchoredPosition = new Vector2(5, 0);
        labelRect.sizeDelta = new Vector2(60, 0);
        entry.Label = labelObj.AddComponent<TextMeshProUGUI>();
        entry.Label.fontSize = 12;
        entry.Label.color = Color.white;
        entry.Label.alignment = TextAlignmentOptions.MidlineLeft;

        return entry;
    }

    private void UpdateTimelineEntry(TimelineEntry entry, GameObject tile, Vector2Int target, 
        float progress, int cursorY, bool isClosest, bool isCursorColumn)
    {
        entry.Container.SetActive(true);
        entry.TileRef = tile;
        entry.IsClosestToSwapRow = isClosest;

        // Scale for closest tile
        var scale = isClosest ? closestTileScale : 1f;
        entry.Container.transform.localScale = new Vector3(scale, scale, 1f);

        // Calculate outcome
        var outcome = CalculateBlockSlipOutcome(tile, target, cursorY, isCursorColumn);

        // Set fill color based on outcome
        Color outcomeColor = outcome switch
        {
            BlockSlipOutcome.SlideUnder => whiteSlideUnder,
            BlockSlipOutcome.Blocked => redBlocked,
            BlockSlipOutcome.QuickNudge => greenNudge,
            BlockSlipOutcome.SwapWithFalling => cyanSwapWithFalling,
            _ => grayNoBlockSlip
        };

        entry.ProgressFill.color = outcomeColor;

        // Update fill width based on progress
        var containerRect = entry.Container.GetComponent<RectTransform>();
        var fillRect = entry.ProgressFill.GetComponent<RectTransform>();
        var totalWidth = containerRect.rect.width;
        fillRect.sizeDelta = new Vector2(totalWidth * progress, 0);

        // Update slider position
        var sliderRect = entry.SliderMarker.GetComponent<RectTransform>();
        sliderRect.anchoredPosition = new Vector2(totalWidth * progress, 0);

        // Update label
        var tileScript = tile.GetComponent<Tile>();
        var tileType = tileScript?.TileType.ToString() ?? "?";
        entry.Label.text = $"→({target.x},{target.y})";

        // Highlight if in cursor column
        entry.BackgroundImage.color = isCursorColumn 
            ? new Color(0.3f, 0.3f, 0.4f, 0.9f) 
            : timelineBackground;
    }

    private BlockSlipOutcome CalculateBlockSlipOutcome(GameObject tile, Vector2Int target, 
        int cursorY, bool isCursorColumn)
    {
        if (!isCursorColumn)
            return BlockSlipOutcome.NoBlockSlip;

        // Target is above cursor row - no blockslip interaction
        if (target.y > cursorY)
            return BlockSlipOutcome.NoBlockSlip;

        var offsetY = gridRiser?.CurrentGridOffset ?? 0f;
        var tileWorldY = tile.transform.position.y;
        
        var swapRowWorldY = cursorY * _tileSize + offsetY;
        var swapRowTop = swapRowWorldY + _tileSize;
        
        // Get thresholds from BlockSlipManager
        var swapWithFallingThreshold = GetSwapWithFallingThreshold();
        var slideUnderBlockedThreshold = GetSlideUnderBlockedThreshold();
        var quickNudgeThreshold = GetQuickNudgeThreshold();

        // Check if tile is in the swap row
        bool isInSwapRow = tileWorldY >= swapRowWorldY && tileWorldY < swapRowTop;

        if (!isInSwapRow)
        {
            // Tile is above swap row - would be quick nudge if swap happens
            if (tileWorldY >= swapRowTop)
                return BlockSlipOutcome.QuickNudge;
            
            // Tile has already passed through
            return BlockSlipOutcome.NoBlockSlip;
        }

        // Tile is in swap row - determine outcome based on position
        
        // SwapWithFalling threshold: tile must be above (1 - threshold) point
        // e.g., threshold 0.5 means tile center must be in row, so check if Y >= row + 0.5*tileSize
        var swapWithFallingThresholdY = swapRowWorldY + _tileSize * (1f - swapWithFallingThreshold);
        
        // SlideUnder blocked threshold: if tile is below this, slide-under is blocked
        var slideUnderBlockedY = swapRowWorldY + _tileSize * slideUnderBlockedThreshold;
        
        // QuickNudge threshold: tile must be above this for quick nudge
        var quickNudgeY = swapRowWorldY + _tileSize * quickNudgeThreshold;

        // Priority order of checks:
        // 1. If above swapWithFalling threshold -> can swap directly with falling tile
        if (tileWorldY >= swapWithFallingThresholdY)
            return BlockSlipOutcome.SwapWithFalling;
        
        // 2. If above quickNudge threshold but below swapWithFalling -> quick nudge
        if (tileWorldY >= quickNudgeY)
            return BlockSlipOutcome.QuickNudge;
        
        // 3. If above slideUnderBlocked threshold -> can still slide under
        if (tileWorldY >= slideUnderBlockedY)
            return BlockSlipOutcome.SlideUnder;
        
        // 4. Below slideUnderBlocked threshold -> blocked
        return BlockSlipOutcome.Blocked;
    }

    private float GetSwapWithFallingThreshold()
    {
        if (_swapWithFallingThresholdField == null || blockSlipManager == null) return 0.5f;
        return (float)_swapWithFallingThresholdField.GetValue(blockSlipManager);
    }

    private float GetSlideUnderBlockedThreshold()
    {
        if (_slideUnderBlockedThresholdField == null || blockSlipManager == null) return 0.4f;
        return (float)_slideUnderBlockedThresholdField.GetValue(blockSlipManager);
    }

    private float GetQuickNudgeThreshold()
    {
        if (_quickNudgeThresholdField == null || blockSlipManager == null) return 0.5f;
        return (float)_quickNudgeThresholdField.GetValue(blockSlipManager);
    }

    #endregion

    #region Last Swap Marker

    /// <summary>
    /// Call this when a swap attempt is made to record the marker position.
    /// </summary>
    public void RecordSwapAttempt()
    {
        var droppingTiles = GetDroppingTiles();
        var dropTargets = GetDropTargets();
        var dropProgress = GetDropProgress();

        if (droppingTiles == null) return;

        var cursorPos = cursorController.CursorPosition;
        var cursorY = cursorPos.y;

        // Clear old markers
        foreach (var markers in _lastSwapMarkers.Values)
        {
            foreach (var marker in markers)
            {
                if (marker.MarkerImage != null)
                    Destroy(marker.MarkerImage.gameObject);
            }
            markers.Clear();
        }

        // Record new markers for each dropping tile
        foreach (var tile in droppingTiles)
        {
            if (tile == null) continue;
            if (!dropTargets.TryGetValue(tile, out var target)) continue;

            var progress = dropProgress.TryGetValue(tile, out var p) ? p : 0f;
            var outcome = CalculateBlockSlipOutcome(tile, target, cursorY, 
                target.x == cursorPos.x || target.x == cursorPos.x + 1);

            // Find the corresponding timeline entry and add marker
            if (_columnTimelines.TryGetValue(target.x, out var entries))
            {
                var entry = entries.FirstOrDefault(e => e.TileRef == tile);
                if (entry != null)
                {
                    var marker = CreateLastSwapMarker(entry, progress, outcome);
                    _lastSwapMarkers[target.x].Add(marker);
                }
            }
        }
    }

    private LastSwapMarker CreateLastSwapMarker(TimelineEntry entry, float progress, BlockSlipOutcome outcome)
    {
        var marker = new LastSwapMarker
        {
            NormalizedPosition = progress,
            Outcome = outcome
        };

        var markerObj = new GameObject("LastSwapMarker");
        markerObj.transform.SetParent(entry.Container.transform, false);
        
        var rect = markerObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0.5f, 0.5f);
        
        var containerRect = entry.Container.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(containerRect.rect.width * progress, 0);
        rect.sizeDelta = new Vector2(2, -4);

        marker.MarkerImage = markerObj.AddComponent<Image>();
        marker.MarkerImage.color = lastSwapMarkerColor;

        return marker;
    }

    #endregion

    #region Reflection Helpers

    private HashSet<GameObject> GetDroppingTiles()
    {
        if (_droppingTilesField == null || blockSlipManager == null) return null;
        return _droppingTilesField.GetValue(blockSlipManager) as HashSet<GameObject>;
    }

    private Dictionary<GameObject, Vector2Int> GetDropTargets()
    {
        if (_dropTargetsField == null || blockSlipManager == null) 
            return new Dictionary<GameObject, Vector2Int>();
        return _dropTargetsField.GetValue(blockSlipManager) as Dictionary<GameObject, Vector2Int> 
            ?? new Dictionary<GameObject, Vector2Int>();
    }

    private Dictionary<GameObject, float> GetDropProgress()
    {
        if (_dropProgressField == null || blockSlipManager == null) 
            return new Dictionary<GameObject, float>();
        return _dropProgressField.GetValue(blockSlipManager) as Dictionary<GameObject, float> 
            ?? new Dictionary<GameObject, float>();
    }

    #endregion
}