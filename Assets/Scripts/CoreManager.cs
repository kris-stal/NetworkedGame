using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

// Manager for handling script references and service initialization
// This script finds all other scripts and services that will be required in other scripts and ensures they are ready
public class CoreManager : MonoBehaviour
{
    // REFERENCES //
    // Singleton instance references for this Core Manager
    public static CoreManager Instance { get; private set; }
    
    // other script references
    public NetworkManager networkManagerInstance { get; private set; }
    public LobbyManager lobbyManagerInstance { get; private set; }
    public MenuManager menuManagerInstance { get; private set; }
    public MenuUIManager menuUIManagerInstance { get; private set; }
    public GameManager gameManagerInstance { get; private set; }
    public GameUIManager gameUIManagerInstance { get; private set; }
    public PingManager pingManagerInstance { get; private set; }



    // PREFABS //
    // For instantiation
    [SerializeField] private GameObject networkManagerPrefab;
    [SerializeField] private GameObject lobbyManagerPrefab;
    [SerializeField] private GameObject menuManagerPrefab;
    [SerializeField] private GameObject menuUIManagerPrefab;
    [SerializeField] private GameObject gameManagerPrefab;
    [SerializeField] private GameObject gameUIManagerPrefab;
    [SerializeField] private GameObject pingManagerPrefab;
    



    // VARIABLES //
    private bool isNetworkInitialized = false;


