using UnityEngine;
namespace WesternLemegeton.Passives
{
    [CreateAssetMenu(fileName = "Passive_000", menuName = "Game/Passive/Passive Data")]
    public class PassiveSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private int passiveID;
        [SerializeField] private string passiveIconResource;
        [SerializeField] private Sprite icon;

        [Header("Text")]
        [SerializeField] private string nameStringKey;
        [TextArea(2, 5)]
        [SerializeField] private string descriptionStringKey;

        [Header("Data")]
        [SerializeField] private PassiveRarity rarity;
        [SerializeField] private string statType;
        [SerializeField] private PassiveCategory category;
        [SerializeField] private float value;
        [SerializeField] private bool hasValue;
        [SerializeField] private PassiveValueType valueType;

        [Header("Ability / Shift")]
        [SerializeField] private string scriptName;

        public int PassiveID => passiveID;
        public string PassiveIconResource => passiveIconResource;
        public Sprite Icon => icon;
        public string NameStringKey => nameStringKey;
        public string DescriptionStringKey => descriptionStringKey;
        public PassiveRarity Rarity => rarity;
        public string StatType => statType;
        public PassiveCategory Category => category;
        public float Value => value;
        public bool HasValue => hasValue;
        public PassiveValueType ValueType => valueType;
        public string ScriptName => scriptName;

#if UNITY_EDITOR
        public void ApplyImportedData(
            int importedPassiveID,
            string importedIconResource,
            Sprite importedIcon,
            bool replaceIcon,
            string importedNameStringKey,
            string importedDescriptionStringKey,
            PassiveRarity importedRarity,
            string importedStatType,
            PassiveCategory importedCategory,
            float importedValue,
            bool importedHasValue,
            PassiveValueType importedValueType,
            string importedScriptName)
        {
            passiveID = importedPassiveID;
            passiveIconResource = importedIconResource ?? string.Empty;

            if (replaceIcon)
            {
                icon = importedIcon;
            }

            nameStringKey = importedNameStringKey ?? string.Empty;
            descriptionStringKey = importedDescriptionStringKey ?? string.Empty;
            rarity = importedRarity;
            statType = importedStatType ?? string.Empty;
            category = importedCategory;
            value = importedValue;
            hasValue = importedHasValue;
            valueType = importedValueType;
            scriptName = importedScriptName ?? string.Empty;
        }
#endif
    }
}
