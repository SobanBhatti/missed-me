using UnityEngine;

namespace Core.GameState
{
    public class LobbyStateRegistrar : MonoBehaviour
    {
        private void Start()
        {
            if (GameStateController.Instance != null)
            {
                GameStateController.Instance.SetState(GameStateType.Lobby);
            }
        }
    }
}