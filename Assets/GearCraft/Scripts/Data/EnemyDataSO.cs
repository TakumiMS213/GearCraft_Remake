using UnityEngine;

[CreateAssetMenu(menuName = "GearCraft/Enemy Data", fileName = "NewEnemy")]
public class EnemyDataSO : ScriptableObject
{
    [Header("Basic")]
    public string enemyName;
    public GameObject prefab;

    [Header("Stats")]
    public float baseHP = 50f;
    public float speed = 5f;
    public float attackInterval = 2f;
    public float bulletSpeed = 5f;

    [Header("AI")]
    public EnemyAIType aiType = EnemyAIType.Straight;

    [Header("Bullets")]
    public GameObject[] bulletPrefabs;

    [Header("Display")]
    public bool isBoss = false;
    public bool isHighlighted = false;

    [Header("Circle Move")]
    public float circleRadius = 3f;
    public float circleDuration = 1.5f;

    [Header("Charger")]
    public float chargeDistance = 5f;
    public float chargeTime = 1f;

    [Header("Bomber")]
    public float explosionRadius = 3f;
    public float explosionDamage = 30f;

    [Header("Summon")]
    public EnemyDataSO summonEnemyData;
    public int summonCount = 0;
    public float summonInterval = 6f;

    [Header("Drops")]
    public MaterialDropEntry[] drops;

    public bool IsBossType => isBoss ||
        aiType == EnemyAIType.Boss_Tank ||
        aiType == EnemyAIType.Boss_Speed ||
        aiType == EnemyAIType.Boss_Artillery ||
        aiType == EnemyAIType.Boss_Final;
}
