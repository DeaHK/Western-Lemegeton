#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using WesternLemegeton.Passives;

namespace WesternLemegeton.Passives.Editor
{
    public static class PassiveExcelImporter
    {
        private const string LastExcelPathKey = "PassiveExcelImporter.LastExcelPath";
        private const string OutputFolder = "Assets/Resources/Passive";
        private const string DatabaseAssetPath = OutputFolder + "/PassiveDatabase.asset";

        private static readonly string[] RequiredHeaders =
        {
            "PassiveID",
            "PassiveIconResource",
            "NameStringKey",
            "DescriptionStringKey",
            "Rarity",
            "StatType",
            "Category",
            "Value",
            "ValueType",
            "ScriptName"
        };

        [MenuItem("Tools/Passive/Import Excel To SO")]
        public static void ImportWithFilePicker()
        {
            string previousPath = EditorPrefs.GetString(
                LastExcelPathKey,
                Application.dataPath
            );

            string initialDirectory = Directory.Exists(previousPath)
                ? previousPath
                : Path.GetDirectoryName(previousPath);

            if (string.IsNullOrWhiteSpace(initialDirectory) ||
                !Directory.Exists(initialDirectory))
            {
                initialDirectory = Application.dataPath;
            }

            string filePath = EditorUtility.OpenFilePanel(
                "Select Passive Excel",
                initialDirectory,
                "xlsx"
            );

            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }

            EditorPrefs.SetString(LastExcelPathKey, filePath);
            Import(filePath);
        }

        [MenuItem("Tools/Passive/Reimport Last Excel")]
        public static void ReimportLastExcel()
        {
            string filePath = EditorPrefs.GetString(LastExcelPathKey, string.Empty);

            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Debug.LogWarning(
                    "No previous passive Excel file was found. " +
                    "Use Tools > Passive > Import Excel To SO first."
                );
                return;
            }

