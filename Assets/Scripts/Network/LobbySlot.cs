using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbySlot : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image slotBackground;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private GameObject hostIndicator;

    [Header("Visual Settings")]
    [SerializeField] private Color emptySlotColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
    [SerializeField] private Color occupiedSlotColor = new Color(0.3f, 0.5f, 0.8f, 0.7f);
    [SerializeField] private Color hostSlotColor = new Color(0.8f, 0.6f, 0.2f, 0.7f);

    private void Awake()
    {
        // Initialize as empty slot
        SetPlayer(null, false);
    }

    public void SetPlayer(string playerName, bool isHost)
    {
        if (string.IsNullOrEmpty(playerName))
        {
            // Empty slot
            if (slotBackground != null)
            {
                slotBackground.color = emptySlotColor;
            }
            
            if (playerNameText != null)
            {
                playerNameText.text = "Empty";
                playerNameText.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            }
            
            if (hostIndicator != null)
            {
                hostIndicator.SetActive(false);
            }
        }
        else
        {
            // Occupied slot
            if (slotBackground != null)
            {
                slotBackground.color = isHost ? hostSlotColor : occupiedSlotColor;
            }
            
            if (playerNameText != null)
            {
                playerNameText.text = playerName;
                playerNameText.color = Color.white;
            }
            
            if (hostIndicator != null)
            {
                hostIndicator.SetActive(isHost);
            }
        }
    }
}
