using Unity.VisualScripting;
using UnityEngine;

public class ballControl : MonoBehaviour
{
    private Rigidbody rb;

    [SerializeField] float ballSpeed = 10.0f;

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
    

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        Invoke("goBall", 2);
    }

    void ResetBall() {
        rb.linearVelocity = Vector3.zero;
        transform.position = Vector3.zero;
    }

    void RestartGame() {
        ResetBall();
        Invoke("goBall", 1);
    }

    // void onCollisionEnter (Collision coll) {
    //     if (coll.collider.CompareTag("Player")) {
    //         Vector3 vel = rb.linearVelocity;
    //         vel.x = rb.linearVelocity.x;
    //         vel.z = (rb.linearVelocity.z / 2.0f) + (coll.collider.attachedRigidbody.linearVelocity.z / 3.0f);
    //         rb.linearVelocity = vel;
    //     }
    //     if (coll.collider.CompareTag("Wall")) {
    //         Vector3 vel = rb.linearVelocity;
    //         vel.x = rb.linearVelocity.x;
    //         vel.z = (rb.linearVelocity.z / 2.0f) + (coll.collider.attachedRigidbody.linearVelocity.z / 3.0f);
    //         rb.linearVelocity = vel;
    //     }
    // }

    // Update is called once per frame
    void Update()
    {
        
    }
}
