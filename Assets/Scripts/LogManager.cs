using UnityEngine;
using System;
using System.IO;

public class LogManager : MonoBehaviour
{
    private string logPath;
    
    void Awake()
    {
        // Create log file in persistent data path
        logPath = Path.Combine(Application.persistentDataPath, "game_log.txt");
        
        // Clear previous log file
        File.WriteAllText(logPath, "=== LOG START ===\n");
        
        // Subscribe to log messages
        Application.logMessageReceived += LogToFile;
        
        Debug.Log($"Logging to: {logPath}");
    }
    
    void OnDestroy()
    {
        // Unsubscribe when destroyed
        Application.logMessageReceived -= LogToFile;
    }
    
    void LogToFile(string logString, string stackTrace, LogType type)
    {
        try 
        {
            // Format: [Time] Type: Message
            string entry = $"[{DateTime.Now:HH:mm:ss}] {type}: {logString}\n";
            
            // If error or exception, add stack trace
            if (type == LogType.Error || type == LogType.Exception)
                entry += $"{stackTrace}\n";
                
            // Append to log file
            File.AppendAllText(logPath, entry);
        }
        catch (Exception e)
        {
            // Fail silently
        }
    }
}