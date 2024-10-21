using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChatBtn : MonoBehaviour
{
    [SerializeField] GameObject chatUI;
    private bool isChatUIOn = false;

    void Start()
    {
        chatUI.SetActive(false);
    }

    public void SettingUI()
    {
        isChatUIOn = !isChatUIOn;
        if (isChatUIOn)
            chatUI.SetActive(true);
        else chatUI.SetActive(false);
    }
}
