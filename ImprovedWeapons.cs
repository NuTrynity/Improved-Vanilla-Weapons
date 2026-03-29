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
        public float projectile_speed = 2f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref turret_rapid_fire, "rapid_turrets_turrets", true);
            Scribe_Values.Look(ref turret_instant_cooldown, "turret_instant_cooldown", true);

            Scribe_Values.Look(ref burst_multiplier, "burst_multiplier", 3);
            Scribe_Values.Look(ref weapon_accuracy, "weapon_accuracy", 2f);
            Scribe_Values.Look(ref projectile_speed, "projectile_speed", 2f);

            base.ExposeData();
        }
    }

    public class ImprovedWeapons : Mod
    {
        public ImprovedWeaponsSettings mod_settings;

        public ImprovedWeapons(ModContentPack content) : base(content)
        {
            mod_settings = GetSettings<ImprovedWeaponsSettings>();
            LongEventHandler.QueueLongEvent(ApplyWeaponChanges, "[SIVW] Changing Weapon Values", true, null);
        }

        #region Mod Settings
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            
            listing.Label("REQUIRES RESTART TO TAKE EFFECT");
            listing.Gap();

            listing.Label($"Projectile Speed: {mod_settings.projectile_speed:F1}");
            mod_settings.projectile_speed = listing.Slider(mod_settings.projectile_speed, 1.0f, 2.0f);
            listing.Gap();
            
            listing.Label("=== Turret Modification ===");
            listing.CheckboxLabeled("Rapid Fire Turrets", ref mod_settings.turret_rapid_fire);
            listing.CheckboxLabeled("Instant Cooldown", ref mod_settings.turret_instant_cooldown);

            listing.Gap();

            listing.Label("=== Weapon Modification ===");
            listing.Label($"Weapon Accuracy: {mod_settings.weapon_accuracy:F1}");
            mod_settings.weapon_accuracy = listing.Slider(mod_settings.weapon_accuracy, 1.0f, 10.0f);

            listing.Label($"Weapon Burst Modifier: {mod_settings.burst_multiplier:F1}");
            mod_settings.burst_multiplier = (int)listing.Slider(mod_settings.burst_multiplier, 1, 3);

            listing.End();
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "[NuT] Slightly Improved Weapons";
        }
        #endregion

        private void ApplyWeaponChanges()
        {
            int weapons_matched = 0;
            int turrets_modified = 0;

            if (StatDefOf.AccuracyTouch != null) StatDefOf.AccuracyTouch.defaultBaseValue *= mod_settings.weapon_accuracy;
            if (StatDefOf.AccuracyShort != null) StatDefOf.AccuracyShort.defaultBaseValue *= mod_settings.weapon_accuracy;
            if (StatDefOf.AccuracyMedium != null) StatDefOf.AccuracyMedium.defaultBaseValue *= mod_settings.weapon_accuracy;
            if (StatDefOf.AccuracyLong != null) StatDefOf.AccuracyLong.defaultBaseValue *= mod_settings.weapon_accuracy;

            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                #region Weapon Mods
                if (thingDef.IsRangedWeapon && thingDef.weaponTags != null)
                {
                    // Changes weapon accuracy
                    if (thingDef.statBases == null)
                        continue;

                    weapons_matched++;

                    // Changes weapon BurstShotCount
                    if (!thingDef.Verbs.NullOrEmpty() && thingDef.building == null)
                    {
                        VerbProperties primaryVerb = thingDef.Verbs[0];

                        if (primaryVerb.burstShotCount > 1)
                            primaryVerb.burstShotCount *= mod_settings.burst_multiplier;

                        primaryVerb.ticksBetweenBurstShots /= 2;

                        // Projectile Speed
                        if (primaryVerb.defaultProjectile != null)
                            primaryVerb.defaultProjectile.projectile.speed *= mod_settings.projectile_speed;
                    }
                }
                #endregion

                #region Turret Mods
                if (thingDef.building?.IsTurret == true)
                {
                    ThingDef gun_def = thingDef.building.turretGunDef;
                    VerbProperties? turret_properties = gun_def?.Verbs?.FirstOrDefault();
                    bool is_artillery = thingDef.building.buildingTags.Contains("Artillery");
                    bool is_modified = false;

                    if (turret_properties != null && mod_settings.turret_rapid_fire)
                    {
                        turret_properties.burstShotCount = mod_settings.burst_multiplier;
                        turret_properties.ticksBetweenBurstShots /= 2;

                        is_modified = true;
                    }

                    if (mod_settings.turret_instant_cooldown)
                    {
                        if (thingDef.building.turretBurstCooldownTime > 0f)
                        {
                            thingDef.building.turretBurstCooldownTime = 0.1f;
                            thingDef.building.turretBurstWarmupTime = new FloatRange(0.0f);
                        }

                        is_modified = true;
                    }

                    if (turret_properties != null && turret_properties.defaultProjectile != null)
                        if (!is_artillery)
                            turret_properties.defaultProjectile.projectile.speed *= mod_settings.projectile_speed;

                    if (is_modified)
                        turrets_modified++;
                }
                #endregion
            }

            Log.Message("[SIVW] Successfully Modified Tagged Weapons");
            Log.Message($"[SIVW] Weapons modified: {weapons_matched}");
            Log.Message($"[SIVW] Turrets modified: {turrets_modified}");
        }
    }
}