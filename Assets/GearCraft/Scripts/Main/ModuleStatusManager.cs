using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class ModuleStatusManager : MonoBehaviour
{
    public StatusManager runtimeStatus;
    public GameObject moduleScrapImage;
    public GameObject moduleRepairImage;
    public GameObject moduleBarrierImage;
    public Transform modulePanelTransform;
    void Start()
    {
    runtimeStatus = StatusManager.Instance ?? FindAnyObjectByType<StatusManager>(); // 実行時インスタンス
        UpdateModuleDisplay();
    }

    void UpdateModuleDisplay()
    {
        if(runtimeStatus.module_scrap == true)
        {
            moduleScrapImage.SetActive(true);
        }
        if(runtimeStatus.module_repair == true)
        {
            moduleRepairImage.SetActive(true);
        }
        if(runtimeStatus.module_barrier == true)
        {
            moduleBarrierImage.SetActive(true);
        }
        if(runtimeStatus.module_scrap == false && runtimeStatus.module_repair == false && runtimeStatus.module_barrier == false)
        {
            Debug.Log("All modules deactivated, hiding panel.");
            modulePanelTransform.gameObject.SetActive(false);
        }
        if(runtimeStatus.module_scrap == true || runtimeStatus.module_repair == true || runtimeStatus.module_barrier == true)
        {
            modulePanelTransform.gameObject.SetActive(true);
        }
    }
}
