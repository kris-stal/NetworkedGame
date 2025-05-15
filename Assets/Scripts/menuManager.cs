using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Authentication;
using System.Threading.Tasks;

// Manager for functionality in the menu
// Handles launching games
public class MenuManager : NetworkBehaviour
{
    // REFERENCES
    // Singleton pattern
    public static MenuManager Instance { get; private set; }

    // Reference other scripts
    private CoreManager coreManagerInstance;
    private PingManager pingManagerInstance;
    private LobbyManager lobbyManagerInstance;
    private MenuUIManager menuUIManagerInstance;
    private GameUIManager gameUIManagerInstance;

    

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
        // Assign script references
        coreManagerInstance = CoreManager.Instance;
        pingManagerInstance = coreManagerInstance.pingManagerInstance;
        menuUIManagerInstance = coreManagerInstance.menuUIManagerInstance;
        lobbyManagerInstance = coreManagerInstance.lobbyManagerInstance;
        gameUIManagerInstance = coreManagerInstance.gameUIManagerInstance;

        // Always show sign-in screen first
        menuUIManagerInstance.ShowSigninScreen();
    }



    // AUTHENTICATION //
    public async Task Authenticate(string playerName)
    {
    try
        {
            bool isInitialized = await coreManagerInstance.InitializeUnityServices(playerName);
            if (!isInitialized)
            {
                Debug.LogError("Authentication aborted: Unity Services failed to initialize.");
                return;
            }

            AuthenticationService.Instance.SignedIn +=  () =>
            {
                Debug.Log($"Signed in as {AuthenticationService.Instance.PlayerId}");
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



    // STARTING GAME //
    // Launch game
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
        if (!NetworkManager.Singleton.IsHost) return;

        Debug.Log("Host is loading BallArena scene...");
        NetworkManager.Singleton.SceneManager.LoadScene("BallArena", LoadSceneMode.Single);

        // Ensure PingManager is active
        if (pingManagerInstance == null)
        {
            Debug.LogError("PingManager is not active in the game scene!");
        }

        // Ensure GameUIManager is ready
        if (gameUIManagerInstance != null)
        {
            gameUIManagerInstance.PopulatePlayerList();
        }
    }


    // Launch network stress test
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

    [ServerRpc(RequireOwnership = false)]
    public void StartStressTestRpcServerRpc()
    {
        if (!NetworkManager.Singleton.IsHost) return;  // Only the host can load the scene

        Debug.Log("Host is loading Network Stress Test scene...");
        NetworkManager.Singleton.SceneManager.LoadScene("NetworkStressTest", LoadSceneMode.Single);
    }
}