using UnityEngine;
using Unity.Netcode;

public class ballControl : NetworkBehaviour
{
    private Rigidbody rb; // rigidbody

    [SerializeField] float ballSpeed = 10.0f; // base movement speed of ball

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>(); // get ball rigidbody
        Invoke("goBall", 2);
    }


    // Collision handling
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;
        // only server handles scoring

        Debug.Log("Ball collision detected with: " + collision.gameObject.name + " (Tag: " + collision.gameObject.tag + ")");// Debug log to verify collision

        if (collision.gameObject.CompareTag("RedGoal"))
        {
            Debug.Log("Red goal hit - Blue scores!"); // Debug log
            gameManager.Instance.Score(false);  // false means Player 2 (Blue) scored
        }
        else if (collision.gameObject.CompareTag("BlueGoal"))
        {
            Debug.Log("Blue goal hit - Red scores!"); // Debug log
            gameManager.Instance.Score(true);   // true means Player 1 (Red) scored
        }
    }
    
    // Move ball in random direction.
    void goBall(){
        float rand = Random.Range(0, 2);
        var vel = rb.linearVelocity;

        if (rand < 1) {
            vel.x = +ballSpeed;
            rb.linearVelocity = vel;
        } 
        else 
        {
            vel.z = +ballSpeed;
            rb.linearVelocity = vel;
        }
    }

    void ResetBall() {
        rb.linearVelocity = Vector3.zero;
        transform.position = Vector3.zero;
    }

    void RestartGame() {
        ResetBall();
        Invoke("goBall", 1);
    }
}
