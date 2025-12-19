using UnityEngine;

namespace PuzzleAttack.Grid
{
    public class Tile : MonoBehaviour
    {
        #region Enums

        public enum MovementState
        {
            Idle, // Stationary in grid
            Swapping, // Horizontal swap animation
            Falling // Falling downward
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        #endregion

        #region Properties

        public int GridX { get; private set; }
        public int GridY { get; private set; }
        public int TileType { get; private set; }

        // Movement state - accessible by BlockSlipManager / GridManager
        public MovementState State { get; set; } = MovementState.Idle;
        public Vector2Int TargetGridPos { get; set; }
        public int AnimationVersion { get; set; }

        // Convenience property
        public bool IsBusy => State != MovementState.Idle;

        // Status Effect properties
        public StatusEffect CurrentEffect { get; private set; }
        public bool HasStatusEffect => CurrentEffect != null;

        #endregion

        #region Private Fields

        private GridManager _gridManager;

        [Header("Sound Effects")] 
        public AudioClip landSound;
        public AudioClip matchSound;

        private AudioSource _audioSource;

        // Status effect tracking
        private float _effectTimer;
        private GameObject _currentEffectVisual;

        #endregion

        #region Public Methods

        public void Initialize(int x, int y, int type, GridManager manager)
        {
            GridX = x;
            GridY = y;
            TileType = type;
            _gridManager = manager;
            TargetGridPos = new Vector2Int(x, y);
        }

        /// <summary>
        ///     Call when this tile starts falling
        /// </summary>
        public void StartFalling(Vector2Int target)
        {
            State = MovementState.Falling;
            TargetGridPos = target;
            AnimationVersion++;
        }

        /// <summary>
        ///     Call when this tile starts a swap
        /// </summary>
        public void StartSwapping(Vector2Int target)
        {
            State = MovementState.Swapping;
            TargetGridPos = target;
            AnimationVersion++;
        }

        /// <summary>
        ///     Call when movement completes
        /// </summary>
        public void FinishMovement()
        {
            State = MovementState.Idle;
        }

        /// <summary>
        ///     Update the fall target (for BlockSlip retargeting)
        /// </summary>
        public void RetargetFall(Vector2Int newTarget)
        {
            TargetGridPos = newTarget;
            // Don't change state, just where we're heading
        }

        public void PlayLandSound()
        {
            if (landSound != null && _audioSource != null) 
                _audioSource.PlayOneShot(landSound);
        }

        public void PlayMatchSound(int combo = 1)
        {
            if (matchSound != null && _audioSource != null)
            {
                var pitch = 1.0f + (combo - 1) * 0.1f;
                pitch = Mathf.Clamp(pitch, 0.5f, 2.0f);
                _audioSource.pitch = pitch;
                _audioSource.PlayOneShot(matchSound);
            }
        }

        #endregion

        
    }
}