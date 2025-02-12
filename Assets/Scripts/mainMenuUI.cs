using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;
using System.Collections.Generic;
using TMPro;

public class mainMenuUI : MonoBehaviour
{
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button startClientButton;
    [SerializeField] private TextMeshProUGUI lobbyNameUI;

    private Lobby hostLobby;
    private float heartbeatTimer;
    private string playerName;


    private void Awake() {
        startHostButton.onClick.AddListener(() => {
            Debug.Log("HOST");
            NetworkManager.Singleton.StartHost();
            
            startHostButton.gameObject.SetActive(false);
            startClientButton.gameObject.SetActive(false);
            lobbyNameUI.gameObject.SetActive(true);

            CreateLobby();
        });

        startClientButton.onClick.AddListener(() => {
            Debug.Log("CLIENT");
            NetworkManager.Singleton.StartClient();
            ListLobbies();
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
    private async void Start()
    {   
        // Subscribe to the OnClientConnectedCallback event
        // += adds this method to the list of methods to call when this event occurs
        //In this case, when a client connects (including the host), HandleClientConnected will be called
        // NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;

        await UnityServices.InitializeAsync();

        AuthenticationService.Instance.SignedIn += () => 
        {
            Debug.Log("Signed in" + AuthenticationService.Instance.PlayerId);
        };
        await AuthenticationService.Instance.SignInAnonymouslyAsync();

        playerName = "kris" + UnityEngine.Random.Range(10, 99);
        Debug.Log(playerName);
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
    }

    private async void HandleLobbyHeartbeat() // Heartbeat function to keep server alive - by default the lobby service automatically shuts down a lobby for 30 seconds of inactivity
    {
        if (hostLobby != null)
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer < 0f) 
            {   
                float heartbeatTimerMax = 15;
                heartbeatTimer  = heartbeatTimerMax;

                await LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id);
            }
        }
    }

    
    private async void CreateLobby() // Creating the lobby
    {
        try {
            string lobbyName = "My Lobby";
            
            int maxPlayers = 4;
            CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = GetPlayer()
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions);
            lobbyNameUI.text = lobby.Name;

            hostLobby = lobby;
            PrintPlayers(hostLobby);

            Debug.Log("Created lobby! " + lobby.Name + " " + lobby.MaxPlayers + " " + lobby.Id + " " + lobby.LobbyCode);
         } catch (LobbyServiceException e) 
         {
            Debug.Log(e);
         }
    }

    private async void ListLobbies() // Viewing lobby
    {
        try {  
            QueryLobbiesOptions queryLobbiesOptions = new QueryLobbiesOptions 
            {
                Count = 25,
                Filters = new List<QueryFilter> 
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT) // query filter to see lobbies will available slots > 0 
                },
                Order = new List<QueryOrder> 
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.Created)
                }
            };
            
            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryLobbiesOptions);

            Debug.Log("Lobbies found :" + queryResponse.Results.Count);
            foreach (Lobby lobby in queryResponse.Results)
                {
                    Debug.Log(lobby.Name + " " + lobby.MaxPlayers);
                }
        } catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    private async void JoinLobbyByCode(string lobbyCode) // Joining lobby via code input
    {   
        try {  
            JoinLobbyByCodeOptions joinLobbyByCodeOptions = new JoinLobbyByCodeOptions {
                Player = GetPlayer()
            };

            // QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync();
            Lobby joinedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinLobbyByCodeOptions);

            Debug.Log ("Joined lobby with code" + lobbyCode);

            PrintPlayers(joinedLobby);

        } catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    private async void JoinLobbyByQuickJoin() // Joining lobby via quick join and the filters set
    {
        try {  
            await LobbyService.Instance.QuickJoinLobbyAsync();
        
        } catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    private Player GetPlayer() 
    {
        return new Player {
            Data = new Dictionary<string, PlayerDataObject> 
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName)}
                }
            };
    }

    private void PrintPlayers(Lobby lobby)
    {
        Debug.Log ("Players in lobby " + lobby.Name);
        foreach (Player player in lobby.Players)
        {
            Debug.Log(player.Id + " " + player.Data["PlayerName"].Value);
        }
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
