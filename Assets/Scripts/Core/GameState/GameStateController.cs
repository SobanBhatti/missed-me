using UnityEngine;
using Core.SceneManagement;

namespace Core.GameState
{
    public class GameStateController : MonoBehaviour
    {
        public static GameStateController Instance { get; private set; }

        [field: SerializeField]
        public GameStateType CurrentState { get; private set; } = GameStateType.None;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            SetState(GameStateType.Boot);
        }

        public void SetState(GameStateType newState)
        {
            if (newState == CurrentState)
                return;

            CurrentState = newState;

            HandleStateChanged(newState);
        }

        public void GoToLobby()
        {
            SetState(GameStateType.Lobby);
        }

        public void StartGame()
        {
            SetState(GameStateType.LoadingGame);
        }

        public void ReturnToMainMenu()
        {
            SetState(GameStateType.ReturningToMenu);
        }

        private void HandleStateChanged(GameStateType state)
        {
            switch (state)
            {
                case GameStateType.Boot:
                    SceneLoader.Instance.LoadScene(SceneNames.MainMenu);
                    break;

                case GameStateType.MainMenu:
                    break;

                case GameStateType.Lobby:
                    SceneLoader.Instance.LoadScene(SceneNames.Lobby);
                    break;

                case GameStateType.LoadingGame:
                    SceneLoader.Instance.LoadScene(SceneNames.Gameplay);
                    break;

                case GameStateType.ReturningToMenu:
                    SceneLoader.Instance.LoadScene(SceneNames.MainMenu);
                    break;

                case GameStateType.Gameplay:
                    break;
            }
        }
    }
}