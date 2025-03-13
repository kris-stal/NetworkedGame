using System.IO;
using System.Threading.Tasks;
using UnityEngine;

public class PlayerProfileManager : MonoBehaviour
{

    public static PlayerProfileManager Instance { get; private set; } // Ensures that this can only be read by other scripts, but only this script can modify it.
    
    private const string PROFILE_SAVE_PATH = "playerProfile.json"; // Local save path

    [System.Serializable]
    public class PlayerProfile
    {
        public string playerName;
        public string playerId;
    }

    private PlayerProfile currentProfile;

    private void Awake()
    {
        LoadPlayerProfile();
    }

    public void SetPlayerName(string newName)
    {
        if (currentProfile == null)
        {
            currentProfile = new PlayerProfile();
        }
        currentProfile.playerName = newName;
        SavePlayerProfile();
    }

    public async Task<string> GetPlayerName()
    {
        await Task.Yield();
        return currentProfile?.playerName ?? "Guest";
    }

    private void SavePlayerProfile()
    {
        string json = JsonUtility.ToJson(currentProfile);
        File.WriteAllText(PROFILE_SAVE_PATH, json);
        Debug.Log("Player profile saved locally.");
    }

    private void LoadPlayerProfile()
    {
        if (File.Exists(PROFILE_SAVE_PATH))
        {
            string json = File.ReadAllText(PROFILE_SAVE_PATH);
            currentProfile = JsonUtility.FromJson<PlayerProfile>(json);
            Debug.Log("Player profile loaded from local save.");
        }
        else
        {
            currentProfile = new PlayerProfile { playerName = "Guest", playerId = System.Guid.NewGuid().ToString() };
            SavePlayerProfile();
            Debug.Log("New player profile created.");
        }
    }

    public string GetAuthenticationId()
    {
        // Get the authentication ID from Unity's Authentication service
        // This should be a persistent ID for the player
        #if UNITY_EDITOR
        // For testing in editor, you could use PlayerPrefs to simulate persistent IDs
        string editorId = PlayerPrefs.GetString("EditorAuthId", "");
        if (string.IsNullOrEmpty(editorId))
        {
            editorId = "editor_auth_" + System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString("EditorAuthId", editorId);
        }
        return editorId;
        #else
        // For actual builds, use Unity Authentication
        return Unity.Services.Authentication.AuthenticationService.Instance.PlayerId;
        #endif
    }
}
