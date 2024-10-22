using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using UnityEngine;
using Newtonsoft.Json;

public class Network : MonoBehaviour
{
    public static Network Inst { get; private set; }
    void Awake() => Inst = this;

    Chat chat;
    bool bServer = false;
    bool bConnect = false;

    Socket socketListen = null;
    Socket socket = null;

    Thread thread = null;
    bool bThreadBegin = false;

    Buffer bufferSend;
    Buffer bufferReceive;

    public string name;
    void Start()
    {
        chat = GetComponent<Chat>();
        bufferSend = new Buffer();
        bufferReceive = new Buffer();
    }

    public void ServerStart(int port, int backlog = 10)
    {
        socketListen = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        IPEndPoint ep = new IPEndPoint(IPAddress.Any, port);
        socketListen.Bind(ep);

        socketListen.Listen(backlog);
        bServer = true;
        Debug.Log("Server Start");
        StartThread();
    }

    public bool IsServer()
    {
        return bServer;
    }

    bool StartThread()
    {
        ThreadStart threadDelegate = new ThreadStart(ThreadProc);
        thread = new Thread(threadDelegate);
        thread.Start();

        bThreadBegin = true;

        return true;
    }

    public void ThreadProc()
    {
        while (bThreadBegin)
        {
            AcceptClient();

            if (socket != null && bConnect == true)
            {
                SendUpdate();
                ReceiveUpdate();
            }

            Thread.Sleep(10);
        }
    }

    public void ClientStart(string address, int port)
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(address, port);

        bConnect = true;
        Debug.Log("Client Start");

        StartThread();
    }

    void AcceptClient()
    {
        if (socketListen != null && socketListen.Poll(0, SelectMode.SelectRead))
        {
            socket = socketListen.Accept();
            bConnect = true;

            Debug.Log("Client Connect");
        }
    }

    public bool IsConnect()
    {
        return bConnect;
    }

    public int Send(byte[] bytes)
    {
        return bufferSend.Write(bytes, bytes.Length);
    }
    public int Send(byte[] bytes, int length)
    {
        return bufferSend.Write(bytes, length);
    }

    public int Receive(ref byte[] bytes, int length)
    {
        return bufferReceive.Read(ref bytes, length);
    }

    private void Update()
    {
        if (IsConnect())
        {
            byte[] bytes = new byte[1024];
            int length = Receive(ref bytes, bytes.Length);
            NetworkMessage message;
            if (length > 0)
            {
                string str = System.Text.Encoding.UTF8.GetString(bytes);
                message = JsonConvert.DeserializeObject<NetworkMessage>(str);

                switch (message.MyType)
                {
                    case Type.ATTACK:
                        EntityManager.Inst.Attack(message.Card.name, message.AttackTarget.name);
                        break;

                    case Type.PUT_CARD:
                        CardManager.Inst.TryPutCard(false);
                        break;

                    case Type.END_TURN:
                        TurnManager.Inst.EndTurn();
                        break;

                    default:
                        chat.AddTalk(str);
                        chat.UpdateUI();
                        break;
                }
            }
        }
    }

    void SendUpdate()
    {
        if (socket.Poll(0, SelectMode.SelectWrite))//socket이 있나없나 미리보기?
        {
            byte[] bytes = new byte[1024];

            int length = bufferSend.Read(ref bytes, bytes.Length);
            while (length > 0)
            {
                socket.Send(bytes, length, SocketFlags.None);
                length = bufferSend.Read(ref bytes, bytes.Length);
            }
        }
    }

    void ReceiveUpdate()
    {
        while (socket.Poll(0, SelectMode.SelectRead))
        {
            byte[] bytes = new byte[1024];

            int length = socket.Receive(bytes, bytes.Length, SocketFlags.None);
            if (length > 0)
            {
                bufferReceive.Write(bytes, length);
            }
        }
    }
}


public enum Type { PUT_CARD, ATTACK, END_TURN, CHAT }
public class NetworkMessage
{
    public Type MyType { get; set; }
    public Card Card { get; set; }
    public Card AttackTarget { get; set; }
    public NetworkMessage() { }

    public NetworkMessage(Type type, Card card)
    {
        MyType = type;
        Card = card;
    }
    public NetworkMessage(Type type, Card card, Card target)
    {
        MyType = type;
        Card = card;
        AttackTarget = target;
    }
}