using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TitlePanel : MonoBehaviour
{
    public void StartGameClick()
    {
        GameManager.Inst.StartGame();
        Active(false);
    }

    public void Active(bool isActive)
    {
        gameObject.SetActive(isActive);
    }
}
