using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyWave", menuName = "Enemy/Wave Data")]
public class EnemyWaveData : ScriptableObject
{
    [System.Serializable]
    public class SpawnData
    {
        public GameObject enemyPrefab;
        public float spawnTime; // Wave開始からの秒数
        public bool LastEnemy = false;
    }

    public SpawnData[] spawns;
}
