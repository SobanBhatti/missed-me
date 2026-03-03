using UnityEngine;

namespace Core.GameState
{
    public class LobbyUI : MonoBehaviour
    {
        public void OnStartMatchClicked()
        {
            if (GameStateController.Instance != null)
            {
                GameStateController.Instance.StartGame();
            }
        }
    }
}