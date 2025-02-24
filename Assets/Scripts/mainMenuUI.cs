using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using System.Threading.Tasks;

public class mainMenuUI : MonoBehaviour
{
    [SerializeField] private Button startHostButton;
    [SerializeField] private Button startClientButton;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button leaveLobbyButton;
    [SerializeField] private Button authenticateButton;
    [SerializeField] private TextMeshProUGUI lobbyNameUI;
    [SerializeField] private TextMeshProUGUI lobbyCodeUI;
    [SerializeField] private TMPro.TMP_InputField codeInputBox;
    [SerializeField] private TMPro.TMP_InputField SigninName;
    [SerializeField] private GameObject lobbyUI;
    [SerializeField] private GameObject menuUI;
    [SerializeField] private GameObject LoginUI;

    private Lobby hostLobby;
    private Lobby joinedLobby;
    private float heartbeatTimer;
    private float lobbyUpdateTimer;
    private string playerName;
    private string lobbyCode;


    // Ran when script is created - before Start
    private void Awake() {
        startHostButton.onClick.AddListener(() => { // On host button click
            Debug.Log("HOST");
            NetworkManager.Singleton.StartHost(); // Start the host via NetworkManager
            
            CreateLobby(); // Create the lobby
            // Hide main menu UI
            menuUI.gameObject.SetActive(false);

            // Show lobby UI
            lobbyUI.gameObject.SetActive(true);
        });

        startClientButton.onClick.AddListener(() => { // On client button click
            Debug.Log("CLIENT");
            NetworkManager.Singleton.StartClient(); // Start as client via NetworkManager

            if (!string.IsNullOrEmpty(codeInputBox.text))
            {
            lobbyCode = codeInputBox.text; // get code input
            JoinLobbyByCode(lobbyCode); // Join lobby via the code input
            
            // Hide main menu UI
            menuUI.gameObject.SetActive(false);

            // Show lobby UI
            lobbyUI.gameObject.SetActive(true);
            }
            else 
            {
                Debug.Log("NO CODE ENTERED");
            }
        });

        
        startGameButton.onClick.AddListener(() => { // On start game button click
            Debug.Log("STARTING GAME");
        });

        leaveLobbyButton.onClick.AddListener(() => { // On leave lobby button click
            
            // Remove player from lobby
            LeaveLobby();
            Debug.Log("LEFT LOBBY");

            // Show main menu UI
            menuUI.gameObject.SetActive(true);

            //. Hide Lobby UI
            lobbyUI.gameObject.SetActive(false);
        });

        authenticateButton.onClick.AddListener(() => { // On authenticate button click
            
            // Sign player in
            AuthenticatePlayer();
            Debug.Log("Signed in as " + playerName);

            // Hide Login UI
            LoginUI.gameObject.SetActive(false);

            // Show main menu UI
            menuUI.gameObject.SetActive(true);
        });
    }

    private async void AuthenticatePlayer() 
    {
        await UnityServices.InitializeAsync(); // Initalize Unity Authentication

        AuthenticationService.Instance.SignedIn += () => // Sign in
        {
            Debug.Log("Signed in" + AuthenticationService.Instance.PlayerId);
        };

        await AuthenticationService.Instance.SignInAnonymouslyAsync(); // Sign in anonymously
        if (!string.IsNullOrEmpty(SigninName.text))
        {
            playerName = SigninName.text;
        }
        else 
        {
            playerName = "kris" + UnityEngine.Random.Range(10, 99); // Pick name randomly
        }
    }


    // Start is called before first frame update, after Awake
    private void Start()
    {   

    }

    // Update is ran every frame
    private void Update()
    {
        HandleLobbyHeartbeat(); 
        HandleLobbyPollForUpdates();
    }

    // Heartbeat function to keep server alive - by default the lobby service automatically shuts down a lobby for 30 seconds of inactivity
    private async void HandleLobbyHeartbeat() 
    {
        if (hostLobby != null) // If there is a lobby
        {
            heartbeatTimer -= Time.deltaTime; // timer goes down in time
            if (heartbeatTimer < 0f) // when timer reaches 0
            {   
                float heartbeatTimerMax = 15; // set max time for timer
                heartbeatTimer  = heartbeatTimerMax; // reset timer

                await LobbyService.Instance.SendHeartbeatPingAsync(hostLobby.Id); // send heartbeat to lobbyservice
            }
        }
    }


