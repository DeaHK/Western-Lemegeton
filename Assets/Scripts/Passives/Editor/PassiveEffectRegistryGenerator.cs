#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using WesternLemegeton.Passives;

namespace WesternLemegeton.Passives.Editor
{
    public static class PassiveEffectRegistryGenerator
    {
        public sealed class EffectInfo
        {
            public string Key;
            public Type Type;
        }

        private const string DataFolder = WesternPassiveDataGeneration.OutputFolder;
        private const string GeneratedEffectFolder = DataFolder + "/Effects/_Generated";
        private const string RegistryAssetPath = WesternPassiveDataGeneration.RegistryAssetPath;
        private const string DatabaseAssetPath = WesternPassiveDataGeneration.DatabaseAssetPath;

        

        [MenuItem("Tools/Western Lemegeton/Passive/Rebuild Effect Registry")]
        public static void RebuildMenu()
        {
            if (Rebuild(out int count, true))
            {
                Debug.Log($"Passive Effect Registry rebuilt. Registered: {count}");
            }
        }

        public static bool TryCollectEffects(
            out List<EffectInfo> effects,
            out List<string> errors)
        {
            effects = new List<EffectInfo>();
            errors = new List<string>();

            Dictionary<string, Type> keys = new(StringComparer.Ordinal);
            IEnumerable<Type> types = TypeCache.GetTypesWithAttribute<PassiveEffectAttribute>();

            foreach (Type type in types.OrderBy(t => t.FullName, StringComparer.Ordinal))
            {
                if (type == null)
                {
                    continue;
                }

                PassiveEffectAttribute attribute =
                    type.GetCustomAttribute<PassiveEffectAttribute>(false);

                if (attribute == null)
                {
                    continue;
                }

                if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters ||
                    !typeof(PassiveEffectScript).IsAssignableFrom(type))
                {
                    errors.Add($"{type.FullName}: PassiveEffect requires a concrete, closed PassiveEffectScript type.");
                    continue;
                }

                string key = attribute.Key?.Trim();

                if (string.IsNullOrWhiteSpace(key))
                {
                    errors.Add($"{type.FullName}: PassiveEffect key is empty.");
                    continue;
                }

                if (keys.TryGetValue(key, out Type existingType))
                {
                    errors.Add(
                        $"Duplicate PassiveEffect key '{key}': " +
                        $"{existingType.FullName}, {type.FullName}"
                    );
                    continue;
                }

                keys.Add(key, type);
                effects.Add(new EffectInfo { Key = key, Type = type });
            }

            return errors.Count == 0;
        }

        public static bool Rebuild(out int registeredCount, bool logErrors)
        {
            registeredCount = 0;

            if (!TryCollectEffects(out List<EffectInfo> effects, out List<string> errors))
            {
                if (logErrors)
                {
                    foreach (string error in errors)
                    {
                        Debug.LogError($"[Passive Registry] {error}");
                    }
                }

                return false;
            }

            EnsureAssetFolder(DataFolder);
            EnsureAssetFolder(DataFolder + "/Effects");
            EnsureAssetFolder(GeneratedEffectFolder);

            PassiveEffectRegistrySO registry =
                AssetDatabase.LoadAssetAtPath<PassiveEffectRegistrySO>(RegistryAssetPath);

            if (registry == null)
            {
                registry = ScriptableObject.CreateInstance<PassiveEffectRegistrySO>();
                AssetDatabase.CreateAsset(registry, RegistryAssetPath);
            }

            List<PassiveEffectRegistrySO.Entry> entries = new();
            HashSet<string> expectedPrototypePaths = new(StringComparer.Ordinal);

            foreach (EffectInfo effect in effects.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                string safeFileName = MakeSafeFileName(effect.Key);
                string prototypePath =
                    $"{GeneratedEffectFolder}/{safeFileName}.asset";

                expectedPrototypePaths.Add(prototypePath);

                PassiveEffectScript prototype =
                    AssetDatabase.LoadAssetAtPath<PassiveEffectScript>(prototypePath);

                if (prototype != null && prototype.GetType() != effect.Type)
                {
                    AssetDatabase.DeleteAsset(prototypePath);
                    prototype = null;
                }

                if (prototype == null)
                {
                    prototype = ScriptableObject.CreateInstance(effect.Type)
                        as PassiveEffectScript;

                    if (prototype == null)
                    {
                        if (logErrors)
                        {
                            Debug.LogError(
                                $"[Passive Registry] Could not create {effect.Type.FullName}."
                            );
                        }

                        return false;
                    }

                    prototype.name = effect.Key;
                    AssetDatabase.CreateAsset(prototype, prototypePath);
                }

                entries.Add(
                    new PassiveEffectRegistrySO.Entry(effect.Key, prototype)
                );
            }

            CleanupOrphanedGeneratedPrototypes(expectedPrototypePaths);

            registry.SetGeneratedEntries(entries);
            EditorUtility.SetDirty(registry);

            PassiveDatabaseSO database =
                AssetDatabase.LoadAssetAtPath<PassiveDatabaseSO>(DatabaseAssetPath);

            if (database != null)
            {
                database.SetEffectRegistry(registry);
                EditorUtility.SetDirty(database);
            }

            WriteLinkXml(effects);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            registeredCount = entries.Count;
            return true;
        }

        

