using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class EntityManager : MonoBehaviour
{
	public static EntityManager Inst { get; private set; }
	void Awake() => Inst = this;

	[SerializeField] GameObject entityPrefab;
	[SerializeField] GameObject damagePrefab;
	[SerializeField] List<Entity> myEntities;
	[SerializeField] List<Entity> otherEntities;
	[SerializeField] GameObject TargetPicker;
	[SerializeField] Entity myEmptyEntity;
	[SerializeField] Entity myBossEntity;
	[SerializeField] Entity otherBossEntity;

	const int MAX_ENTITY_COUNT = 6;
	public bool IsFullMyEntities => myEntities.Count >= MAX_ENTITY_COUNT && !ExistMyEmptyEntity;
	bool IsFullOtherEntities => otherEntities.Count >= MAX_ENTITY_COUNT;
	bool ExistTargetPickEntity => targetPickEntity != null;
	bool ExistMyEmptyEntity => myEntities.Exists(x => x == myEmptyEntity);
	int MyEmptyEntityIndex => myEntities.FindIndex(x => x == myEmptyEntity);
	bool CanMouseInput => TurnManager.Inst.myTurn && !TurnManager.Inst.isLoading;

	Entity selectEntity;
	Entity targetPickEntity;
	WaitForSeconds delay1 = new WaitForSeconds(1);
	WaitForSeconds delay2 = new WaitForSeconds(2);



	void Start()
	{
		TurnManager.OnTurnStarted += OnTurnStarted;
	}

	void OnDestroy()
	{
		TurnManager.OnTurnStarted -= OnTurnStarted;
	}

	void OnTurnStarted(bool myTurn)
	{
		AttackableReset(myTurn);

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

	public bool SpawnEntity(bool isMine, Item item, Vector3 spawnPos)
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

		if (isMine)
			myEntities[MyEmptyEntityIndex] = entity;
		else
			otherEntities.Insert(Random.Range(0, otherEntities.Count), entity);

		entity.isMine = isMine;
		entity.Setup(item);
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

	void Attack(Entity attacker, Entity defender)
	{
		// _attacker가 _defender의 위치로 이동하다 원래 위치로 온다, 이때 order가 높다
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

	void AttackCallback(params Entity[] entities)
	{
		// 죽을 사람 골라서 죽음 처리
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

}
