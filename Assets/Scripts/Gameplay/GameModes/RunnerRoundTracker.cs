using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Session.Core;

namespace Gameplay.GameModes
{
    public class RunnerRoundTracker : NetworkBehaviour
    {
        private readonly HashSet<ulong> fallenRunners = new();

        private int totalRunners;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            totalRunners = CountRunners();
        }

        private int CountRunners()
        {
            int count = 0;

            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (LobbySlotManager.Instance.GetTeamForClient(client.ClientId) == 1)
                    count++;
            }

            return count;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void NotifyRunnerFellServerRpc(ulong clientId)
        {
            if (fallenRunners.Contains(clientId))
                return;

            fallenRunners.Add(clientId);

            Debug.Log($"Runner fell: {fallenRunners.Count}/{totalRunners}");

            if (fallenRunners.Count >= totalRunners)
            {
                Debug.Log("All runners fell. Ending round.");

                MatchSessionManager.Instance.LoadNextRound();
            }
        }
    }
}