            Import(filePath);
        }

        public static void Import(string filePath, int minimumPassiveId = 1000)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                Debug.LogError($"[Passive Excel] File not found: {filePath}");
                return;
            }

            if (!string.Equals(
                    Path.GetExtension(filePath),
                    ".xlsx",
                    StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogError("[Passive Excel] Only .xlsx files are supported.");
                return;
            }

            SimpleXlsxReader.SheetData sheet;

            try
            {
                sheet = SimpleXlsxReader.ReadSheet(filePath, "Sheet1");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return;
            }

            List<string> missingHeaders = RequiredHeaders
                .Where(required => !sheet.Headers.Any(
                    header => string.Equals(
                        header,
                        required,
                        StringComparison.OrdinalIgnoreCase
                    )
                ))
                .ToList();

            if (missingHeaders.Count > 0)
            {
                Debug.LogError(
                    "[Passive Excel] Missing required columns: " +
                    string.Join(", ", missingHeaders)
                );
                return;
            }

            ValidateStatTypes(sheet, minimumPassiveId);

            PassiveEffectRegistryGenerator.TryCollectEffects(
                out List<PassiveEffectRegistryGenerator.EffectInfo> effectInfos,
                out List<string> registryErrors
            );

            foreach (string error in registryErrors)
            {
                Debug.LogError($"[Passive Registry] {error}");
            }

            HashSet<string> registeredScriptNames = new(
                effectInfos.Select(info => info.Key),
                StringComparer.Ordinal
            );

            EnsureAssetFolder(OutputFolder);

            Dictionary<int, PassiveSO> existingById = LoadExistingPassives();
            HashSet<int> excelIds = new();
            List<PassiveSO> importedPassives = new();

            int createdCount = 0;
            int updatedCount = 0;
            int errorCount = registryErrors.Count;
            int warningCount = 0;

            foreach (SimpleXlsxReader.RowData row in sheet.Rows)
            {
                string idText = Get(row, "PassiveID");

                if (string.IsNullOrWhiteSpace(idText))
                {
                    continue;
                }

                if (!TryParseInt(idText, out int passiveID) || passiveID <= 0)
                {
                    LogRowError(row, $"Invalid PassiveID '{idText}'.");
                    errorCount++;
                    continue;
                }

                // IDs below 1000 are preserved donor examples, not Astra content.
                if (passiveID < minimumPassiveId) continue;

                if (!excelIds.Add(passiveID))
                {
                    LogRowError(row, $"Duplicate PassiveID {passiveID} in Excel.");
                    errorCount++;
                    continue;
                }

                if (!TryParseEnum(Get(row, "Rarity"), out PassiveRarity rarity))
                {
                    LogRowError(row, $"Invalid Rarity '{Get(row, "Rarity")}'.");
                    errorCount++;
                    continue;
                }

                if (!TryParseEnum(Get(row, "Category"), out PassiveCategory category))
                {
                    LogRowError(row, $"Invalid Category '{Get(row, "Category")}'.");
                    errorCount++;
                    continue;
                }

                string valueText = Get(row, "Value");
                bool hasValue = !string.IsNullOrWhiteSpace(valueText);
                float value = 0f;

                if (hasValue && !SimpleXlsxReader.TryParseFloat(valueText, out value))
                {
                    LogRowError(row, $"Invalid Value '{valueText}'.");
                    errorCount++;
                    continue;
                }

                string valueTypeText = Get(row, "ValueType");
                PassiveValueType valueType = PassiveValueType.None;

                if (!string.IsNullOrWhiteSpace(valueTypeText) &&
                    !TryParseEnum(valueTypeText, out valueType))
                {
                    LogRowError(row, $"Invalid ValueType '{valueTypeText}'.");
                    errorCount++;
                    continue;
                }

                string statType = Get(row, "StatType").Trim();
                string scriptName = Get(row, "ScriptName").Trim();

                int errorsBeforeRow = errorCount;
                ValidateRow(
                    row,
                    passiveID,
                    category,
                    statType,
                    hasValue,
                    valueType,
                    scriptName,
                    registeredScriptNames,
                    ref errorCount,
                    ref warningCount
                );

                if (errorCount != errorsBeforeRow) continue;

                string iconResource = Get(row, "PassiveIconResource").Trim();
                Sprite importedIcon = null;
                bool replaceIcon = false;

                if (!string.IsNullOrWhiteSpace(iconResource))
                {
                    importedIcon = LoadSprite(iconResource);

                    // Astra's existing IMGUI art is Texture2D. Preserve its resource
                    // path without changing those texture import settings to Sprite.
                    if (importedIcon == null && Resources.Load<Texture2D>(iconResource) == null)
                    {
                        LogRowWarning(
                            row,
                            $"PassiveIconResource '{iconResource}' could not be loaded. " +
                            "Existing icon will be preserved."
                        );
                        warningCount++;
                    }
                    else
                    {
                        replaceIcon = true;
                    }
                }

                bool created = false;

                if (!existingById.TryGetValue(passiveID, out PassiveSO passive) ||
                    passive == null)
                {
                    passive = ScriptableObject.CreateInstance<PassiveSO>();
                    string assetPath =
                        $"{OutputFolder}/Passive_{passiveID:D3}.asset";

                    assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);
                    AssetDatabase.CreateAsset(passive, assetPath);
                    existingById[passiveID] = passive;
                    created = true;
                }

                passive.ApplyImportedData(
                    passiveID,
                    iconResource,
                    importedIcon,
                    replaceIcon,
                    Get(row, "NameStringKey"),
                    Get(row, "DescriptionStringKey"),
                    rarity,
                    statType,
                    category,
                    value,
                    hasValue,
                    valueType,
                    scriptName
                );

                EditorUtility.SetDirty(passive);
                importedPassives.Add(passive);

                if (created)
                {
                    createdCount++;
                }
                else
                {
                    updatedCount++;
                }
            }

            importedPassives.Sort(
                (a, b) => a.PassiveID.CompareTo(b.PassiveID)
            );

            PassiveDatabaseSO database =
                AssetDatabase.LoadAssetAtPath<PassiveDatabaseSO>(DatabaseAssetPath);

            if (database == null)
            {
                database = ScriptableObject.CreateInstance<PassiveDatabaseSO>();
                AssetDatabase.CreateAsset(database, DatabaseAssetPath);
            }

            if (errorCount > 0)
            {
                throw new InvalidDataException($"Passive import rejected: {errorCount} error(s).");
            }
            database.SetImportedPassives(importedPassives);
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();

            bool registryBuilt = PassiveEffectRegistryGenerator.Rebuild(
                out int registeredCount,
                true
            );

            AssetDatabase.Refresh();

            string registryStatus = registryBuilt
                ? $"Registry: {registeredCount} effect(s)"
                : "Registry: build failed";

            Debug.Log(
                $"[Passive Excel] Import complete. " +
                $"Created {createdCount}, Updated {updatedCount}, " +
                $"Errors {errorCount}, Warnings {warningCount}. " +
                registryStatus + ".\n" +
                $"Database: {DatabaseAssetPath}"
            );
        }

        // Preflight before any asset is created or changed. Scripted effects may
        // leave StatType empty, but any supplied key must be a supported gameplay stat.
        public static void ValidateStatTypes(SimpleXlsxReader.SheetData sheet, int minimumPassiveId = 1000)
        {
            var errors = new List<string>();
            foreach (var row in sheet.Rows)
            {
                if (!TryParseInt(Get(row, "PassiveID"), out int id) || id < minimumPassiveId) continue;
                string stat = Get(row, "StatType").Trim();
                bool flat = string.Equals(Get(row, "Category").Trim(), "Flat", StringComparison.OrdinalIgnoreCase);
                if ((flat || stat.Length > 0) && !AstraPassiveStatKeys.IsSupported(stat))
                    errors.Add($"Row {row.RowNumber}, PassiveID {id}: unknown StatType '{stat}'. " +
                        "Choose PlayerDamage, MoveSpeed or RavenDamage.");
            }
            if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
        }

        private static void ValidateRow(
            SimpleXlsxReader.RowData row,
            int passiveID,
            PassiveCategory category,
            string statType,
            bool hasValue,
            PassiveValueType valueType,
            string scriptName,
            HashSet<string> registeredScriptNames,
            ref int errorCount,
            ref int warningCount)
        {
            if (category == PassiveCategory.Flat)
            {
                if (string.IsNullOrWhiteSpace(statType))
                {
                    LogRowError(row, $"PassiveID {passiveID}: Flat requires StatType.");
                    errorCount++;
                }

                if (!hasValue)
                {
                    LogRowError(row, $"PassiveID {passiveID}: Flat requires Value.");
                    errorCount++;
                }

                if (valueType == PassiveValueType.None)
                {
                    LogRowError(
                        row,
                        $"PassiveID {passiveID}: Flat requires ValueType (Raw/Percent)."
                    );
                    errorCount++;
                }

                if (!string.IsNullOrWhiteSpace(scriptName))
                {
                    LogRowWarning(
                        row,
                        $"PassiveID {passiveID}: ScriptName is ignored for Flat."
                    );
                    warningCount++;
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(scriptName))
            {
                LogRowError(
                    row,
                    $"PassiveID {passiveID}: {category} requires ScriptName."
                );
                errorCount++;
                return;
            }

            if (!registeredScriptNames.Contains(scriptName))
            {
                LogRowError(
                    row,
                    $"PassiveID {passiveID}: ScriptName '{scriptName}' is not " +
                    "registered by [PassiveEffect(\"...\")]."
                );
                errorCount++;
            }
        }

        private static Dictionary<int, PassiveSO> LoadExistingPassives()
        {
            Dictionary<int, PassiveSO> result = new();
            string[] guids = AssetDatabase.FindAssets(
                "t:PassiveSO",
                new[] { OutputFolder }
            );

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                PassiveSO passive = AssetDatabase.LoadAssetAtPath<PassiveSO>(path);

                if (passive == null)
                {
                    continue;
                }

                if (result.TryGetValue(passive.PassiveID, out PassiveSO duplicate))
                {
                    Debug.LogError(
                        $"[Passive Excel] Duplicate existing PassiveID " +
                        $"{passive.PassiveID}: " +
                        $"{AssetDatabase.GetAssetPath(duplicate)} / {path}"
                    );
                    continue;
                }

                result.Add(passive.PassiveID, passive);
            }

            return result;
        }

        private static Sprite LoadSprite(string resource)
        {
            if (string.IsNullOrWhiteSpace(resource))
            {
                return null;
            }

            string normalized = resource.Replace('\\', '/').Trim();

            if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return AssetDatabase.LoadAssetAtPath<Sprite>(normalized);
            }

            Sprite resourcesSprite = Resources.Load<Sprite>(normalized);

            if (resourcesSprite != null)
            {
                return resourcesSprite;
            }

            string fileName = Path.GetFileNameWithoutExtension(normalized);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            string[] guids = AssetDatabase.FindAssets($"{fileName} t:Sprite");

            if (guids.Length != 1)
            {
                return null;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string normalized = folderPath.Replace('\\', '/').TrimEnd('/');

            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string[] parts = normalized.Split('/');

            if (parts.Length == 0 || parts[0] != "Assets")
            {
                throw new ArgumentException(
                    $"Asset folder must start with Assets/: {folderPath}"
                );
            }

            string current = "Assets";

            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static string Get(SimpleXlsxReader.RowData row, string header)
        {
            return row.Cells.TryGetValue(header, out string value)
                ? value ?? string.Empty
                : string.Empty;
        }

        private static bool TryParseEnum<T>(string text, out T value)
            where T : struct
        {
            return Enum.TryParse(text?.Trim(), true, out value);
        }

        private static bool TryParseInt(string text, out int value)
        {
            if (int.TryParse(
                    text,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                return true;
            }

            if (double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double numeric) &&
                Math.Abs(numeric - Math.Round(numeric)) < 0.000001)
            {
                value = (int)Math.Round(numeric);
                return true;
            }

            value = 0;
            return false;
        }

        private static void LogRowError(
            SimpleXlsxReader.RowData row,
            string message)
        {
            Debug.LogError($"[Passive Excel / Row {row.RowNumber}] {message}");
        }

        private static void LogRowWarning(
            SimpleXlsxReader.RowData row,
            string message)
        {
            Debug.LogWarning($"[Passive Excel / Row {row.RowNumber}] {message}");
        }
    }
}
#endif
