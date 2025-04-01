    // Method to join a lobby by ID (from searching lobbies)
    // public async Task<bool> JoinLobbyById(string lobbyId)
    // {
    //     try
    //     {
    //         // Make sure player is authenticated
    //         if (!AuthenticationService.Instance.IsSignedIn)
    //         {
    //             Debug.LogError("Player is not signed in");
    //             return false;
    //         }
            
    //         // Join the lobby
    //         JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
    //         {
    //             Player = GetPlayer()
    //         };
            
    //         joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);

    //         // Retrieve host connection details from lobby metadata
    //         if (joinedLobby.Data.TryGetValue("HostIP", out DataObject hostIPData) &&
    //             joinedLobby.Data.TryGetValue("HostPort", out DataObject hostPortData))
    //         {
    //             string hostIP = hostIPData.Value;
    //             int hostPort = int.Parse(hostPortData.Value);

    //             Debug.Log($"Joining lobby with Host IP: {hostIP}, Port: {hostPort}");

    //             // Configure NetworkManager to connect to host IP and port
    //             var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
    //             if (transport != null)
    //             {
    //                 transport.SetConnectionData(hostIP, (ushort)hostPort);  // ✅ Use hostIP from lobby!
    //                 Debug.Log($"Client connecting to: {hostIP}:{hostPort}");
    //             }
    //         }


    //         // Start as client
    //         if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
    //         {
    //             bool success = NetworkManager.Singleton.StartClient();
    //             if (!success)
    //             {
    //                 Debug.LogError("Failed to start as client!");
    //                 return false;
    //             }

    //             // Retry logic if client connection fails
    //             if (!NetworkManager.Singleton.IsConnectedClient)
    //             {
    //                 Debug.LogError("Client connection failed. Retrying...");
    //                 NetworkManager.Singleton.Shutdown();
    //                 await Task.Delay(1000);  // Small delay before retrying
    //                 NetworkManager.Singleton.StartClient();
    //             }

    //             Debug.Log("Started as network client");
    //         }
    //         return true;
    //     }
    //     catch (LobbyServiceException e)
    //     {
    //         Debug.LogError($"Failed to join lobby: {e.Message}");
    //         return false;
    //     }
    // }

    
    // // Get local IP address
    // public string GetLocalIPAddress()
    // {
    //     var host = Dns.GetHostEntry(Dns.GetHostName());
    //     foreach (var ip in host.AddressList)
    //     {
    //         if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().StartsWith("127."))
    //         {
    //             return ip.ToString(); // Returns the first valid LAN IP
    //         }
    //     }
        
    //     throw new Exception("No valid local IP found.");
    // }

    // // Get current port from NetworkManager
    // private int GetCurrentPort()
    // {
    //     var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
    //     return transport != null ? transport.ConnectionData.Port : 7777; // Default port is 7777, trying 7778
    // }

    // Creating the lobby
    // public async Task<bool> CreateLobby()
    // {
    //     try
    //     {
    //         string lobbyName = playerName + "'s Lobby";
    //         int maxPlayers = 4;

    //         // Get host's IP and Port
    //         string hostIP = GetLocalIPAddress();
    //         int hostPort = GetCurrentPort();

    //         CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions
    //         {
    //             IsPrivate = false,
    //             Player = GetPlayer(),
    //             Data = new Dictionary<string, DataObject>
    //             {
    //                 // Store host connection details in lobby metadata
    //                 { "HostIP", new DataObject(DataObject.VisibilityOptions.Public, hostIP) },
    //                 { "HostPort", new DataObject(DataObject.VisibilityOptions.Public, hostPort.ToString()) }
    //             }
    //         };

    //         Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions);
    //         hostLobby = lobby;
    //         joinedLobby = lobby;

    //         // Output lobby details
    //         Debug.Log("Created lobby! " + lobby.Name + " " + lobby.MaxPlayers + " " + lobby.Id + " " + lobby.LobbyCode);
    //         Debug.Log($"Lobby Host IP: {hostIP}, Port: {hostPort} ");

    //         // Output players
    //         PrintPlayers(hostLobby);
    
    //         // save this lobby as last joined lobby
    //         PlayerPrefs.SetString("LastJoinedLobby", lobby.Id);
    //         PlayerPrefs.Save();

    //         // Start hosting through NetworkManager
    //         if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
    //         {
    //             bool success = NetworkManager.Singleton.StartHost();
    //             if (!success)
    //             {
    //                 Debug.LogError("Failed to start as host!");
    //                 return false;
    //             }
    //             Debug.Log("Started as network host");
    //         }
            
    //         return true;
    //     }
    //     catch (LobbyServiceException e)
    //     {
    //         Debug.Log(e);
    //         return false;
    //     }
    // }