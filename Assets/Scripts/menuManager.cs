using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using System.Threading.Tasks;

public class MenuManager : MonoBehaviour
{
    // Singleton pattern
    public static MenuManager Instance { get; private set; }

    // Reference other scripts via CoreManager
    private CoreManager coreManagerInstance;
    private MenuUIManager menuUIManagerInstance;
    private LobbyManager lobbyManagerInstance;

    // Private Variables only this script accesses
    private string playerName;
    

    // Awake is ran when script is created - before Start
    private void Awake()
    {
        // Set Singleton Pattern
        // If theres an instance already which is not this one 
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject); // destroy this one to prevent duplicates
            return;
        }

        // Else, there is no other instance so set this as the instance
        Instance = this;

    }

    // Start is called before first frame update, after Awake
    private void Start()
    {
        // Assign manager instances
        coreManagerInstance = CoreManager.Instance;
        menuUIManagerInstance = coreManagerInstance.menuUIManagerInstance;
        lobbyManagerInstance = coreManagerInstance.lobbyManagerInstance;

        // Always show sign-in screen first
        menuUIManagerInstance.ShowSigninScreen();
        
        Debug.Log("Showing sign-in screen. User must sign in manually.");
    }

    public async Task Authenticate(string playerName)
    {
    try
        {
            bool isInitialized = await CoreManager.Instance.InitializeUnityServices(playerName);
            if (!isInitialized)
            {
                Debug.LogError("Authentication aborted: Unity Services failed to initialize.");
                return;
            }

            AuthenticationService.Instance.SignedIn += async () =>
            {
                Debug.Log($"Signed in as {AuthenticationService.Instance.PlayerId}");
                await lobbyManagerInstance.SearchAndRefreshLobbies(); // Refresh lobbies after sign-in
            };

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"Authentication complete for player: {playerName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Authentication failed: {e.Message}");
            throw;
        }
    }

    // Start game as host
    public async void StartGame()
    {
        if (lobbyManagerInstance.IsHost)
        {

            Debug.Log($"Starting game check - Connected clients count: {NetworkManager.Singleton.ConnectedClients.Count}");
            Debug.Log($"Connected client IDs count: {NetworkManager.Singleton.ConnectedClientsIds.Count}");

            // Print all connected clients for debugging
            foreach (var client in NetworkManager.Singleton.ConnectedClients)
            {
                Debug.Log($"Connected client: ID={client.Key}, IsConnected={client.Value != null}");
            }


            // Allow starting even with just the host (client count would be 1)
            if (NetworkManager.Singleton.ConnectedClients.Count >= 1)
            {
                Debug.Log("Starting game as host");

                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
                {
                    // Small delay to ensure connections are fully established
                    await Task.Delay(1000);
                    
                    Debug.Log($"Final check - Connected clients: {NetworkManager.Singleton.ConnectedClientsIds.Count}");
                    foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
                    {
                        Debug.Log($"Client connected: {clientId}");
                    }

                    StartGameServerRpc();
                }
                else 
                {
                     Debug.LogError("Network not initialized as host! Cannot start game.");
                }
            }
            else 
            {
                Debug.LogError($"No clients or only host connected. Count: {NetworkManager.Singleton.ConnectedClientsIds.Count}");
            }
        }
        else 
        {
            Debug.LogError("Cannot start game: not a host");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void StartGameServerRpc()
    {
        if (!NetworkManager.Singleton.IsHost) return;  // Only the host can load the scene

        Debug.Log("Host is loading BallArena scene...");
        NetworkManager.Singleton.SceneManager.LoadScene("BallArena", LoadSceneMode.Single);
    }

    public void StartStressTest()
    {
        if (lobbyManagerInstance.IsHost)
        {
            Debug.Log("Starting stress test as host!");
            
            // Make sure NetworkManager is already set up
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                // Load the game scene
                NetworkManager.Singleton.SceneManager.LoadScene("NetworkStressTest", LoadSceneMode.Single);
                Debug.Log("Loading scene: NetworkStressTest");
            }
            else
            {
                Debug.LogError("Network not initialized as host! Cannot start stress test.");
            }
        }
        else
        {
            Debug.LogError("Cannot start stress test: not a host.");
        }
    }
}