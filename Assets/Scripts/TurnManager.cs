using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Random = UnityEngine.Random;

public class TurnManager : MonoBehaviour
{
    public static TurnManager Inst { get; private set; }
    void Awake() => Inst = this;

    [Header("References")]
    [SerializeField] private NetworkProtocol networkProtocol;

    [Header("Develop")]
    [SerializeField][Tooltip("턴의 시작을 정합니다")] ETurnMode eTurnMode;
    [SerializeField][Tooltip("카드 배분이 매우 빨라집니다")] bool fastMode;
    [SerializeField][Tooltip("시작 카드 개수를 정합니다")] int startCardCount;

    [Header("Properties")]
    public bool isLoading; // 게임 끝나면 isLoading을 true로 하면 카드와 엔티티 클릭방지
    public bool myTurn;

    enum ETurnMode { Random, My, Other }
    WaitForSeconds delay05 = new WaitForSeconds(0.5f);
    WaitForSeconds delay07 = new WaitForSeconds(0.7f);

    public static Action<bool> OnAddCard;
    public static event Action<bool> OnTurnStarted;

    void Start()
    {
        // NetworkProtocol 컴포넌트 찾기 (Inspector에서 할당되지 않은 경우)
        if (networkProtocol == null)
            networkProtocol = GetComponent<NetworkProtocol>();
    }

    void GameSetup()
    {
        if (fastMode)
            delay05 = new WaitForSeconds(0.05f);

        if (GameManager.Inst.isSinglegame)
        {
            switch (eTurnMode)
            {
                case ETurnMode.Random:
                    myTurn = Random.Range(0, 2) == 0;
                    break;
                case ETurnMode.My:
                    myTurn = true;
                    break;
                case ETurnMode.Other:
                    myTurn = false;
                    break;
            }
        }
    }

    public void StartGame(bool isFirstPlayer)
    {
        myTurn = isFirstPlayer;
        StartCoroutine(StartGameCo());
    }

    public IEnumerator StartGameCo()
    {
        GameSetup();
        isLoading = true;

        for (int i = 0; i < startCardCount; i++)
        {
            yield return delay05;
            OnAddCard?.Invoke(false);
            yield return delay05;
            OnAddCard?.Invoke(true);
        }
        StartCoroutine(StartTurnCo());
    }

    IEnumerator StartTurnCo()
    {
        isLoading = true;
        if (myTurn)
        {
            GameManager.Inst.Notification("나의 턴");
        }

        yield return delay07;
        OnAddCard?.Invoke(myTurn);
        yield return delay07;
        isLoading = false;
        OnTurnStarted?.Invoke(myTurn);
    }

    public void EndTurn()
    {
        if (networkProtocol != null && myTurn)
        {
            networkProtocol.SendMessage(NetworkMessageType.TurnEnd);
        }

        myTurn = !myTurn;
        StartCoroutine(StartTurnCo());
    }
}