    // Update function for clients
    private async void HandleLobbyPollForUpdates() 
    {
        if (joinedLobby != null) // If there is a lobby that client is connected to
        {
            lobbyUpdateTimer -= Time.deltaTime; // timer goes down in time
            if (lobbyUpdateTimer < 0f) // when timer reaches 0
            {   
                float lobbyUpdateTimerMax = 1.1f; // set max time for timer
                lobbyUpdateTimer  = lobbyUpdateTimerMax; // reset timer

                Lobby lobby = await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id); // update the lobby connected to
                joinedLobby = lobby; // set joined lobby as this currently connected lobby
            }
        }
    }

    
    // Creating the lobby
    private async void CreateLobby() 
    {
        try {
            string lobbyName = playerName + "'s Lobby";
            int maxPlayers = 4;

            CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions // Create new options for lobby
            {
                IsPrivate = false, // Public game, no code required
                Player = GetPlayer() // Get host player
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions); // Create the lobby instance with the lobby options

            hostLobby = lobby;

            // Update UI
            lobbyNameUI.text = lobby.Name; 
            lobbyCodeUI.text = ("Code: " + lobby.LobbyCode);

            // Output lobby details
            Debug.Log("Created lobby! " + lobby.Name + " " + lobby.MaxPlayers + " " + lobby.Id + " " + lobby.LobbyCode);

            // Output players
            PrintPlayers(hostLobby);

         } catch (LobbyServiceException e) 
         {
            Debug.Log(e);
         }
    }

    // Listing lobbies
    private async void ListLobbies()
    {
        try {  
            QueryLobbiesOptions queryLobbiesOptions = new QueryLobbiesOptions // Create new options for search
            {
                Count = 25, // Show 25 lobbies
                Filters = new List<QueryFilter> // New filters
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT) // query filter to see lobbies will available slots > 0 
                },
                Order = new List<QueryOrder> // New order
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.Created) // ascending, by time created
                }
            };
            
            QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync(queryLobbiesOptions); // create the search


            // Output results of search
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

    // Joining lobby via code input
    private async void JoinLobbyByCode(string lobbyCode) 
    {   
        try {  
            JoinLobbyByCodeOptions joinLobbyByCodeOptions = new JoinLobbyByCodeOptions { // Create new options for joining
                Player = GetPlayer() // Get player
            };

            // QueryResponse queryResponse = await LobbyService.Instance.QueryLobbiesAsync();
            Lobby joinedLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinLobbyByCodeOptions); // Joined lobby

            // Output that joined lobby
            Debug.Log ("Joined lobby with code" + lobbyCode);

            // Update UI
            lobbyNameUI.text = joinedLobby.Name; 
            lobbyCodeUI.text = ("Code: " + joinedLobby.LobbyCode);

            // Output player count
            PrintPlayers(joinedLobby);

        } catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }


    // Joining lobby via quick join and the filters set
    private async void JoinLobbyByQuickJoin() 
    {
        try {  
            await LobbyService.Instance.QuickJoinLobbyAsync(); // Start quickjoin
        
        } catch (LobbyServiceException e)
        {
            Debug.Log(e);
        }
    }

    // Get player name
    private Player GetPlayer() 
    {
        return new Player {
            Data = new Dictionary<string, PlayerDataObject> 
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName)}
                }
            };
    }



    // Output players for client (joinedLobby)
    private void PrintPlayers()
    {
        PrintPlayers(joinedLobby);
    }

    // Output players in lobby
    private void PrintPlayers(Lobby lobby)
    {
        Debug.Log ("Players in lobby " + lobby.Name);
        foreach (Player player in lobby.Players)
        {
            Debug.Log(player.Id + " " + player.Data["PlayerName"].Value);
        }
    }


    // Leaving lobby
    // Unity handles host migration automatically
    private async void LeaveLobby()
    {
        try {
            await LobbyService.Instance.RemovePlayerAsync(joinedLobby.Id, AuthenticationService.Instance.PlayerId); // Remote player from joinedLobby 
        } catch (LobbyServiceException e) 
        {
            Debug.Log(e);
        }
    }
}
