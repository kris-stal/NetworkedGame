using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// Manager for handling the Network Stress Test
// Handles functionality in stress testing the network by serializing data to share between host and client.
public class NetworkStressTest : NetworkBehaviour
{
    // VARIABLES //
    // Network variables to share
    private NetworkVariable<int> randomNumber = new NetworkVariable<int>(
        1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<MyCustomData> customData = new NetworkVariable<MyCustomData>(
        new MyCustomData { _int = 0, _bool = false, message = "Initial", randomMatrix = new List<float>(new float[1000]) },
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    // My Custom Data structure to share
    public struct MyCustomData : INetworkSerializable
    {
        public int _int;
        public bool _bool;
        public string message;
        public Vector3 position;
        public float randomFloat;
        public List<float> randomMatrix;

        public void Initialize(int matrixSize)
        {
            _int = 0;
            _bool = false;
            message = "Initial";
            position = Vector3.zero;
            randomFloat = 0f;
            randomMatrix = new List<float>(new float[matrixSize]); // Pre-allocate matrix
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _int);
            serializer.SerializeValue(ref _bool);
            serializer.SerializeValue(ref message);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref randomFloat);

            int count = randomMatrix.Count;
            serializer.SerializeValue(ref count);

            if (serializer.IsWriter)
            {
                for (int i = 0; i < count; i++)
                {
                    float value = randomMatrix[i];
                    serializer.SerializeValue(ref value);
                }
            }
            else
            {
                if (randomMatrix == null || randomMatrix.Count != count)
                    randomMatrix = new List<float>(new float[count]);

                for (int i = 0; i < count; i++)
                {
                    float value = 0;
                    serializer.SerializeValue(ref value);
                    randomMatrix[i] = value;
                }
            }
        }
    }




    private void Start()
    {
        if (IsOwner)
        {
            // Initialize MyCustomData with a pre-allocated matrix
            MyCustomData data = new MyCustomData();
            data.Initialize(100);
            customData.Value = data;

            StartCoroutine(RandomDataSyncRoutine());
            StartCoroutine(RPCSpamRoutine());
        }
    }

    public override void OnNetworkSpawn()
    {
        randomNumber.OnValueChanged += (prev, next) => Debug.Log(OwnerClientId + " Random: " + next);
        customData.OnValueChanged += (prev, next) =>
        { 
            Debug.Log(OwnerClientId + " Custom Data: " + next._int + ", " + next._bool + ", " + next.message);
            Debug.Log("Matrix First Value: " + next.randomMatrix[0] + ", Last Value: " + next.randomMatrix[next.randomMatrix.Count - 1]);
        };
    }

    

    // DATA ROUTINES //
    // Routine to generate new variables for existing data structure every 2 seconds
    private IEnumerator RandomDataSyncRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(2f);

            // Modify existing values instead of reallocating memory
            for (int i = 0; i < 10; i++)
            {
                customData.Value.randomMatrix[i] = Random.Range(-1000f, 1000f);
            }

            MyCustomData updatedData = customData.Value;
            updatedData._int = Random.Range(0, 500);
            updatedData._bool = Random.value > 0.5f;
            updatedData.message = "Data-" + Random.Range(0, 1000);
            updatedData.position = new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f));
            updatedData.randomFloat = Random.Range(0f, 100f);

            customData.Value = updatedData; // Assign the modified struct
        }
    }


    // Routine to consistently send new data (a big array of size 1000) across to server from clients
    private IEnumerator RPCSpamRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f); // 
            int[] bigArray = new int[50]; //
            for (int i = 0; i < bigArray.Length; i++) bigArray[i] = Random.Range(0, 1000);
            
            SendSpamServerRpc(bigArray);
        }
    }



    // RPCS //
    // Clients call this RPC to send the big array to host
    [ServerRpc]
    private void SendSpamServerRpc(int[] bigArray, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"Received RPC from {rpcParams.Receive.SenderClientId}, Data Size: {bigArray.Length} elements");
        
        // Optionally, broadcast the RPC to all clients to amplify the load
        SendSpamClientRpc(bigArray);
    }


    // Clients recieve back the big array RPC from the host, causing more network stress
    [ClientRpc]
    private void SendSpamClientRpc(int[] bigArray)
    {
        Debug.Log($"Client received RPC with {bigArray.Length} elements.");
    }
}
