using UnityEngine;

namespace Core.GameState
{
    public class MainMenuUI : MonoBehaviour
    {
        public void OnStartClicked()
        {
            if (GameStateController.Instance != null)
            {
                GameStateController.Instance.GoToLobby();
            }
        }
    }
}