using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PunkDriveManager : MonoBehaviour
{
    [SerializeField] private float damageAmount = 0.1f;      // 1回のダメージ量
    [SerializeField] private float damageInterval = 0.5f;  // ダメージを与える間隔（秒）
    public GameObject HitEffect;

    private PlayerController playerController;
    private float STR;

    // 敵ごとのコルーチンを追跡
    private Dictionary<Collider2D, Coroutine> activeCoroutines = new Dictionary<Collider2D, Coroutine>();

    private void Start()
    {
        playerController = FindAnyObjectByType<PlayerController>();
        if (playerController == null)
        {
            Debug.LogError("PlayerControllerが見つかりません。このスクリプトはPlayerControllerを持つオブジェクトが存在するシーンで使用してください。");
            enabled = false;
            return;
        }
        STR = playerController.STR;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("PunkDriveManager detected collision with: " + other.gameObject.name);
        if(!gameObject.activeSelf) return;
        if (other.CompareTag("Enemy"))
        {
            // すでにダメージ処理中ならスキップ
            if (activeCoroutines.ContainsKey(other)) return;

            EnemyController ec = other.GetComponent<EnemyController>();
            if (ec != null)
            {
                Coroutine loop = StartCoroutine(DamageLoop(other, ec));
                activeCoroutines.Add(other, loop);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            // 触れている敵リストから削除
            if (activeCoroutines.ContainsKey(other))
            {
                StopCoroutine(activeCoroutines[other]);
                activeCoroutines.Remove(other);
            }
        }
    }

    private IEnumerator DamageLoop(Collider2D target, EnemyController enemy)
    {
        while (true)
        {
            if (enemy == null) yield break; // 👈 Destroyされたら終了
            enemy.TakeDamage(damageAmount + STR/10);

            if (HitEffect != null)
            {
                Instantiate(HitEffect, target.ClosestPoint(transform.position), Quaternion.identity);
            }

            yield return new WaitForSeconds(damageInterval);
        }
    }

    private void OnDisable()
    {
        // プレイヤーが無効化されたとき全コルーチンを停止
        foreach (var pair in activeCoroutines)
        {
            if (pair.Value != null)
                StopCoroutine(pair.Value);
        }
        activeCoroutines.Clear();
    }
}
