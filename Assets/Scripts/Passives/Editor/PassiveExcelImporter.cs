#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using WesternLemegeton.Passives;

namespace WesternLemegeton.Passives.Editor
{
    public static class PassiveExcelImporter
    {
        private const string LastExcelPathKey = "WesternLemegeton.Passive.LastWorkbook";
        private static readonly string[] RequiredHeaders =
        {
            "PassiveID", "PassiveIconResource", "NameStringKey", "DescriptionStringKey",
            "Rarity", "StatType", "Category", "Value", "ValueType", "ScriptName"
        };

        public sealed class PassiveImportRow
        {
            public int RowNumber { get; internal set; }
            public int PassiveID { get; internal set; }
            public string IconResource { get; internal set; }
            public string DisplayName { get; internal set; }
            public string Description { get; internal set; }
            public PassiveRarity Rarity { get; internal set; }
            public string StatType { get; internal set; }
            public PassiveCategory Category { get; internal set; }
            public float Value { get; internal set; }
            public bool HasValue { get; internal set; }
            public PassiveValueType ValueType { get; internal set; }
            public string ScriptName { get; internal set; }
            public Sprite ResolvedIcon { get; internal set; }
            public bool ReplaceIcon { get; internal set; }
            internal PassiveSO ExistingPassive;
            internal string AppliedIconResource;
        }

        public sealed class PassiveImportValidationResult
        {
            internal readonly List<PassiveImportRow> ParsedRows = new();
            internal readonly List<string> ErrorMessages = new();
            internal readonly List<string> WarningMessages = new();
            public IReadOnlyList<PassiveImportRow> Rows => ParsedRows.AsReadOnly();
            public IReadOnlyList<string> Errors => ErrorMessages.AsReadOnly();
            public IReadOnlyList<string> Warnings => WarningMessages.AsReadOnly();
            public int FlatCount => ParsedRows.Count(r => r.Category == PassiveCategory.Flat);
            public int AbilityCount => ParsedRows.Count(r => r.Category == PassiveCategory.Ability);
            public int ShiftCount => ParsedRows.Count(r => r.Category == PassiveCategory.Shift);
            public int RarityCount(PassiveRarity rarity) => ParsedRows.Count(r => r.Rarity == rarity);
        }

