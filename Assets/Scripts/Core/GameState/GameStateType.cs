namespace Core.GameState
{
    public enum GameStateType
    {
        None = 0,

        Boot,
        MainMenu,
        Lobby,
        LoadingGame,
        Gameplay,
        ReturningToMenu
    }
}