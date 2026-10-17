using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WesternLemegeton.Stigmas.Editor
{
    public static class StigmaFoundationVerification
    {
        static int checks;
        [MenuItem("Tools/Western Lemegeton/Stigma/Verify Foundation")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run foundation checks outside Play Mode.");
            checks = 0;
            var db = AssetDatabase.LoadAssetAtPath<StigmaDatabaseSO>(StigmaFoundationSetup.DatabasePath);
            Check(db && db.Validate(out _), "Stored database is valid");
            Check(db.Definitions.Count >= 3 && db.Combinations.Count >= 6, "Base definitions and combinations present");
            var state = new StigmaLoadout(db);
            var changes = new List<StigmaChange>();
            state.Changed += changes.Add;
            Check(state.TotalAcquiredCount == 0 && !state.BothEquipped && state.EffectiveLevel == 0, "New run starts empty");
            Check(!state.TryEquip(StigmaOwner.Player, "ros", out _) && changes.Count == 0, "Unowned equip fails without events");
            Check(!state.TryAcquire(StigmaOwner.Player, "missing", out _) && changes.Count == 0, "Unknown acquisition is rejected");
            Check(state.TryAcquire(StigmaOwner.Player, "ros", out _) && changes.Count == 1 && changes[0].Owner == StigmaOwner.Player, "Acquisition publishes owner event");
            Check(!state.TryAcquire(StigmaOwner.Player, "ros", out _) && state.GetAcquired(StigmaOwner.Player).Count == 1 && changes.Count == 1, "Same-owner duplicate does not occupy another slot");
            Check(state.GetAcquired(StigmaOwner.Raven).Count == 0 && !state.Owns(StigmaOwner.Raven, "ros"), "Player acquisition does not grant raven ownership");
            Check(!state.TryEquip(StigmaOwner.Raven, "ros", out _) && changes.Count == 1, "Cannot equip another owner's acquisition");
            Check(state.TryEquip(StigmaOwner.Player, "ros", out _), "Equip acquired definition");
            Check(state.Get(StigmaOwner.Player).Level == 1 && state.EffectiveLevel == 0, "First equip is level one and one-sided effects inactive");
            Check(state.TrySetLevel(StigmaOwner.Player, 3, out _) && state.EffectiveLevel == 0, "One-sided upgrading does not activate effects");
            Check(!state.TrySetLevel(StigmaOwner.Raven, 2, out _), "Unequipped actor cannot level");
            Check(state.TryAcquire(StigmaOwner.Raven, "ros", out _) && changes[changes.Count - 1].Owner == StigmaOwner.Raven, "Raven may acquire the same definition independently");
            Check(state.TryEquip(StigmaOwner.Raven, "ros", out _), "Both actors may equip their independently acquired same type");
            Check(state.EffectiveLevel == 1 && state.Combination.Id == "flood" && state.ChargeCapacity == 4, "Same-type pair and minimum level");
            Check(state.TryAcquire(StigmaOwner.Player, "ardor", out _) && state.TryEquip(StigmaOwner.Player, "ardor", out _), "Replace after acquisition");
            Check(state.Get(StigmaOwner.Player).Level == 1 && state.GetLevel(StigmaOwner.Player, "ros") == 3 && state.Get(StigmaOwner.Raven).Level == 1, "New stigma has its own level without overwriting previous or other owner levels");
            Check(state.Combination.Id == "vapor", "Mixed combination lookup");
            state.TryEquip(StigmaOwner.Player, "ros", out _);
            Check(state.Get(StigmaOwner.Player).Level == 3, "Re-equipping restores the stigma's own level");
            state.TryEquip(StigmaOwner.Player, "ardor", out _); state.TrySetLevel(StigmaOwner.Player, 3, out _);
            Check(state.TrySetLevel(StigmaOwner.Raven, 2, out _) && state.EffectiveLevel == 2, "Effective level follows lower actor");
            int eventsBefore = changes.Count;
            Check(!state.TrySetLevel(StigmaOwner.Raven, 0, out _) && !state.TrySetLevel(StigmaOwner.Raven, 4, out _) && changes.Count == eventsBefore, "Invalid levels are side-effect free");
            Check(!state.TryEquip(StigmaOwner.Player, "ardor", out _) && changes.Count == eventsBefore, "Same equipment does not emit replacement cleanup");
            foreach (var combination in db.Combinations)
            {
                Check(db.FindCombination(combination.First, combination.Second) == combination, combination.Id + " forward lookup");
                Check(db.FindCombination(combination.Second, combination.First) == combination, combination.Id + " reverse lookup");
                Check(combination.ChargeCapacity == combination.ChargeCost * 2, combination.Id + " capacity");
            }
            Check(db.Rules.ClampChargeAfterReplacement(8, 4) == 4 && db.Rules.ClampChargeAfterReplacement(2, 4) == 2, "Replacement clamps, never refills charge");
            Check(db.Rules.MaxChargeGrantsPerEnemy == 1 && db.Rules.MaximumSlowFraction == .5f && db.Rules.LinkSkillKey == KeyCode.G, "Agreed rule defaults");
            Check(StigmaCombat.LeavesTrace(StigmaOwner.Player, StigmaAttackKind.Skill) &&
                StigmaCombat.LeavesTrace(StigmaOwner.Player, StigmaAttackKind.Basic) &&
                StigmaCombat.LeavesTrace(StigmaOwner.Raven, StigmaAttackKind.Basic), "Player skills and both basic attacks leave traces");
            foreach (var kind in new[] { StigmaAttackKind.Followup, StigmaAttackKind.Link, StigmaAttackKind.Familiar, StigmaAttackKind.DamageOverTime })
                Check(!StigmaCombat.LeavesTrace(StigmaOwner.Player, kind) && !StigmaCombat.LeavesTrace(StigmaOwner.Raven, kind), "No trace from " + kind);
            Check(!StigmaCombat.LeavesTrace(StigmaOwner.Raven, StigmaAttackKind.Skill), "Raven special attacks are excluded");
            VerifyEnhanced(db);
            state.ResetRun();
            Check(state.TotalAcquiredCount == 0 && !state.Get(StigmaOwner.Player).IsEquipped && !state.Get(StigmaOwner.Raven).IsEquipped && state.Combination == null, "Reset clears both inventories and equipment");
            Check(state.GetLevel(StigmaOwner.Player, "ros") == 0 && state.GetLevel(StigmaOwner.Raven, "ros") == 0, "Reset clears per-owner per-stigma levels");
            Check(changes[changes.Count - 1].Kind == StigmaChangeKind.Reset, "Reset publishes after clearing state");
            VerifyExtension(db);
            VerifyStoredUi();
            string result = "STIGMA_FOUNDATION_VERIFIED: " + checks + " checks passed (Edit Mode; no combat-effect claims).";
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/stigma-foundation-verification.txt", result);
            Debug.Log(result);
        }

        static void VerifyExtension(StigmaDatabaseSO original)
        {
            var db = UnityEngine.Object.Instantiate(original);
            var extras = new List<StigmaDefinitionSO>();
            GameObject holder = null, ui = null;
            try
            {
                db.Definitions = new List<StigmaDefinitionSO>(original.Definitions);
                db.Combinations = new List<StigmaCombinationSO>(original.Combinations);
                for (int i = 0; i < 4; i++)
                {
                    var definition = UnityEngine.Object.Instantiate(original.Definitions[0]);
                    definition.Id = "test_extension_" + i; definition.DisplayName = "검사용 확장 " + i;
                    if (i == 0) definition.BaseDefinition = original.Definitions[0];
                    db.Definitions.Add(definition); extras.Add(definition);
                }
                Check(db.Validate(out _), "New and enhanced types require no enum changes");
                holder = new GameObject("Stigma verification state");
                var manager = holder.AddComponent<StigmaManager>(); manager.Configure(db);
                foreach (var definition in db.Definitions) Check(manager.TryAcquire(StigmaOwner.Player, definition.Id, out _), "Acquire " + definition.Id);
                Check(manager.Loadout.GetAcquired(StigmaOwner.Player).Count > 6, "Collection is not silently limited to visible slot count");
                manager.TryEquip(StigmaOwner.Player, extras[0].Id, out _);
                manager.TryAcquire(StigmaOwner.Raven, original.Definitions[0].Id, out _);
                manager.TryEquip(StigmaOwner.Raven, original.Definitions[0].Id, out _);
                Check(manager.Loadout.BothEquipped && !manager.Loadout.Combination, "Enhanced type does not inherit a base combination silently");
                ui = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editable/UI/GameUI.prefab"));
                var view = ui.GetComponent<GameUIView>().StigmaView; view.Bind(manager);
                Check(view.Slots.Length == 6 && view.NextButton.gameObject.activeSelf, "UI keeps six slots with overflow paging");
                view.SetPage(1);
                Check(view.Slots[0].Name.text == db.Definitions[6].DisplayName + " · Lv.1", "Seventh entry appears on second page with its own level");
                int remaining = db.Definitions.Count - 6;
                for (int i = 0; i < view.Slots.Length; i++)
                    Check(view.Slots[i].Button.interactable == (i < remaining), "Second-page slot occupancy " + i);
                view.SelectOwner(StigmaOwner.Raven); view.SelectSlot(0);
                Check(view.SelectedOwner == StigmaOwner.Raven && view.Page == 0 && view.SelectedId == original.Definitions[0].Id && !view.NextButton.gameObject.activeSelf && !view.Slots[1].Button.interactable, "Switch owner resets paging and displays only raven acquisitions");
                view.SelectOwner(StigmaOwner.Player);
                Check(view.NextButton.gameObject.activeSelf, "Player inventory remains intact after switching owner");
                manager.ResetRun();
                view.Refresh();
                Check(view.Page == 0 && !view.Slots[0].Button.interactable && !view.NextButton.gameObject.activeSelf, "UI reset clears stale selection and paging");
                db.Definitions.Add(extras[0]);
                Check(!db.Validate(out _), "Duplicate ID validation");
                db.Definitions.RemoveAt(db.Definitions.Count - 1);
                db.Combinations.Add(db.Combinations[0]);
                Check(!db.Validate(out _), "Duplicate pair validation");
                db.Combinations.RemoveAt(db.Combinations.Count - 1);
                extras[0].BaseDefinition = extras[0];
                Check(!db.Validate(out _), "Cyclic enhancement rejected");
            }
            finally
            {
                if (ui) UnityEngine.Object.DestroyImmediate(ui);
                if (holder) UnityEngine.Object.DestroyImmediate(holder);
                foreach (var d in extras) UnityEngine.Object.DestroyImmediate(d);
                UnityEngine.Object.DestroyImmediate(db);
            }
        }

        static void VerifyStoredUi()
        {
            var host = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editable/UI/GameUI.prefab").GetComponent<GameUIView>();
            Check(host.StigmaView && !host.transform.Find("Stigma_Build_Tree"), "Stigma management is the only stigma panel");
            Check(!host.StigmaView.gameObject.activeSelf && host.StigmaView.Slots.Length == 6, "New view starts hidden and has six slots");
            Check(host.StigmaView.DetailScroll && host.StigmaView.DetailScroll.content == host.StigmaView.DetailEffect.rectTransform &&
                host.StigmaView.DetailScroll.viewport.GetComponent<UnityEngine.UI.RectMask2D>(), "Combination descriptions have an authored masked scroll area");
            Check(host.StigmaView.OffersPanel && host.StigmaView.OffersTitle && host.StigmaView.OfferCards.Length == 3, "Three-card selection panel is authored");
            foreach (var card in host.StigmaView.OfferCards)
                Check(card.Button && card.Frame && card.Icon && card.Owner && card.Name && card.Effect, "Owner-labelled card references are complete");
            foreach (var slot in host.StigmaView.Slots) Check(slot.Button && slot.Frame && slot.Icon && slot.Name && slot.Equipped, "Complete serialized slot references");
            Check(host.StigmaView.Art && host.StigmaView.Art.Get("WaterIcon"), "Existing icon fallback is available");
            var systems = GameObject.Find("Game_Systems");
            Check(systems && systems.GetComponent<StigmaManager>() && systems.GetComponent<StigmaDebugMenu>(), "Main scene components connected");
            Check(systems.GetComponent<StigmaManager>().Database == AssetDatabase.LoadAssetAtPath<StigmaDatabaseSO>(StigmaFoundationSetup.DatabasePath), "Main scene database assigned");
        }

        static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Stigma foundation check failed: " + description);
            checks++;
        }

        static void VerifyEnhanced(StigmaDatabaseSO db)
        {
            var state = new StigmaLoadout(db);
            var enhanced = db.Find("flagra");
            var ardor = db.Find("ardor");
            Check(enhanced && enhanced.Icon && enhanced.BaseDefinition == ardor, "Flagra definition/icon/lineage saved");
            Check(!state.IsRoomCandidate(StigmaOwner.Player, enhanced), "Upgrade not offered without same-owner base");
            state.TryAcquire(StigmaOwner.Player, "ardor", out _);
            state.TryEquip(StigmaOwner.Player, "ardor", out _);
            state.TrySetLevel(StigmaOwner.Player, 3, out _);
            Check(state.IsRoomCandidate(StigmaOwner.Player, enhanced) && !state.IsRoomCandidate(StigmaOwner.Raven, enhanced), "Upgrade candidate gate is owner-specific");
            state.TryAcquire(StigmaOwner.Raven, "ros", out _); state.TryEquip(StigmaOwner.Raven, "ros", out _);
            state.TryAcquire(StigmaOwner.Player, "flagra", out _);
            Check(state.Get(StigmaOwner.Player).Definition == enhanced && state.Get(StigmaOwner.Player).Level == 1 && state.GetLevel(StigmaOwner.Player, "ardor") == 3, "Upgrade is a separate level-one stigma and does not overwrite ancestor level");
            Check(!state.TryEquip(StigmaOwner.Player, "ardor", out _) && state.Combination.Id == "vapor", "Downgrade blocked and explicit base combination inherited");
            Check(!state.IsRoomCandidate(StigmaOwner.Player, enhanced), "Owned upgrade excluded from cards");
            var expected = new[] { new[] {1.1f,1.5f,2,2.4f,3,3.6f,4}, new[] {1.1f,1.6f,2.1f,2.6f,3.3f,4,4.5f}, new[] {1.1f,1.7f,2.2f,2.8f,3.6f,4.5f,5} };
            for (int level = 1; level <= 3; level++)
            {
                state.TrySetLevel(StigmaOwner.Player, level, out _);
                Check(state.EffectLevel(StigmaOwner.Player) == level && state.EffectiveLevel == 1, "Flagra own level is independent of raven combination level");
                for (int ammo = 0; ammo <= 6; ammo++)
                {
                    var shot = StigmaWeaponEffects.Resolve(state, ammo, 4);
                    Check(Mathf.Approximately(shot.DamageMultiplier, expected[level-1][ammo]) && shot.Range == 8 && shot.ForcesReload && shot.ResetDodgeOnReload == (level == 3), "Flagra shot tier/ammo " + level + "/" + ammo);
                    Check(Mathf.Approximately(shot.CooldownMultiplier, .3f + .7f * ammo / 6f), "Linear cooldown reduction uses pre-shot ammo " + ammo);
                }
            }
            state.TryAcquire(StigmaOwner.Raven, "ardor", out _); state.TryEquip(StigmaOwner.Raven, "ardor", out _);
            Check(!state.IsRoomCandidate(StigmaOwner.Raven, enhanced) && !state.TryAcquire(StigmaOwner.Raven, "flagra", out _) && !state.TryEquip(StigmaOwner.Raven, "flagra", out _), "Raven cannot receive or equip Flagra even with base owned");
            Check(StigmaWeaponEffects.Resolve(state, 6, 4).DamageMultiplier == 5 && state.Combination.Id == "burn_rush", "Player upgrade keeps level three effects with level one raven base combination");
            state.TryAcquire(StigmaOwner.Player, "ros", out _);
            var preview = StigmaDescriptions.Detail(db.Find("ros"), state.GetLevel(StigmaOwner.Player, "ros"), state.Get(StigmaOwner.Player), state.Get(StigmaOwner.Raven), db.FindCombination(db.Find("ros"), ardor), db.Rules, "플레이어");
            Check(preview.Contains("로스 Lv.1") && !preview.Contains("로스 Lv.3"), "Unequipped stigma preview uses its own stored level");
            state.TryEquip(StigmaOwner.Player, "ros", out _); state.TrySetLevel(StigmaOwner.Player, 2, out _);
            state.TryEquip(StigmaOwner.Player, "flagra", out _);
            Check(state.Get(StigmaOwner.Player).Level == 3 && state.GetLevel(StigmaOwner.Player, "ros") == 2 && state.GetLevel(StigmaOwner.Raven, "ros") == 1, "Flagra, player Ros and raven Ros levels remain independent");
            state.ResetRun();
            Check(state.TryAcquire(StigmaOwner.Player, "flagra", out _) && state.TryEquip(StigmaOwner.Player, "flagra", out _), "Direct debug acquisition/equip bypasses base prerequisite");
            Check(!state.TryAcquire(StigmaOwner.Player, "ardor", out _), "Cannot reacquire ancestor after direct upgrade grant");
        }

        [MenuItem("Tools/Western Lemegeton/Stigma/Capture Management Preview")]
        public static void CapturePreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Capture the isolated preview outside Play Mode.");
            var scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = null;
            try
            {
                var db = AssetDatabase.LoadAssetAtPath<StigmaDatabaseSO>(StigmaFoundationSetup.DatabasePath);
                var holder = new GameObject("Preview state"); SceneManager.MoveGameObjectToScene(holder, scene);
                var manager = holder.AddComponent<StigmaManager>(); manager.Configure(db);
                manager.TryAcquire(StigmaOwner.Player, "ros", out _); manager.TryAcquire(StigmaOwner.Raven, "ardor", out _);
                manager.TryEquip(StigmaOwner.Player, "ros", out _); manager.TryEquip(StigmaOwner.Raven, "ardor", out _);
                manager.TrySetLevel(StigmaOwner.Player, 2, out _);
                var ui = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editable/UI/GameUI.prefab"));
                SceneManager.MoveGameObjectToScene(ui, scene);
                var host = ui.GetComponent<GameUIView>();
                foreach (Transform child in ui.transform) child.gameObject.SetActive(false);
                host.StigmaView.gameObject.SetActive(true);
                Font font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 18);
                foreach (var text in ui.GetComponentsInChildren<Text>(true)) text.font = font;
                foreach (var transform in ui.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
                var cameraObject = new GameObject("Preview camera"); SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 5;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black; camera.cullingMask = 1 << 31;
                target = new RenderTexture(1280, 720, 24); camera.targetTexture = target;
                var canvas = ui.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                host.StigmaView.Bind(manager);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                var previous = RenderTexture.active;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                    Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/stigma-management-preview.png", image.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
                Debug.Log("STIGMA_MANAGEMENT_PREVIEW: Logs/stigma-management-preview.png");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (target) UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
