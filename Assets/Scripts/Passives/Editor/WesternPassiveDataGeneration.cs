#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WesternLemegeton.Passives;

namespace WesternLemegeton.Passives.Editor
{
    public static class WesternPassiveDataGeneration
    {
        public const string WorkbookAssetPath = "Assets/Data/Passive/패시브.xlsx";
        public const string SheetName = "Passive";
        public const string OutputFolder = "Assets/Resources/Passive";
        public const string DatabaseAssetPath = OutputFolder + "/PassiveDatabase.asset";
        public const string RegistryAssetPath = OutputFolder + "/PassiveEffectRegistry.asset";

        [MenuItem("Tools/Western Lemegeton/Passive/Validate Workbook")]
        public static void ValidateWorkbook()
        {
            PassiveExcelImporter.LogValidation(PassiveExcelImporter.ParseAndValidate(Path.GetFullPath(WorkbookAssetPath)));
        }

        [MenuItem("Tools/Western Lemegeton/Passive/Import Workbook")]
        public static void ImportWorkbook()
        {
            PassiveExcelImporter.Import(Path.GetFullPath(WorkbookAssetPath));
        }

        [MenuItem("Tools/Western Lemegeton/Passive/Verify Generated Data")]
        public static void VerifyGeneratedData()
        {
            var validation = PassiveExcelImporter.ParseAndValidate(Path.GetFullPath(WorkbookAssetPath));
            var errors = new List<string>(validation.Errors);
            var warnings = new List<string>(validation.Warnings);
            var database = AssetDatabase.LoadAssetAtPath<PassiveDatabaseSO>(DatabaseAssetPath);
            if (database == null) errors.Add("Western passive database is missing or unreadable: " + DatabaseAssetPath);
            else
            {
                if (database.Passives.Count != validation.Rows.Count)
                    errors.Add($"Database count {database.Passives.Count} differs from workbook count {validation.Rows.Count}.");
                var byId = new Dictionary<int, PassiveSO>();
                foreach (var passive in database.Passives)
                {
                    if (passive == null) { errors.Add("Database contains a null PassiveSO."); continue; }
                    if (byId.ContainsKey(passive.PassiveID)) errors.Add("Database contains duplicate PassiveID " + passive.PassiveID);
                    else byId.Add(passive.PassiveID, passive);
                }
                foreach (var row in validation.Rows)
                {
                    if (!byId.TryGetValue(row.PassiveID, out var data))
                    { errors.Add("Database is missing PassiveID " + row.PassiveID); continue; }
                    if (data.NameStringKey != row.DisplayName || data.DescriptionStringKey != row.Description ||
                        data.Rarity != row.Rarity || data.Category != row.Category || data.StatType != row.StatType ||
                        data.HasValue != row.HasValue || data.Value != row.Value || data.ValueType != row.ValueType || data.ScriptName != row.ScriptName)
                        errors.Add("Workbook/data field mismatch for PassiveID " + row.PassiveID);
                    // A blank source icon deliberately leaves the existing SO resource/Sprite intact.
                    if (row.IconResource.Length > 0 && (data.PassiveIconResource != row.IconResource || data.Icon != row.ResolvedIcon))
                        errors.Add("Workbook/data icon mismatch for PassiveID " + row.PassiveID);
                    if (data.Category != PassiveCategory.Flat && !HasRegisteredPrototype(database.EffectRegistry, data.ScriptName))
                        warnings.Add($"PassiveID {row.PassiveID}, effect '{data.ScriptName}': Imported as data, but not runtime-ready until a PassiveEffect is registered.");
                }
            }
            // The registry may be absent for data verification, but an existing asset must be readable.
            if (File.Exists(Path.GetFullPath(RegistryAssetPath)) && AssetDatabase.LoadAssetAtPath<PassiveEffectRegistrySO>(RegistryAssetPath) == null)
                errors.Add("Western registry asset exists but cannot be read: " + RegistryAssetPath);
            foreach (string warning in warnings.Distinct()) Debug.LogWarning("[Western Passive Verification] " + warning);
            foreach (string error in errors.Distinct()) Debug.LogError("[Western Passive Verification] " + error);
            if (errors.Count != 0) throw new InvalidDataException($"Western passive data verification failed: {errors.Count} error(s).");
            Debug.Log($"WESTERN_PASSIVE_DATA_VERIFIED: {validation.Rows.Count} definitions, {warnings.Distinct().Count()} warnings. No assets were modified.");
        }

        private static bool HasRegisteredPrototype(PassiveEffectRegistrySO registry, string key) =>
            registry != null && registry.Entries.Any(entry => entry != null && entry.Key == key && entry.Prototype != null);
    }
}
#endif