        // Read-only preflight: no folders, objects, dirty flags, saves or refreshes.
        public static PassiveImportValidationResult ParseAndValidate(string filePath)
        {
            var result = new PassiveImportValidationResult();
            var registeredKeys = new HashSet<string>(StringComparer.Ordinal);
            if (!PassiveEffectRegistryGenerator.TryCollectEffects(out var effects, out var registryErrors))
                result.ErrorMessages.AddRange(registryErrors.Select(e => "Registry: " + e));
            foreach (var effect in effects) registeredKeys.Add(effect.Key);

            Dictionary<int, PassiveSO> existing = LoadExistingPassives(result);
            SimpleXlsxReader.SheetData sheet = null;
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    throw new FileNotFoundException("Western passive workbook not found.", filePath);
                if (!string.Equals(Path.GetExtension(filePath), ".xlsx", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Only .xlsx workbooks are supported.");
                sheet = SimpleXlsxReader.ReadRequiredSheet(filePath, WesternPassiveDataGeneration.SheetName);
            }
            catch (Exception exception) { result.ErrorMessages.Add(exception.Message); }

            if (sheet == null) return result;
            foreach (string header in RequiredHeaders)
                if (!sheet.Headers.Contains(header, StringComparer.Ordinal))
                    result.ErrorMessages.Add("Missing required header: " + header);
            foreach (var group in sheet.Headers.GroupBy(h => h, StringComparer.OrdinalIgnoreCase))
                if (group.Count() > 1) result.ErrorMessages.Add("Duplicate header: " + group.Key);

            var excelIds = new HashSet<int>();
            foreach (var source in sheet.Rows)
                ParseRow(source, existing, excelIds, registeredKeys, result);
            if (sheet.Rows.Count == 0) result.ErrorMessages.Add("Passive sheet contains no definitions.");
            foreach (var passive in existing.Values)
                if (!excelIds.Contains(passive.PassiveID))
                    result.WarningMessages.Add($"Orphan PassiveID {passive.PassiveID}: {AssetDatabase.GetAssetPath(passive)}. Preserved; not deleted.");

            CheckOutputAssetTypes(result);
            return result;
        }

        private static void ParseRow(SimpleXlsxReader.RowData source, Dictionary<int, PassiveSO> existing,
            HashSet<int> ids, HashSet<string> registeredKeys, PassiveImportValidationResult result)
        {
            var row = new PassiveImportRow
            {
                RowNumber = source.RowNumber, IconResource = Get(source, "PassiveIconResource").Trim(),
                DisplayName = Get(source, "NameStringKey"), Description = Get(source, "DescriptionStringKey"),
                StatType = Get(source, "StatType").Trim(), ScriptName = Get(source, "ScriptName").Trim()
            };
            result.ParsedRows.Add(row);
            bool idValid = TryParseInt(Get(source, "PassiveID"), out int id) && id > 0;
            row.PassiveID = id;
            if (!idValid)
                Error(result, row, "PassiveID must be a positive integer.");
            else if (!ids.Add(id)) Error(result, row, "Duplicate Excel PassiveID.");
            bool rarityValid = TryParseEnum(Get(source, "Rarity"), out PassiveRarity rarity);
            if (!rarityValid) Error(result, row, "Invalid Rarity: " + Get(source, "Rarity"));
            row.Rarity = rarity;
            bool categoryValid = TryParseEnum(Get(source, "Category"), out PassiveCategory category);
            if (!categoryValid) Error(result, row, "Invalid Category: " + Get(source, "Category"));
            row.Category = category;
            string valueText = Get(source, "Value");
            row.HasValue = !string.IsNullOrWhiteSpace(valueText);
            if (row.HasValue)
            {
                if (!SimpleXlsxReader.TryParseFloat(valueText, out float value) || float.IsNaN(value) || float.IsInfinity(value))
                    Error(result, row, "Invalid Value: " + valueText);
                else row.Value = value;
            }
            string valueTypeText = Get(source, "ValueType");
            if (!string.IsNullOrWhiteSpace(valueTypeText))
            {
                if (!TryParseEnum(valueTypeText, out PassiveValueType valueType))
                    Error(result, row, "Invalid ValueType: " + valueTypeText);
                else row.ValueType = valueType;
            }
            if (string.IsNullOrWhiteSpace(row.DisplayName)) Error(result, row, "NameStringKey (display name) is required.");
            else if (Regex.IsMatch(row.DisplayName, @"^Name_\d+$")) Warning(result, row, "Display name is a placeholder; original text preserved.");
            if (string.IsNullOrWhiteSpace(row.Description)) Warning(result, row, "DescriptionStringKey (display description) is empty.");

            if (categoryValid) ValidateCategory(row, registeredKeys, result);
            // A source discrepancy is reported, never used to change Value or ValueType.
            if (row.StatType == WesternPassiveStatKeys.MoveSpeed && row.HasValue)
            {
                Match described = Regex.Match(row.Description, @"이동\s*속도[^\d+-]*([+-]?\d+(?:\.\d+)?)");
                if (described.Success && SimpleXlsxReader.TryParseFloat(described.Groups[1].Value, out float amount) && amount != row.Value)
                    Warning(result, row, $"Move description mentions {amount.ToString(CultureInfo.InvariantCulture)}, but Value is {row.Value.ToString(CultureInfo.InvariantCulture)}. Source preserved without correction.");
            }
            existing.TryGetValue(id, out row.ExistingPassive);
            ResolveIcon(row, result);
        }

        private static void ValidateCategory(PassiveImportRow row, HashSet<string> registeredKeys, PassiveImportValidationResult result)
        {
            bool flat = row.Category == PassiveCategory.Flat;
            if (flat || row.StatType.Length > 0)
            {
                if (!WesternPassiveStatKeys.IsKnown(row.StatType)) Error(result, row, "Unknown or missing StatType: '" + row.StatType + "'.");
                else if (!WesternPassiveStatKeys.IsInitialRuntimeSupported(row.StatType))
                    Warning(result, row, "Known StatType '" + row.StatType + "' is not connected to the initial gameplay bridge. Data import is allowed.");
            }
            if (flat)
            {
                if (!row.HasValue) Error(result, row, "Flat requires Value.");
                if (row.ValueType != PassiveValueType.Raw && row.ValueType != PassiveValueType.Percent)
                    Error(result, row, "Flat requires ValueType Raw or Percent.");
                if (row.ScriptName.Length > 0) Warning(result, row, "ScriptName is unused for Flat; original string preserved.");
            }
            else if (row.ScriptName.Length == 0) Error(result, row, row.Category + " requires ScriptName.");
            else if (!registeredKeys.Contains(row.ScriptName))
                Warning(result, row, "Missing effect '" + row.ScriptName + "'. Imported as data, but not runtime-ready until a PassiveEffect is registered.");
        }

        private static void ResolveIcon(PassiveImportRow row, PassiveImportValidationResult result)
        {
            if (row.IconResource.Length == 0)
            {
                row.AppliedIconResource = row.ExistingPassive != null ? row.ExistingPassive.PassiveIconResource : string.Empty;
                row.ResolvedIcon = row.ExistingPassive != null ? row.ExistingPassive.Icon : null;
                row.ReplaceIcon = false;
                if (row.ResolvedIcon == null) Warning(result, row, "Icon is missing. Empty resource/null Sprite allowed; existing resource and Sprite are preserved.");
                return;
            }
            row.AppliedIconResource = row.IconResource;
            row.ResolvedIcon = LoadSprite(row.IconResource);
            row.ReplaceIcon = true;
            if (row.ResolvedIcon == null) Error(result, row, "PassiveIconResource cannot resolve a Sprite: '" + row.IconResource + "'.");
        }

        private static Dictionary<int, PassiveSO> LoadExistingPassives(PassiveImportValidationResult result)
        {
            var existing = new Dictionary<int, PassiveSO>();
            string folder = WesternPassiveDataGeneration.OutputFolder;
            if (!AssetDatabase.IsValidFolder(folder)) return existing;
            foreach (string guid in AssetDatabase.FindAssets("t:PassiveSO", new[] { folder }).OrderBy(g => g, StringComparer.Ordinal))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var passive = AssetDatabase.LoadAssetAtPath<PassiveSO>(path);
                if (passive == null) { result.ErrorMessages.Add("Unreadable PassiveSO: " + path); continue; }
                if (existing.TryGetValue(passive.PassiveID, out var duplicate))
                    result.ErrorMessages.Add($"Duplicate existing PassiveID {passive.PassiveID}: {AssetDatabase.GetAssetPath(duplicate)} / {path}. Import blocked.");
                else existing.Add(passive.PassiveID, passive);
            }
            return existing;
        }

