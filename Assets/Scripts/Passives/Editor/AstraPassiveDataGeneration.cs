#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WesternLemegeton.Passives;

namespace WesternLemegeton.Passives.Editor
{
    public static class AstraPassiveDataGeneration
    {
        public const string WorkbookPath = "Assets/Data/Passive/패시브.xlsx";

        [MenuItem("Tools/Passive/Import Astra Workbook")]
        public static void ImportWorkbook()
        {
            // Same donor workbook schema and converter. IDs 1-999 remain reference
            // rows; only Astra's 1000+ definitions enter its official database.
            PassiveExcelImporter.Import(Path.GetFullPath(WorkbookPath));
            VerifyGeneratedData();
        }

        public static void VerifyGeneratedData()
        {
            var sheet = SimpleXlsxReader.ReadSheet(Path.GetFullPath(WorkbookPath), "Passive");
            var rows = sheet.Rows.Where(r => r.Cells.TryGetValue("PassiveID", out var id) &&
                int.TryParse(id, out int number) && number >= 1000).ToList();
            var database = AssetDatabase.LoadAssetAtPath<PassiveDatabaseSO>(
                "Assets/Resources/Passive/PassiveDatabase.asset");
            if (!database || !database.EffectRegistry || rows.Count == 0 ||
                rows.Count != database.Passives.Count)
                throw new InvalidDataException("Passive catalog does not match the Astra workbook rows.");
            foreach (var row in rows)
            {
                var cells = row.Cells;
                var data = database.GetById(int.Parse(cells["PassiveID"]));
                if (!data || data.NameStringKey != cells["NameStringKey"] ||
                    data.DescriptionStringKey != cells["DescriptionStringKey"] ||
                    data.PassiveIconResource != cells["PassiveIconResource"] ||
                    data.Rarity != (PassiveRarity)Enum.Parse(typeof(PassiveRarity), cells["Rarity"], true) ||
                    data.Category != (PassiveCategory)Enum.Parse(typeof(PassiveCategory), cells["Category"], true) ||
                    data.StatType != cells["StatType"] || data.ScriptName != (cells.TryGetValue("ScriptName", out var script) ? script : string.Empty))
                    throw new InvalidDataException($"Passive row {row.RowNumber} mapping mismatch.");
                bool hasValue = !string.IsNullOrWhiteSpace(cells["Value"]);
                if (data.HasValue != hasValue || (hasValue &&
                    (!SimpleXlsxReader.TryParseFloat(cells["Value"], out float value) ||
                    !Mathf.Approximately(data.Value, value))))
                    throw new InvalidDataException($"Passive row {row.RowNumber} value mismatch.");
                var valueType = string.IsNullOrWhiteSpace(cells["ValueType"]) ? PassiveValueType.None :
                    (PassiveValueType)Enum.Parse(typeof(PassiveValueType), cells["ValueType"], true);
                if (data.ValueType != valueType)
                    throw new InvalidDataException($"Passive row {row.RowNumber} unit mismatch.");
                if (!string.IsNullOrWhiteSpace(data.PassiveIconResource) && !data.Icon &&
                    !Resources.Load<Texture2D>(data.PassiveIconResource))
                    throw new InvalidDataException($"Missing passive icon: {data.PassiveID}");
                if (data.Category != PassiveCategory.Flat && !database.EffectRegistry.Contains(data.ScriptName))
                    throw new InvalidDataException($"Missing effect: {data.ScriptName}");
            }
            Debug.Log($"ASTRA_PASSIVE_DATA_VERIFIED: {rows.Count} definitions, " +
                $"{database.EffectRegistry.Entries.Count} registered effects. Play Mode was not used.");
        }
    }
}
#endif
