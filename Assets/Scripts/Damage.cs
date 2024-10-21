using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DG.Tweening;

public class Damage : MonoBehaviour
{
    [SerializeField] TMP_Text damageTMP;
    Transform tr;

    public void SetupTransform(Transform tr)
    {
        this.tr = tr;
    }

    void Update()
    {
        if (tr != null)
            transform.position = tr.position;
    }

    public void Damaged(int damage)
    {
        if (damage <= 0)
            return;

        GetComponent<Order>().SetOrder(1000);
        damageTMP.text = $"-{damage}";

        Sequence sequence = DOTween.Sequence()
            .Append(transform.DOScale(Vector3.one * 9.84f, 0.5f).SetEase(Ease.InOutBack))
            .AppendInterval(1.2f)
            .Append(transform.DOScale(Vector3.zero, 0.5f).SetEase(Ease.InOutBack))
            .OnComplete(() => Destroy(gameObject));
    }
}