        private static void CheckOutputAssetTypes(PassiveImportValidationResult result)
        {
            string databasePath = WesternPassiveDataGeneration.DatabaseAssetPath;
            if (File.Exists(Path.GetFullPath(databasePath)) && AssetDatabase.LoadAssetAtPath<PassiveDatabaseSO>(databasePath) == null)
                result.ErrorMessages.Add("Existing database asset cannot be read as PassiveDatabaseSO: " + databasePath);
            string registryPath = WesternPassiveDataGeneration.RegistryAssetPath;
            if (File.Exists(Path.GetFullPath(registryPath)) && AssetDatabase.LoadAssetAtPath<PassiveEffectRegistrySO>(registryPath) == null)
                result.ErrorMessages.Add("Existing registry asset cannot be read as PassiveEffectRegistrySO: " + registryPath);
        }

        public static void LogValidation(PassiveImportValidationResult result)
        {
            foreach (string error in result.Errors) Debug.LogError("[Western Passive] " + error);
            foreach (string warning in result.Warnings) Debug.LogWarning("[Western Passive] " + warning);
            Debug.Log($"Definitions: {result.Rows.Count}\nFlat: {result.FlatCount}\nAbility: {result.AbilityCount}\nShift: {result.ShiftCount}\n" +
                $"Common: {result.RarityCount(PassiveRarity.Common)}\nUncommon: {result.RarityCount(PassiveRarity.Uncommon)}\n" +
                $"Rare: {result.RarityCount(PassiveRarity.Rare)}\nLegendary: {result.RarityCount(PassiveRarity.Legendary)}\n" +
                $"WESTERN_PASSIVE_VALIDATION: {result.Rows.Count} definitions, {result.Errors.Count} errors, {result.Warnings.Count} warnings. No assets were modified.");
        }

