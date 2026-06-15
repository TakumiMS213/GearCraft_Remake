using UnityEngine;

namespace GearCraft.Scripts.Data
{
    [CreateAssetMenu(menuName = "GearCraft/Loading Tip Data", fileName = "LoadingTipData")]
    public sealed class LoadingTipData : ScriptableObject
    {
        [SerializeField, TextArea(2, 4)] private string[] tips;

        public bool HasTips => tips != null && tips.Length > 0;

        public string GetRandomTip()
        {
            if (!HasTips)
            {
                return string.Empty;
            }

            return tips[Random.Range(0, tips.Length)];
        }
    }
}
