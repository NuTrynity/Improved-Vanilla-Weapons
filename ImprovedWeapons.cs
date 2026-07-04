using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using RimWorld;
using Verse;

namespace ImprovedVanillaWeapons
{
    public class ImprovedWeaponsSettings : ModSettings
    {
        public bool turret_rapid_fire = true;
        public bool turret_instant_cooldown = true;

        public int burst_multiplier = 3;
        public float weapon_accuracy = 2f;
        public bool faster_projectiles = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref turret_rapid_fire, "rapid_turrets_turrets", true);
            Scribe_Values.Look(ref turret_instant_cooldown, "turret_instant_cooldown", true);

            Scribe_Values.Look(ref burst_multiplier, "burst_multiplier", 3);
            Scribe_Values.Look(ref weapon_accuracy, "weapon_accuracy", 2f);
            Scribe_Values.Look(ref faster_projectiles, "faster_projectiles", true);

            base.ExposeData();
        }
    }

    public class ImprovedWeapons : Mod
    {
        public ImprovedWeaponsSettings mod_settings;

        // Structured dictionaries to save original baseline values
        private static Dictionary<ThingDef, Dictionary<StatDef, float>> originalWeaponAccuracies = new Dictionary<ThingDef, Dictionary<StatDef, float>>();
        private static Dictionary<ThingDef, (int burst, float cooldown, int ticks)> originalWeaponVerbs = new Dictionary<ThingDef, (int, float, int)>();
        private static Dictionary<ThingDef, (int burst, FloatRange warmup, float cooldown)> originalTurretProperties = new Dictionary<ThingDef, (int, FloatRange, float)>();
        private static Dictionary<ProjectileProperties, float> originalProjectileSpeeds = new Dictionary<ProjectileProperties, float>();
        private static float? originalPawnShootingAccuracy = null;

        public ImprovedWeapons(ModContentPack content) : base(content)
        {
            mod_settings = GetSettings<ImprovedWeaponsSettings>();
            // Cache and apply once at startup
            LongEventHandler.QueueLongEvent(InitializeAndApply, "[SIVW] Caching and Initializing Weapon Values", true, null);
        }

        #region Mod Settings UI
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            
            listing.Label("<color=yellow>CHANGES NEED A SAVE RELOAD TO TAKE EFFECT</color>");
            listing.Gap();
            
            listing.Label("=== Turret Modification ===");
            listing.CheckboxLabeled("Rapid Fire Turrets", ref mod_settings.turret_rapid_fire);
            listing.CheckboxLabeled("Instant Cooldown", ref mod_settings.turret_instant_cooldown);
            listing.Gap();

            listing.Label("=== Modifications ===");
            listing.Label($"Weapon Accuracy: {mod_settings.weapon_accuracy:F1}x");
            mod_settings.weapon_accuracy = listing.Slider(mod_settings.weapon_accuracy, 1.0f, 5.0f);

            listing.Label($"Weapon Burst Modifier: {mod_settings.burst_multiplier:F0}x");
            mod_settings.burst_multiplier = (int)listing.Slider(mod_settings.burst_multiplier, 1, 5);

            listing.Gap();
            listing.CheckboxLabeled("Faster Projectiles", ref mod_settings.faster_projectiles);

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "[NuT] Slightly Improved Weapons";
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            ApplyWeaponChanges();
        }
        #endregion

        private void InitializeAndApply()
        {
            CacheVanillaValues();
            ApplyWeaponChanges();
        }

        private void CacheVanillaValues()
        {
            // 1. Cache global Pawn shooting accuracy
            originalPawnShootingAccuracy = StatDefOf.ShootingAccuracyPawn.defaultBaseValue;

            List<StatDef> weapon_accuracies = new List<StatDef>
            {
                StatDefOf.AccuracyLong, StatDefOf.AccuracyMedium, StatDefOf.AccuracyShort, StatDefOf.AccuracyTouch
            };

            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                // 2. Cache Weapon data
                if (thingDef.IsRangedWeapon)
                {
                    if (thingDef.statBases != null)
                    {
                        var statDict = new Dictionary<StatDef, float>();
                        foreach (StatModifier stat_mod in thingDef.statBases)
                        {
                            if (weapon_accuracies.Contains(stat_mod.stat))
                                statDict[stat_mod.stat] = stat_mod.value;
                        }
                        originalWeaponAccuracies[thingDef] = statDict;
                    }

                    if (!thingDef.Verbs.NullOrEmpty() && thingDef.building == null)
                    {
                        VerbProperties primaryVerb = thingDef.Verbs[0];
                        originalWeaponVerbs[thingDef] = (primaryVerb.burstShotCount, primaryVerb.defaultCooldownTime, primaryVerb.ticksBetweenBurstShots);

                        if (primaryVerb.defaultProjectile?.projectile != null && !originalProjectileSpeeds.ContainsKey(primaryVerb.defaultProjectile.projectile))
                        {
                            originalProjectileSpeeds[primaryVerb.defaultProjectile.projectile] = primaryVerb.defaultProjectile.projectile.speed;
                        }
                    }
                }

                // 3. Cache Turret data
                if (thingDef.building?.IsTurret == true)
                {
                    BuildingProperties bProps = thingDef.building;
                    VerbProperties? turretVerb = bProps.turretGunDef?.Verbs?.FirstOrDefault();

                    int origBurst = turretVerb != null ? turretVerb.burstShotCount : 0;
                    originalTurretProperties[thingDef] = (origBurst, bProps.turretBurstWarmupTime, bProps.turretBurstCooldownTime);

                    if (turretVerb?.defaultProjectile?.projectile != null && !originalProjectileSpeeds.ContainsKey(turretVerb.defaultProjectile.projectile))
                    {
                        originalProjectileSpeeds[turretVerb.defaultProjectile.projectile] = turretVerb.defaultProjectile.projectile.speed;
                    }
                }
            }
        }

        private void ApplyWeaponChanges()
        {
            int weapons_modified = 0;
            int turrets_modified = 0;

            // Restore global pawn accuracy to vanilla base before multiplying
            if (originalPawnShootingAccuracy.HasValue)
                StatDefOf.ShootingAccuracyPawn.defaultBaseValue = originalPawnShootingAccuracy.Value * mod_settings.weapon_accuracy;

            List<StatDef> weapon_accuracies = new List<StatDef>
            {
                StatDefOf.AccuracyLong, StatDefOf.AccuracyMedium, StatDefOf.AccuracyShort, StatDefOf.AccuracyTouch
            };

            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                #region Apply Weapon Mods
                if (thingDef.IsRangedWeapon)
                {
                    // Reset and update accuracy stats from clean cache
                    if (thingDef.statBases != null && originalWeaponAccuracies.TryGetValue(thingDef, out var cachedStats))
                    {
                        foreach (StatModifier stat_mod in thingDef.statBases)
                        {
                            if (cachedStats.TryGetValue(stat_mod.stat, out float vanillaValue))
                            {
                                stat_mod.value = Mathf.Clamp01(vanillaValue * mod_settings.weapon_accuracy);
                            }
                        }
                    }
                    
                    weapons_modified++;

                    // Reset and update Verb properties from clean cache
                    if (!thingDef.Verbs.NullOrEmpty() && thingDef.building == null && originalWeaponVerbs.TryGetValue(thingDef, out var cachedVerb))
                    {
                        VerbProperties primaryVerb = thingDef.Verbs[0];

                        primaryVerb.burstShotCount = cachedVerb.burst > 2 ? cachedVerb.burst * mod_settings.burst_multiplier : cachedVerb.burst;
                        primaryVerb.defaultCooldownTime = cachedVerb.cooldown > 0 ? 0f : cachedVerb.cooldown; // Keeps it instantly refreshed
                        primaryVerb.ticksBetweenBurstShots = cachedVerb.ticks / 2;

                        // Projectile Speed
                        if (primaryVerb.defaultProjectile?.projectile != null && originalProjectileSpeeds.TryGetValue(primaryVerb.defaultProjectile.projectile, out float vanillaSpeed))
                        {
                            primaryVerb.defaultProjectile.projectile.speed = mod_settings.faster_projectiles ? 210f : vanillaSpeed;
                        }
                    }
                }
                #endregion

                #region Apply Turret Mods
                if (thingDef.building?.IsTurret == true && originalTurretProperties.TryGetValue(thingDef, out var cachedTurret))
                {
                    BuildingProperties building_properties = thingDef.building;
                    VerbProperties? turret_properties = building_properties.turretGunDef?.Verbs?.FirstOrDefault();

                    bool is_modified = false;

                    // Reset and update burst settings
                    if (turret_properties != null)
                    {
                        if (mod_settings.turret_rapid_fire && cachedTurret.burst > 1)
                        {
                            turret_properties.burstShotCount = cachedTurret.burst * mod_settings.burst_multiplier;
                            is_modified = true;
                        }
                        else
                        {
                            turret_properties.burstShotCount = cachedTurret.burst; // Revert to vanilla
                        }
                    }

                    // Reset and update cooldown settings
                    if (mod_settings.turret_instant_cooldown)
                    {
                        building_properties.turretBurstCooldownTime = 1.0f;
                        building_properties.turretBurstWarmupTime = new FloatRange(0.0f);
                        is_modified = true;
                    }
                    else
                    {
                        building_properties.turretBurstCooldownTime = cachedTurret.cooldown;
                        building_properties.turretBurstWarmupTime = cachedTurret.warmup;
                    }

                    // Reset and update Projectile speeds
                    if (turret_properties?.defaultProjectile?.projectile != null && originalProjectileSpeeds.TryGetValue(turret_properties.defaultProjectile.projectile, out float vanillaSpeed))
                    {
                        if (!building_properties.IsMortar && mod_settings.faster_projectiles)
                            turret_properties.defaultProjectile.projectile.speed = 210f;
                        else
                            turret_properties.defaultProjectile.projectile.speed = vanillaSpeed;
                    }

                    if (is_modified)
                        turrets_modified++;
                }
                #endregion
            }

            Log.Message($"[SIVW] Dynamic Settings Refreshed! Weapons: {weapons_modified}, Turrets: {turrets_modified}");
        }
    }
}
