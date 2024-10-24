using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using UnityEngine;
using DG.Tweening.Core.Easing;
using System;

public class NetworkManager : MonoBehaviour
{
    [SerializeField] GameManager gameManager;

    public delegate void MessageReceiveHandler(byte[] message, int length);
    public event MessageReceiveHandler OnMessageReceived;

    public event Action OnConnected;

    bool bServer = false;
    bool bConnect = false;

    Socket socketServer = null;
    Socket socketClient = null;

    bool bThread = false;
    Thread thread = null;

    Buffer bufferSend;

    public string ip;

    void Start()
    {
        bufferSend = new Buffer();
    }

    public int Send(byte[] bytes, int length)
    {
        return bufferSend.Write(bytes, length);
    }

    public bool IsServer()
    {
        return bServer;
    }

    public bool IsConnect()
    {
        return bConnect;
    }

    public void StartServer()
    {
        StartServer(10000, 10);
    }

    public void StartClient()
    {
        StartClient(ip, 10000);
    }

    public void StartServer(int port, int backlog = 10)
    {
        socketServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socketServer.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socketServer.Bind(new IPEndPoint(IPAddress.Any, port));
        socketServer.Listen(backlog);

        bServer = true;
        Debug.Log("Server Start");
        StartThread();
    }

    public void StopServer()
    {
        bThread = false;
        if (thread != null)
        {
            thread.Join();
            thread = null;
        }

        Disconnect();

        if (socketServer != null)
        {
            socketServer.Close();
            socketServer = null;
        }

        bServer = false;
    }

    public bool StartClient(string address, int port)
    {
        try
        {
            socketClient = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketClient.Connect(address, port);
            if (StartThread())
            {
                bConnect = true;
                UnityMainThreadDispatcher._instance.Enqueue(() =>
                {
                    OnConnected?.Invoke();
                });
                return true;
            }
        }
        catch (SocketException ex)
        {
            Debug.LogError($"Connection failed: {ex.Message}");
            Disconnect();
        }
        return false;
    }

    public void Disconnect()
    {
        bConnect = false;

        if (socketClient != null)
        {
            socketClient.Shutdown(SocketShutdown.Both);
            socketClient.Close();
            socketClient = null;
        }
    }

    bool StartThread()
    {
        bThread = true;
        thread = new Thread(new ThreadStart(NetworkUpdate));
        thread.Start();

        return true;
    }

    public void NetworkUpdate()
    {
        try
        {
            while (bThread)
            {
                WaitClient();

                if (socketClient != null && bConnect == true)
                {
                    UpdateSend();
                    UpdateReceive();
                }

                Thread.Sleep(5);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Network error: {e.Message}");
            Disconnect();
        }
    }


    void WaitClient()
    {
        if (socketServer != null && socketServer.Poll(0, SelectMode.SelectRead))
        {
            socketClient = socketServer.Accept();
            bConnect = true;
            Debug.Log("Client connected successfully");

            // 메인 스레드에서 이벤트를 발생시키기 위해 UnityMainThreadDispatcher 사용
            UnityMainThreadDispatcher._instance.Enqueue(() =>
            {
                OnConnected?.Invoke();
            });
        }
    }


    void UpdateSend()
    {
        if (socketClient.Poll(0, SelectMode.SelectWrite))
        {
            byte[] bytes = new byte[1024];

            int length = bufferSend.Read(ref bytes, bytes.Length);
            while (length > 0)
            {
                socketClient.Send(bytes, length, SocketFlags.None);
                length = bufferSend.Read(ref bytes, bytes.Length);
            }
        }
    }


    void UpdateReceive()
    {
        try
        {
            while (socketClient != null && socketClient.Connected && socketClient.Poll(0, SelectMode.SelectRead))
            {
                byte[] bytes = new byte[1024];

                int length = socketClient.Receive(bytes, bytes.Length, SocketFlags.None);
                if (length > 0)
                {
                    Debug.Log($"Received data length: {length}");
                    var messageBytes = new byte[length];
                    Array.Copy(bytes, messageBytes, length);
                    UnityMainThreadDispatcher._instance.Enqueue(() => {
                        try
                        {
                            OnMessageReceived?.Invoke(messageBytes, length);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Error processing received message: {e.Message}");
                        }
                    });
                }
                else if (length == 0)
                {
                    Debug.Log("Connection closed by remote host");
                    Disconnect();
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in UpdateReceive: {ex.Message}");
            Disconnect();
        }
    }

    private void CloseSocket()
    {
        if (socketServer != null)
        {
            socketServer.Shutdown(SocketShutdown.Both);
            socketServer.Close();
            socketServer = null;
        }

        if (socketClient != null)
        {
            socketClient.Shutdown(SocketShutdown.Both);
            socketClient.Close();
            socketClient = null;
        }
    }
    private void OnDestroy()
    {
        CloseSocket();
    }
    private void OnApplicationQuit()
    {
        CloseSocket();
    }
}