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

        if (lobbyIdText != null)
            lobbyIdText.text = $"Lobby ID: {net.CurrentLobby.Id}";
    }
}