using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using DG.Tweening.Core.Easing;
using System;
using Random = UnityEngine.Random;

public class EntityManager : MonoBehaviour
{
    public static EntityManager Inst { get; private set; }

    [Header("Network")]
    [SerializeField] NetworkProtocol networkProtocol;

    [Header("Prefabs")]
    [SerializeField] GameObject entityPrefab;
    [SerializeField] GameObject damagePrefab;

    [Header("Entities")]
    [SerializeField] GameObject TargetPicker;
    [SerializeField] Entity myEmptyEntity;
    [SerializeField] Entity myBossEntity;
    [SerializeField] Entity otherBossEntity;
    [SerializeField] List<Entity> myEntities;
    [SerializeField] List<Entity> otherEntities;

    const int MAX_ENTITY_COUNT = 6;

    private Dictionary<string, Entity> entityRegistry = new Dictionary<string, Entity>();

    Entity selectEntity;
    Entity targetPickEntity;
    WaitForSeconds delay1 = new WaitForSeconds(1);
    WaitForSeconds delay2 = new WaitForSeconds(2);

    public bool IsFullMyEntities => myEntities.Count >= MAX_ENTITY_COUNT && !ExistMyEmptyEntity;
    bool IsFullOtherEntities => otherEntities.Count >= MAX_ENTITY_COUNT;
    bool ExistTargetPickEntity => targetPickEntity != null;
    bool ExistMyEmptyEntity => myEntities.Exists(x => x == myEmptyEntity);
    int MyEmptyEntityIndex => myEntities.FindIndex(x => x == myEmptyEntity);
    bool CanMouseInput => TurnManager.Inst.myTurn && !TurnManager.Inst.isLoading;

    void Awake()
    {
        Inst = this;
        Entity.OnEntitySpawned += RegisterEntity;
        Entity.OnEntityDestroyed += UnregisterEntity;
    }

    void OnDestroy()
    {
        TurnManager.OnTurnStarted -= OnTurnStarted;
    }

    void Start()
    {
        if (networkProtocol == null)
            networkProtocol = FindObjectOfType<NetworkProtocol>();
        TurnManager.OnTurnStarted += OnTurnStarted;
    }

    void OnTurnStarted(bool myTurn)
    {
        AttackableReset(myTurn);
        if (GameManager.Inst.isSinglegame)
            if (!myTurn)
                StartCoroutine(AICo());
    }

    void Update()
    {
        ShowTargetPicker(ExistTargetPickEntity);
    }

    IEnumerator AICo()
    {
        CardManager.Inst.TryPutCard(false);
        yield return delay1;

        // attackable이 true인 모든 otherEntites를 가져와 순서를 섞는다
        var attackers = new List<Entity>(otherEntities.FindAll(x => x.attackable == true));
        for (int i = 0; i < attackers.Count; i++)
        {
            int rand = Random.Range(i, attackers.Count);
            Entity temp = attackers[i];
            attackers[i] = attackers[rand];
            attackers[rand] = temp;
        }

        // 보스를 포함한 myEntities를 랜덤하게 시간차 공격한다
        foreach (var attacker in attackers)
        {
            var defenders = new List<Entity>(myEntities);
            defenders.Add(myBossEntity);
            int rand = Random.Range(0, defenders.Count);
            Attack(attacker, defenders[rand]);

            if (TurnManager.Inst.isLoading)
                yield break;

            yield return delay2;
        }
        TurnManager.Inst.EndTurn();
    }

    private void RegisterEntity(Entity entity)
    {
        if (!entityRegistry.ContainsKey(entity.entityId))
        {
            entityRegistry[entity.entityId] = entity;
        }
    }

    private void UnregisterEntity(Entity entity)
    {
        if (entityRegistry.ContainsKey(entity.entityId))
        {
            entityRegistry.Remove(entity.entityId);
        }
    }

    void EntityAlignment(bool isMine)
    {
        float targetY = isMine ? -4.35f : 4.15f;
        var targetEntities = isMine ? myEntities : otherEntities;

        for (int i = 0; i < targetEntities.Count; i++)
        {
            float targetX = (targetEntities.Count - 1) * -3.4f + i * 6.8f;

            var targetEntity = targetEntities[i];
            targetEntity.originPos = new Vector3(targetX, targetY, 0);
            targetEntity.MoveTransform(targetEntity.originPos, true, 0.5f);
            targetEntity.GetComponent<Order>()?.SetOriginOrder(i);
        }
    }

