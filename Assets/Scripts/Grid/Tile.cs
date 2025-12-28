using Unity.VisualScripting;
using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Core tile component. Holds grid position, type, movement state, and status effects.
    /// Uses SpriteEffects2D for visual feedback.
    /// Note: Garbage blocks are now handled by GarbageBlock component, not Tile.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Tile : MonoBehaviour
    {
        public enum MovementState { Idle, Swapping, Falling }

        #region Inspector Fields

        [Header("Sound Effects")]
        public AudioClip landSound;
        public AudioClip matchSound;

        #endregion

        #region Properties

        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public int TileType { get; private set; }

        public MovementState State { get; set; } = MovementState.Idle;
        public Vector2Int TargetGridPos { get; set; }
        public int AnimationVersion { get; set; }

        public bool IsBusy => State != MovementState.Idle;
        public bool IsIdle => State == MovementState.Idle;
        public bool IsFalling => State == MovementState.Falling;
        public bool IsSwapping => State == MovementState.Swapping;

        // Status effects
        public TileStatus CurrentStatus { get; private set; } = TileStatus.None;
        public bool HasStatus => CurrentStatus != TileStatus.None;
        public float StatusTimer { get; private set; }

        #endregion

        #region Private Fields

        private GridManager _gridManager;
        private AudioSource _audioSource;
        private SpriteEffects2D _effects;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            
            // Add SpriteEffects2D if not present
            _effects = GetComponent<SpriteEffects2D>();
            if (_effects == null)
            {
                _effects = gameObject.AddComponent<SpriteEffects2D>();
            }
        }

        #endregion

        #region Initialization

        public void Initialize(int x, int y, int type, GridManager manager)
        {
            GridX = x;
            GridY = y;
            TileType = type;
            _gridManager = manager;
            TargetGridPos = new Vector2Int(x, y);
            DebugRename();
        }

        #endregion

        #region Movement Control

        public void StartFalling(Vector2Int target)
        {
            State = MovementState.Falling;
            TargetGridPos = target;
            AnimationVersion++;
        }

        public void StartSwapping(Vector2Int target)
        {
            State = MovementState.Swapping;
            TargetGridPos = target;
            AnimationVersion++;
        }

        public void FinishMovement()
        {
            State = MovementState.Idle;
            DebugRename();
        }

        public void RetargetFall(Vector2Int newTarget)
        {
            TargetGridPos = newTarget;
        }

        #endregion

        #region Status Effects

        /// <summary>
        /// Returns true if this tile can be swapped given its current status.
        /// </summary>
        public bool CanSwap()
        {
            return CurrentStatus switch
            {
                TileStatus.Frozen => false,
                TileStatus.Locked => false,
                _ => true
            };
        }

        /// <summary>
        /// Returns true if this tile can be matched given its current status.
        /// </summary>
        public bool CanMatch()
        {
            return CurrentStatus switch
            {
                TileStatus.Burning => false, // Must be cured first
                TileStatus.Locked => false,
                _ => true
            };
        }

        /// <summary>
        /// Returns true if this tile should continue moving horizontally after a swap.
        /// </summary>
        public bool HasMomentum()
        {
            return CurrentStatus == TileStatus.Frozen;
        }

        public void ApplyStatus(TileStatus status, float duration = 0f)
        {
            CurrentStatus = status;
            StatusTimer = duration;
            UpdateStatusVisual();
        }

        public void ClearStatus()
        {
            CurrentStatus = TileStatus.None;
            StatusTimer = 0f;
            ClearStatusVisual();
        }

        public void TickStatus(float deltaTime)
        {
            if (!HasStatus || StatusTimer <= 0f) return;
            
            StatusTimer -= deltaTime;
            if (StatusTimer <= 0f)
            {
                ClearStatus();
            }
        }

        /// <summary>
        /// Called when a match occurs adjacent to this tile. Used to cure burning blocks.
        /// </summary>
        public void OnAdjacentMatch()
        {
            if (CurrentStatus == TileStatus.Burning)
            {
                ClearStatus();
            }
        }

        private void UpdateStatusVisual()
        {
            if (_effects == null) return;

            switch (CurrentStatus)
            {
                case TileStatus.Frozen:
                    _effects.SetTint(new Color(0.5f, 0.8f, 1f, 1f), 0.4f, SpriteEffects2D.TintMode.Multiply);
                    _effects.SetWobble(true, 0.003f); // Subtle shimmer
                    break;
                    
                case TileStatus.Burning:
                    _effects.SetTint(new Color(1f, 0.3f, 0f, 1f), 0.5f, SpriteEffects2D.TintMode.Additive);
                    _effects.SetWobble(true, 0.005f); // Fire flicker
                    break;
                    
                case TileStatus.Poisoned:
                    _effects.SetTint(new Color(0.4f, 0.8f, 0.2f, 1f), 0.4f, SpriteEffects2D.TintMode.Multiply);
                    _effects.SetWobble(true, 0.002f);
                    break;
                    
                case TileStatus.Locked:
                    _effects.SetTint(new Color(0.5f, 0.5f, 0.5f, 1f), 0.6f, SpriteEffects2D.TintMode.Multiply);
                    _effects.SetSaturation(0.3f); // Desaturate
                    break;
                    
                case TileStatus.Charged:
                    _effects.SetTint(new Color(1f, 1f, 0.5f, 1f), 0.3f, SpriteEffects2D.TintMode.Additive);
                    _effects.SetWobble(true, 0.004f);
                    break;
                    
                case TileStatus.None:
                default:
                    ClearStatusVisual();
                    break;
            }
        }

        private void ClearStatusVisual()
        {
            if (_effects == null) return;
            
            _effects.ClearTint();
            _effects.ClearWave();
            _effects.SetSaturation(1f);
        }

        #endregion

        #region Visual Effects (Public API)

        /// <summary>
        /// Get the SpriteEffects2D component for external effect control.
        /// </summary>
        public SpriteEffects2D Effects => _effects;

        /// <summary>
        /// Apply a flash effect (useful for match feedback).
        /// </summary>
        public void Flash(Color color, float duration = 0.1f)
        {
            _effects?.ApplyFlash(color, duration);
        }

        /// <summary>
        /// Set highlight state (for cursor selection, etc).
        /// </summary>
        public void SetHighlight(bool highlighted, Color highlightColor = default)
        {
            if (_effects == null) return;

            if (highlighted)
            {
                var color = highlightColor == default ? Color.white : highlightColor;
                _effects.SetOutline(true, color, 0.02f);
            }
            else
            {
                _effects.ClearOutline();
            }
        }

        #endregion

        #region Audio

        public void PlayLandSound()
        {
            if (landSound != null && _audioSource != null)
                _audioSource.PlayOneShot(landSound);
        }

        public void PlayMatchSound(int combo = 1, int chain = 1)
        {
            if (matchSound == null || _audioSource == null) return;
            
            var pitch = Mathf.Clamp(1.0f + (combo - 1) * 0.1f, 0.5f, 2.0f);
            _audioSource.pitch = pitch;
            _audioSource.PlayOneShot(matchSound);
        }

        #endregion

        #region Debug
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void DebugRename()
        {
            string tileColor = "Undefined";
            
            if (TileType == 0)
                tileColor = "Red";
            if (TileType == 1)
                tileColor = "Yellow";
            if (TileType == 2)
                tileColor = "Purple";
            if (TileType == 3)
                tileColor = "Grey";
            if (TileType == 4)
                tileColor = "Green";
            if (TileType == 5)
                tileColor = "Blue";
            
            this.name = tileColor + " (" + GridX + ", " + GridY + ")";
        }

        #endregion
        
    }

    /// <summary>
    /// Status effects that can be applied to tiles.
    /// </summary>
    public enum TileStatus
    {
        None,
        Frozen,    // Continues moving horizontally until blocked; cannot be swapped
        Burning,   // Cannot match until cured by adjacent match
        Poisoned,  // Spreads to adjacent tiles over time
        Locked,    // Cannot move or match until unlocked
        Charged    // Will trigger special effect when matched
    }
}