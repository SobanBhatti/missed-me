using TMPro;
using UnityEngine;

public class LobbyLobbyIdDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text lobbyIdText;

    private void OnEnable()
    {
        var net = FindFirstObjectByType<RelayLobbyNetwork>();
        if (net == null || net.CurrentLobby == null)
        {
            if (lobbyIdText != null)
                lobbyIdText.text = "Lobby: (not connected)";
            return;
        }

        // Display the short 6-digit code if available, otherwise fall back to full lobby ID
        string displayCode = net.ShortJoinCode;
        if (string.IsNullOrEmpty(displayCode) && net.CurrentLobby.Data != null)
        {
            // Try to get short code from lobby data (for clients who joined)
            if (net.CurrentLobby.Data.TryGetValue("shortJoinCode", out var shortCodeObj))
            {
                displayCode = shortCodeObj.Value;
            }
        }
        
        if (lobbyIdText != null)
        {
            if (!string.IsNullOrEmpty(displayCode))
                lobbyIdText.text = $"Join Code: {displayCode}";
            else
                lobbyIdText.text = $"Lobby ID: {net.CurrentLobby.Id}";
        }
    }
}