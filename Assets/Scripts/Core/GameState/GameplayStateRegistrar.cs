using UnityEngine;

namespace Core.GameState
{
    public class GameplayStateRegistrar : MonoBehaviour
    {
        private void Start()
        {
            if (GameStateController.Instance != null)
            {
                GameStateController.Instance.SetState(GameStateType.Gameplay);
            }
        }
    }
}