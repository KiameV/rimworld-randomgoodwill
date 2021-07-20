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

        private Vector2 scroll = Vector2.zero;
        private float lastY;

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
            string minString = "min".Translate().CapitalizeFirst();
            string maxString = "max".Translate().CapitalizeFirst();
            CreateFactions();
            PopulateFactions();
            if (buffers == null || buffers.Length != Factions.Count * 2)
            {
                UpdateBuffers();
            }
            float half = rect.width * 0.5f;
            float width = half - 10;

            float y = rect.y + 10f;
            var r = new Rect(0, y, 200, 28);
            Widgets.Label(r, "RG.EnableFactionDynamicColors".Translate());
            Widgets.Checkbox(225, y - 2, ref dynamicColors);
            r.width += 50;
            if (Mouse.IsOver(r))
                Widgets.DrawHighlight(r);
            TooltipHandler.TipRegion(r, "RG.EnableFactionDynamicColorsTip".Translate());
            y += 32;

            Widgets.Label(new Rect(0, y, 200, 28), "RG.EnableFactionRandomGoodwill".Translate());
            Widgets.Checkbox(225, y - 2, ref randomGoodwill);


            if (randomGoodwill)
            {
                Widgets.Label(new Rect(half, rect.y, width, 28), "RG.GoodwillMinMax".Translate());
                Widgets.BeginScrollView(new Rect(half, rect.y + 34, width, rect.height - 10f), ref scroll, new Rect(0, 0, width, lastY));
                lastY = 0;
                int i = 0;
                foreach (var f in Factions.Values)
                {
                    if (f.Faction != null)
                    {
                        Widgets.Label(new Rect(0, lastY, 150, 28), f.Faction.LabelCap);
                        if (Widgets.ButtonText(new Rect(200, lastY, 100, 28), "RG.Default".Translate()))
                        {
                            var gw = GetInitialGoodwill(f.Faction);
                            f.Min = gw;
                            f.Max = gw;
                            buffers[i] = gw.ToString();
                            buffers[i + 1] = gw.ToString();
                        }
                        lastY += 32;
                        Widgets.Label(new Rect(10, lastY, 150, 28), minString);
                        Widgets.TextFieldNumeric(new Rect(200, lastY, 100, 28), ref f.Min, ref buffers[i], -100, 100);
                        lastY += 30;
                        Widgets.Label(new Rect(10, lastY, 150, 28), maxString);
                        Widgets.TextFieldNumeric(new Rect(200, lastY, 100, 28), ref f.Max, ref buffers[i + 1], -100, 100);
                        lastY += 38;
                        i += 2;
                        if (i < buffers.Length)
                        {
                            Widgets.DrawLineHorizontal(0, lastY, 300);
                            lastY += 8;
                        }
                    }
                }
                Widgets.EndScrollView();
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