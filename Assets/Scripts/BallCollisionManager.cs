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

    private void OnCollisionStay(Collision collision)
    {
        if (!IsServer) return;

        // Only check for walls
        if (collision.gameObject.CompareTag("Wall"))
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb.linearVelocity.magnitude < 0.2f)
            {
                // Nudge the ball away from the wall normal
                Vector3 nudge = collision.contacts[0].normal * 0.5f;
                rb.AddForce(nudge, ForceMode.Impulse);
            }
        }
    }
}
