using UnityEngine;
using System.Text;
using Newtonsoft.Json;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Inst { get; private set; }
    private Network network;
    private byte[] readBuffer = new byte[1024];

    public bool IsServer => network.IsServer();
    public bool IsConnected => network.IsConnect();
    public string PlayerName { get; private set; }

    void Awake()
    {
        Inst = this;
        network = GetComponent<Network>();
    }

    void Start()
    {
        PlayerName = $"Player_{Random.Range(1000, 9999)}";
    }

    public void StartServer()
    {
        StartServer(10000);
    }

    public void StartClient()
    {
        StartClient("127.0.0.1", 10000);
    }
    public void StartServer(int port)
    {
        try
        {
            network.ServerStart(port);
            network.name = PlayerName;
            Debug.Log($"Server started with name: {PlayerName}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error starting server: {e.Message}");
        }
    }

    public void StartClient(string address, int port)
    {
        try
        {
            network.ClientStart(address, port);
            network.name = PlayerName;
            Debug.Log($"Client started with name: {PlayerName}");

            // 접속 알림
            SendMessage(new NetworkMessage
            {
                Type = "CLIENT_CONNECTED",
                PlayerName = PlayerName
            });
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error starting client: {e.Message}");
        }
    }

    void Update()
    {
        if (network != null && network.IsConnect())
        {
            int length = network.Receive(ref readBuffer, readBuffer.Length);
            if (length > 0)
            {
                string receivedMessage = Encoding.UTF8.GetString(readBuffer, 0, length);
                ProcessReceivedMessage(receivedMessage);
            }
        }
    }

    private void ProcessReceivedMessage(string message)
    {
        try
        {
            var data = JsonConvert.DeserializeObject<NetworkMessage>(message);

            // 서버와 클라이언트 모두 메시지 처리
            switch (data.Type)
            {
                case "CLIENT_CONNECTED":
                    if (IsServer)
                    {
                        Debug.Log($"Client connected: {data.PlayerName}");
                        // 새로운 클라이언트에게 게임 시작 메시지 전송
                        SendMessage(new NetworkMessage
                        {
                            Type = "START_GAME",
                            IsMine = false,
                            PlayerName = data.PlayerName
                        });
                    }
                    break;

                case "START_GAME":
                    if (!IsServer)
                    {
                        GameManager.Inst.StartGame();
                    }
                    break;

                case "ADD_CARD":
                    CardManager.Inst.AddCard(data.IsMine);
                    break;

                case "PUT_CARD":
                    CardManager.Inst.TryPutCard(data.IsMine);
                    if (IsServer)
                    {
                        // 서버는 다른 플레이어에게도 전달
                        data.IsMine = false;
                        SendMessage(data);
                    }
                    break;

                case "ATTACK":
                    EntityManager.Inst.Attack(data.AttackerName, data.DefenderName);
                    if (IsServer)
                    {
                        // 서버는 다른 플레이어에게도 전달
                        SendMessage(data);
                    }
                    break;

                case "END_TURN":
                    TurnManager.Inst.EndTurn();
                    if (IsServer)
                    {
                        // 서버는 다른 플레이어에게도 전달
                        SendMessage(data);
                    }
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error processing message: {e.Message}");
        }
    }

    public void SendMessage(NetworkMessage message)
    {
        if (!network.IsConnect()) return;

        try
        {
            string jsonMessage = JsonConvert.SerializeObject(message);
            byte[] messageBytes = Encoding.UTF8.GetBytes(jsonMessage);
            network.Send(messageBytes, messageBytes.Length);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error sending message: {e.Message}");
        }
    }
}

public class NetworkMessage
{
    public string Type { get; set; }
    public bool IsMine { get; set; }
    public string PlayerName { get; set; }
    public string AttackerName { get; set; }
    public string DefenderName { get; set; }
    // 추가 필요한 데이터 필드...
}