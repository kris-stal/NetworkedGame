using UnityEngine;
using Unity.Netcode;
using TMPro;
public class ScoreUI : NetworkBehaviour
{
    [SerializeField] private TextMeshProUGUI player1ScoreText;
    [SerializeField] private TextMeshProUGUI player2ScoreText;

    void Update()
    {
        if (gameManager.Instance != null)
        {
            player1ScoreText.text = gameManager.Instance.GetPlayer1Score().ToString();
            player2ScoreText.text = gameManager.Instance.GetPlayer2Score().ToString();
        }
    }
}