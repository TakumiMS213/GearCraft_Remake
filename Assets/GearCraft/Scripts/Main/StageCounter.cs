using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StageCounter : MonoBehaviour
{
    public static StageCounter Instance { get; private set; }
    public int StageCount = 1;

    private void Awake()
    {
        // シングルトン実装
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // 2つ目が生成されたら破棄
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // シーンを跨いでも保持
    }
}
