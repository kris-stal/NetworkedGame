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
    public ReconnectManager reconnectManagerInstance { get; private set; }



    // PREFABS //
    // For instantiation
    [SerializeField] private GameObject networkManagerPrefab;
    [SerializeField] private GameObject lobbyManagerPrefab;
    [SerializeField] private GameObject menuManagerPrefab;
    [SerializeField] private GameObject menuUIManagerPrefab;
    [SerializeField] private GameObject gameManagerPrefab;
    [SerializeField] private GameObject gameUIManagerPrefab;
    [SerializeField] private GameObject pingManagerPrefab;
    [SerializeField] private GameObject reconnectManagerPrefab;
    



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

        // Check if NetworkManager.Singleton already exists
        if (NetworkManager.Singleton != null)
        {
            Debug.Log("NetworkManager singleton already exists.");
            networkManagerInstance = NetworkManager.Singleton; // Ensure reference consistency
            DontDestroyOnLoad(networkManagerInstance.gameObject);
            isNetworkInitialized = true;
            return true;
        }

        // Check if a NetworkManager exists in the scene using a tag
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

        // If no existing instance, instantiate from prefab
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

        // No prefab assigned, so fail
        Debug.LogError("NetworkManager prefab not assigned in inspector!");
        return false;
    }



    // MANAGER INITIALIZATION
    public void InitializeManagerInstances()
    {
        // Always find the scripts which are persistent between scenes.
            lobbyManagerInstance = GetOrInstantiateManager(
                lobbyManagerInstance,
                "LobbyManager", 
                lobbyManagerPrefab,
                "LobbyManager could not be found or instantiated!"
                );

            pingManagerInstance = GetOrInstantiateManager(
                pingManagerInstance,
                "PingManager", 
                pingManagerPrefab,
                "PingManager could not be found or instantiated!"
                );

            reconnectManagerInstance = GetOrInstantiateManager(
                reconnectManagerInstance,
                "ReconnectManager", 
                reconnectManagerPrefab,
                "ReconnectManager could not be found or instantiated!"
                );


        // Check current scene
        string currentScene = SceneManager.GetActiveScene().name;
        Debug.Log($"CoreManager running in scene: {currentScene}");

        // For Menu scene
        if (currentScene == "LobbyMenu")
        {
            // Find MenuManager and MenuUIManager, checking by tag and prefab instantiation
            menuManagerInstance = GetOrInstantiateManager(
                menuManagerInstance,
                "MenuManager", // Tag assigned to the GameObject
                menuManagerPrefab, // Prefab to instantiate if not found
                "MenuManager could not be found or instantiated!"
                );

            menuUIManagerInstance = GetOrInstantiateManager(
                menuUIManagerInstance,
                "MenuUIManager", 
                menuUIManagerPrefab,
                "MenuUIManager could not be found or instantiated!"
                );
        }

        

        // For Game scene
        else if (currentScene == "BallArena" || currentScene == "NetworkStressTest")
        {
            // Find GameManager and GameUIManager
            gameManagerInstance = GetOrInstantiateManager(
                gameManagerInstance,
                "GameManager", 
                gameManagerPrefab, 
                "GameManager could not be found or instantiated!"
                );

            gameUIManagerInstance = GetOrInstantiateManager(
                gameUIManagerInstance,
                "GameUIManager", 
                gameUIManagerPrefab, 
                "GameUIManager could not be found or instantiated!"
                );
        }
    }

    // Helper for handling manager scripts
    private T GetOrInstantiateManager<T>(
        T existingInstance,
        string tag,
        GameObject prefab,
        string errorMessage) where T : MonoBehaviour
    {
        if (existingInstance != null)
            return existingInstance;

        T instance = GameObject.FindGameObjectWithTag(tag)?.GetComponent<T>();

        if (instance == null && prefab != null)
        {
            GameObject obj = Instantiate(prefab);
            instance = obj.GetComponent<T>();
            DontDestroyOnLoad(obj);
            Debug.Log($"{typeof(T).Name} instantiated from prefab.");
        }

        if (instance == null)
        {
            Debug.LogError(errorMessage);
            throw new System.Exception(errorMessage); // Throw an exception to prevent further issues
        }

        return instance;
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
