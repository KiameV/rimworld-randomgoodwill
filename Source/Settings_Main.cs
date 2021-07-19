using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RandomGoodwill
{
    public class Controller : Mod
    {
        public Controller(ModContentPack content) : base(content)
        {
            base.GetSettings<Settings>();
        }

        public override string SettingsCategory()
        {
            return "RandomGoodwill".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.GetSettings<Settings>().DoWindowContents(inRect);
        }
    }

    public class FactionMinMax : IExposable
    {
        public FactionDef Faction;
        public string defName;
        public int Min;
        public int Max;

        public void ExposeData()
        {
            Scribe_Values.Look(ref defName, "defName");
            Scribe_Values.Look(ref Min, "min");
            Scribe_Values.Look(ref Max, "max");
        }
    }

    public class Settings : ModSettings
    {
        public static bool randomGoodwill = true;
        public static bool dynamicColors = true;

        public static Dictionary<string, FactionMinMax> Factions = new Dictionary<string, FactionMinMax>();
        public static bool createdFactions = false, populatedFactions = false;

        struct Buffers
        {
            public string Min, Max;
            public Buffers(string min, string max)
            {
                this.Min = min;
                this.Max = max;
            }
        }
        private string[] buffers;

        public void DoWindowContents(Rect rect)
        {
            string min = "min".Translate().CapitalizeFirst();
            string max = "max".Translate().CapitalizeFirst();
            CreateFactions();
            PopulateFactions();
            if (buffers == null || buffers.Length != Factions.Count * 2)
            {
                UpdateBuffers();
            }
            float half = rect.width * 0.5f;
            float width = 250f;

            Listing_Standard list = new Listing_Standard { ColumnWidth = width};
            list.Begin(new Rect(rect.x, rect.y, width, rect.height));
            list.Gap(24);
            list.CheckboxLabeled("RG.EnableFactionDynamicColors".Translate(), ref dynamicColors, "RFC.EnableFactionDynamicColorsTip".Translate());
            list.Gap(24);
            list.CheckboxLabeled("RG.EnableFactionRandomGoodwill".Translate(), ref randomGoodwill, "RFC.EnableFactionRandomGoodwillToolTip".Translate());
            list.End();

            if (randomGoodwill)
            {
                Widgets.Label(new Rect(half, rect.y, width, 28), "RG.GoodwillMinMax".Translate());
                list = new Listing_Standard { ColumnWidth = width };
                list.Begin(new Rect(half, rect.y + 34, width, rect.height));
                int i = 0;
                foreach (var f in Factions.Values)
                {
                    if (f.Faction != null)
                    {
                        list.Label(f.Faction.LabelCap);
                        list.TextFieldNumericLabeled(min, ref f.Min, ref buffers[i], -100, 100);
                        list.TextFieldNumericLabeled(max, ref f.Max, ref buffers[i + 1], -100, 100);
                        if (list.ButtonText("RG.Default".Translate()))
                        {
                            var gw = GetInitialGoodwill(f.Faction);
                            f.Min = gw;
                            f.Max = gw;
                            buffers[i] = gw.ToString();
                            buffers[i + 1] = gw.ToString();
                        }
                        list.GapLine(6);
                        i += 2;
                    }
                }
                list.End();
            }
        }

        private List<FactionMinMax> fmm = null;

        public override void ExposeData()
        {
            CreateFactions();

            if (fmm == null)
                fmm = new List<FactionMinMax>(Factions.Count);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                foreach (var kv in Factions)
                {
                    kv.Value.defName = kv.Key;
                    fmm.Add(kv.Value);
                }
            }

            base.ExposeData();
            Scribe_Values.Look(ref randomGoodwill, "RandomGoodwill", true);
            Scribe_Values.Look(ref dynamicColors, "DynamicColors", true);

            if (fmm != null)
            {
                Scribe_Collections.Look(ref fmm, "factionGoodwill");
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                foreach (var f in fmm)
                    Factions[f.defName] = f;
            }

            if (Scribe.mode == LoadSaveMode.Saving || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                fmm?.Clear();
                fmm = null;

                UpdateBuffers();
            }
        }

        private void UpdateBuffers()
        {
            if (buffers == null || buffers.Length != Factions.Count * 2)
                buffers = new string[Factions.Count * 2];
            int i = 0;
            foreach (var f in Factions.Values)
            {
                buffers[i] = f.Min.ToString();
                buffers[i + 1] = f.Max.ToString();
                i += 2;
            }
        }

        private void CreateFactions()
        {
            if (Factions == null)
                Factions = new Dictionary<string, FactionMinMax>();

            if (Factions.Count > 0 && createdFactions)
                return;

            foreach (var f in DefDatabase<FactionDef>.AllDefsListForReading)
            {
                if (f != null && !Factions.ContainsKey(f.defName))
                {
                    if (f == FactionDefOf.PlayerColony || f == FactionDefOf.PlayerTribe)
                        continue;

                    if (!f.hidden && !f.permanentEnemy)
                    {
                        var gw = GetInitialGoodwill(f);
                        Factions.Add(f.defName, new FactionMinMax()
                        {
                            Min = gw,
                            Max = gw
                        });
                    }
                }
            }
            createdFactions = Factions.Count > 0;
            if (createdFactions)
                UpdateBuffers();
        }

        private void PopulateFactions()
        {
            if (!populatedFactions)
            {
                foreach (var f in Factions.Values)
                {
                    if (f.defName != null && f.defName != "")
                    {
                        var def = DefDatabase<FactionDef>.GetNamed(f.defName, false);
                        if (def != null)
                        {
                            f.Faction = def;
                        }
                        else
                        {
                            Log.Warning($"[Random Goodwill] failed to load faction {f.defName}");
                        }
                    }
                }
                populatedFactions = true;
            }
        }

        private static int GetInitialGoodwill(FactionDef a)
        {
            if (a.permanentEnemy)
            {
                return -100;
            }
            if (a.permanentEnemyToEveryoneExcept != null && !(a.permanentEnemyToEveryoneExcept.Contains(FactionDefOf.PlayerTribe) || a.permanentEnemyToEveryoneExcept.Contains(FactionDefOf.PlayerColony)))
            {
                return -100;
            }
            if (a.naturalEnemy)
            {
                return -80;
            }
            return 0;
        }
    }
}