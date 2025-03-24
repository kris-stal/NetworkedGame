using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

// Core Manager to manage all other managers in finding/intializing them.
// This script finds all other scripts and services that will be required in other scripts.
public class CoreManager : MonoBehaviour
{
    // Singleton instance references for this Core Manager
    private static CoreManager _instance;
    public static CoreManager Instance => _instance;
    
    // other Manager references
    public NetworkManager networkManagerInstance { get; private set; }
    public LobbyManager lobbyManagerInstance { get; private set; }
    public MenuManager menuManagerInstance { get; private set; }
    public MenuUIManager menuUIManagerInstance { get; private set; }
    public GameManager gameManagerInstance { get; private set; }
    public GameUIManager gameUIManagerInstance { get; private set; }


    // Manager Prefabs
    [SerializeField] private GameObject networkManagerPrefab;
    [SerializeField] private GameObject lobbyManagerPrefab;
    [SerializeField] private GameObject menuManagerPrefab;
    [SerializeField] private GameObject menuUIManagerPrefab;
    [SerializeField] private GameObject gameManagerPrefab;
    [SerializeField] private GameObject gameUIManagerPrefab;


    // Variables
    private bool isNetworkInitialized = false;


    // Awake is first called
    void Awake()
    {
        // Singleton to ensure only one instance exists
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject); // Keep alive across scenes
        }
        // If theres an instance already which is not this one (likely as this was loaded in new scene)
        else if (_instance != this) 
        {
            Destroy(gameObject);
        }


        // Initialize Managers at start
        InitializeNetworkManager();
        InitializeManagerInstances();
              
        // Subscribe to scene loaded event
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    { 


    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void InitializeNetworkManager()
    {
        if (isNetworkInitialized) return; // If already initialized, exit

        // First, check if a singleton instance of NetworkManager already exists
        if (NetworkManager.Singleton != null)
        {
            Debug.Log("NetworkManager singleton already exists.");
            DontDestroyOnLoad(NetworkManager.Singleton.gameObject);
            isNetworkInitialized = true;
            return;
        }

        // Second, check if a NetworkManager exists in the scene using a tag
        GameObject existingNetworkManager = GameObject.FindGameObjectWithTag("NetworkManager");
        if (existingNetworkManager != null)
        {
            Debug.Log("NetworkManager found in scene.");
            DontDestroyOnLoad(existingNetworkManager);
            isNetworkInitialized = true;
            return;
        }

        // Third, If no singleton and no existing object in scene, instantiate from prefab
        if (networkManagerPrefab != null) // Only if there is a prefab set
        {
            // Instantiate the whole GameObject
            GameObject networkManagerObject = Instantiate(networkManagerPrefab); 

            // Ensure the NetworkManager component is attached to the instantiated GameObject
            networkManagerInstance = networkManagerObject.GetComponent<NetworkManager>(); 

            // If it's missing, log an error
            if (networkManagerInstance == null)
            {
                Debug.LogError("NetworkManager component missing from the prefab.");
                return;
            }

            DontDestroyOnLoad(networkManagerObject); // Ensure it persists across scenes
            Debug.Log("NetworkManager instantiated from prefab.");
        }

        // Not found
        else 
        {
            Debug.LogError("NetworkManager prefab not assigned in inspector!");
            return;
        }

        isNetworkInitialized = true;
        Debug.Log("NetworkManager initialization complete.");
    }

    private void InitializeManagerInstances()
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
                    DontDestroyOnLoad(menuManagerObject);
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
                    DontDestroyOnLoad(menuUIManagerObject);
                    Debug.Log("MenuUIManager instantiated from prefab.");
                }

                if (menuUIManagerInstance == null) Debug.LogError("MenuUIManager not found!");
            }
        }

        // For Game scene
        else if (currentScene == "BallArena")
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
                    DontDestroyOnLoad(gameManagerObject);
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
                    DontDestroyOnLoad(gameUIManagerObject);
                    Debug.Log("GameUIManager instantiated from prefab.");
                }

                if (gameUIManagerInstance == null) Debug.LogError("GameUIManager not found!");
            }
        }
    }


    // Scene loaded event
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Initialize Managers again
        InitializeNetworkManager();
        InitializeManagerInstances();
    }

    // Cleanup when this is destroyed
    private void OnDestroy()
    {
        // Unsubscribe from events
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
