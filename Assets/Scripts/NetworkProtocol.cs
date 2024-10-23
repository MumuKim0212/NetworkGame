using System;
using System.Collections;
using Newtonsoft.Json;
using UnityEngine;

public enum NetworkMessageType
{
    Waiting,
    GameStart,
    TurnEnd,
    CardPlay,
    EntityAttack,
    EntitySpawn,
    GameOver
}

[Serializable]
public class NetworkMessage
{
    public NetworkMessageType Type { get; set; }
    public string Data { get; set; }
}

[Serializable]
public class CardPlayData
{
    public string CardId { get; set; }
    public float SpawnPosX { get; set; }
    public float SpawnPosY { get; set; }
}

[Serializable]
public class EntityAttackData
{
    public int AttackerIndex { get; set; }
    public int DefenderIndex { get; set; }
}

public class NetworkProtocol : MonoBehaviour
{
    private NetworkManager networkManager;
    NetworkMessage message = new NetworkMessage();
    private bool gameStarted = false;

    void Start()
    {
        networkManager = GetComponent<NetworkManager>();
        networkManager.OnMessageReceived += OnMessageReceived;

        // 연결 상태 변경 이벤트 추가
        networkManager.OnConnected += OnConnected;
    }


    // 연결 완료시 호출되는 메서드
    private void OnConnected()
    {
        Debug.Log($"OnConnected - IsServer: {networkManager.IsServer()}");
        if (networkManager.IsServer())
        {
            // 서버는 자신의 게임을 먼저 시작하고
            Debug.Log("Server starting game and sending start message to client");
            GameManager.Inst.StartNetworkGame(true);  // 서버는 선공
            // 클라이언트에게 게임 시작 메시지를 보냄
            SendMessage(NetworkMessageType.GameStart, "false");
        }
    }

    private void OnMessageReceived(byte[] messageBuffer, int length)
    {
        string jsonMessage = System.Text.Encoding.UTF8.GetString(messageBuffer, 0, length);
        try
        {
            Debug.Log($"Received raw message: {jsonMessage}");
            NetworkMessage message = JsonConvert.DeserializeObject<NetworkMessage>(jsonMessage);
            Debug.Log($"Received message type: {message.Type}");
            HandleMessage(message);
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing message: {e.Message}");
            Debug.LogError($"Received json: {jsonMessage}");
        }
    }

    private void HandleMessage(NetworkMessage message)
    {
        Debug.Log($"Handling message type: {message.Type}, IsServer: {networkManager.IsServer()}");

        switch (message.Type)
        {
            case NetworkMessageType.GameStart:
                if (!networkManager.IsServer() && !gameStarted)
                {
                    Debug.Log("Client received game start message, starting game");
                    gameStarted = true;
                    GameManager.Inst.StartNetworkGame(false);  // 클라이언트는 후공
                }
                break;

            case NetworkMessageType.CardPlay:
                try
                {
                    CardPlayData cardData = JsonConvert.DeserializeObject<CardPlayData>(message.Data);
                    Debug.Log($"Received CardPlay message: CardId={cardData.CardId}, Pos=({cardData.SpawnPosX}, {cardData.SpawnPosY})");
                    CardManager.Inst.OnReceiveCardPlay(cardData);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error handling CardPlay message: {e.Message}");
                    Debug.LogError($"Message data: {message.Data}");
                }
                break;

            case NetworkMessageType.EntityAttack:
                EntityAttackData attackData = JsonConvert.DeserializeObject<EntityAttackData>(message.Data);
                EntityManager.Inst.OnReceiveAttack(attackData);
                break;

            case NetworkMessageType.TurnEnd:
                Debug.Log("Received TurnEnd message");
                TurnManager.Inst.EndTurn();
                break;

            case NetworkMessageType.GameOver:
                bool isWin = message.Data == "true";
                StartCoroutine(GameManager.Inst.GameOver(!isWin));
                break;
        }
    }

    public void SendMessage(NetworkMessageType type, object data = null)
    {
        message.Type = type;
        message.Data = data != null ? JsonConvert.SerializeObject(data) : null;

        string jsonMessage = JsonConvert.SerializeObject(message);
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(jsonMessage);
        networkManager.Send(bytes, bytes.Length);
        Debug.Log($"Sending message type: {type}, Data: {message.Data}");
    }

    void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnMessageReceived -= OnMessageReceived;
            networkManager.OnConnected -= OnConnected;
        }
    }
}