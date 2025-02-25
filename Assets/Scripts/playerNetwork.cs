using Unity.Netcode;
using UnityEngine;

// Main script for player objects
public class playerNetwork : NetworkBehaviour
{
    [SerializeField] private float acceleration = 5f;
    [SerializeField] private float deceleration = 5f;
    [SerializeField] private float maxSpeed = 20f;
    public Rigidbody rb; // player rigidbody
    public Vector3 InputKey; // input keys
    

    // awake is called when script is first made
    private void Awake() 
    {
        if (!IsOwner) return; // if not owner of player object return
        rb = GetComponent<Rigidbody>(); // get rigidbody of this player object
    }

    // Test for syncing variable value, randomly generated integer:
    private NetworkVariable<int> randomNumber = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    // New NetworkVariable int, (starting value, read permissions, write permissions)
    // Everyone is able to read the value, only owners of this specific network variable (every player has one) can alter the value of their owned variable

    // New custom data struct:
    private NetworkVariable<MyCustomData> customIntBool = new NetworkVariable<MyCustomData>(
        new MyCustomData {
            _int = 56,
            _bool = true,
            message = "parkinglotscarecrow",
        }, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);


    // Data serialization (which is handled for set data types like the int in random num) is the process of converting
    // data objects in a data structure into a byte stream for storage, transfer and distribution. This is used in sending data
    // over objects.
    // For MyCustomData, the data within must be serialized:
    public struct MyCustomData : INetworkSerializable {
        public int _int;
        public bool _bool;
        public string message;

