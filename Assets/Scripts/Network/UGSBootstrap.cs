using System.Threading.Tasks;
using UnityEngine;

using Unity.Services.Core;
using Unity.Services.Authentication;

public class UGSBootstrap : MonoBehaviour
{
    public static bool IsReady { get; private set; }
    public static string PlayerId => AuthenticationService.Instance?.PlayerId;

    private async void Awake()
    {
        // Ensure only one exists (Bootstrap scene persists)
        var existing = FindObjectsByType<UGSBootstrap>(FindObjectsSortMode.None);
        if (existing.Length > 1)
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
        await InitializeAsync();
    }

    private static async Task InitializeAsync()
    {
        if (IsReady) return;

        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();

        IsReady = true;
        Debug.Log($"UGS ready. PlayerId={PlayerId}");
    }
}