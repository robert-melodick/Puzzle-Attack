#if DEBUG

using UnityEngine;

public class DebugTimeScaler : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Minus))   // - key
            Time.timeScale = 0.1f;            // slow motion
        if (Input.GetKeyDown(KeyCode.Equals)) // = key
            Time.timeScale = 1f;              // normal
        if (Input.GetKeyDown(KeyCode.Alpha0))
            Time.timeScale = 0f;              // freeze
    }
}

#endif