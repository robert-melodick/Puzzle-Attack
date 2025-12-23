using UnityEngine;

namespace PuzzleAttack.Grid
{
    /// <summary>
    /// Lightweight reference placed in grid cells occupied by garbage blocks.
    /// Points back to the owning GarbageBlock for collision and logic checks.
    /// The anchor cell (bottom-left) contains the actual GarbageBlock component.
    /// Other cells contain only this reference.
    /// </summary>
    public class GarbageReference : MonoBehaviour
    {
        /// <summary>
        /// The garbage block that owns this cell.
        /// </summary>
        public GarbageBlock Owner { get; private set; }

        /// <summary>
        /// This cell's position relative to the garbage anchor (0,0 = anchor/bottom-left).
        /// </summary>
        public Vector2Int LocalOffset { get; private set; }

        /// <summary>
        /// The absolute grid position of this reference.
        /// </summary>
        public Vector2Int GridPosition => Owner != null 
            ? Owner.AnchorPosition + LocalOffset 
            : Vector2Int.zero;

        /// <summary>
        /// Whether this reference is at the anchor position (contains the actual GarbageBlock).
        /// </summary>
        public bool IsAnchor => LocalOffset == Vector2Int.zero;

        /// <summary>
        /// Initialize this reference to point to a garbage block.
        /// </summary>
        public void Initialize(GarbageBlock owner, Vector2Int localOffset)
        {
            Owner = owner;
            LocalOffset = localOffset;
        }

        /// <summary>
        /// Update the local offset (used when garbage shrinks during conversion).
        /// </summary>
        public void UpdateLocalOffset(Vector2Int newOffset)
        {
            LocalOffset = newOffset;
        }

        /// <summary>
        /// Check if the owning garbage block is in a specific state.
        /// </summary>
        public bool IsOwnerFalling => Owner != null && Owner.IsFalling;
        public bool IsOwnerConverting => Owner != null && Owner.IsConverting;
        public bool IsOwnerSettled => Owner != null && Owner.IsSettled;
    }
}