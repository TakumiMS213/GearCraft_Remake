using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using System;
using Unity.VisualScripting;
using UnityEditor;
// 追加: GameManagerの名前空間をインポート


public class UIRangeDisplay : MonoBehaviour
{
    [System.Serializable]
    public class UIRange
    {
        public GameObject uiObject;   // 表示切り替え対象のUI（画像など）
        public float minX;            // 表示開始X座標
        public float maxX;            // 表示終了X座標
    }

    public Transform player;          // プレイヤーのTransform
    public UIRange[] uiRanges;        // UIごとのX範囲設定

    public TransitionManager transitionManager;  // ← これを追加

    public int uplimit;
    public AudioSource selectSound_statusUp;
    public AudioSource cantSelect;

    [SerializeField] private Image fadeImage; // 黒いImage (最初は Alpha=0 にしておくこと)
    public StatusManager status; // ステータスマネージャーの参照

    void Start()
    {
    status = StatusManager.Instance ?? FindAnyObjectByType<StatusManager>();
        uplimit = 1;
        if (fadeImage != null)
        {
            fadeImage.color = new Color(0, 0, 0, 0);
        }
    }

    void Update()
    {
        if (player == null)
        {
            Debug.LogWarning("playerが未設定です");
            return;
        }
        if (transitionManager == null) Debug.LogWarning("transitionManagerが未設定です");
        if (selectSound_statusUp == null) Debug.LogWarning("selectSound_statusUpが未設定です");
        if (cantSelect == null) Debug.LogWarning("cantSelectが未設定です");
        if (fadeImage == null) Debug.LogWarning("fadeImageが未設定です");
        if (uiRanges == null) Debug.LogWarning("uiRangesが未設定です");

        float playerX = player.position.x;

        foreach (UIRange range in uiRanges)
        {
            if (range.uiObject == null)
            {
                Debug.LogWarning("uiObjectが未設定のUIRangeがあります");
                continue;
            }
            // 透明度を1、色を白に初期化
            var img = range.uiObject.GetComponent<UnityEngine.UI.Image>();
            if (img != null && img.color != Color.white)
            {
                img.color = new Color(1f, 1f, 1f, 1f);
            }
            bool inRange = playerX >= range.minX && playerX <= range.maxX;
            range.uiObject.SetActive(inRange);

            if (Input.GetKeyDown(KeyCode.Space))
            {
                if(uplimit == 0 || range.uiObject.name == "MedicalWindow" && status != null && status.HP >= 100 || status.UseCraftSpacebuff == true){cantSelect.Play();}
                if(range.uiObject.name == "GateRepaierWindow" && inRange && uplimit >= 1 && status != null && status.UseCraftSpacebuff == false)
                {
                    status.GATE += 50;
                    transitionManager.PlayTransition(3);
                    selectSound_statusUp.Play();
                    uplimit -= 1;
                    status.UseCraftSpacebuff = true;
                }
                if(range.uiObject.name == "FreezeWindow" && inRange && uplimit >= 1 && status != null && status.UseCraftSpacebuff == false)
                {
                    int choice = UnityEngine.Random.Range(0,2);
                    if(choice == 0)
                    {
                        status.STR += 3;
                        transitionManager.PlayTransition(0);
                        selectSound_statusUp.Play();
                        uplimit -= 1;
                    }
                    if (choice == 1)
                    {
                        status.ACC += 3;
                        transitionManager.PlayTransition(1);
                        selectSound_statusUp.Play();
                        uplimit -= 1;
                    }
                    status.UseCraftSpacebuff = true;
                }
                if(range.uiObject.name == "MedicalWindow" && status != null && status.HP < 100 && inRange && uplimit >= 1 && status.UseCraftSpacebuff == false)
                {
                    status.HP += 999;
                    transitionManager.PlayTransition(2);
                    selectSound_statusUp.Play();
                    uplimit -= 1;
                    status.UseCraftSpacebuff = true;
                }
                if(range.uiObject.name == "craftTableWindow" && inRange)
                {               
                    selectSound_statusUp.Play();
                    fadeImage.DOFade(0.5f, 0.5f).OnComplete(() =>{                    
                    SceneManager.LoadScene("Craft");  
                    });                                    
                }
                if (range.uiObject.name == "BedWindow" && inRange)
                {
                    selectSound_statusUp.Play();
                    fadeImage.DOFade(1f, 2f).OnComplete(() =>
                    {
                        SceneManager.LoadScene("Main");
                    });
                }
            }
        }
    }
}