        private static void CleanupOrphanedGeneratedPrototypes(
            HashSet<string> expectedPaths)
        {
            string[] guids = AssetDatabase.FindAssets(
                string.Empty,
                new[] { GeneratedEffectFolder }
            );

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                PassiveEffectScript effectAsset =
                    AssetDatabase.LoadAssetAtPath<PassiveEffectScript>(path);

                if (effectAsset != null && !expectedPaths.Contains(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static void WriteLinkXml(List<EffectInfo> effects)
        {
            string generatedFolder = GetGeneratedSourceFolder();

            if (string.IsNullOrWhiteSpace(generatedFolder))
            {
                Debug.LogWarning(
                    "[Passive Registry] Could not locate Passive/Generated folder. " +
                    "IL2CPP link.xml was not generated."
                );
                return;
            }

            string assetPath = generatedFolder + "/link.xml";
            string absolutePath = Path.GetFullPath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));

            StringBuilder builder = new();
            builder.AppendLine("<linker>");

            foreach (IGrouping<string, EffectInfo> assemblyGroup in effects
                         .GroupBy(e => e.Type.Assembly.GetName().Name)
                         .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                builder.Append("  <assembly fullname=\"");
                builder.Append(EscapeXml(assemblyGroup.Key));
                builder.AppendLine("\">");

                foreach (EffectInfo effect in assemblyGroup
                             .OrderBy(e => e.Type.FullName, StringComparer.Ordinal))
                {
                    builder.Append("    <type fullname=\"");
                    builder.Append(EscapeXml(effect.Type.FullName ?? effect.Type.Name));
                    builder.AppendLine("\" preserve=\"all\" />");
                }

                builder.AppendLine("  </assembly>");
            }

            builder.AppendLine("</linker>");
            string content = builder.ToString();

            if (!File.Exists(absolutePath) || File.ReadAllText(absolutePath) != content)
            {
                File.WriteAllText(absolutePath, content, new UTF8Encoding(false));
            }
        }

        private static string GetGeneratedSourceFolder()
        {
            string[] generatorGuids =
                AssetDatabase.FindAssets("PassiveEffectRegistryGenerator t:Script");

            foreach (string guid in generatorGuids)
            {
                string generatorPath = AssetDatabase.GUIDToAssetPath(guid);

                if (!generatorPath.EndsWith(
                        "/PassiveEffectRegistryGenerator.cs",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string editorFolder = Path.GetDirectoryName(generatorPath)
                    ?.Replace('\\', '/');

                if (string.IsNullOrWhiteSpace(editorFolder))
                {
                    continue;
                }

                string passiveFolder = Path.GetDirectoryName(editorFolder)
                    ?.Replace('\\', '/');

                if (string.IsNullOrWhiteSpace(passiveFolder))
                {
                    continue;
                }

                return passiveFolder + "/Generated";
            }

            return null;
        }

        private static string MakeSafeFileName(string key)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            StringBuilder builder = new(key.Length);

            foreach (char c in key)
            {
                builder.Append(invalid.Contains(c) ? '_' : c);
            }

            string result = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? "PassiveEffect" : result;
        }

        private static string EscapeXml(string value)
        {
            return (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            string normalized = folderPath.Replace('\\', '/').TrimEnd('/');

            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string[] parts = normalized.Split('/');
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
    }
}
#endif
