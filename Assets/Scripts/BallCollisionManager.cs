using UnityEngine;
using Unity.Netcode;

public class BallCollisionHandler : NetworkBehaviour
{
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return; // Only the server should handle scoring

        if (collision.gameObject.CompareTag("RedGoal"))
        {
            GameManager.Instance.Score(false); // Blue scores
        }
        else if (collision.gameObject.CompareTag("BlueGoal"))
        {
            GameManager.Instance.Score(true); // Red scores
        }
    }
}