    // Awake is ran when script is created - before Start
    private async void Awake()
    {
        // Set Singleton Pattern
        // If theres an instance already which is not this one (likely as this was loaded in new scene)
        if (Instance != null && Instance != this) 
        {
            Destroy(gameObject); // destroy this one to prevent duplicates
            return;
        }

        // Else, there is no other instance so set this as the instance
        Instance = this;
        DontDestroyOnLoad(gameObject);


        // Initialize Services and Managers at start
        await InitializeUnityServices();
        InitializeNetworkManager();
        InitializeManagerInstances();
              
        // Subscribe to scene loaded event
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    { 
        // Ensure networkmanager's scene management is active.
        NetworkManager.Singleton.NetworkConfig.EnableSceneManagement = true;

    }

    // SERVICE INITIALIZATION //
    public async Task<bool> InitializeUnityServices(string playerProfile = null)
    {
        try
        {
            if (UnityServices.State == ServicesInitializationState.Initialized)
            {
                Debug.Log("Unity Services already initialized.");
                return true;
            }

            Debug.Log("Initializing Unity Services...");

            InitializationOptions options = new InitializationOptions();
            if (!string.IsNullOrEmpty(playerProfile))
            {
                options.SetProfile(playerProfile);
            }

            await UnityServices.InitializeAsync(options);
            Debug.Log("Unity Services initialized successfully");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to initialize Unity Services: {e.Message}");
            return false;
        }
    }

    public bool InitializeNetworkManager()
    {
        if (isNetworkInitialized) return true; // If already initialized, exit

        // 1️⃣ Check if NetworkManager.Singleton already exists
        if (NetworkManager.Singleton != null)
        {
            Debug.Log("NetworkManager singleton already exists.");
            networkManagerInstance = NetworkManager.Singleton; // Ensure reference consistency
            DontDestroyOnLoad(networkManagerInstance.gameObject);
            isNetworkInitialized = true;
            return true;
        }

        // 2️⃣ Check if a NetworkManager exists in the scene using a tag
        GameObject existingNetworkManager = GameObject.FindGameObjectWithTag("NetworkManager");
        if (existingNetworkManager != null)
        {
            Debug.Log("NetworkManager found in scene.");
            networkManagerInstance = existingNetworkManager.GetComponent<NetworkManager>();

            if (networkManagerInstance == null)
            {
                Debug.LogError("Found NetworkManager object, but it has no NetworkManager component!");
                return false;
            }

            DontDestroyOnLoad(existingNetworkManager);
            isNetworkInitialized = true;
            return true;
        }

        // 3️⃣ If no existing instance, instantiate from prefab
        if (networkManagerPrefab != null)
        {
            GameObject networkManagerObject = Instantiate(networkManagerPrefab);
            networkManagerInstance = networkManagerObject.GetComponent<NetworkManager>();

            if (networkManagerInstance == null)
            {
                Debug.LogError("NetworkManager component missing from the prefab.");
                return false;
            }

            DontDestroyOnLoad(networkManagerObject);
            Debug.Log("NetworkManager instantiated from prefab.");

            isNetworkInitialized = true;
            return true;
        }

        // 4️⃣ No prefab assigned, so fail
        Debug.LogError("NetworkManager prefab not assigned in inspector!");
        return false;
    }



    // MANAGER INITIALIZATION
    public void InitializeManagerInstances()
    {
        // Always find Lobby Manager which is persistent between scenes.
        if (lobbyManagerInstance == null)
        {
            // First, try finding using the singleton or by tag
            lobbyManagerInstance = LobbyManager.Instance ?? GameObject.FindGameObjectWithTag("LobbyManager")?.GetComponent<LobbyManager>();

            // If not found, instantiate from prefab
            if (lobbyManagerInstance == null && lobbyManagerPrefab != null)
            {
                GameObject lobbyManagerObject = Instantiate(lobbyManagerPrefab);
                lobbyManagerInstance = lobbyManagerObject.GetComponent<LobbyManager>();
                DontDestroyOnLoad(lobbyManagerObject); // Ensure it persists across scenes
                Debug.Log("LobbyManager instantiated from prefab.");
            }

            // If still null after checking all methods, log an error
            if (lobbyManagerInstance == null) Debug.LogError("LobbyManager not found!");
        }

        if (pingManagerInstance == null)
        {
            // First, try finding using the singleton or by tag
            pingManagerInstance = PingManager.Instance ?? GameObject.FindGameObjectWithTag("PingManager")?.GetComponent<PingManager>();

            // If not found, instantiate from prefab
            if (pingManagerInstance == null && pingManagerPrefab != null)
            {
                GameObject pingManagerObject = Instantiate(pingManagerPrefab);
                lobbyManagerInstance = pingManagerObject.GetComponent<LobbyManager>();
                DontDestroyOnLoad(pingManagerObject); // Ensure it persists across scenes
                Debug.Log("PingManager instantiated from prefab.");
            }

            // If still null after checking all methods, log an error
            if (pingManagerInstance == null) Debug.LogError("PingManager not found!");
        }


        // Check current scene
        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"CoreManager running in scene: {currentScene}");

        // For Menu scene
        if (currentScene == "LobbyMenu")
        {
            // Find MenuManager and MenuUIManager, checking by tag and prefab instantiation
            if (menuManagerInstance == null)
            {
                menuManagerInstance = MenuManager.Instance ?? GameObject.FindGameObjectWithTag("MenuManager")?.GetComponent<MenuManager>();

                // Instantiate from prefab if not found
                if (menuManagerInstance == null && menuManagerPrefab != null)
                {
                    GameObject menuManagerObject = Instantiate(menuManagerPrefab);
                    menuManagerInstance = menuManagerObject.GetComponent<MenuManager>();
                    Debug.Log("MenuManager instantiated from prefab.");
                }

                if (menuManagerInstance == null) Debug.LogError("MenuManager not found!");
            }

            if (menuUIManagerInstance == null)
            {
                menuUIManagerInstance = MenuUIManager.Instance ?? GameObject.FindGameObjectWithTag("MenuUIManager")?.GetComponent<MenuUIManager>();

                // Instantiate from prefab if not found
                if (menuUIManagerInstance == null && menuUIManagerPrefab != null)
                {
                    GameObject menuUIManagerObject = Instantiate(menuUIManagerPrefab);
                    menuUIManagerInstance = menuUIManagerObject.GetComponent<MenuUIManager>();
                    Debug.Log("MenuUIManager instantiated from prefab.");
                }

                if (menuUIManagerInstance == null) Debug.LogError("MenuUIManager not found!");
            }
        }

        // For Game scene
        else if (currentScene == "BallArena" || currentScene == "NetworkStressTest")
        {
            // Find GameManager and GameUIManager
            if (gameManagerInstance == null)
            {
                gameManagerInstance = GameManager.Instance ?? GameObject.FindGameObjectWithTag("GameManager")?.GetComponent<GameManager>();

                // Instantiate from prefab if not found
                if (gameManagerInstance == null && gameManagerPrefab != null)
                {
                    GameObject gameManagerObject = Instantiate(gameManagerPrefab);
                    gameManagerInstance = gameManagerObject.GetComponent<GameManager>();
                    Debug.Log("GameManager instantiated from prefab.");
                }

                if (gameManagerInstance == null) Debug.LogError("GameManager not found!");
            }

            if (gameUIManagerInstance == null)
            {
                gameUIManagerInstance = GameUIManager.Instance ?? GameObject.FindGameObjectWithTag("GameUIManager")?.GetComponent<GameUIManager>();

                // Instantiate from prefab if not found
                if (gameUIManagerInstance == null && gameUIManagerPrefab != null)
                {
                    GameObject gameUIManagerObject = Instantiate(gameUIManagerPrefab);
                    gameUIManagerInstance = gameUIManagerObject.GetComponent<GameUIManager>();
                    Debug.Log("GameUIManager instantiated from prefab.");
                }

                if (gameUIManagerInstance == null) Debug.LogError("GameUIManager not found!");
            }
        }
    }


    
    // EVENTS //
    // Scene loaded event
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Initialize Managers again
        InitializeNetworkManager();
        InitializeManagerInstances();
    }



    // CLEANUP //
    private void OnDestroy()
    {
        // Unsubscribe from events
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