    public Entity FindEntityById(string entityId)
    {
        if (string.IsNullOrEmpty(entityId)) return null;

        if (entityRegistry.TryGetValue(entityId, out Entity entity))
        {
            return entity;
        }

        // Registry에서 못 찾은 경우 기존 방식으로 검색
        return myEntities.Find(e => e.entityId == entityId) ??
               otherEntities.Find(e => e.entityId == entityId);
    }

    public void InsertMyEmptyEntity(float xPos)
    {
        if (IsFullMyEntities)
            return;

        if (!ExistMyEmptyEntity)
            myEntities.Add(myEmptyEntity);

        Vector3 emptyEntityPos = myEmptyEntity.transform.position;
        emptyEntityPos.x = xPos;
        myEmptyEntity.transform.position = emptyEntityPos;

        int _emptyEntityIndex = MyEmptyEntityIndex;
        myEntities.Sort((entity1, entity2) => entity1.transform.position.x.CompareTo(entity2.transform.position.x));
        if (MyEmptyEntityIndex != _emptyEntityIndex)
            EntityAlignment(true);
    }

    public void RemoveMyEmptyEntity()
    {
        if (!ExistMyEmptyEntity)
            return;

        myEntities.RemoveAt(MyEmptyEntityIndex);
        EntityAlignment(true);
    }

    // 스폰 성공 여부
    public bool SpawnEntity(bool isMine, Item item, Vector3 spawnPos, string entityId = null)
    {
        if (isMine)
        {
            if (IsFullMyEntities || !ExistMyEmptyEntity)
                return false;
        }
        else
        {
            if (IsFullOtherEntities)
                return false;
        }

        var entityObject = Instantiate(entityPrefab, spawnPos, Utils.QI);
        var entity = entityObject.GetComponent<Entity>();

        entity.isMine = isMine;
        entity.Setup(item, entityId);  // entityId 생성

        // 엔티티 리스트에 추가
        if (isMine)
            myEntities[MyEmptyEntityIndex] = entity;
        else
            otherEntities.Insert(Random.Range(0, otherEntities.Count), entity);

        EntityAlignment(isMine);
        return true;
    }

    public void EntityMouseDown(Entity entity)
    {
        if (!CanMouseInput)
            return;

        selectEntity = entity;
    }

    public void EntityMouseUp()
    {
        if (!CanMouseInput)
            return;

        // selectEntity, targetPickEntity 둘다 존재하면 공격한다. 바로 null, null로 만든다.
        if (selectEntity && targetPickEntity && selectEntity.attackable)
            Attack(selectEntity, targetPickEntity);

        selectEntity = null;
        targetPickEntity = null;
    }

    public void EntityMouseDrag()
    {
        if (!CanMouseInput || selectEntity == null)
            return;

        // other 타겟엔티티 찾기
        bool existTarget = false;
        foreach (var hit in Physics2D.RaycastAll(Utils.MousePos, Vector3.forward))
        {
            Entity entity = hit.collider?.GetComponent<Entity>();
            if (entity != null && !entity.isMine && selectEntity.attackable)
            {
                targetPickEntity = entity;
                existTarget = true;
                break;
            }
        }
        if (!existTarget)
            targetPickEntity = null;
    }

    void Attack(Entity attacker, Entity defender, bool isNetworkReceived = false)
    {
        if (!isNetworkReceived && TurnManager.Inst.myTurn && networkProtocol != null)
        {
            bool isAttackingEnemyBoss = (defender == otherBossEntity);  // 상대방 보스 공격
            bool isAttackingMyBoss = (defender == myBossEntity);        // 내 보스 공격
            int attackerIdx = myEntities.IndexOf(attacker);

            EntityAttackData attackData = new EntityAttackData
            {
                AttackerEntityId = attacker.entityId,
                DefenderEntityId = isAttackingEnemyBoss || isAttackingMyBoss ? "" : defender.entityId,
                IsAttackingBoss = isAttackingEnemyBoss || isAttackingMyBoss,
                IsAttackingEnemyBoss = isAttackingEnemyBoss
            };

            if (GameManager.Inst.isSinglegame == false)
                networkProtocol.SendMessage(NetworkMessageType.EntityAttack, attackData);
        }

        // 공격 애니메이션과 데미지 처리
        attacker.attackable = false;
        attacker.GetComponent<Order>().SetMostFrontOrder(true);

        Sequence sequence = DOTween.Sequence()
            .Append(attacker.transform.DOMove(defender.originPos, 0.4f)).SetEase(Ease.InSine)
            .AppendCallback(() =>
            {
                attacker.Damaged(defender.attack);
                defender.Damaged(attacker.attack);
                SpawnDamage(defender.attack, attacker.transform);
                SpawnDamage(attacker.attack, defender.transform);
            })
            .Append(attacker.transform.DOMove(attacker.originPos, 0.4f)).SetEase(Ease.OutSine)
            .OnComplete(() => AttackCallback(attacker, defender));
    }

