using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayLobbyNetwork : MonoBehaviour
{
    [Header("Lobby")]
    [SerializeField] private string lobbyName = "MissedMeLobby";
    [SerializeField] private int maxPlayers = 8;

    private const string RelayJoinCodeKey = "relayJoinCode";

    private NetworkManager _nm;
    private UnityTransport _utp;

    public Lobby CurrentLobby { get; private set; }

    private void Awake()
    {
        _nm = FindFirstObjectByType<NetworkManager>();
        _utp = FindFirstObjectByType<UnityTransport>();

        if (_nm == null) Debug.LogError("RelayLobbyNetwork: NetworkManager not found in scene.");
        if (_utp == null) Debug.LogError("RelayLobbyNetwork: UnityTransport not found in scene.");
    }

    public async Task HostAsync()
    {
        EnsureUGSReady();

        // 1) Create Relay allocation (host)
        Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

        // 2) Create Lobby and publish relay join code
        var createOptions = new CreateLobbyOptions
        {
            IsPrivate = false,
            Data = new System.Collections.Generic.Dictionary<string, DataObject>
            {
                {
                    RelayJoinCodeKey,
                    new DataObject(DataObject.VisibilityOptions.Public, joinCode)
                }
            }
        };

        CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createOptions);

        // 3) Configure transport to use Relay (host)
        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(alloc, "dtls");
        _utp.SetRelayServerData(relayServerData);

        // 4) Start Host
        bool started = _nm.StartHost();
        if (!started) Debug.LogError("RelayLobbyNetwork: StartHost failed.");
        else Debug.Log($"Host started. LobbyId={CurrentLobby.Id} JoinCode={joinCode}");
    }

    public async Task JoinAsync(string lobbyId)
    {
        EnsureUGSReady();

        // 1) Join Lobby
        CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);

        // 2) Read Relay join code from lobby data
        if (CurrentLobby.Data == null || !CurrentLobby.Data.TryGetValue(RelayJoinCodeKey, out DataObject relayCodeObj))
        {
            Debug.LogError("RelayLobbyNetwork: Lobby missing relayJoinCode data.");
            return;
        }

        string joinCode = relayCodeObj.Value;

        // 3) Join Relay allocation (client)
        JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);

        // 4) Configure transport to use Relay (client)
        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(joinAlloc, "dtls");
        _utp.SetRelayServerData(relayServerData);

        // 5) Start Client
        bool started = _nm.StartClient();
        if (!started) Debug.LogError("RelayLobbyNetwork: StartClient failed.");
        else Debug.Log($"Client started. LobbyId={CurrentLobby.Id}");
    }

    public async Task LeaveLobbyAsync()
    {
        if (CurrentLobby == null) return;

        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, playerId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LeaveLobbyAsync failed: {e.Message}");
        }
        finally
        {
            CurrentLobby = null;
        }
    }

    private static void EnsureUGSReady()
    {
        if (!UGSBootstrap.IsReady)
            throw new InvalidOperationException("UGSBootstrap is not ready yet. Start from Bootstrap and wait for UGS init.");
    }
}