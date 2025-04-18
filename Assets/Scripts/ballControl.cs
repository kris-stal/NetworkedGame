using UnityEngine;
using Unity.Netcode;

// Manager for ball functionality
// Handles the movement, collision and resetting of the main ball.
public class ballControl : NetworkBehaviour
{
    // REFERENCES //
    private CoreManager coreManagerInstance;
    private GameManager gameManagerInstance;



    // VARIABLES //
    private Rigidbody rb; // rigidbody of ball
    [SerializeField] float ballSpeed = 10.0f; // base movement speed of ball



    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Assign manager instances
        coreManagerInstance = CoreManager.Instance;
        gameManagerInstance = coreManagerInstance.gameManagerInstance;

        rb = GetComponent<Rigidbody>(); // get ball rigidbody
    }



    // COLLISION HANDLING //
    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer) return;
        // only server handles scoring

        if (collision.gameObject.CompareTag("RedGoal"))
        {
            Debug.Log("Red goal hit - Blue scores!"); // Debug log
            gameManagerInstance.Score(false);  // false means Player 2 (Blue) scored
        }
        else if (collision.gameObject.CompareTag("BlueGoal"))
        {
            Debug.Log("Blue goal hit - Red scores!"); // Debug log
            gameManagerInstance.Score(true);   // true means Player 1 (Red) scored
        }
    }
    


    // MOVEMENT HANDLING //
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



    // BALL STATE HANDLING //
    void ResetBall() {
        rb.linearVelocity = Vector3.zero;
        transform.position = Vector3.zero;
    }

    void RestartGame() {
        ResetBall();
        Invoke("goBall", 1);
    }
}
