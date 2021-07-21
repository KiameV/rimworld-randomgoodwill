using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using Verse;

namespace RandomGoodwill
{
    [StaticConstructorOnStartup]
    internal static class Main
    {
        internal static bool IsConfigMapsLoaded = false;
        static Main()
        {
            var harmony = new Harmony("com.rimworld.mod.factioncontrol");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            IsConfigMapsLoaded = ModLister.GetActiveModWithIdentifier("configurablemaps.kv.rw") != null;
        }
    }

    [HarmonyPatch(typeof(Page_CreateWorldParams), "DoWindowContents")]
    public static class Patch_Page_CreateWorldParams_DoWindowContents
    {
        static void Postfix(Rect rect)
        {
            float y = rect.y + rect.height - 78f;
            Text.Font = GameFont.Small;
            string label = "RandomGoodwill".Translate();
            float x = 0f;
            if (Main.IsConfigMapsLoaded)
                x += 170;
            if (Widgets.ButtonText(new Rect(x, y, 150, 32), label))
            {
                if (!Find.WindowStack.TryRemove(typeof(SettingsWindow)))
                {
                    Find.WindowStack.Add(new SettingsWindow());
                }
            }
        }
    }

    [HarmonyPatch(typeof(LoadedModManager), "GetSettingsFilename", null)]
    public static class LoadedModManager_GetSettingsFilename
    {
        private static void Prefix(string modIdentifier, string modHandleName, ref string __result)
        {
            if (modHandleName.Contains("Controller_ModdedFactions"))
            {
                modHandleName = "Controller_ModdedFactions";
            }
            __result = Path.Combine(GenFilePaths.ConfigFolderPath, GenText.SanitizeFilename(string.Format("Mod_{0}_{1}.xml", modIdentifier, modHandleName)));
        }
    }

    [HarmonyPatch(typeof(Faction))]
    [HarmonyPatch("Color", MethodType.Getter)]
    [HarmonyPriority(Priority.Last)]
    public static class Patch_Faction_get_Color
    {
        public static void Postfix(Faction __instance, ref Color __result)
        {
            if (!Settings.dynamicColors ||
                __instance.IsPlayer)
            {
                return;
            }

            if (__instance.HostileTo(Faction.OfPlayerSilentFail))
            {
                float goodwill = GoodWillToColor(__instance.GoodwillWith(Faction.OfPlayerSilentFail));
                __result = new Color(0.75f, goodwill, goodwill);
            }
        }

        private static float GoodWillToColor(int goodwill)
        {
            float v = Math.Abs(goodwill) * 0.01f;
            //if (v > .65f)
            //    v = .65f;
            if (v > 1f)
                v = 1f;
            else if (v < 0.35f)
                v = 0.35f;
            v = 1 - v;
            return v;
        }
    }

    [HarmonyPatch(typeof(WorldGenerator), "GenerateWorld")]
    public class WorldGenerator_Generate
    {
        public static void Postfix(World __result)
        {
            if (Settings.randomGoodwill && Settings.Factions.Count > 0)
            {
                foreach (var f in __result.factionManager.AllFactionsVisible)
                {
                    if (Settings.Factions.TryGetValue(f.def.defName, out FactionMinMax fmm))
                    {
                        FactionRelation fr = f.RelationWith(Faction.OfPlayer, true);
                        if (fr != null)
                        {
                            int min = fmm.Min, max = fmm.Max;
                            if (max < min)
                            {
                                Log.Warning($"[Random Goodwill] min is greater than max for {f.def.label} will flip the values.");
                                var i = min;
                                min = max;
                                max = i;
                            }
                            fr.baseGoodwill = Rand.RangeInclusive(min, max);
                            if (fr.baseGoodwill >= 0)
                            {
                                fr.kind = FactionRelationKind.Neutral;
                            }
                            else
                            {
                                fr.kind = FactionRelationKind.Hostile;
                            }
                        }
                    }
                }
            }
        }
    }

    /*
    [HarmonyPatch(typeof(Faction), "TryAffectGoodwillWith")]
    public static class Patch_Faction_TryAffectGoodwillWith
    {
        static bool Prefix(Faction __instance, ref bool __result, Faction other, int goodwillChange, bool canSendMessage, bool canSendHostilityLetter, string reason, GlobalTargetInfo? lookTarget)
        {
            if (!Controller.Settings.relationsChangeOverTime)
            {
                if (other == Faction.OfPlayer &&
                    goodwillChange < 0 &&
                    !canSendMessage && !canSendHostilityLetter && reason == null && lookTarget == null)
                {
                    __result = false;
                    return false;
                }
                if (reason != null && 
                    __instance.def.naturalColonyGoodwill != null &&
                    (reason.IndexOf("GoodwillChangedReason_NaturallyOverTime".Translate(__instance.def.naturalColonyGoodwill.min.ToString())) != -1 || 
                     reason.IndexOf("GoodwillChangedReason_NaturallyOverTime".Translate(__instance.def.naturalColonyGoodwill.max.ToString())) != -1))
                {

                    __result = false;
                    return false;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(LetterStack), "ReceiveLetter", typeof(string), typeof(string), typeof(LetterDef), typeof(string))]
    public static class Patch_LetterStack_ReceiveLetter
    {
        static bool Prefix(string label, string text, LetterDef textLetterDef, string debugInfo)
        {
            if (!Controller.Settings.relationsChangeOverTime && 
                textLetterDef == LetterDefOf.NegativeEvent && 
                label == "LetterLabelFactionBaseProximity".Translate())
            {
                return false;
            }
            return true;
        }
    }
    */
}
