using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine;


public class OldScripts : MonoBehaviour
{
    private string playerName;
    private Lobby hostLobby;
    private Lobby joinedLobby;

    // Method to join a lobby by ID (from searching lobbies)
    public async Task<bool> JoinLobbyById(string lobbyId)
    {
        try
        {
            // Make sure player is authenticated
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                UnityEngine.Debug.Log("Player is not signed in");
                return false;
            }
            
            // Join the lobby
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions
            {
                Player = GetPlayer()
            };
            
            joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);

            // Retrieve host connection details from lobby metadata
            if (joinedLobby.Data.TryGetValue("HostIP", out DataObject hostIPData) &&
                joinedLobby.Data.TryGetValue("HostPort", out DataObject hostPortData))
            {
                string hostIP = hostIPData.Value;
                int hostPort = int.Parse(hostPortData.Value);

                UnityEngine.Debug.Log($"Joining lobby with Host IP: {hostIP}, Port: {hostPort}");

                // Configure NetworkManager to connect to host IP and port
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                if (transport != null)
                {
                    transport.SetConnectionData(hostIP, (ushort)hostPort);  
                    UnityEngine.Debug.Log($"Client connecting to: {hostIP}:{hostPort}");
                }
            }


            // Start as client
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                bool success = NetworkManager.Singleton.StartClient();
                if (!success)
                {
                    UnityEngine.Debug.LogError("Failed to start as client!");
                    return false;
                }

                // Retry logic if client connection fails
                if (!NetworkManager.Singleton.IsConnectedClient)
                {
                    UnityEngine.Debug.LogError("Client connection failed. Retrying...");
                    NetworkManager.Singleton.Shutdown();
                    await Task.Delay(1000);  // Small delay before retrying
                    NetworkManager.Singleton.StartClient();
                }

                UnityEngine.Debug.Log("Started as network client");
            }
            return true;
        }
        catch (LobbyServiceException e)
        {
            UnityEngine.Debug.LogError($"Failed to join lobby: {e.Message}");
            return false;
        }
    }

    private Player GetPlayer()
    {
        throw new NotImplementedException();
    }


    // Get local IP address
    public string GetLocalIPAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == AddressFamily.InterNetwork && !ip.ToString().StartsWith("127."))
            {
                return ip.ToString(); // Returns the first valid LAN IP
            }
        }
        
        throw new Exception("No valid local IP found.");
    }

    // Get current port from NetworkManager
    private int GetCurrentPort()
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        return transport != null ? transport.ConnectionData.Port : 7777; // Default port is 7777, trying 7778
    }

    //Creating the private Lobby joinedLobby;
    public async Task<bool> CreateLobby()
    {
        try
        {
            string lobbyName = playerName + "'s Lobby";
            int maxPlayers = 4;

            // Get host's IP and Port
            string hostIP = GetLocalIPAddress();
            int hostPort = GetCurrentPort();

            CreateLobbyOptions createLobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = GetPlayer(),
                Data = new Dictionary<string, DataObject>
                {
                    // Store host connection details in lobby metadata
                    { "HostIP", new DataObject(DataObject.VisibilityOptions.Public, hostIP) },
                    { "HostPort", new DataObject(DataObject.VisibilityOptions.Public, hostPort.ToString()) }
                }
            };

            Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, createLobbyOptions);
            hostLobby = lobby;
            joinedLobby = lobby;

            // Output lobby details
            UnityEngine.Debug.Log("Created lobby! " + lobby.Name + " " + lobby.MaxPlayers + " " + lobby.Id + " " + lobby.LobbyCode);
            UnityEngine.Debug.Log($"Lobby Host IP: {hostIP}, Port: {hostPort} ");

            // Output players
            PrintPlayers(hostLobby);
    
            // save this lobby as last joined lobby
            PlayerPrefs.SetString("LastJoinedLobby", lobby.Id);
            PlayerPrefs.Save();

            // Start hosting through NetworkManager
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
            {
                bool success = NetworkManager.Singleton.StartHost();
                if (!success)
                {
                    UnityEngine.Debug.LogError("Failed to start as host!");
                    return false;
                }
                UnityEngine.Debug.Log("Started as network host");
            }
            
            return true;
        }
        catch (LobbyServiceException e)
        {
            UnityEngine.Debug.Log(e);
            return false;
        }
    }

    private void PrintPlayers(Lobby hostLobby)
    {
        throw new NotImplementedException();
    }
}
