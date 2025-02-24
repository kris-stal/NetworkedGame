using UnityEngine;
using Unity.Netcode;
using TMPro;
using UnityEngine.UI;
public class gameUI : NetworkBehaviour
{

     public static gameManager Instance { get; private set; }
    [SerializeField] private Button resumeGameButton;
    [SerializeField] private Button leaveGameButton;
    [SerializeField] private GameObject menuUI;
    [SerializeField] private TextMeshProUGUI player1ScoreText;
    [SerializeField] private TextMeshProUGUI player2ScoreText;
    

    private void Awake()
    {
        resumeGameButton.onClick.AddListener(() => { // On resume game button click

            menuUI.gameObject.SetActive(false);
        });


        leaveGameButton.onClick.AddListener(() => { // On resume game button click

            gameManager.Instance.LeaveGame();
        });
    }


    private void Update()
    {
        if ((!menuUI.gameObject.activeSelf) && Input.GetKeyDown(KeyCode.Escape)) // If menu ui isnt already up and player presses escape
        {
            menuUI.gameObject.SetActive(true); // show menu ui
        }
        else if (menuUI.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape)) // if menu ui is already up and player presses escape
        {
            menuUI.gameObject.SetActive(false); // hide menu ui 
        }

        if (gameManager.Instance != null)
        {
            player1ScoreText.text = gameManager.Instance.GetPlayer1Score().ToString();
            player2ScoreText.text = gameManager.Instance.GetPlayer2Score().ToString();
        }
    }
}