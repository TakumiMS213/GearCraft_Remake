using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StageCounter : MonoBehaviour
{
    public static StageCounter Instance { get; private set; }
    public int StageCount = 1;

    private void Awake()
    {
        StageCount = Mathf.Max(1, StageCount);

        // シングルトン実装
        if (Instance != null && Instance != this)
        {
            if (GetComponent<RunSaveService>() != null) Destroy(this);
            else Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // シーンを跨いでも保持
    }
}
