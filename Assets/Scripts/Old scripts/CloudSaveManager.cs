// using UnityEngine;
// using Unity.Services.Core;
// using Unity.Services.Authentication;
// using Unity.Services.CloudSave;
// using System.Collections.Generic;
// using System.Threading.Tasks;
// using System;

// public class CloudSaveManager : MonoBehaviour
// {
//     public static CloudSaveManager Instance { get; private set; }

//     private void Awake()
//     {
//         if (Instance == null)
//         {
//             Instance = this;
//             DontDestroyOnLoad(gameObject);
//         }
//         else
//         {
//             Destroy(gameObject);
//         }
//     }

//     async void Start()
//     {
//         try
//         {
//             // Initialize Unity Services
//             await UnityServices.InitializeAsync();
            
//             // Sign in anonymously
//             if (!AuthenticationService.Instance.IsSignedIn)
//             {
//                 await AuthenticationService.Instance.SignInAnonymouslyAsync();
//                 Debug.Log("Signed in anonymously with ID: " + AuthenticationService.Instance.PlayerId);
//             }
//         }
//         catch (Exception e)
//         {
//             Debug.LogError($"Error initializing Unity Services: {e.Message}");
//         }
//     }

//     // Save game state to Unity Cloud
//     public async Task SaveGameState(GameManager.GameState gameState)
//     {
//         try
//         {
//             // Convert the GameState to JSON
//             string jsonData = JsonUtility.ToJson(gameState);
            
//             // Create a dictionary with a single key for the game state
//             var data = new Dictionary<string, object>
//             {
//                 { "gameState", jsonData }
//             };

//             // Save the data to the cloud under the player's account
//             await CloudSaveService.Instance.Data.Player.SaveAsync(data);
            
//             Debug.Log("Game state saved to Unity Cloud");
//         }
//         catch (Exception e)
//         {
//             Debug.LogError($"Error saving game state: {e.Message}");
//         }
//     }

//     // Load game state from Unity Cloud
//     public async Task<GameManager.GameState> LoadGameState()
//     {
//         try
//         {
//             // Get all saved data for the player
//             var savedData = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { "gameState" });

//             if (!savedData.ContainsKey("gameState"))
//             {
//                 Debug.Log("No saved game state found");
//                 return null;
//             }

//             // Get the JSON string from the saved data
//             string jsonData = savedData["gameState"].Value.GetAsString();
            
//             // Deserialize the JSON to a GameState object
//             GameManager.GameState loadedState = JsonUtility.FromJson<GameManager.GameState>(jsonData);

//             Debug.Log("Game state loaded from Unity Cloud");
//             return loadedState;
//         }
//         catch (Exception e)
//         {
//             Debug.LogError($"Error loading game state: {e.Message}");
//             return null;
//         }
//     }

//     // Save individual player data
//     public async Task SavePlayerData(string playerAuthId, GameManager.PlayerState playerState)
//     {
//         try
//         {
//             // Convert player state to JSON
//             string jsonData = JsonUtility.ToJson(playerState);
            
//             // Create a dictionary with the player data
//             var playerData = new Dictionary<string, object>
//             {
//                 { $"player_{playerAuthId}", jsonData }
//             };

//             // Save to the cloud
//             await CloudSaveService.Instance.Data.Player.SaveAsync(playerData);
//             Debug.Log($"Player data saved for {playerAuthId}");
//         }
//         catch (Exception e)
//         {
//             Debug.LogError($"Error saving player data: {e.Message}");
//         }
//     }

//     // Load player data
//     public async Task<GameManager.PlayerState> LoadPlayerData(string playerAuthId)
//     {
//         try
//         {
//             string key = $"player_{playerAuthId}";
//             var loadedData = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { key });

//             if (!loadedData.ContainsKey(key))
//             {
//                 Debug.Log($"No saved data found for player {playerAuthId}");
//                 return null;
//             }

//             string jsonData = loadedData[key].Value.GetAsString();
//             GameManager.PlayerState playerState = JsonUtility.FromJson<GameManager.PlayerState>(jsonData);

//             Debug.Log($"Player data loaded for {playerAuthId}");
//             return playerState;
//         }
//         catch (Exception e)
//         {
//             Debug.LogError($"Error loading player data: {e.Message}");
//             return null;
//         }
//     }
// }