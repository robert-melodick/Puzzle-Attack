using TMPro;
using UnityEngine;

public class VersionDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text versionText;

    private void Awake()
    {
        if (versionText == null)
            versionText = GetComponent<TMP_Text>();

        versionText.text = $"Version {Application.version}";
    }
}