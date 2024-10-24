using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class Intro : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] GameObject titlePanel;
    public void OnPointerClick(PointerEventData eventData)
    {
        titlePanel.SetActive(true);
    }
}
