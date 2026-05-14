using UnityEngine;

/// <summary>
/// 敵AI戦略インターフェース（Strategyパターン）
/// </summary>
public interface IEnemyAI
{
    void Initialize(EnemyController owner, EnemyDataSO data);
    void UpdateAI();
    void OnAttack();
    void OnDeath();
}
