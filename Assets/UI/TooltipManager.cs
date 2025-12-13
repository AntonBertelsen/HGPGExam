using UnityEngine;
using TMPro;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance;
    public TextMeshProUGUI infoText;
    public GameObject infoPanel;

    void Awake()
    {
        Instance = this;
        HideTooltip(); // Start hidden
    }

    public void ShowTooltip(string text)
    {
        infoPanel.SetActive(true);
        infoText.text = text;
    }

    public void HideTooltip()
    {
        infoPanel.SetActive(false);
    }
}