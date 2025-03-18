using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NetworkStressTest : NetworkBehaviour
{
    private NetworkVariable<int> randomNumber = new NetworkVariable<int>(
        1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<MyCustomData> customData = new NetworkVariable<MyCustomData>(
        new MyCustomData { _int = 0, _bool = false, message = "Initial", randomMatrix = new List<float>(new float[1000]) },
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    
    public struct MyCustomData : INetworkSerializable
    {
        public int _int;
        public bool _bool;
        public string message;
        public Vector3 position;
        public float randomFloat;
        public List<float> randomMatrix;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _int);
            serializer.SerializeValue(ref _bool);
            serializer.SerializeValue(ref message);
            serializer.SerializeValue(ref position);
            serializer.SerializeValue(ref randomFloat);

            // Manually serialize the large matrix
            if (serializer.IsWriter)
            {
                int count = randomMatrix.Count;
                serializer.SerializeValue(ref count);
                for (int i = 0; i < count; i++)
                {
                    float value = randomMatrix[i];
                    serializer.SerializeValue(ref value);
                }
            }
            else
            {
                int count = 0;
                serializer.SerializeValue(ref count);
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
            StartCoroutine(RandomDataSyncRoutine());
            StartCoroutine(RPCSpamRoutine());
        }
    }

    private IEnumerator RandomDataSyncRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.005f); // Adjust frequency of updates

            // Generate random values
            randomNumber.Value = Random.Range(0, 1000);

            // Generate the matrix
            List<float> largeMatrix = new List<float>(10000);
            for (int i = 0; i < 10000; i++)
            {
                largeMatrix.Add(Random.Range(-1000f, 1000f));
            }

            customData.Value = new MyCustomData
            {
                _int = Random.Range(0, 500),
                _bool = Random.value > 0.5f,
                message = "Data-" + Random.Range(0, 1000),
                position = new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f)),
                randomFloat = Random.Range(0f, 100f),
                randomMatrix = largeMatrix,
            };
        }
    }

    private IEnumerator RPCSpamRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.005f); // 200 RPCs per second for each client
            int[] bigArray = new int[5000]; // 20KB per RPC
            for (int i = 0; i < bigArray.Length; i++) bigArray[i] = Random.Range(0, 1000);
            
            SendSpamServerRpc(bigArray);
        }
    }

    [ServerRpc]
    private void SendSpamServerRpc(int[] bigArray, ServerRpcParams rpcParams = default)
    {
        Debug.Log($"Received RPC from {rpcParams.Receive.SenderClientId}, Data Size: {bigArray.Length} elements");
        
        // Optionally, broadcast the RPC to all clients to amplify the load
        SendSpamClientRpc(bigArray);
    }

    [ClientRpc]
    private void SendSpamClientRpc(int[] bigArray)
    {
        Debug.Log($"Client received RPC with {bigArray.Length} elements.");
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
}
