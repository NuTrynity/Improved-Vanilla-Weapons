using Verse;
using RimWorld;

namespace ImprovedVanillaWeapons
{
    public class ImprovedWeapons : Mod
    {
        public const string CustomWeaponTag = "slightlyImprovedWeaponsTag";
        public ImprovedWeapons(ModContentPack content) : base(content)
        {
            LongEventHandler.QueueLongEvent(() => ApplyWeaponChanges(CustomWeaponTag), "LoadingDefs_SIVW_CustomTagChanges", true, null);
            Log.Message("[SIVW] Successfully Modified Tagged Weapons");
        }

        private void ApplyWeaponChanges(string weaponTag)
        {
            int weaponMatched = 0;
            int projectilesModified = 0;
            
            foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs)
            {
                if (thingDef.IsRangedWeapon && thingDef.weaponTags != null && thingDef.weaponTags.Contains(weaponTag))
                {
                    weaponMatched++;
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

                        if (primaryVerb.defaultProjectile != null)
                        {
                            primaryVerb.defaultProjectile.projectile.speed *= 1.25f;
                            projectilesModified++;
                        }
                    }
                }
            }
            
            Log.Message($"[SIVW] Weapons modified : {weaponMatched}, projectiles modified : {projectilesModified}");
        }
    }
}