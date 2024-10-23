using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Chat : MonoBehaviour
{
    NetworkManager network;
    public InputField id;
    public InputField chat;

    List<string> list;
    public Text[] text;
    public Image backUI;

    void Start()
    {
        network = GetComponent<NetworkManager>();
        list = new List<string>();
    }

    public void BeginServer()
    {
        network.StartServer(10000, 10);
        network.name = id.text;
    }

    public void BeginClient()
    {
        network.StartClient("127.0.0.1", 10000);

        network.name = id.text;
    }

    public void AddTalk(string str)
    {
        while (list.Count >= 5)
        {
            list.RemoveAt(0);
        }

        list.Add(str);
        UpdateTalk();
    }

    public void SendTalk()
    {
        string str = network.name + ": " + chat.text;
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str);
        network.Send(bytes, bytes.Length);

        AddTalk(str);
    }

    void UpdateTalk()
    {
        for (int i = 0; i < list.Count; i++)
        {
            text[i].text = list[i];
        }
    }

    public void UpdateUI()
    {
        if (!backUI.IsActive())
        {
            backUI.gameObject.SetActive(true);
        }
    }
}
