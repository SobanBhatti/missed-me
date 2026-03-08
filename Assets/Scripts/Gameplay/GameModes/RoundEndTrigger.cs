using UnityEngine;
using Session.Core;

namespace Gameplay.GameModes
{
    public class RoundEndTrigger : MonoBehaviour
    {
        public void EndRound()
        {
            if (MatchSessionManager.Instance == null)
            {
                Debug.LogError("RoundEndTrigger: MatchSessionManager not found.");
                return;
            }

            MatchSessionManager.Instance.LoadNextRound();
        }
    }
}