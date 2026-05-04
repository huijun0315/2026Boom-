using UnityEngine;

[CreateAssetMenu(fileName = "SkinCatalog", menuName = "Boom/Skin Catalog")]
public class SkinCatalog : ScriptableObject
{
    [System.Serializable]
    public struct OptionBinding
    {
        [Tooltip("成就馆选项索引（左侧第几个，0开始）")]
        public int optionIndex;
        [Tooltip("绑定到 skinSlots 的插槽索引（0开始）")]
        public int slotIndex;
    }

    [Header("Slots")]
    [Tooltip("皮肤插槽列表。配置1请放在 slot 0。")]
    public SkinConfig[] skinSlots;

    [Header("Achievement Option Bindings")]
    [Tooltip("手动配置：成就馆选项索引 -> 皮肤插槽索引")]
    public OptionBinding[] optionBindings;

    [Tooltip("找不到绑定时使用的默认插槽")]
    public int defaultSlotIndex = 0;

    public int GetSlotForOption(int optionIndex)
    {
        if (optionBindings != null)
        {
            for (int i = 0; i < optionBindings.Length; i++)
            {
                if (optionBindings[i].optionIndex == optionIndex)
                    return optionBindings[i].slotIndex;
            }
        }

        if (skinSlots != null && optionIndex >= 0 && optionIndex < skinSlots.Length)
            return optionIndex;

        return defaultSlotIndex;
    }

    public SkinConfig GetSkinBySlot(int slotIndex)
    {
        if (skinSlots == null || skinSlots.Length == 0) return null;
        int clamped = Mathf.Clamp(slotIndex, 0, skinSlots.Length - 1);
        return skinSlots[clamped];
    }
}
