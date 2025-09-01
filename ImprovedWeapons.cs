using Verse;
using System.Linq;
using RimWorld;

namespace ImprovedVanillaWeapons
{
    public class ImprovedWeapons : Mod
    {
        public const string CustomWeaponTag = "slightlyImprovedWeaponsTag";
        public ImprovedWeapons(ModContentPack content) : base(content)
        {
            LongEventHandler.QueueLongEvent(() => ApplyWeaponChanges(CustomWeaponTag), "LoadingDefs_SIVM_CustomTagChanges", true, null);
            Log.Message("[SIVW] Successfully Modified Tagged Weapons");
        }

        private void ApplyWeaponChanges(string weapon_tag)
        {
            int weapons_matched = 0;
            int turrets_modified = 0;

            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                if (thingDef.IsRangedWeapon && thingDef.weaponTags != null && thingDef.weaponTags.Contains(weapon_tag))
                {
                    weapons_matched++;
                    // Changes weapon accuracy
                    if (thingDef.statBases != null)
                    {
                        StatModifier accuracyTouch = thingDef.statBases.FirstOrDefault(sm => sm.stat == StatDefOf.AccuracyTouch);
                        StatModifier accuracyShort = thingDef.statBases.FirstOrDefault(sm => sm.stat == StatDefOf.AccuracyShort);
                        StatModifier accuracyMedium = thingDef.statBases.FirstOrDefault(sm => sm.stat == StatDefOf.AccuracyMedium);
                        StatModifier accuracyLong = thingDef.statBases.FirstOrDefault(sm => sm.stat == StatDefOf.AccuracyLong);

                        if (accuracyTouch != null)
                        {
                            accuracyTouch.value *= 1.5f;
                        }
                        if (accuracyShort != null)
                        {
                            accuracyShort.value *= 1.5f;
                        }
                        if (accuracyMedium != null)
                        {
                            accuracyMedium.value *= 1.5f;
                        }
                        if (accuracyLong != null)
                        {
                            accuracyLong.value *= 1.5f;
                        }
                    }

                    // Changes weapon BurstShotCount
                    if (thingDef.Verbs != null && thingDef.Verbs.Any())
                    {
                        VerbProperties primaryVerb = thingDef.Verbs[0];

                        if (primaryVerb.burstShotCount > 1)
                        {
                            primaryVerb.burstShotCount *= 3;
                        }

                        primaryVerb.ticksBetweenBurstShots /= 2;
                    }
                }
            }
            
            // Turret Cooldown
            var all_turrets = DefDatabase<ThingDef>.AllDefs.Where(td => (td.building != null && td.building.IsTurret));
            
            foreach (var turretDef in all_turrets)
            {
                if (turretDef.building.turretBurstCooldownTime > 0)
                {
                    turretDef.building.turretBurstCooldownTime = 0.1f;
                    turretDef.building.turretBurstWarmupTime = new FloatRange(0.0f);
                    turrets_modified++;
                }
            }
            
            Log.Message($"[SIVW] Weapons modified: {weapons_matched},  turrets modified: {turrets_modified}.");
        }
    }
}