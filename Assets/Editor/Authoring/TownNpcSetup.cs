using System;
using UnityEditor;
using UnityEngine;

namespace WesternLemegeton.EditorTools
{
    /// <summary>
    /// Imports the supplied town NPC illustrations and places reusable 2.5D NPC prefabs in Hirva.
    /// Run from Western Lemegeton > Setup Town NPCs.
    /// </summary>
    public static class TownNpcSetup
    {
        private const string NpcFolder = "Assets/Editable/Shared/npc";
        private const string ActorFolder = "Assets/Editable/Actors";
        private const string TownPrefabPath = "Assets/Editable/Rooms/Hirva.prefab";

        private sealed class NpcDefinition
        {
            public string Image;
            public string Prefab;
            public string ObjectName;
            public Vector2 TownPosition;
            public float Scale = 1.25f;
        }

        private static readonly NpcDefinition[] Npcs =
        {
            new NpcDefinition { Image = "강아지 소녀.png", Prefab = "NPC_DogGirl.prefab", ObjectName = "NPC - Dog Girl", TownPosition = new Vector2(-9.0f, -1.7f) },
            new NpcDefinition { Image = "고양이 소녀.png", Prefab = "NPC_CatGirl.prefab", ObjectName = "NPC - Cat Girl", TownPosition = new Vector2(-2.2f, -1.9f) },
            new NpcDefinition { Image = "양 캐릭터.png", Prefab = "NPC_Sheep.prefab", ObjectName = "NPC - Sheep", TownPosition = new Vector2(7.8f, -2.0f) }
        };

        [MenuItem("Western Lemegeton/Setup Town NPCs")]
        public static void Setup()
        {
            EnsureFolders();
            foreach (var definition in Npcs)
                ImportNpcSprite(definition);

            AssetDatabase.Refresh();
            foreach (var definition in Npcs)
                CreateNpcPrefab(definition);

            PlaceNpcsInTown();
            AssetDatabase.SaveAssets();
            Debug.Log("Town NPC setup complete: 3 reusable NPC prefabs placed around Hirva's street.");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(ActorFolder))
                AssetDatabase.CreateFolder("Assets/Editable", "Actors");
        }

        private static string ImagePath(NpcDefinition definition) => NpcFolder + "/" + definition.Image;

        private static void ImportNpcSprite(NpcDefinition definition)
        {
            var importer = AssetImporter.GetAtPath(ImagePath(definition)) as TextureImporter;
            if (!importer)
                throw new InvalidOperationException("NPC image not found or not a texture: " + ImagePath(definition));

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 1024;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
            settings.spritePivot = new Vector2(.5f, 0);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static void CreateNpcPrefab(NpcDefinition definition)
        {
            string spritePath = ImagePath(definition);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (!sprite)
                throw new InvalidOperationException("Unity did not import the NPC image as a Sprite: " + spritePath);

            string prefabPath = ActorFolder + "/" + definition.Prefab;
            var root = new GameObject(definition.ObjectName);
            try
            {
                root.layer = PaperWorld.LogicLayer;
                root.transform.localScale = Vector3.one * definition.Scale;
                var source = root.AddComponent<SpriteRenderer>();
                source.sprite = sprite;
                source.sortingOrder = 0;

                var paperCard = root.AddComponent<PaperCard>();
                paperCard.Source = source;
                paperCard.Flat = false;

                var worldProp = root.AddComponent<WorldProp>();
                worldProp.Visual = source;
                worldProp.Flat = false;

                var viewObject = new GameObject("Rendered NPC");
                viewObject.layer = 0;
                viewObject.transform.SetParent(root.transform, false);
                var view = viewObject.AddComponent<SpriteRenderer>();
                view.sprite = sprite;
                view.sortingOrder = 0;
                paperCard.Visible = view;

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void PlaceNpcsInTown()
        {
            var town = PrefabUtility.LoadPrefabContents(TownPrefabPath);
            try
            {
                var scenery = town.transform.Find("Scenery");
                if (!scenery)
                    throw new InvalidOperationException("Hirva prefab has no Scenery root.");

                var npcRoot = scenery.Find("Town_NPCs");
                if (!npcRoot)
                {
                    var container = new GameObject("Town_NPCs");
                    npcRoot = container.transform;
                    npcRoot.SetParent(scenery, false);
                }

                foreach (var definition in Npcs)
                {
                    string prefabPath = ActorFolder + "/" + definition.Prefab;
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (!prefab)
                        throw new InvalidOperationException("NPC prefab was not created: " + prefabPath);

                    var existing = npcRoot.Find(definition.ObjectName);
                    Transform placed;
                    if (existing)
                    {
                        placed = existing;
                    }
                    else
                    {
                        var instance = PrefabUtility.InstantiatePrefab(prefab, npcRoot) as GameObject;
                        if (!instance)
                            throw new InvalidOperationException("Could not instantiate NPC prefab: " + prefabPath);
                        instance.name = definition.ObjectName;
                        placed = instance.transform;
                    }

                    placed.localPosition = PaperWorld.Point(definition.TownPosition);
                    placed.localRotation = Quaternion.identity;
                    placed.localScale = Vector3.one * definition.Scale;
                }

                PrefabUtility.SaveAsPrefabAsset(town, TownPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(town);
            }
        }
    }
}
