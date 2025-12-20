// Interface for cursor commands from Players and AIs

namespace PuzzleAttack.Grid
{
    public interface ICursorCommands
    {
        // Movement
        void MoveLeft();
        void MoveRight();
        void MoveUp();
        void MoveDown();

        // Grid Manipulators
        void Swap();
        void FastRiseGrid();
    }
}