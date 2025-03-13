using Unity.Netcode;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NetworkStressTest : NetworkBehaviour
{
    private NetworkVariable<int> randomNumber = new NetworkVariable<int>(
        1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private NetworkVariable<MyCustomData> customData = new NetworkVariable<MyCustomData>(
        new MyCustomData { _int = 0, _bool = false, message = "Initial" },
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
            // serializer.SerializeValue(ref randomMatrix);
        }
    }

    private void Start()
    {
        if (IsOwner)
        {
            
            StartCoroutine(RandomDataSyncRoutine());
        }
    }

    private IEnumerator RandomDataSyncRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.01f); // Adjust frequency of updates

            // Generate random values
            randomNumber.Value = Random.Range(0, 1000);

            customData.Value = new MyCustomData
            {
                _int = Random.Range(0, 500),
                _bool = Random.value > 0.5f,
                message = "Data-" + Random.Range(0, 1000),
                position = new Vector3(Random.Range(-10f, 10f), 0, Random.Range(-10f, 10f)),
                randomFloat = Random.Range(0f, 100f),
                
                // List<float> randomMatrix = new List<string>(),
                // foreach (int i in randomMatrix)
            };
        }
    }

    public override void OnNetworkSpawn()
    {
        randomNumber.OnValueChanged += (prev, next) => Debug.Log(OwnerClientId + " Random: " + next);
        customData.OnValueChanged += (prev, next) => Debug.Log(OwnerClientId + " Custom Data: " + next._int + ", " + next._bool + ", " + next.message);
    }
}
