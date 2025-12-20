using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Core tile component. Holds grid position, type, movement state, and status effects.
    /// </summary>
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

        // Special tile flags
        public bool IsGarbage { get; private set; }
        public int GarbageWidth { get; private set; } = 1;
        public int GarbageHeight { get; private set; } = 1;

        #endregion

        #region Private Fields

        private GridManager _gridManager;
        private AudioSource _audioSource;
        private GameObject _statusVisual;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
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
        }

        public void InitializeAsGarbage(int x, int y, int width, int height, GridManager manager)
        {
            GridX = x;
            GridY = y;
            TileType = -1; // Garbage uses special type
            _gridManager = manager;
            IsGarbage = true;
            GarbageWidth = width;
            GarbageHeight = height;
            TargetGridPos = new Vector2Int(x, y);
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
            if (IsGarbage) return false;
            
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
            if (IsGarbage) return false;
            
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
            
            // Garbage blocks convert to normal tiles when matched adjacent
            if (IsGarbage)
            {
                ConvertFromGarbage();
            }
        }

        private void ConvertFromGarbage()
        {
            IsGarbage = false;
            GarbageWidth = 1;
            GarbageHeight = 1;
            // Type will be assigned by the system that handles garbage conversion
        }

        private void UpdateStatusVisual()
        {
            // Status visual implementation - overlay sprite or particle effect
            // Placeholder for visual feedback system
        }

        private void ClearStatusVisual()
        {
            if (_statusVisual != null)
            {
                Destroy(_statusVisual);
                _statusVisual = null;
            }
        }

        #endregion

        #region Audio

        public void PlayLandSound()
        {
            if (landSound != null && _audioSource != null)
                _audioSource.PlayOneShot(landSound);
        }

        public void PlayMatchSound(int combo = 1)
        {
            if (matchSound == null || _audioSource == null) return;
            
            var pitch = Mathf.Clamp(1.0f + (combo - 1) * 0.1f, 0.5f, 2.0f);
            _audioSource.pitch = pitch;
            _audioSource.PlayOneShot(matchSound);
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