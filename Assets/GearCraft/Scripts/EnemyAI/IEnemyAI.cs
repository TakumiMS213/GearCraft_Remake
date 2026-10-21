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

/// <summary>Update外のTweenなども、ボスのスタンと同時に停止・再開する。</summary>
public interface IBossStunHandler
{
    void SetStunned(bool stunned);
}
