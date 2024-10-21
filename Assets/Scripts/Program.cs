using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

public class GameServer
{
    private TcpListener tcpListener;
    private List<ClientHandler> clients = new List<ClientHandler>();
    private List<Game> games = new List<Game>();

    public GameServer(int port)
    {
        tcpListener = new TcpListener(IPAddress.Any, port);
    }

    public void Start()
    {
        tcpListener.Start();
        Console.WriteLine("Server started. Waiting for connections...");

        while (true)
        {
            TcpClient tcpClient = tcpListener.AcceptTcpClient();
            ClientHandler clientHandler = new ClientHandler(tcpClient, this);
            clients.Add(clientHandler);

            Thread clientThread = new Thread(new ThreadStart(clientHandler.Handle));
            clientThread.Start();
        }
    }

    public void BroadcastMessage(string message, List<ClientHandler> recipients)
    {
        foreach (ClientHandler client in recipients)
        {
            client.SendMessage(message);
        }
    }

    public void RemoveClient(ClientHandler client)
    {
        clients.Remove(client);
    }

    public void CreateGame(ClientHandler player1, ClientHandler player2)
    {
        Game game = new Game(player1, player2);
        games.Add(game);
        game.Start();
    }
}

public class ClientHandler
{
    private TcpClient tcpClient;
    private NetworkStream stream;
    private GameServer server;
    private Game currentGame;

    public ClientHandler(TcpClient client, GameServer server)
    {
        this.tcpClient = client;
        this.server = server;
        stream = client.GetStream();
    }

    public void Handle()
    {
        byte[] buffer = new byte[1024];
        int bytesRead;

        while (true)
        {
            bytesRead = 0;
            try
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
            }
            catch
            {
                break;
            }

            if (bytesRead == 0)
                break;

            string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
            ProcessMessage(message);
        }

        server.RemoveClient(this);
        tcpClient.Close();
    }

    public void SendMessage(string message)
    {
        byte[] buffer = Encoding.ASCII.GetBytes(message);
        stream.Write(buffer, 0, buffer.Length);
    }

    private void ProcessMessage(string message)
    {
        NetworkMessage networkMessage = JsonConvert.DeserializeObject<NetworkMessage>(message);

        switch (networkMessage.Type)
        {
            case "START_GAME":
                // 게임 시작 로직
                break;
            case "ADD_CARD":
                // 카드 추가 로직
                break;
            case "PUT_CARD":
                // 카드 배치 로직
                break;
            case "ATTACK":
                // 공격 로직
                break;
            case "END_TURN":
                // 턴 종료 로직
                break;
        }

        // 메시지를 상대방에게 전달
        if (currentGame != null)
        {
            currentGame.BroadcastMessage(message, this);
        }
    }

    public void SetGame(Game game)
    {
        currentGame = game;
    }
}

public class Game
{
    private ClientHandler player1;
    private ClientHandler player2;
    private ClientHandler currentPlayer;

    public Game(ClientHandler player1, ClientHandler player2)
    {
        this.player1 = player1;
        this.player2 = player2;
        player1.SetGame(this);
        player2.SetGame(this);
    }

    public void Start()
    {
        // 랜덤으로 첫 번째 플레이어 선택
        currentPlayer = new Random().Next(2) == 0 ? player1 : player2;

        // 게임 시작 메시지 전송
        NetworkMessage startMessage = new NetworkMessage { Type = "GAME_START" };
        player1.SendMessage(JsonConvert.SerializeObject(startMessage));
        player2.SendMessage(JsonConvert.SerializeObject(startMessage));

        // 첫 번째 플레이어의 턴 시작
        StartTurn();
    }

    public void BroadcastMessage(string message, ClientHandler sender)
    {
        ClientHandler recipient = (sender == player1) ? player2 : player1;
        recipient.SendMessage(message);
    }

    private void StartTurn()
    {
        NetworkMessage turnMessage = new NetworkMessage { Type = "START_TURN", IsMine = true };
        currentPlayer.SendMessage(JsonConvert.SerializeObject(turnMessage));

        turnMessage.IsMine = false;
        ClientHandler otherPlayer = (currentPlayer == player1) ? player2 : player1;
        otherPlayer.SendMessage(JsonConvert.SerializeObject(turnMessage));
    }

    public void EndTurn()
    {
        currentPlayer = (currentPlayer == player1) ? player2 : player1;
        StartTurn();
    }
}

class Program
{
    static void Main(string[] args)
    {
        GameServer server = new GameServer(8888);
        server.Start();
    }
}