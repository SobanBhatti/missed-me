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
    private const string ShortJoinCodeKey = "shortJoinCode";

    private NetworkManager _nm;
    private UnityTransport _utp;

    public Lobby CurrentLobby { get; private set; }
    public string ShortJoinCode { get; private set; }

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

        // 2) Generate a simple 6-digit join code
        string shortCode = GenerateShortJoinCode();
        ShortJoinCode = shortCode;

        // 3) Create Lobby and publish relay join code and short code
        var createOptions = new CreateLobbyOptions
        {
            IsPrivate = false,
            Data = new System.Collections.Generic.Dictionary<string, DataObject>
            {
                {
                    RelayJoinCodeKey,
                    new DataObject(DataObject.VisibilityOptions.Public, joinCode)
                },
                {
                    ShortJoinCodeKey,
                    new DataObject(DataObject.VisibilityOptions.Public, shortCode)
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
        else Debug.Log($"Host started. LobbyId={CurrentLobby.Id} JoinCode={joinCode} ShortCode={shortCode}");
    }
    
    public async Task JoinByShortCodeAsync(string shortCode)
    {
        EnsureUGSReady();
        
        // Query public lobbies and filter by short join code in data
        var queryLobbiesOptions = new QueryLobbiesOptions
        {
            Count = 25
        };
        
        var queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryLobbiesOptions);
        
        if (queryResponse.Results == null || queryResponse.Results.Count == 0)
        {
            throw new Exception($"No lobbies found. Make sure the host has created a lobby.");
        }
        
        // Find lobby with matching short code
        foreach (var lobby in queryResponse.Results)
        {
            if (lobby.Data != null && 
                lobby.Data.TryGetValue(ShortJoinCodeKey, out var shortCodeObj) &&
                shortCodeObj.Value == shortCode)
            {
                // Found matching lobby, join it
                await JoinAsync(lobby.Id);
                return;
            }
        }
        
        throw new Exception($"No lobby found with join code: {shortCode}");
    }
    
    private string GenerateShortJoinCode()
    {
        // Generate a random 6-digit code (000000-999999)
        System.Random random = new System.Random();
        int code = random.Next(0, 1000000);
        return code.ToString("D6"); // Format as 6 digits with leading zeros
    }

    public async Task JoinAsync(string lobbyId)
    {
        EnsureUGSReady();

        // 1) Join Lobby
        CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);

        // 2) Read short join code from lobby data (for display)
        if (CurrentLobby.Data != null && CurrentLobby.Data.TryGetValue(ShortJoinCodeKey, out DataObject shortCodeObj))
        {
            ShortJoinCode = shortCodeObj.Value;
        }

        // 3) Read Relay join code from lobby data
        if (CurrentLobby.Data == null || !CurrentLobby.Data.TryGetValue(RelayJoinCodeKey, out DataObject relayCodeObj))
        {
            Debug.LogError("RelayLobbyNetwork: Lobby missing relayJoinCode data.");
            return;
        }

        string joinCode = relayCodeObj.Value;

        // 4) Join Relay allocation (client)
        JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);

        // 5) Configure transport to use Relay (client)
        RelayServerData relayServerData = AllocationUtils.ToRelayServerData(joinAlloc, "dtls");
        _utp.SetRelayServerData(relayServerData);

        // 6) Start Client
        bool started = _nm.StartClient();
        if (!started) Debug.LogError("RelayLobbyNetwork: StartClient failed.");
        else Debug.Log($"Client started. LobbyId={CurrentLobby.Id} ShortCode={ShortJoinCode}");
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