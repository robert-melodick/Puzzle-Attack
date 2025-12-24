using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Configuration settings for garbage block conversion behavior.
    /// Create instances via Assets > Create > PuzzleAttack > Garbage Conversion Settings
    /// </summary>
    [CreateAssetMenu(fileName = "GarbageConversionSettings", menuName = "PuzzleAttack/Garbage Conversion Settings")]
    public class GarbageConversionSettings : ScriptableObject
    {
        [Header("Timing")]
        [Tooltip("Delay between each tile spawning within a row")]
        [Range(0.01f, 3f)]
        public float timeBetweenTileSpawns = 0.05f;

        [Tooltip("Delay between converting each row")]
        [Range(0.1f, 3f)]
        public float timeBetweenRowConversions = 0.3f;

        [Tooltip("Duration of the scan/highlight effect traveling up the garbage")]
        [Range(0f, 3f)]
        public float scanEffectDuration = 0.5f;

        [Tooltip("Delay before starting conversion after being triggered")]
        [Range(0f, 3f)]
        public float conversionStartDelay = 0.1f;

        [Header("Conversion Rules")]
        [Tooltip("Number of rows converted per adjacent match")]
        [Range(1, 6)]
        public int rowsPerMatch = 1;

        [Tooltip("If true, scan effect plays through entire height even if only converting partial rows")]
        public bool iterateThroughEmptySpace = true;

        [Tooltip("If true, garbage blocks sharing edges will also begin conversion")]
        public bool propagateToCluster = true;

        [Tooltip("If true, cluster converts bottom-to-top sequentially. If false, all convert simultaneously")]
        public bool clusterConvertsSequentially = true;

        [Header("Tile Spawn Order")]
        [Tooltip("Order in which tiles spawn within a converting row")]
        public GarbageBlock.ConversionOrder tileSpawnOrder = GarbageBlock.ConversionOrder.LeftToRight;

        [Header("Visual Effects")]
        [Tooltip("Enable the scanning highlight effect during conversion")]
        public bool useScanEffect = true;

        [Tooltip("Color of the scan highlight")]
        public Color scanHighlightColor = Color.white;

        [Tooltip("Intensity of the scan highlight (0-1)")]
        [Range(0f, 1f)]
        public float scanHighlightIntensity = 0.5f;

        [Tooltip("Flash color when conversion starts")]
        public Color conversionStartFlash = new Color(1f, 1f, 1f, 0.8f);

        [Tooltip("Duration of the start flash")]
        [Range(0f, 0.5f)]
        public float conversionStartFlashDuration = 0.1f;

        [Header("Behavior")]
        [Tooltip("If true, converted tiles are held in place until entire conversion sequence completes")]
        public bool holdTilesUntilComplete = true;

        [Tooltip("If true, garbage block also stays afloat during conversion")]
        public bool holdGarbageDuringConversion = true;

        [Header("Audio")]
        [Tooltip("Sound played when scan effect passes a row")]
        public AudioClip scanTickSound;

        [Tooltip("Sound played when a tile is spawned from conversion")]
        public AudioClip tileSpawnSound;

        [Tooltip("Sound played when conversion sequence completes")]
        public AudioClip conversionCompleteSound;

        /// <summary>
        /// Calculate total time for converting a garbage block of given height.
        /// </summary>
        public float CalculateConversionTime(int height, int rowsToConvert)
        {
            var actualRows = Mathf.Min(rowsToConvert, height);
            var scanTime = useScanEffect ? scanEffectDuration : 0f;
            
            if (iterateThroughEmptySpace)
            {
                scanTime = useScanEffect ? (scanEffectDuration * height / Mathf.Max(1, actualRows)) : 0f;
            }

            // Time = scan + (rows * (tiles_per_row * tile_delay + row_delay))
            // Assuming average width of 6 for estimate
            var estimatedWidth = 6f;
            var rowTime = estimatedWidth * timeBetweenTileSpawns + timeBetweenRowConversions;
            
            return conversionStartDelay + scanTime + (actualRows * rowTime);
        }

        /// <summary>
        /// Get default settings.
        /// </summary>
        public static GarbageConversionSettings GetDefault()
        {
            var settings = CreateInstance<GarbageConversionSettings>();
            // All defaults are set via field initializers
            return settings;
        }
    }
}