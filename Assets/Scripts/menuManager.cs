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
    private CoreManager coreManager;
    private MenuUIManager menuUIManagerInstance;
    private LobbyManager lobbyManagerInstance;
    private GameManager gameManagerInstance;

    // Private Variables only this script accesses
    private string playerName;
    

    // Awake is called first
    private void Awake()
    {
        // Set Singleton Pattern
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Assign manager instances
        coreManager = CoreManager.Instance;
        menuUIManagerInstance = coreManager.menuUIManagerInstance;
        lobbyManagerInstance = coreManager.lobbyManagerInstance;
        gameManagerInstance = coreManager.gameManagerInstance;
    }

    // Start is called before first frame update, after Awake
    private async void Start()
    {
        // Firstly checking if Unity Services is active,
        // if player was already signed in, 
        // and if player was in game already.
        try
        {
            await UnityServices.InitializeAsync();
            Debug.Log("Unity Services initialized successfully");

            // Check if the user is already signed in
            if (AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("User already signed in, skipping sign-in screen.");
                menuUIManagerInstance.ShowMainMenuScreen();
                return;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
            return;
        }

        // Always show sign-in screen first
        menuUIManagerInstance.ShowSigninScreen();
        
        Debug.Log("Showing sign-in screen. User must sign in manually.");
    }


    // Setter for player name
    public void SetPlayerName(string name)
    {
        playerName = name;
        lobbyManagerInstance.SetPlayerName(name);
    }

    public async Task Authenticate(string playerName)
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                Debug.Log("Initializing Unity Services...");
                InitializationOptions options = new InitializationOptions();
                options.SetProfile(playerName);
                await UnityServices.InitializeAsync(options);
            }

            AuthenticationService.Instance.SignedIn += async () =>
            {
                Debug.Log($"Signed in as {AuthenticationService.Instance.PlayerId}");
                await LobbyManager.Instance.SearchAndRefreshLobbies(); // Refresh lobbies after sign-in
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

    // Create a lobby and start as host
    public async Task<bool> CreateLobby()
    {
        bool lobbyCreated = await lobbyManagerInstance.CreateLobby();
        if (lobbyCreated)
        {
            // Start hosting
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                bool success = NetworkManager.Singleton.StartHost();
                if (!success)
                {
                    Debug.LogError("Failed to start as host!");
                    return false;
                }
                Debug.Log("Started as network host");
            }
            
            return true;
        }
        
        return false;
    }

    // Join a lobby and start as client
    public async Task<bool> JoinLobbyByCode(string lobbyCode)
    {
        bool lobbyJoined = await lobbyManagerInstance.JoinLobbyByCode(lobbyCode);
        if (lobbyJoined)
        {
            // Start as client
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                bool success = NetworkManager.Singleton.StartClient();
                if (!success)
                {
                    Debug.LogError("Failed to start as client!");
                    return false;
                }
                Debug.Log("Started as network client");
            }
            
            return true;
        }
        
        return false;
    }

    // Leave lobby and shut down network connection
    public async Task<bool> LeaveLobby()
    {
        bool leftLobby = await lobbyManagerInstance.LeaveLobby();
        if (leftLobby)
        {
            // Shut down Network Manager connection
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                NetworkManager.Singleton.Shutdown();
                Debug.Log("Network shutdown");
            }
            
            return true;
        }
        
        return false;
    }

    // Start game as host
    public void StartGame()
    {
        if (lobbyManagerInstance.IsHost)
        {
            Debug.Log("Starting game as host");
            
            // Make sure NetworkManager is already set up
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                // Load the game scene
                NetworkManager.Singleton.SceneManager.LoadScene("BallArena", LoadSceneMode.Single);
                gameManagerInstance.onLoadArena();
                Debug.Log("Loading scene: BallArena");
            }
            else
            {
                Debug.LogError("Network not initialized as host! Cannot start game.");
            }
        }
        else
        {
            Debug.LogError("Cannot start game: not a host");
        }
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