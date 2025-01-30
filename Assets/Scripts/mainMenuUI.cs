using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class mainMenuUI : MonoBehaviour
{
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button startClientButton;


    private void Awake() {
        startHostButton.onClick.AddListener(() => {
            Debug.Log("HOST");
            NetworkManager.Singleton.StartHost();
            Hide();
        });

        startClientButton.onClick.AddListener(() => {
            Debug.Log("CLIENT");
            NetworkManager.Singleton.StartClient();
            Hide();
        });
    }

    private void Hide() {
        gameObject.SetActive(false);
    }


    // Method called when a client connects to the network
    // The clientId parameter tells us which client connected
    private void HandleClientConnected(ulong clientId)
    {
        // Loading the lobby menu after connection
        SceneManager.LoadScene("LobbyMenu");
    }


    // Start is called before first frame update
    void Start()
    {   
        // Subscribe to the OnClientConnectedCallback event
        // += adds this method to the list of methods to call when this event occurs
        //In this case, when a client connects (including the host), HandleClientConnected will be called
        NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
    }

    // OnDestroy is called when this script is destroyed for cleanup
    void OnDestroy()
    {   
        // Check if NetworkManager still exists
        if (NetworkManager.Singleton != null)

            // Unsubscribe from the event to prevent calling methods on destroyed objects
            // -= removes this method from the lists of methods to call when this event occurs
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
    }
}
