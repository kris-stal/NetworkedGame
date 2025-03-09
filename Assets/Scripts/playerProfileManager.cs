using System.IO;
using UnityEngine;

public class PlayerProfileManager : MonoBehaviour
{
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

    public string GetPlayerName()
    {
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
}