    // 죽을 사람 골라서 죽음 처리
    void AttackCallback(params Entity[] entities)
    {
        entities[0].GetComponent<Order>().SetMostFrontOrder(false);

        foreach (var entity in entities)
        {
            if (!entity.isDie || entity.isBossOrEmpty)
                continue;

            if (entity.isMine)
                myEntities.Remove(entity);
            else
                otherEntities.Remove(entity);

            Sequence sequence = DOTween.Sequence()
                .Append(entity.transform.DOShakePosition(1.3f))
                .Append(entity.transform.DOScale(Vector3.zero, 0.3f)).SetEase(Ease.OutCirc)
                .OnComplete(() =>
                {
                    EntityAlignment(entity.isMine);
                    Destroy(entity.gameObject);
                });
        }
        StartCoroutine(CheckBossDie());
    }

    IEnumerator CheckBossDie()
    {
        yield return delay2;

        if (myBossEntity.isDie)
            StartCoroutine(GameManager.Inst.GameOver(false));

        if (otherBossEntity.isDie)
            StartCoroutine(GameManager.Inst.GameOver(true));
    }

    public void DamageBoss(bool isMine, int damage)
    {
        var targetBossEntity = isMine ? myBossEntity : otherBossEntity;
        targetBossEntity.Damaged(damage);
        StartCoroutine(CheckBossDie());
    }

    void ShowTargetPicker(bool isShow)
    {
        TargetPicker.SetActive(isShow);
        if (ExistTargetPickEntity)
            TargetPicker.transform.position = targetPickEntity.transform.position;
    }

    void SpawnDamage(int damage, Transform tr)
    {
        if (damage <= 0)
            return;

        var damageComponent = Instantiate(damagePrefab).GetComponent<Damage>();
        damageComponent.SetupTransform(tr);
        damageComponent.Damaged(damage);
    }

    public void AttackableReset(bool isMine)
    {
        var targetEntites = isMine ? myEntities : otherEntities;
        targetEntites.ForEach(x => x.attackable = true);
    }

    public void OnReceiveAttack(EntityAttackData attackData)
    {
        try
        {
            if (TurnManager.Inst.myTurn)
                return;

            // 최대 3번까지 공격자 찾기 시도
            Entity attacker = null;
            for (int i = 0; i < 3; i++)
            {
                attacker = otherEntities.Find(e => e.entityId == attackData.AttackerEntityId);
                if (attacker != null) break;

                Debug.Log($"Attempt {i + 1}: Waiting for attacker entity... ID: {attackData.AttackerEntityId}");
                System.Threading.Thread.Sleep(100); // 잠시 대기
            }

            if (attacker == null)
            {
                Debug.LogError($"Could not find attacker with ID: {attackData.AttackerEntityId}");
                LogEntityDebugInfo(); // 디버깅용 정보 출력
                return;
            }

            Entity defender;
            if (attackData.IsAttackingBoss)
            {
                defender = attackData.IsAttackingEnemyBoss ? myBossEntity : otherBossEntity;
                if (defender == null)
                {
                    Debug.LogError("Target boss entity is null");
                    return;
                }
            }
            else
            {
                defender = myEntities.Find(e => e.entityId == attackData.DefenderEntityId);
                if (defender == null)
                {
                    Debug.LogError($"Could not find defender with ID: {attackData.DefenderEntityId}");
                    LogEntityDebugInfo();
                    return;
                }
            }

            Debug.Log($"Processing attack: {attacker.entityId} -> {defender.entityId}");
            Attack(attacker, defender, true);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error in OnReceiveAttack: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // 디버깅용 엔티티 정보 출력
    private void LogEntityDebugInfo()
    {
        Debug.Log("=== Current Entity Status ===");
        Debug.Log("Other Entities:");
        foreach (var entity in otherEntities)
        {
            Debug.Log($"ID: {entity.entityId}, Name: {entity.item.name}, Position: {entity.transform.position}");
        }
        Debug.Log("My Entities:");
        foreach (var entity in myEntities)
        {
            Debug.Log($"ID: {entity.entityId}, Name: {entity.item.name}, Position: {entity.transform.position}");
        }
    }

}