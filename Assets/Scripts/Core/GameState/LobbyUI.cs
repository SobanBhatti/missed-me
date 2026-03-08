using UnityEngine;
using Unity.Netcode;
using Session.Core;

namespace Core.GameState
{
    public class LobbyUI : MonoBehaviour
    {
        public void OnStartMatchClicked()
        {
            var networkManager = NetworkManager.Singleton;

            // Only the host/server can start the match
            if (networkManager != null && !networkManager.IsServer)
            {
                Debug.LogWarning("LobbyUI: Only the host can start the match.");
                return;
            }

            if (MatchSessionManager.Instance != null)
            {
                MatchSessionManager.Instance.StartMatch();
            }
            else
            {
                Debug.LogError("LobbyUI: MatchSessionManager not found.");
            }
        }
    }
}