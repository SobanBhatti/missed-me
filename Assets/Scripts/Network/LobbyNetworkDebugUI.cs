using Core.GameState;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyNetworkDebugUI : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;

    [Tooltip("Lobby Id to join (paste from host log).")]
    [SerializeField] private TMP_InputField lobbyIdInput;

    private RelayLobbyNetwork _net;

    private void Awake()
    {
        _net = FindFirstObjectByType<RelayLobbyNetwork>();
        if (_net == null)
        {
            Debug.LogError("LobbyNetworkDebugUI: RelayLobbyNetwork not found. Ensure it's on NetworkManager in Bootstrap.");
            enabled = false;
            return;
        }

        if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
        if (joinButton != null) joinButton.onClick.AddListener(OnJoinClicked);
    }

    private async void OnHostClicked()
    {
        try
        {
            SetInteractable(false);
            await _net.HostAsync();

            // Use your existing flow: MainMenu -> Lobby
            GameStateController.Instance.SetState(GameStateType.Lobby);
        }
        catch (Exception e)
        {
            Debug.LogError($"Host failed: {e.Message}");
            SetInteractable(true);
        }
    }

    private async void OnJoinClicked()
    {
        try
        {
            string lobbyId = lobbyIdInput != null ? lobbyIdInput.text : string.Empty;
            if (string.IsNullOrWhiteSpace(lobbyId))
            {
                Debug.LogError("Join failed: Lobby Id is empty.");
                return;
            }

            SetInteractable(false);
            await _net.JoinAsync(lobbyId.Trim());

            // Use your existing flow: MainMenu -> Lobby
            GameStateController.Instance.SetState(GameStateType.Lobby);
        }
        catch (Exception e)
        {
            Debug.LogError($"Join failed: {e.Message}");
            SetInteractable(true);
        }
    }

    private void SetInteractable(bool value)
    {
        if (hostButton != null) hostButton.interactable = value;
        if (joinButton != null) joinButton.interactable = value;
        if (lobbyIdInput != null) lobbyIdInput.interactable = value;
    }
}