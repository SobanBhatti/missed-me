using UnityEngine;

namespace Core.GameState
{
    public class MainMenuStateRegistrar : MonoBehaviour
    {
        private void Start()
        {
            if (GameStateController.Instance != null)
            {
                GameStateController.Instance.SetState(GameStateType.MainMenu);
            }
        }
    }
}