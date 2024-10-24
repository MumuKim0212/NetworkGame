using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 치트, UI, 랭킹, 게임오버
public class GameManager : MonoBehaviour
{
    public static GameManager Inst { get; private set; }
    void Awake() => Inst = this;

    [Multiline(10)]
    [SerializeField]
    string cheatInfo =
        "에디터 넘버패드에서 동작\r\n\r\n" +
        "1. 내 카드 추가\r\n" +
        "2. 상대 카드 추가\r\n" +
        "3. 턴 종료\r\n" +
        "4. 상대 카드 놓기\r\n" +
        "5. 내 보스 데미지 -19\r\n" +
        "6. 상대 보스 데미지 -19";
    [SerializeField] NotificationPanel notificationPanel;
    [SerializeField] ResultPanel resultPanel;
    [SerializeField] TitlePanel titlePanel;
    [SerializeField] GameObject chatButton;
    [SerializeField] CameraEffect cameraEffect;
    [SerializeField] GameObject endTurnBtn;

    private Queue<bool> gameStartRequests = new Queue<bool>();
    public bool isSinglegame;
    WaitForSeconds delay2 = new WaitForSeconds(2);


    void Start()
    {
        UISetup();
    }

    void UISetup()
    {
        notificationPanel.ScaleZero();
        resultPanel.ScaleZero();
        titlePanel.Active(true);
        cameraEffect.SetGrayScale(false);
    }

    void Update()
    {
        if (gameStartRequests.Count > 0)
        {
            bool isFirstPlayer = gameStartRequests.Dequeue();
            StartGameInternal(isFirstPlayer);
        }

#if UNITY_EDITOR
        InputCheatKey();
#endif
    }

    void InputCheatKey()
    {
        if (Input.GetKeyDown(KeyCode.Q))
            TurnManager.OnAddCard?.Invoke(true);

        if (Input.GetKeyDown(KeyCode.W))
            TurnManager.OnAddCard?.Invoke(false);

        if (Input.GetKeyDown(KeyCode.E))
            TurnManager.Inst.EndTurn();

        if (Input.GetKeyDown(KeyCode.R))
            CardManager.Inst.TryPutCard(false);

        if (Input.GetKeyDown(KeyCode.T))
            EntityManager.Inst.DamageBoss(true, 19);

        if (Input.GetKeyDown(KeyCode.Y))
            EntityManager.Inst.DamageBoss(false, 19);
    }

    // 로컬 게임 시작
    public void StartGame()
    {
        isSinglegame = true;
        UISetup();
        titlePanel.Active(false);
        chatButton.SetActive(true);
        endTurnBtn.SetActive(true);
        StartCoroutine(TurnManager.Inst.StartGameCo());
    }

    public void StartNetworkGame(bool isFirstPlayer)
    {
        isSinglegame = false;
        gameStartRequests.Enqueue(isFirstPlayer);
    }


    private void StartGameInternal(bool isFirstPlayer)
    {
        // 기존 UI 초기화
        UISetup();
        titlePanel.Active(false);
        endTurnBtn.SetActive(true);
        chatButton.SetActive(true);

        TurnManager.Inst.myTurn = isFirstPlayer;
        StartCoroutine(TurnManager.Inst.StartGameCo());

    }

    public void Notification(string message)
    {
        notificationPanel.Show(message);
    }

    public IEnumerator GameOver(bool isMyWin)
    {
        TurnManager.Inst.isLoading = true;
        endTurnBtn.SetActive(false);
        yield return delay2;

        TurnManager.Inst.isLoading = true;
        resultPanel.Show(isMyWin ? "승리" : "패배");
        cameraEffect.SetGrayScale(true);
    }
}