        public static void Import(string filePath)
        {
            var result = ParseAndValidate(filePath);
            LogValidation(result);
            if (result.Errors.Count != 0) throw new InvalidDataException("Western passive import rejected by preflight.");
            ApplyImport(result);
            EditorPrefs.SetString(LastExcelPathKey, Path.GetFullPath(filePath));
        }

        [MenuItem("Tools/Western Lemegeton/Passive/Reimport Last Workbook")]
        public static void ReimportLastWorkbook()
        {
            Import(EditorPrefs.GetString(LastExcelPathKey, Path.GetFullPath(WesternPassiveDataGeneration.WorkbookAssetPath)));
        }

        // Only Import can reach this mutation phase, after the complete preflight.
        private static void ApplyImport(PassiveImportValidationResult result)
        {
            if (result.Errors.Count != 0) throw new InvalidDataException("Cannot apply an invalid Western workbook.");
            if (!PassiveEffectRegistryGenerator.TryCollectEffects(out _, out var errors))
                throw new InvalidDataException("Western registry collection failed: " + string.Join("\n", errors));
            EnsureAssetFolder(WesternPassiveDataGeneration.OutputFolder);
            var database = AssetDatabase.LoadAssetAtPath<PassiveDatabaseSO>(WesternPassiveDataGeneration.DatabaseAssetPath);
            if (database == null)
            {
                database = ScriptableObject.CreateInstance<PassiveDatabaseSO>();
                AssetDatabase.CreateAsset(database, WesternPassiveDataGeneration.DatabaseAssetPath);
            }
            if (!PassiveEffectRegistryGenerator.Rebuild(out int registeredCount, true))
                throw new InvalidOperationException("Western registry rebuild failed. PassiveSO data was not applied; prepared assets may remain.");
            var registry = AssetDatabase.LoadAssetAtPath<PassiveEffectRegistrySO>(WesternPassiveDataGeneration.RegistryAssetPath);
            if (registry == null) throw new InvalidDataException("Western registry rebuild produced no readable registry.");

            var imported = new List<PassiveSO>();
            foreach (var row in result.Rows.OrderBy(r => r.PassiveID))
            {
                var passive = row.ExistingPassive;
                if (passive == null)
                {
                    passive = ScriptableObject.CreateInstance<PassiveSO>();
                    string path = AssetDatabase.GenerateUniqueAssetPath($"{WesternPassiveDataGeneration.OutputFolder}/Passive_{row.PassiveID:D3}.asset");
                    AssetDatabase.CreateAsset(passive, path);
                }
                passive.ApplyImportedData(row.PassiveID, row.AppliedIconResource, row.ResolvedIcon, row.ReplaceIcon,
                    row.DisplayName, row.Description, row.Rarity, row.StatType, row.Category, row.Value, row.HasValue, row.ValueType, row.ScriptName);
                EditorUtility.SetDirty(passive);
                imported.Add(passive);
            }
            database.SetImportedPassives(imported);
            database.SetEffectRegistry(registry);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"WESTERN_PASSIVE_IMPORTED: {imported.Count} definitions; {registeredCount} effects; {result.Warnings.Count} warnings.");
        }

        private static Sprite LoadSprite(string resource)
        {
            string normalized = resource.Replace('\\', '/');
            return normalized.StartsWith("Assets/", StringComparison.Ordinal)
                ? AssetDatabase.LoadAssetAtPath<Sprite>(normalized) : Resources.Load<Sprite>(normalized);
        }

        private static string Get(SimpleXlsxReader.RowData row, string key) => row.Cells.TryGetValue(key, out string value) ? value ?? string.Empty : string.Empty;
        private static bool TryParseEnum<T>(string text, out T value) where T : struct =>
            Enum.TryParse(text?.Trim(), true, out value) && Enum.IsDefined(typeof(T), value);
        private static bool TryParseInt(string text, out int value)
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return true;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) &&
                number >= int.MinValue && number <= int.MaxValue && number == Math.Truncate(number))
            { value = (int)number; return true; }
            value = 0;
            return false;
        }
        private static void Error(PassiveImportValidationResult result, PassiveImportRow row, string message) =>
            result.ErrorMessages.Add($"Row {row.RowNumber}, PassiveID {row.PassiveID}: {message}");
        private static void Warning(PassiveImportValidationResult result, PassiveImportRow row, string message) =>
            result.WarningMessages.Add($"Row {row.RowNumber}, PassiveID {row.PassiveID}: {message}");
        private static void EnsureAssetFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
