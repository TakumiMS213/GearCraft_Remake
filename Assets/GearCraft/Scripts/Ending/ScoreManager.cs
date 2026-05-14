using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public int score = 0;
    public TMPro.TMP_Text scoreText;

    

    void Awake()
    {
        int str = StatusManager.Instance.STR;
        int acc = StatusManager.Instance.ACC;
        int stageCount = StageCounter.Instance.StageCount;
        bool killAllEnemies = StatusManager.Instance.killAllEnemies;
        
        if(killAllEnemies)
        {
            score = (str*1000 + acc*1000 + stageCount*500) * 2;
        }
        else if (!killAllEnemies)
        {
            score = str*1000 + acc*1000 + stageCount*500;
        }
    }

    public void GetScore()
    {
        scoreText.text = "YOUR SCORE : " + score.ToString();
    }
}
