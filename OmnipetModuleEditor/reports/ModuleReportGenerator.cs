using OmnipetModuleEditor.Models;
using OmnipetModuleEditor.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace OmnipetModuleEditor.Reports
{
    /// <summary>
    /// Generates a comprehensive validation report for a module.
    /// Each section collects issues in a single pass over the data.
    /// </summary>
    public class ModuleReportGenerator
    {
        private readonly string modulePath;
        private readonly Module module;

        // Data loaded once
        private List<Pet> pets;
        private List<BattleEnemy> enemies;
        private List<Item> items;
        private List<Quest> quests;
        private List<Event> events;

        // Lookup sets built once
        private HashSet<string> unlockNames;
        private HashSet<int> battleAreas;
        private Dictionary<string, List<Pet>> petsByName;
        private HashSet<int> petVersions;
        // enemy lookup: "area|round|version" -> enemy list
        private Dictionary<string, List<BattleEnemy>> enemyByAreaRoundVersion;

        public ModuleReportGenerator(string modulePath, Module module)
        {
            this.modulePath = modulePath;
            this.module = module;
        }

        /// <summary>
        /// Runs all checks and returns the full report text.
        /// </summary>
        public string Generate()
        {
            LoadAllData();
            BuildLookups();

            var allSections = new List<(string Title, List<ReportEntry> Entries)>
            {
                ("Module", CheckModule()),
                ("Unlocks", CheckUnlocks()),
                ("Backgrounds", CheckBackgrounds()),
                ("Pets", CheckPets()),
                ("Battle", CheckBattle()),
                ("Items", CheckItems()),
                ("Quests/Events", CheckQuestsEvents())
            };

            var sb = new StringBuilder();

            foreach (var section in allSections)
            {
                if (section.Entries != null && section.Entries.Count > 0)
                    AppendSection(sb, section.Title, section.Entries);
            }

            sb.AppendLine();
            sb.AppendLine("==========================================================");
            sb.AppendLine("  Report Complete");
            sb.AppendLine("==========================================================");

            return sb.ToString();
        }

        #region Data Loading

        private void LoadAllData()
        {
            pets = LoadJson<Pet>("monster.json", "monster");
            enemies = LoadJson<BattleEnemy>("battle.json", "enemies");
            items = LoadJson<Item>("item.json", "item");
            quests = LoadJson<Quest>("quests.json", "quests");
            events = LoadJson<Event>("events.json", "events");
        }

        private List<T> LoadJson<T>(string fileName, string rootProperty)
        {
            string path = Path.Combine(modulePath, fileName);
            if (!File.Exists(path))
                return new List<T>();
            try
            {
                string json = File.ReadAllText(path);
                using (var doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty(rootProperty, out var element))
                        return JsonSerializer.Deserialize<List<T>>(element.GetRawText()) ?? new List<T>();
                }
            }
            catch { }
            return new List<T>();
        }

        private void BuildLookups()
        {
            // Unlock names set
            unlockNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (module?.Unlocks != null)
            {
                foreach (var u in module.Unlocks)
                {
                    if (!string.IsNullOrWhiteSpace(u.Name))
                        unlockNames.Add(u.Name);
                }
            }

            // Battle areas
            battleAreas = new HashSet<int>();
            enemyByAreaRoundVersion = new Dictionary<string, List<BattleEnemy>>(StringComparer.OrdinalIgnoreCase);
            if (enemies != null)
            {
                foreach (var e in enemies)
                {
                    battleAreas.Add(e.Area);
                    string key = $"{e.Area}|{e.Round}|{e.Version}";
                    if (!enemyByAreaRoundVersion.ContainsKey(key))
                        enemyByAreaRoundVersion[key] = new List<BattleEnemy>();
                    enemyByAreaRoundVersion[key].Add(e);
                }
            }

            // Pet lookups
            petsByName = new Dictionary<string, List<Pet>>(StringComparer.OrdinalIgnoreCase);
            petVersions = new HashSet<int>();
            if (pets != null)
            {
                foreach (var p in pets)
                {
                    petVersions.Add(p.Version);
                    if (!string.IsNullOrWhiteSpace(p.Name))
                    {
                        if (!petsByName.ContainsKey(p.Name))
                            petsByName[p.Name] = new List<Pet>();
                        petsByName[p.Name].Add(p);
                    }
                }
            }
        }

        #endregion

        #region Section Formatting

        private void AppendSection(StringBuilder sb, string title, List<ReportEntry> entries)
        {
            sb.AppendLine("----------------------------------------------------------");
            sb.AppendLine($"  {title}");
            sb.AppendLine("----------------------------------------------------------");

            // Group by level for ordering: Error first, Warning, Info
            var ordered = entries
                .OrderBy(e => e.Level == ReportLevel.Error ? 0 : e.Level == ReportLevel.Warning ? 1 : 2)
                .ToList();

            foreach (var entry in ordered)
            {
                string prefix = $"[{entry.Level}]";
                if (entry.Items != null && entry.Items.Count > 0)
                {
                    sb.AppendLine($"{prefix} {entry.Items.Count} {entry.Message}");
                    foreach (var item in entry.Items)
                        sb.AppendLine($"\t- {item}");
                }
                else
                {
                    sb.AppendLine($"{prefix} {entry.Message}");
                }
            }
            sb.AppendLine();
        }

        #endregion

        #region Module Checks

        private List<ReportEntry> CheckModule()
        {
            var results = new List<ReportEntry>();
            if (module == null) return results;

            // [Error]
            if (string.IsNullOrWhiteSpace(module.Name))
                results.Add(ReportEntry.Error("Module has no name"));

            if (!HasModuleSprite("Flag.png"))
                results.Add(ReportEntry.Error("Module has no flag sprite"));

            if (module.AdventureMode && !HasModuleSprite("BattleIcon.png"))
                results.Add(ReportEntry.Error("Module has Adventure Mode set, but no battle icon"));

            if (module.AdventureMode && (enemies == null || enemies.Count == 0))
                results.Add(ReportEntry.Error("Module has Adventure Mode set but no data in the Battle tab"));

            // [Warning]
            if (string.IsNullOrWhiteSpace(module.VisibleStats))
                results.Add(ReportEntry.Warning("Module has no visible stats"));

            if (string.IsNullOrWhiteSpace(module.Version))
                results.Add(ReportEntry.Warning("Module has no version set"));

            if (string.IsNullOrWhiteSpace(module.Author))
                results.Add(ReportEntry.Warning("Module has no author set"));

            if (string.IsNullOrWhiteSpace(module.Description))
                results.Add(ReportEntry.Warning("Module has no description set"));

            if (module.UseGCells && !string.IsNullOrWhiteSpace(module.VisibleStats)
                && !module.VisibleStats.Contains("gcell"))
                results.Add(ReportEntry.Warning("Module uses G-Cells, but G-Cells not in visible stats"));

            if ((module.VitalValueBase > 0 || module.VitalValueLoss > 0)
                && !string.IsNullOrWhiteSpace(module.VisibleStats)
                && !module.VisibleStats.Contains("vital"))
                results.Add(ReportEntry.Warning("Module has Vital Value base/loss set but Vital Values not in visible stats"));

            if (module.TrainingEffortGain == 0)
                results.Add(ReportEntry.Warning("Module has no value for Training Effort Gain"));

            if (!string.IsNullOrWhiteSpace(module.BattleCostType)
                && !module.BattleCostType.Equals("Nothing", StringComparison.OrdinalIgnoreCase)
                && module.BattleCostAmount <= 0)
                results.Add(ReportEntry.Warning("Module has a Battle Cost Type set (not Nothing), but no Battle Cost Amount"));

            // [Info]
            if (string.IsNullOrWhiteSpace(module.BattleMinigame) || module.BattleMinigame == "None")
                results.Add(ReportEntry.Info("Module has no Battle Minigame"));

            if (string.IsNullOrWhiteSpace(module.BattleProtocol) || module.BattleProtocol == "None")
                results.Add(ReportEntry.Info("Module has no Battle Protocol"));

            if (module.BattleEnableFeeding && !module.BattleSequentialRounds)
                results.Add(ReportEntry.Info("Module has Battle Enable Feeding as true, but not Battle Sequential Rounds"));

            if (string.Equals(module.Ruleset, "dmx", StringComparison.OrdinalIgnoreCase) && module.TraitedEggStartingLevel == 0)
                results.Add(ReportEntry.Info("Module has ruleset DMX but no Traited Egg Starting Level (==0)"));

            if (module.BattleAtributeAdvantage == 0)
                results.Add(ReportEntry.Info("Module has no value for Battle Attribute Advantage"));

            return results;
        }

        #endregion

        #region Unlock Checks

        private List<ReportEntry> CheckUnlocks()
        {
            var results = new List<ReportEntry>();
            if (module?.Unlocks == null || module.Unlocks.Count == 0)
                return results;

            var errorAdventureMissingArea = new List<string>();
            var errorEggMissingVersion = new List<string>();
            var warnAdventureAreaNotInBattle = new List<string>();
            var warnEggNoStage0Pet = new List<string>();
            var warnMissingLabel = new List<string>();

            foreach (var u in module.Unlocks)
            {
                string display = !string.IsNullOrWhiteSpace(u.Name) ? u.Name : "(unnamed)";

                // [Error]
                if (string.Equals(u.Type, "adventure", StringComparison.OrdinalIgnoreCase))
                {
                    if (!u.Area.HasValue || u.Area.Value == 0)
                        errorAdventureMissingArea.Add(display);
                }

                if (string.Equals(u.Type, "egg", StringComparison.OrdinalIgnoreCase))
                {
                    if (!u.Version.HasValue)
                        errorEggMissingVersion.Add(display);
                }

                // [Warning]
                if (string.Equals(u.Type, "adventure", StringComparison.OrdinalIgnoreCase)
                    && u.Area.HasValue && u.Area.Value > 0
                    && !battleAreas.Contains(u.Area.Value))
                    warnAdventureAreaNotInBattle.Add($"{display} (area {u.Area.Value})");

                if (string.Equals(u.Type, "egg", StringComparison.OrdinalIgnoreCase)
                    && u.Version.HasValue)
                {
                    bool hasStage0 = pets != null && pets.Any(p => p.Stage == 0 && p.Version == u.Version.Value);
                    if (!hasStage0)
                        warnEggNoStage0Pet.Add($"{display} (ver {u.Version.Value})");
                }

                if (string.IsNullOrWhiteSpace(u.Label))
                    warnMissingLabel.Add(display);
            }

            if (errorAdventureMissingArea.Count > 0)
                results.Add(ReportEntry.Error("Unlock(s) of type adventure but no area value", errorAdventureMissingArea));
            if (errorEggMissingVersion.Count > 0)
                results.Add(ReportEntry.Error("Unlock(s) of type egg, but no version set", errorEggMissingVersion));
            if (warnAdventureAreaNotInBattle.Count > 0)
                results.Add(ReportEntry.Warning("Unlock(s) of type adventure but the set area doesn't exist in Battle data", warnAdventureAreaNotInBattle));
            if (warnEggNoStage0Pet.Count > 0)
                results.Add(ReportEntry.Warning("Unlock(s) of type egg, but there's no stage 0 pet with that version", warnEggNoStage0Pet));
            if (warnMissingLabel.Count > 0)
                results.Add(ReportEntry.Warning("Unlock(s) with no label defined", warnMissingLabel));

            return results;
        }

        #endregion

        #region Background Checks

        private List<ReportEntry> CheckBackgrounds()
        {
            var results = new List<ReportEntry>();
            if (module?.Backgrounds == null || module.Backgrounds.Count == 0)
                return results;

            var errorMissingSprites = new List<string>();
            var warnNameNotInUnlocks = new List<string>();
            var warnMissingLabel = new List<string>();
            var infoMissingHiRes = new List<string>();

            foreach (var bg in module.Backgrounds)
            {
                string display = !string.IsNullOrWhiteSpace(bg.Label) ? bg.Label
                    : !string.IsNullOrWhiteSpace(bg.Name) ? bg.Name : "(unnamed)";

                if (!BackgroundHasAnySprite(bg))
                    errorMissingSprites.Add(display);

                if (!string.IsNullOrWhiteSpace(bg.Name) && !unlockNames.Contains(bg.Name))
                    warnNameNotInUnlocks.Add(display);

                if (string.IsNullOrWhiteSpace(bg.Label))
                    warnMissingLabel.Add(!string.IsNullOrWhiteSpace(bg.Name) ? bg.Name : "(unnamed)");

                if (!BackgroundHasHiRes(bg))
                    infoMissingHiRes.Add(display);
            }

            if (errorMissingSprites.Count > 0)
                results.Add(ReportEntry.Error("Background(s) without valid sprites", errorMissingSprites));
            if (warnNameNotInUnlocks.Count > 0)
                results.Add(ReportEntry.Warning("Background(s) with name that doesn't exist in the unlocks", warnNameNotInUnlocks));
            if (warnMissingLabel.Count > 0)
                results.Add(ReportEntry.Warning("Background(s) with no label defined", warnMissingLabel));
            if (infoMissingHiRes.Count > 0)
                results.Add(ReportEntry.Info("Background(s) without HiRes version", infoMissingHiRes));

            return results;
        }

        #endregion

        #region Pet Checks

        private List<ReportEntry> CheckPets()
        {
            var results = new List<ReportEntry>();
            if (pets == null || pets.Count == 0)
                return results;

            bool isDmx = string.Equals(module?.Ruleset, "dmx", StringComparison.OrdinalIgnoreCase);
            bool useConditionHearts = module?.CareConditionHeart ?? false;
            bool enableSpecialAtk = module?.EnableSpecialAttackSprite ?? false;
            int globalHp = module?.BattleGlobalHitPoints ?? 0;
            string primaryFormat = module?.PrimarySpriteFormat ?? "Color";

            // Error lists
            var errNoAtkAlt2Dmx = new List<string>();
            var errNoAtkMain = new List<string>();
            var errNoName = new List<string>();
            var errSpecialNoKey = new List<string>();
            var errSpecialKeyNotInUnlocks = new List<string>();
            var errConditionHeartsZero = new List<string>();
            var errNoSpriteAtAll = new List<string>();
            var errNoHpAndNoGlobal = new List<string>();
            var errMultiEvolNoReq = new List<string>();
            var errStage0NoEvol = new List<string>();
            var errEvolvesToWrongVersion = new List<string>();

            // Warning lists
            var warnNoAtkAlt = new List<string>();
            var warnNoIndex = new List<string>();
            var warnSpecialFrameMissing = new List<string>();
            var warnMissingFrames = new List<string>();
            var warnMissingPrimaryStyle = new List<string>();
            var warnMinWeightHigh = new List<string>();
            var warnOrphanPet = new List<string>();
            var warnFirstEvolNoReq = new List<string>();
            var warnEvolvesToLesserStage = new List<string>();

            // Info lists
            var infoHighStageZeroPower = new List<string>();
            var infoEvolvesToSameStage = new List<string>();
            var infoPowerOver300 = new List<string>();

            // Build set of all pet names across all versions for evolution target checks
            var allPetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Build set for "name|version" duplicate check
            var nameVersionSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicateNameVersionFound = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Build set of all evolution targets and sources for orphan check
            var evolveTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var evolveSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var p in pets)
            {
                if (!string.IsNullOrWhiteSpace(p.Name))
                    allPetNames.Add(p.Name);
            }

            // First pass: collect evolution targets/sources and check duplicates
            foreach (var p in pets)
            {
                string name = p.Name ?? "";
                string key = $"{name}|{p.Version}";
                if (!nameVersionSet.Add(key) && !string.IsNullOrWhiteSpace(name))
                    duplicateNameVersionFound.Add(key);

                if (p.Evolve != null)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                        evolveSources.Add(name);
                    foreach (var evo in p.Evolve)
                    {
                        if (!string.IsNullOrWhiteSpace(evo.To))
                            evolveTargets.Add(evo.To);
                    }
                }
            }

            if (duplicateNameVersionFound.Count > 0)
            {
                var displayItems = duplicateNameVersionFound.Select(k =>
                {
                    var parts = k.Split('|');
                    return $"{parts[0]} (version {parts[1]})";
                }).ToList();
                results.Add(ReportEntry.Error("Pet(s) with same name and version (duplicates)", displayItems));
            }

            // Main single pass over all pets
            foreach (var p in pets)
            {
                string name = p.Name ?? "";
                string display = !string.IsNullOrWhiteSpace(name) ? name : "(unnamed)";
                bool hasPower = p.Power > 0;

                // [Error] no name
                if (string.IsNullOrWhiteSpace(name))
                    errNoName.Add($"(stage {p.Stage}, ver {p.Version})");

                // [Error] DMX no atk_alt_2
                if (isDmx && hasPower && p.AtkAlt2 == 0)
                    errNoAtkAlt2Dmx.Add(display);

                // [Error] power > 0 no atk_main
                if (hasPower && p.AtkMain == 0)
                    errNoAtkMain.Add(display);

                // [Error] Special but no key
                if (p.Special && string.IsNullOrWhiteSpace(p.SpecialKey))
                    errSpecialNoKey.Add(display);

                // [Error] Special key not in unlocks
                if (!string.IsNullOrWhiteSpace(p.SpecialKey) && !unlockNames.Contains(p.SpecialKey))
                    errSpecialKeyNotInUnlocks.Add($"{display} (key: {p.SpecialKey})");

                // [Error] condition hearts = 0 when module uses condition hearts (stage > 0)
                if (useConditionHearts && p.Stage > 0 && p.ConditionHearts == 0)
                    errConditionHeartsZero.Add(display);

                // [Error] no sprite at all
                if (!string.IsNullOrWhiteSpace(name) && !PetHasAnySpriteAtAll(name))
                    errNoSpriteAtAll.Add(display);

                // [Error] no global HP and pet HP = 0 (for power > 0)
                if (hasPower && globalHp == 0 && p.Hp == 0)
                    errNoHpAndNoGlobal.Add(display);

                // [Error] 2+ evolutions without requirements
                if (p.Evolve != null && p.Evolve.Count >= 2)
                {
                    int noReqCount = p.Evolve.Count(ev => !EvolutionHasRequirements(ev));
                    if (noReqCount >= 2)
                        errMultiEvolNoReq.Add(display);
                }

                // [Error] stage 0 without evolutions
                if (p.Stage == 0 && (p.Evolve == null || p.Evolve.Count == 0))
                    errStage0NoEvol.Add(display);

                // [Error] evolves to pet that only exists on different version or doesn't exist
                if (p.Evolve != null)
                {
                    foreach (var evo in p.Evolve)
                    {
                        if (!string.IsNullOrWhiteSpace(evo.To))
                        {
                            if (!allPetNames.Contains(evo.To))
                            {
                                errEvolvesToWrongVersion.Add($"{display} -> {evo.To} (not found)");
                            }
                            else if (petsByName.ContainsKey(evo.To))
                            {
                                var targets = petsByName[evo.To];
                                bool compatible = targets.Any(t =>
                                    t.Version == 0 || t.Version == p.Version || p.Version == 0);
                                if (!compatible)
                                    errEvolvesToWrongVersion.Add($"{display} (ver {p.Version}) -> {evo.To} (only on ver {string.Join(",", targets.Select(t => t.Version))})");
                            }
                        }
                    }
                }

                // [Warning] no atk_alt (power > 0)
                if (hasPower && p.AtkAlt == 0)
                    warnNoAtkAlt.Add(display);

                // [Warning] no index
                if (!p.Index.HasValue && p.Stage > 0)
                    warnNoIndex.Add(display);

                // [Warning] special frame missing (enable special atk sprite is true)
                if (enableSpecialAtk && hasPower && !string.IsNullOrWhiteSpace(name))
                {
                    if (!PetHasSpriteFrame(name, 15))
                        warnSpecialFrameMissing.Add(display);
                }

                // [Warning] missing some frames (has some but not all, ignore frame 15, eggs only have 3 frames)
                if (!string.IsNullOrWhiteSpace(name))
                {
                    int expectedFrames = p.Stage == 0 ? 3 : 15;
                    var loadedSprites = SpriteUtils.LoadSprites(name, modulePath,
                        PetUtils.FixedNameFormat, expectedFrames,
                        module?.PrimarySpriteFormat ?? "Color",
                        module?.SecondarySpriteFormat ?? "HD");
                    if (loadedSprites.HasSprites)
                    {
                        int missingCount = 0;
                        for (int i = 0; i < expectedFrames; i++)
                        {
                            if (!loadedSprites.Sprites.ContainsKey(i.ToString()))
                                missingCount++;
                        }
                        if (missingCount > 0 && missingCount < expectedFrames)
                            warnMissingFrames.Add($"{display} (missing {missingCount}/{expectedFrames} frames)");
                    }
                }

                // [Warning] missing sprite for primary style
                if (!string.IsNullOrWhiteSpace(name))
                {
                    string primaryFolder = SpriteUtils.GetFolderForFormat(primaryFormat);
                    if (!PetHasSpriteInFolder(name, primaryFolder))
                        warnMissingPrimaryStyle.Add(display);
                }

                // [Warning] min_weight > 90
                if (p.MinWeight > 90)
                    warnMinWeightHigh.Add($"{display} (min_weight: {p.MinWeight})");

                // [Warning] orphan pet
                if (!string.IsNullOrWhiteSpace(name) && p.Stage > 0)
                {
                    bool isTarget = evolveTargets.Contains(name);
                    bool isSource = p.Evolve != null && p.Evolve.Count > 0;
                    if (!isTarget && !isSource)
                        warnOrphanPet.Add(display);
                }

                // [Warning] first evolution without requirements (only if pet has > 1 evolution)
                if (p.Evolve != null && p.Evolve.Count > 1 && p.Stage > 0)
                {
                    if (!EvolutionHasRequirements(p.Evolve[0]))
                        warnFirstEvolNoReq.Add(display);
                }

                // [Warning] evolves to pet of lesser stage
                if (p.Evolve != null)
                {
                    foreach (var evo in p.Evolve)
                    {
                        if (!string.IsNullOrWhiteSpace(evo.To) && petsByName.ContainsKey(evo.To))
                        {
                            var targets = petsByName[evo.To];
                            foreach (var target in targets)
                            {
                                if (target.Stage < p.Stage)
                                {
                                    warnEvolvesToLesserStage.Add($"{display} (stage {p.Stage}) -> {evo.To} (stage {target.Stage})");
                                    break;
                                }
                            }
                        }
                    }
                }

                // [Info] stage > 2 but 0 power
                if (p.Stage > 2 && p.Power == 0)
                    infoHighStageZeroPower.Add(display);

                // [Info] evolves to same stage
                if (p.Evolve != null)
                {
                    foreach (var evo in p.Evolve)
                    {
                        if (!string.IsNullOrWhiteSpace(evo.To) && petsByName.ContainsKey(evo.To))
                        {
                            if (petsByName[evo.To].Any(t => t.Stage == p.Stage))
                            {
                                infoEvolvesToSameStage.Add($"{display} -> {evo.To} (same stage {p.Stage})");
                                break;
                            }
                        }
                    }
                }

                // [Info] power > 300
                if (p.Power > 300)
                    infoPowerOver300.Add($"{display} (power: {p.Power})");
            }

            // Add collected errors
            if (errNoName.Count > 0)
                results.Add(ReportEntry.Error("Pet(s) with no name defined", errNoName));
            if (errNoAtkAlt2Dmx.Count > 0)
                results.Add(ReportEntry.Error("Pet(s) with ruleset DMX but no atk_alt_2 value (power > 0)", errNoAtkAlt2Dmx));
            if (errNoAtkMain.Count > 0)
                results.Add(ReportEntry.Error("Pet(s) with power > 0 and no atk_main sprite defined", errNoAtkMain));
            if (errSpecialNoKey.Count > 0)
                results.Add(ReportEntry.Error("Pet(s) set as Special, but has no Special Key", errSpecialNoKey));
            if (errSpecialKeyNotInUnlocks.Count > 0)
                results.Add(ReportEntry.Error("Pet(s) with Special Key not present in the module's unlocks", errSpecialKeyNotInUnlocks));
            if (errConditionHeartsZero.Count > 0)
                results.Add(ReportEntry.Error("Pet(s) with module set as Use Condition Hearts, but 0 condition hearts (stage > 0)", errConditionHeartsZero));
            if (errNoSpriteAtAll.Count > 0)
                results.Add(ReportEntry.Error("Pet(s) with no sprite at all (primary or secondary)", errNoSpriteAtAll));
            if (errNoHpAndNoGlobal.Count > 0)
                results.Add(ReportEntry.Error("Pet(s) with no global HP value set and pet HP is 0 (power > 0)", errNoHpAndNoGlobal));
            if (errMultiEvolNoReq.Count > 0)
                results.Add(ReportEntry.Error("Pet(s) with 2 or more evolutions without requirements", errMultiEvolNoReq));
            if (errStage0NoEvol.Count > 0)
                results.Add(ReportEntry.Error("Stage 0 pet(s) without evolutions", errStage0NoEvol));
            if (errEvolvesToWrongVersion.Count > 0)
                results.Add(ReportEntry.Error("Pet(s) that evolve to a pet that only exists on a different version or doesn't exist", errEvolvesToWrongVersion));

            if (warnNoAtkAlt.Count > 0)
                results.Add(ReportEntry.Warning("Pet(s) with no atk_alt sprite defined (power > 0)", warnNoAtkAlt));
            if (warnNoIndex.Count > 0)
                results.Add(ReportEntry.Warning("Pet(s) with no index value defined", warnNoIndex));
            if (warnSpecialFrameMissing.Count > 0)
                results.Add(ReportEntry.Warning("Pet(s) missing special frame (15) while Enable Special Attack Sprite is true", warnSpecialFrameMissing));
            if (warnMissingFrames.Count > 0)
                results.Add(ReportEntry.Warning("Pet(s) missing sprites (has some but not all frames)", warnMissingFrames));
            if (warnMissingPrimaryStyle.Count > 0)
                results.Add(ReportEntry.Warning("Pet(s) missing sprite for the module's primary style", warnMissingPrimaryStyle));
            if (warnMinWeightHigh.Count > 0)
                results.Add(ReportEntry.Warning("Pet(s) with min_weight > 90", warnMinWeightHigh));
            if (warnOrphanPet.Count > 0)
                results.Add(ReportEntry.Warning("Pet(s) with no other pet that evolves to it or that it evolves to", warnOrphanPet));
            if (warnFirstEvolNoReq.Count > 0)
                results.Add(ReportEntry.Warning("Pet(s) with first evolution without requirements", warnFirstEvolNoReq));
            if (warnEvolvesToLesserStage.Count > 0)
                results.Add(ReportEntry.Warning("Pet(s) that evolve to a pet of lesser stage", warnEvolvesToLesserStage));

            if (infoHighStageZeroPower.Count > 0)
                results.Add(ReportEntry.Info("Pet(s) with stage > 2 but 0 power", infoHighStageZeroPower));
            if (infoEvolvesToSameStage.Count > 0)
                results.Add(ReportEntry.Info("Pet(s) that evolve to another of the same stage", infoEvolvesToSameStage));
            if (infoPowerOver300.Count > 0)
                results.Add(ReportEntry.Info("Pet(s) with power > 300", infoPowerOver300));
            if (pets.Count > 400)
                results.Add(ReportEntry.Info($"Module has more than 400 pets ({pets.Count})"));

            return results;
        }

        #endregion

        #region Battle Checks

        private List<ReportEntry> CheckBattle()
        {
            var results = new List<ReportEntry>();
            if (enemies == null || enemies.Count == 0)
                return results;

            bool isDmx = string.Equals(module?.Ruleset, "dmx", StringComparison.OrdinalIgnoreCase);
            bool enableSpecialAtk = module?.EnableSpecialAttackSprite ?? false;
            int globalHp = module?.BattleGlobalHitPoints ?? 0;
            string primaryFormat = module?.PrimarySpriteFormat ?? "Color";

            // Error lists
            var errPowerZero = new List<string>();
            var errStageZero = new List<string>();
            var errNoName = new List<string>();
            var errNoSprite = new List<string>();
            var errNoHpNoGlobal = new List<string>();
            var errDmxNoAtkAlt2 = new List<string>();
            var errNoAtkMain = new List<string>();

            // Warning lists
            var warnMissingPrimary = new List<string>();
            var warnMissingFrames = new List<string>();
            var warnSpecialFrameMissing = new List<string>();
            var warnInaccessibleRound = new List<string>();
            var warnUnlockNotInUnlocks = new List<string>();
            var warnPowerOver250 = new List<string>();

            // Info lists
            var infoVersionNoPet = new List<string>();

            // Build lookup: for each (area, version), what rounds exist?
            var roundsByAreaVersion = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in enemies)
            {
                string avKey = $"{e.Area}|{e.Version}";
                if (!roundsByAreaVersion.ContainsKey(avKey))
                    roundsByAreaVersion[avKey] = new HashSet<int>();
                roundsByAreaVersion[avKey].Add(e.Round);
            }

            // Track versions seen for Info check
            var enemyVersions = new HashSet<int>();

            foreach (var e in enemies)
            {
                string name = e.Name ?? "";
                string display = !string.IsNullOrWhiteSpace(name) ? $"{name} (A{e.Area}R{e.Round}v{e.Version})" : $"(unnamed) (A{e.Area}R{e.Round}v{e.Version})";

                enemyVersions.Add(e.Version);

                // [Error] power == 0
                if (e.Power == 0)
                    errPowerZero.Add(display);

                // [Error] stage 0
                if (e.Stage == 0)
                    errStageZero.Add(display);

                // [Error] no name
                if (string.IsNullOrWhiteSpace(name))
                    errNoName.Add($"(A{e.Area}R{e.Round}v{e.Version})");

                // [Error] no sprite at all
                if (!string.IsNullOrWhiteSpace(name) && !PetHasAnySpriteAtAll(name))
                    errNoSprite.Add(display);

                // [Error] no global HP and enemy HP = 0
                if (globalHp == 0 && e.Hp == 0)
                    errNoHpNoGlobal.Add(display);

                // [Error] DMX no atk_alt_2
                if (isDmx && e.AtkAlt2 == 0)
                    errDmxNoAtkAlt2.Add(display);

                // [Error] no atk_main
                if (e.AtkMain == 0)
                    errNoAtkMain.Add(display);

                // [Warning] missing primary style sprite
                if (!string.IsNullOrWhiteSpace(name))
                {
                    string primaryFolder = SpriteUtils.GetFolderForFormat(primaryFormat);
                    if (!PetHasSpriteInFolder(name, primaryFolder))
                        warnMissingPrimary.Add(display);
                }

                // [Warning] missing frames (has some but not all, ignore frame 15)
                if (!string.IsNullOrWhiteSpace(name))
                {
                    int expectedFrames = 15;
                    var loadedSprites = SpriteUtils.LoadSprites(name, modulePath,
                        PetUtils.FixedNameFormat, expectedFrames,
                        module?.PrimarySpriteFormat ?? "Color",
                        module?.SecondarySpriteFormat ?? "HD");
                    if (loadedSprites.HasSprites)
                    {
                        int missingCount = 0;
                        for (int i = 0; i < expectedFrames; i++)
                        {
                            if (!loadedSprites.Sprites.ContainsKey(i.ToString()))
                                missingCount++;
                        }
                        if (missingCount > 0 && missingCount < expectedFrames)
                            warnMissingFrames.Add($"{display} (missing {missingCount}/{expectedFrames} frames)");
                    }
                }

                // [Warning] special frame missing
                if (enableSpecialAtk && !string.IsNullOrWhiteSpace(name))
                {
                    if (!PetHasSpriteFrame(name, 15))
                        warnSpecialFrameMissing.Add(display);
                }

                // [Warning] inaccessible round
                if (e.Round > 1)
                {
                    string avKey = $"{e.Area}|{e.Version}";
                    if (roundsByAreaVersion.ContainsKey(avKey))
                    {
                        var rounds = roundsByAreaVersion[avKey];
                        bool accessible = true;
                        for (int r = 1; r < e.Round; r++)
                        {
                            if (!rounds.Contains(r))
                            {
                                accessible = false;
                                break;
                            }
                        }
                        if (!accessible)
                            warnInaccessibleRound.Add($"{display} (round {e.Round} but gaps in earlier rounds)");
                    }
                }

                // [Warning] unlock not in unlocks
                if (!string.IsNullOrWhiteSpace(e.Unlock) && !unlockNames.Contains(e.Unlock))
                    warnUnlockNotInUnlocks.Add($"{display} (unlock: {e.Unlock})");

                // [Warning] power > 250
                if (e.Power > 250)
                    warnPowerOver250.Add($"{display} (power: {e.Power})");
            }

            // [Info] version with no pet
            foreach (var v in enemyVersions)
            {
                if (v > 0 && !petVersions.Contains(v))
                    infoVersionNoPet.Add($"Version {v}");
            }

            // Add errors
            if (errPowerZero.Count > 0)
                results.Add(ReportEntry.Error("Enemy(s) with power == 0", errPowerZero));
            if (errStageZero.Count > 0)
                results.Add(ReportEntry.Error("Enemy(s) with stage 0", errStageZero));
            if (errNoName.Count > 0)
                results.Add(ReportEntry.Error("Enemy(s) with no name", errNoName));
            if (errNoSprite.Count > 0)
                results.Add(ReportEntry.Error("Enemy(s) with no sprite at all (primary or secondary)", errNoSprite));
            if (errNoHpNoGlobal.Count > 0)
                results.Add(ReportEntry.Error("Enemy(s) with no global HP set and enemy HP is 0", errNoHpNoGlobal));
            if (errDmxNoAtkAlt2.Count > 0)
                results.Add(ReportEntry.Error("Enemy(s) with ruleset DMX but no atk_alt_2 sprite", errDmxNoAtkAlt2));
            if (errNoAtkMain.Count > 0)
                results.Add(ReportEntry.Error("Enemy(s) with no atk_main sprite", errNoAtkMain));

            // Add warnings
            if (warnMissingPrimary.Count > 0)
                results.Add(ReportEntry.Warning("Enemy(s) missing sprite for the module's primary style", warnMissingPrimary));
            if (warnMissingFrames.Count > 0)
                results.Add(ReportEntry.Warning("Enemy(s) missing sprites (has some but not all frames)", warnMissingFrames));
            if (warnSpecialFrameMissing.Count > 0)
                results.Add(ReportEntry.Warning("Enemy(s) missing special frame (15) while Enable Special Attack Sprite is true", warnSpecialFrameMissing));
            if (warnInaccessibleRound.Count > 0)
                results.Add(ReportEntry.Warning("Enemy(s) set to an inaccessible round (gaps in earlier rounds for same area/version)", warnInaccessibleRound));
            if (warnUnlockNotInUnlocks.Count > 0)
                results.Add(ReportEntry.Warning("Enemy(s) with unlock set that doesn't exist in the unlocks", warnUnlockNotInUnlocks));
            if (warnPowerOver250.Count > 0)
                results.Add(ReportEntry.Warning("Enemy(s) with power > 250", warnPowerOver250));

            // Add info
            if (infoVersionNoPet.Count > 0)
                results.Add(ReportEntry.Info("Enemy version(s) set to a version that has no pet in the Pet tab", infoVersionNoPet));

            return results;
        }

        #endregion

        #region Item Checks

        private List<ReportEntry> CheckItems()
        {
            var results = new List<ReportEntry>();
            if (items == null || items.Count == 0)
                return results;

            // Error lists
            var errNoName = new List<string>();
            var errNoIcon = new List<string>();
            var errComponentNoItem = new List<string>();
            var errBoostNoTime = new List<string>();
            var errBoostNoStatus = new List<string>();
            var errBoostNoAmount = new List<string>();
            var errStatusChangeNoStatus = new List<string>();
            var errStatusChangeNoAmount = new List<string>();

            // Warning lists
            var warnNoEffect = new List<string>();
            var warnNoAmount = new List<string>();

            // Info lists
            var infoNoDescription = new List<string>();
            var infoNoWeightGain = new List<string>();

            foreach (var item in items)
            {
                string display = !string.IsNullOrWhiteSpace(item.Name) ? item.Name
                    : !string.IsNullOrWhiteSpace(item.Id) ? item.Id : "(unnamed)";

                // [Error] no name
                if (string.IsNullOrWhiteSpace(item.Name))
                    errNoName.Add(display);

                // [Error] no icon
                if (string.IsNullOrWhiteSpace(item.SpriteName) || !ItemHasIcon(item.SpriteName))
                    errNoIcon.Add(display);

                // [Error] component but no component item
                if (string.Equals(item.Effect, "component", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(item.ComponentItem))
                    errComponentNoItem.Add(display);

                // [Error] status_boost but no boost time
                if (string.Equals(item.Effect, "status_boost", StringComparison.OrdinalIgnoreCase)
                    && item.BoostTime <= 0)
                    errBoostNoTime.Add(display);

                // [Error] status_boost but no status
                if (string.Equals(item.Effect, "status_boost", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(item.Status))
                    errBoostNoStatus.Add(display);

                // [Error] status_boost but no amount
                if (string.Equals(item.Effect, "status_boost", StringComparison.OrdinalIgnoreCase)
                    && item.Amount <= 0)
                    errBoostNoAmount.Add(display);

                // [Error] status_change but no status
                if (string.Equals(item.Effect, "status_change", StringComparison.OrdinalIgnoreCase)
                    && string.IsNullOrWhiteSpace(item.Status))
                    errStatusChangeNoStatus.Add(display);

                // [Error] status_change but no amount
                if (string.Equals(item.Effect, "status_change", StringComparison.OrdinalIgnoreCase)
                    && item.Amount <= 0)
                    errStatusChangeNoAmount.Add(display);

                // [Warning] no effect
                if (string.IsNullOrWhiteSpace(item.Effect))
                    warnNoEffect.Add(display);

                // [Warning] no amount (only if effect is set and not component)
                if (!string.IsNullOrWhiteSpace(item.Effect)
                    && !string.Equals(item.Effect, "component", StringComparison.OrdinalIgnoreCase)
                    && item.Amount <= 0)
                    warnNoAmount.Add(display);

                // [Info] no description
                if (string.IsNullOrWhiteSpace(item.Description))
                    infoNoDescription.Add(display);

                // [Info] no weight gain
                if (item.WeightGain == 0)
                    infoNoWeightGain.Add(display);
            }

            // Add errors
            if (errNoName.Count > 0)
                results.Add(ReportEntry.Error("Item(s) with no name", errNoName));
            if (errNoIcon.Count > 0)
                results.Add(ReportEntry.Error("Item(s) with no icon", errNoIcon));
            if (errComponentNoItem.Count > 0)
                results.Add(ReportEntry.Error("Item(s) with effect component, but no component item set", errComponentNoItem));
            if (errBoostNoTime.Count > 0)
                results.Add(ReportEntry.Error("Item(s) with status boost effect, but no boost time", errBoostNoTime));
            if (errBoostNoStatus.Count > 0)
                results.Add(ReportEntry.Error("Item(s) with status boost effect, but no status set", errBoostNoStatus));
            if (errBoostNoAmount.Count > 0)
                results.Add(ReportEntry.Error("Item(s) with status boost effect, but no amount set", errBoostNoAmount));
            if (errStatusChangeNoStatus.Count > 0)
                results.Add(ReportEntry.Error("Item(s) with status change effect, but no status set", errStatusChangeNoStatus));
            if (errStatusChangeNoAmount.Count > 0)
                results.Add(ReportEntry.Error("Item(s) with status change effect, but no amount set", errStatusChangeNoAmount));

            // Add warnings
            if (warnNoEffect.Count > 0)
                results.Add(ReportEntry.Warning("Item(s) with no effect", warnNoEffect));
            if (warnNoAmount.Count > 0)
                results.Add(ReportEntry.Warning("Item(s) with no amount", warnNoAmount));

            // Add info
            if (infoNoDescription.Count > 0)
                results.Add(ReportEntry.Info("Item(s) with no description", infoNoDescription));
            if (infoNoWeightGain.Count > 0)
                results.Add(ReportEntry.Info("Item(s) with no weight gain", infoNoWeightGain));

            return results;
        }

        #endregion

        #region Quest/Event Checks

        private List<ReportEntry> CheckQuestsEvents()
        {
            var results = new List<ReportEntry>();

            // --- Quest checks ---
            if (quests != null && quests.Count > 0)
            {
                var errMinBiggerThanMax = new List<string>();
                var errRewardItemNoItem = new List<string>();
                var errNoQuantity = new List<string>();

                foreach (var q in quests)
                {
                    string display = !string.IsNullOrWhiteSpace(q.Name) ? q.Name
                        : !string.IsNullOrWhiteSpace(q.Id) ? q.Id : "(unnamed)";

                    // [Error] min > max
                    if (q.TargetAmountRange != null && q.TargetAmountRange.Length >= 2
                        && q.TargetAmountRange[0] > q.TargetAmountRange[1])
                        errMinBiggerThanMax.Add(display);

                    // [Error] reward type item but no item set
                    if (q.RewardType == RewardType.Item && string.IsNullOrWhiteSpace(q.RewardValue))
                        errRewardItemNoItem.Add(display);

                    // [Error] no quantity
                    if (q.RewardQuantity <= 0)
                        errNoQuantity.Add(display);
                }

                if (errMinBiggerThanMax.Count > 0)
                    results.Add(ReportEntry.Error("Quest(s) with min amount bigger than max amount", errMinBiggerThanMax));
                if (errRewardItemNoItem.Count > 0)
                    results.Add(ReportEntry.Error("Quest(s) with reward type item, but no item set", errRewardItemNoItem));
                if (errNoQuantity.Count > 0)
                    results.Add(ReportEntry.Error("Quest(s) with no quantity set", errNoQuantity));
            }

            // --- Event checks ---
            if (events != null && events.Count > 0)
            {
                var errItemPkgNoItem = new List<string>();
                var errEnemyBattleNoAreaRound = new List<string>();
                var errItemPkgNoQuantity = new List<string>();

                var warnChanceNot100 = new List<string>();

                foreach (var ev in events)
                {
                    string display = !string.IsNullOrWhiteSpace(ev.Name) ? ev.Name
                        : !string.IsNullOrWhiteSpace(ev.Id) ? ev.Id : "(unnamed)";

                    // [Error] ItemPackage but no item
                    if (ev.Type == EventType.ItemPackage && string.IsNullOrWhiteSpace(ev.Item))
                        errItemPkgNoItem.Add(display);

                    // [Error] EnemyBattle but no area/round or area/round has no enemy
                    if (ev.Type == EventType.EnemyBattle)
                    {
                        if (!ev.Area.HasValue || !ev.Round.HasValue)
                        {
                            errEnemyBattleNoAreaRound.Add($"{display} (no area/round set)");
                        }
                        else
                        {
                            // Check if any enemy exists at that area/round (any version)
                            bool found = false;
                            foreach (var key in enemyByAreaRoundVersion.Keys)
                            {
                                var parts = key.Split('|');
                                if (parts.Length >= 2
                                    && int.TryParse(parts[0], out int a) && a == ev.Area.Value
                                    && int.TryParse(parts[1], out int r) && r == ev.Round.Value)
                                {
                                    found = true;
                                    break;
                                }
                            }
                            if (!found)
                                errEnemyBattleNoAreaRound.Add($"{display} (A{ev.Area.Value}R{ev.Round.Value} has no enemy)");
                        }
                    }

                    // [Error] ItemPackage but no quantity
                    if (ev.Type == EventType.ItemPackage && (!ev.ItemQuantity.HasValue || ev.ItemQuantity.Value <= 0))
                        errItemPkgNoQuantity.Add(display);
                }

                // [Warning] sum of chance != 100%
                double totalChance = events.Sum(ev => ev.ChancePercent);
                if (Math.Abs(totalChance - 100.0) > 0.01 && events.Count > 0)
                    warnChanceNot100.Add($"Total: {totalChance:F2}%");

                if (errItemPkgNoItem.Count > 0)
                    results.Add(ReportEntry.Error("Event(s) of type ItemPackage but no item set", errItemPkgNoItem));
                if (errEnemyBattleNoAreaRound.Count > 0)
                    results.Add(ReportEntry.Error("Event(s) of type EnemyBattle but no area/round set or area/round has no enemy", errEnemyBattleNoAreaRound));
                if (errItemPkgNoQuantity.Count > 0)
                    results.Add(ReportEntry.Error("Event(s) of type ItemPackage but no quantity set", errItemPkgNoQuantity));
                if (warnChanceNot100.Count > 0)
                    results.Add(ReportEntry.Warning("Event(s) sum of chance isn't 100%", warnChanceNot100));
            }

            return results;
        }

        #endregion

        #region Helpers

        private bool HasModuleSprite(string fileName)
        {
            if (string.IsNullOrEmpty(modulePath)) return false;
            return File.Exists(Path.Combine(modulePath, fileName));
        }

        private bool PetHasAnySpriteAtAll(string petName)
        {
            if (string.IsNullOrEmpty(petName)) return false;
            var spriteName = SpriteUtils.GetSpriteName(petName, PetUtils.FixedNameFormat);
            var gameRoot = Directory.GetParent(modulePath)?.Parent?.FullName;

            foreach (var format in SpriteUtils.AllFormats)
            {
                var folder = SpriteUtils.GetFolderForFormat(format);

                var dir = Path.Combine(modulePath, folder, spriteName);
                if (Directory.Exists(dir) && Directory.GetFiles(dir, "*.png").Length > 0) return true;

                var zip = Path.Combine(modulePath, folder, $"{spriteName}.zip");
                if (File.Exists(zip)) return true;

                if (!string.IsNullOrEmpty(gameRoot))
                {
                    dir = Path.Combine(gameRoot, "assets", folder, spriteName);
                    if (Directory.Exists(dir) && Directory.GetFiles(dir, "*.png").Length > 0) return true;

                    zip = Path.Combine(gameRoot, "assets", folder, $"{spriteName}.zip");
                    if (File.Exists(zip)) return true;
                }
            }
            return false;
        }

        private bool PetHasSpriteInFolder(string petName, string folder)
        {
            if (string.IsNullOrEmpty(petName)) return false;
            var spriteName = SpriteUtils.GetSpriteName(petName, PetUtils.FixedNameFormat);
            var gameRoot = Directory.GetParent(modulePath)?.Parent?.FullName;

            var dir = Path.Combine(modulePath, folder, spriteName);
            if (Directory.Exists(dir) && Directory.GetFiles(dir, "*.png").Length > 0) return true;

            var zip = Path.Combine(modulePath, folder, $"{spriteName}.zip");
            if (File.Exists(zip)) return true;

            if (!string.IsNullOrEmpty(gameRoot))
            {
                dir = Path.Combine(gameRoot, "assets", folder, spriteName);
                if (Directory.Exists(dir) && Directory.GetFiles(dir, "*.png").Length > 0) return true;

                zip = Path.Combine(gameRoot, "assets", folder, $"{spriteName}.zip");
                if (File.Exists(zip)) return true;
            }
            return false;
        }

        private bool PetHasSpriteFrame(string petName, int frame)
        {
            if (string.IsNullOrEmpty(petName)) return false;
            var sprites = SpriteUtils.LoadSprites(petName, modulePath,
                PetUtils.FixedNameFormat, frame + 1,
                module?.PrimarySpriteFormat ?? "Color",
                module?.SecondarySpriteFormat ?? "HD");
            return sprites.Sprites.ContainsKey(frame.ToString());
        }

        private bool ItemHasIcon(string spriteName)
        {
            if (string.IsNullOrWhiteSpace(spriteName)) return false;
            string itemsFolder = Path.Combine(modulePath, "items");
            if (!Directory.Exists(itemsFolder)) return false;
            return File.Exists(Path.Combine(itemsFolder, spriteName + ".png"));
        }

        private bool BackgroundHasAnySprite(Background bg)
        {
            if (string.IsNullOrWhiteSpace(bg.Name)) return false;
            string bgDir = Path.Combine(modulePath, "backgrounds");
            if (!Directory.Exists(bgDir)) return false;

            if (bg.DayNight)
            {
                foreach (var time in new[] { "day", "dusk", "night" })
                {
                    if (File.Exists(Path.Combine(bgDir, $"bg_{bg.Name}_{time}.png")))
                        return true;
                }
            }
            else
            {
                if (File.Exists(Path.Combine(bgDir, $"bg_{bg.Name}.png")))
                    return true;
            }
            return false;
        }

        private bool BackgroundHasHiRes(Background bg)
        {
            if (string.IsNullOrWhiteSpace(bg.Name)) return false;
            string bgDir = Path.Combine(modulePath, "backgrounds");
            if (!Directory.Exists(bgDir)) return false;

            if (bg.DayNight)
            {
                foreach (var time in new[] { "day", "dusk", "night" })
                {
                    if (!File.Exists(Path.Combine(bgDir, $"bg_{bg.Name}_{time}_high.png")))
                        return false;
                }
                return true;
            }
            else
            {
                return File.Exists(Path.Combine(bgDir, $"bg_{bg.Name}_high.png"));
            }
        }

        private bool EvolutionHasRequirements(Evolution evo)
        {
            if (evo == null) return false;

            if (HasRange(evo.ConditionHearts)) return true;
            if (HasRange(evo.Training)) return true;
            if (HasRange(evo.Battles)) return true;
            if (HasRange(evo.WinRatio)) return true;
            if (HasRange(evo.WinCount)) return true;
            if (HasRange(evo.Mistakes)) return true;
            if (HasRange(evo.Level)) return true;
            if (HasRange(evo.Overfeed)) return true;
            if (HasRange(evo.SleepDisturbances)) return true;
            if (evo.Area.HasValue) return true;
            if (evo.Stage.HasValue) return true;
            if (evo.Version.HasValue) return true;
            if (!string.IsNullOrWhiteSpace(evo.Attribute)) return true;
            if (!string.IsNullOrWhiteSpace(evo.Jogress)) return true;
            if (evo.SpecialEncounter.HasValue && evo.SpecialEncounter.Value) return true;
            if (HasRange(evo.Stage5)) return true;
            if (HasRange(evo.Stage6)) return true;
            if (HasRange(evo.Stage7)) return true;
            if (HasRange(evo.Stage8)) return true;
            if (!string.IsNullOrWhiteSpace(evo.Item)) return true;
            if (evo.TimeRange != null && evo.TimeRange.Length > 0) return true;
            if (HasRange(evo.Trophies)) return true;
            if (HasRange(evo.VitalValues)) return true;
            if (HasRange(evo.Weigth)) return true;
            if (HasRange(evo.QuestsCompleted)) return true;
            if (HasRange(evo.Pvp)) return true;
            if (evo.GCellHatch.HasValue && evo.GCellHatch.Value) return true;
            if (HasRange(evo.BlueGCells)) return true;
            if (HasRange(evo.YellowGCells)) return true;
            if (HasRange(evo.RedGCells)) return true;

            return false;
        }

        private bool HasRange(int[] range)
        {
            return range != null && range.Length >= 2 && (range[0] != 0 || range[1] != 0);
        }

        #endregion
    }

    #region Report Model

    public enum ReportLevel
    {
        Error,
        Warning,
        Info
    }

    public class ReportEntry
    {
        public ReportLevel Level { get; set; }
        public string Message { get; set; }
        public List<string> Items { get; set; }

        public static ReportEntry Error(string message, List<string> items = null)
            => new ReportEntry { Level = ReportLevel.Error, Message = message, Items = items };

        public static ReportEntry Warning(string message, List<string> items = null)
            => new ReportEntry { Level = ReportLevel.Warning, Message = message, Items = items };

        public static ReportEntry Info(string message, List<string> items = null)
            => new ReportEntry { Level = ReportLevel.Info, Message = message, Items = items };
    }

    #endregion
}
