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
                // ���� ���� ����
                break;
            case "ADD_CARD":
                // ī�� �߰� ����
                break;
            case "PUT_CARD":
                // ī�� ��ġ ����
                break;
            case "ATTACK":
                // ���� ����
                break;
            case "END_TURN":
                // �� ���� ����
                break;
        }

        // �޽����� ���濡�� ����
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
        // �������� ù ��° �÷��̾� ����
        currentPlayer = new Random().Next(2) == 0 ? player1 : player2;

        // ���� ���� �޽��� ����
        NetworkMessage startMessage = new NetworkMessage { Type = "GAME_START" };
        player1.SendMessage(JsonConvert.SerializeObject(startMessage));
        player2.SendMessage(JsonConvert.SerializeObject(startMessage));

        // ù ��° �÷��̾��� �� ����
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