using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Lobbies.Models;

public class LobbyListItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI lobbyNameText;
    [SerializeField] private TextMeshProUGUI playerCountText;
    [SerializeField] private Button joinButton;
    
    private string lobbyId;
    
    public Button JoinButton => joinButton;
    
    public void Initialize(Lobby lobby)
    {
        lobbyId = lobby.Id;
        
        // Set lobby name - use host's name or lobby name if available
        string lobbyName = "Unknown Lobby";
        if (lobby.Players.Count > 0)
        {
            // Assuming the first player is the host
            Player host = lobby.Players[0];
            if (host.Data != null && host.Data.ContainsKey("PlayerName"))
            {
                lobbyName = host.Data["PlayerName"].Value;
                lobbyName += "'s Lobby";
            }
        }
        
        // Or use the lobby name directly if available
        if (!string.IsNullOrEmpty(lobby.Name))
        {
            lobbyName = lobby.Name;
        }
        
        lobbyNameText.text = lobbyName;
        
        // Set player count
        playerCountText.text = $"{lobby.Players.Count} / {lobby.MaxPlayers}";
    }
}