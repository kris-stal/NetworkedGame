using Unity.Services.CloudSave;
using Unity.Services.Authentication;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

public class playerProfileManager : MonoBehaviour
{
    public static playerProfileManager Instance { get; private set; } // Singleton Pattern


    private void Awake()
    {   
        // Singleton Pattern
        // If an instance already exists, destroy this one 
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Set the instance to this script
        Instance = this;

        // Optionally, make this object persist across scenes
        DontDestroyOnLoad(gameObject);
    }


    // Save player name

    public async Task SetPlayerName(string playerName)
    {
        if (AuthenticationService.Instance.IsSignedIn)
        {
            // Create a dictionary to store player data
            var playerData = new Dictionary<string, object>
            {
                { "playerName", playerName }
            };

            try
            {
                // Save the player name using the new method
                await CloudSaveService.Instance.Data.Player.SaveAsync(playerData);
                Debug.Log($"Player name updated to {playerName} on the cloud.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to save player name: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("Player is not signed in, can't save name.");
        }
    }

    // This method loads the player name from Cloud Save
    public async Task<string> GetPlayerName()
    {
        try
        {
            // Ensure CloudSaveService is initialized
            if (CloudSaveService.Instance != null)
            {
                // Create a HashSet with the keys you want to load
                HashSet<string> keys = new HashSet<string> { "playerName" };

                // Load the data from Cloud Save asynchronously
                var data = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                // Check if the playerName exists in the returned data
                if (data.ContainsKey("playerName"))
                {
                    // Access the value and cast it to a string
                    return data["playerName"].Value.ToString();
                }
                else
                {
                    Debug.LogWarning("Player name not found in Cloud Save.");
                    return string.Empty;
                }
            }
            else
            {
                Debug.LogError("CloudSaveService.Instance is not initialized.");
                return string.Empty;
            }
        }
        catch (Exception ex) // Ensure this exception handling is in place
        {
            Debug.LogError($"Error loading player name from Cloud Save: {ex.Message}");
            return string.Empty;
        }
    }
}