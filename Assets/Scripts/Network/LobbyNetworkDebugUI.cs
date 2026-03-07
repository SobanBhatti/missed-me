using Core.GameState;
using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyNetworkDebugUI : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;

    [Tooltip("Lobby Id or 6-digit join code to join.")]
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
            string input = lobbyIdInput != null ? lobbyIdInput.text.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                Debug.LogError("Join failed: Join code is empty.");
                return;
            }

            SetInteractable(false);
            
            // If input is 6 digits, try joining by short code first
            if (input.Length == 6 && Regex.IsMatch(input, @"^\d{6}$"))
            {
                try
                {
                    await _net.JoinByShortCodeAsync(input);
                }
                catch
                {
                    // If short code join fails, try as regular lobby ID
                    await _net.JoinAsync(input);
                }
            }
            else
            {
                // Try as regular lobby ID
                await _net.JoinAsync(input);
            }

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