        // Importing base serializer:
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Altering the serialize method for MyCustomData:
            serializer.SerializeValue(ref _int);
            serializer.SerializeValue(ref _bool);
            serializer.SerializeValue(ref message);
        }
    }

    // Altering OnNetworkSpawn method to see values of variables and data structs:
    public override void OnNetworkSpawn() {
        // When randomNumber value changes,
        randomNumber.OnValueChanged += (int previousValue, int newValue) => {
        // Output OnwerClientID + that owner's value of the randomNumber
        Debug.Log(OwnerClientId + "; randomNumber: " + randomNumber.Value);
        };
        // This is better than using Debug.Log in the update method as we only need to if the value of randomNumber changes correctly between players when it actually does change value
        // so this way there are less repeated messages.

        // Custom Data struct version:
        customIntBool.OnValueChanged += (MyCustomData previousValue, MyCustomData newValue) => {
        // Output OnwerClientID + that owner's values of the custom data struct
        Debug.Log(OwnerClientId + "; randomNumber: " + newValue._int + "; " + newValue._bool + "; " + newValue.message);
        };
    }


    // Update is called once per frame
    private void Update()
    {
        // if NOT the local owner of this player object, return
        if (!IsOwner) return;    

        InputKey = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));

        // Press 'I' to generate new random number so can test sync between host and client:
        if (Input.GetKeyDown(KeyCode.I)) {
            randomNumber.Value = UnityEngine.Random.Range(0, 100);
        }

        // Press 'C' to set value of MyCustomData to specified variable values:
        if (Input.GetKeyDown(KeyCode.C)) {
            customIntBool.Value = new MyCustomData {
                _int = 10,
                _bool = false,
                message = "MESSAGE REDACTED",
            }; 
        }

        // Press R 
        if (Input.GetKeyDown(KeyCode.R)) {
            TestServerRpc();
        }
    }

    private void FixedUpdate() 
    {
        if (!IsOwner) return;

        // Apply movement locally first for responsive input
        if (InputKey != Vector3.zero)
        {
            // Local prediction
            Vector3 force = InputKey * acceleration;
            rb.AddForce(force, ForceMode.Force);

            if (rb.linearVelocity.magnitude > maxSpeed) 
            {
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
            }

            // Then send to server
            RequestAccelerateServerRpc(InputKey);
        }
        else 
        {
            // Local prediction for deceleration
            if (rb.linearVelocity.magnitude > 0.1f)
            {
                rb.AddForce(rb.linearVelocity * -deceleration, ForceMode.Force);
            }
            
            // Then send to server
            RequestDecelerateServerRpc();
        }
    }

    // Collision check
    private void OnCollisionEnter(Collision collision)
    {
        // Allow client-side collision response for immediate feedback
        if (!IsServer && IsOwner)
        {
            HandleCollisionLocally(collision);
        }
        
        if (!IsServer) return;
        
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Ball"))
        {
            // Get the other player's NetworkBehaviour component
            playerNetwork otherPlayer = collision.gameObject.GetComponent<playerNetwork>();
            
            // Only apply force if we're colliding with another player
            if (otherPlayer != null)
            {
                // Calculate relative velocity
                Vector3 relativeVelocity = collision.relativeVelocity;
                float forceMagnitude = relativeVelocity.magnitude;
                Vector3 forceDirection = collision.contacts[0].normal;
                
                // Apply force to both objects in opposite directions
                float forceMultiplier = 0.2f; // Adjust this value to control bounce strength
                Vector3 force = forceDirection * forceMagnitude * forceMultiplier;
                
                // Apply opposite forces to each player
                rb.AddForce(force, ForceMode.Impulse);
                otherPlayer.rb.AddForce(-force, ForceMode.Impulse);
            }
        }
    }

    // Client-side collision prediction
    private void HandleCollisionLocally(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Ball"))
        {
            Vector3 relativeVelocity = collision.relativeVelocity;
            float forceMagnitude = relativeVelocity.magnitude;
            Vector3 forceDirection = collision.contacts[0].normal;
            
            float forceMultiplier = 0.2f;
            Vector3 force = forceDirection * forceMagnitude * forceMultiplier;
            
            // Apply local force for immediate feedback
            rb.AddForce(force, ForceMode.Impulse);
        }
    }

    // Server Rpc
    // When client calls ServerRpc function, the server runs the function NOT the client
    // Clients can use Server Remote Procedure Calls to ask for movement / actions.

    // Testing ServerRpc
    [ServerRpc]
    private void  TestServerRpc() 
    {
        Debug.Log("TestServerRpc " + OwnerClientId);
    }


    // Asking for acceleration in movement
    [ServerRpc]
    private void RequestAccelerateServerRpc(Vector3 InputKey, ServerRpcParams rpcParams = default)
    {

        // server is authoritative
        Vector3 force = InputKey * acceleration;
        rb.AddForce(force, ForceMode.Force);

        if (rb.linearVelocity.magnitude > maxSpeed) 
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
        
        // Sync only to other clients, not back to the owner
        var clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };
        
        SyncMovementClientRpc(transform.position, clientRpcParams);
        SyncVelocityClientRpc(rb.linearVelocity, clientRpcParams);
    }

    // Asking for deceleration in movement
    [ServerRpc]
    private void RequestDecelerateServerRpc(ServerRpcParams rpcParams = default)
    {
        // Only apply deceleration if we're not in a collision
        if (rb.linearVelocity.magnitude > 0.1f)  // Small threshold to prevent jitter
        {
            rb.AddForce(rb.linearVelocity * -deceleration, ForceMode.Force);
        }
        
        SyncMovementClientRpc(transform.position);
        SyncVelocityClientRpc(rb.linearVelocity);
    }


    // Client Rpc
    // Used to be run from server to send messages to clients.
    // Clients cannot run this Rpc.

    // Testing ClientRpc
    [ClientRpc]
    private void TestClientRpc()
    {
        Debug.Log("TestClientRpc " + OwnerClientId);
    }


    // Syncing movement to clients
    [ClientRpc]
    private void SyncMovementClientRpc(Vector3 newPos, ClientRpcParams rpcParams = default)
    {
        if (IsOwner) return;

        // Update pos for client 
        transform.position = newPos;
    }

    // Syncing velocity to clients
    [ClientRpc]
    private void SyncVelocityClientRpc(Vector3 newVelocity, ClientRpcParams rpcParams = default)
    {
        if (IsOwner) return;

        // Update velocity for client
        rb.linearVelocity = newVelocity;
    }
}
