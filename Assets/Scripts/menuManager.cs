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

        // Move to an event?
        // bool isUnityInitialized = await coreManagerInstance.InitializeUnityServices();

        //     if (isUnityInitialized)
        //     {
        //         if (AuthenticationService.Instance.IsSignedIn)
        //         {
        //             Debug.Log("User already signed in, skipping sign-in screen.");
        //             menuUIManagerInstance.ShowMainMenuScreen();
        //             return;
        //         }
        //     }
        //     else
        //     {
        //         Debug.LogError("Unity Services failed, cannot proceed!");
        //         // Handle failure (show error message, retry, etc.)
        //     }

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