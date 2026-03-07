using UnityEngine;
using Unity.Netcode;

namespace Core.GameState
{
    public class LobbyUI : MonoBehaviour
    {
        public void OnStartMatchClicked()
        {
            // Only allow the host/server to start the game
            var networkManager = NetworkManager.Singleton;
            if (networkManager != null && !networkManager.IsServer)
            {
                Debug.LogWarning("LobbyUI: Only the host can start the match.");
                return;
            }

            if (GameStateController.Instance != null)
            {
                GameStateController.Instance.StartGame();
            }
        }
    }
}