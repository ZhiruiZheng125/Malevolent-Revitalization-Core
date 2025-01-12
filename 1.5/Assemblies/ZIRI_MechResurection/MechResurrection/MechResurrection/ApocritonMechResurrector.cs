//using HarmonyLib;
//using Microsoft.SqlServer.Server;
//using Mono.Cecil.Cil;
//using System.Linq.Expressions;
//using System.Reflection;
//using System.Reflection.Emit;
//using System.Runtime.InteropServices;
//using System.Runtime.InteropServices.ComTypes;
//using System.Runtime.Remoting.Messaging;
//using System.Security.Cryptography;
//using System.Text;
//using System.Threading.Tasks;
//using RimWorld.Planet;
//using RimWorld.Utility;
//using System;
//using System.Collections;
//using System.Data;
//using System.Diagnostics.Eventing.Reader;
//using Verse.AI;
//using Verse.AI.Group;
//using Verse.Noise;
//using Verse.Sound;
//using static HarmonyLib.Code;
//using static RimWorld.FoodUtility;
//using static UnityEngine.GraphicsBuffer;
//using static UnityEngine.Scripting.GarbageCollector;
//using System.Net.NetworkInformation;
//using static System.Net.Mime.MediaTypeNames;
using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static RimWorld.MechClusterSketch;

namespace ZIRI_ApocritonMechResurrector
{

    [DefOf]
    public static class StatDefOf
    {
        public static StatDef MechResurrectMaxChargePoint;
    }

    [DefOf]
    public static class ApocritonMechResurrector_HediffDefOf
    {
        public static HediffDef ApocritonMechResurrector;

        static ApocritonMechResurrector_HediffDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ApocritonMechResurrector_HediffDefOf));
        }
    }

    [DefOf]
    public static class ApocritonMechResurrector_AbilityDefOf
    {
        public static AbilityDef ApocritonMechResurrector;

        static ApocritonMechResurrector_AbilityDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(ApocritonMechResurrector_AbilityDefOf));
        }
    }




    //LTS mech resurrect start
    public class CompProperties_MechanitorResurrectMech : CompProperties_AbilityEffect
    {
        public CompProperties_MechanitorResurrectMech()
        {
            this.compClass = typeof(CompAbilityEffect_MechanitorResurrectMech);
        }

        public EffecterDef appliedEffecterDef;

        public EffecterDef resolveEffecterDef;

        public int maxCorpseAgeTicks = int.MaxValue;

        public List<MechChargeCosts> costs = new List<MechChargeCosts>();

    }

    public class CompAbilityEffect_MechanitorResurrectMech : CompAbilityEffect
    {

        [LoadAlias("charges")]
        public int resurrectCharges;

        private Dictionary<MechWeightClass, int> costsByWeightClass;

        private Gizmo gizmo;

        public int ChargesRemaining => resurrectCharges;

        public override bool CanCast => resurrectCharges > 0;

        public new CompProperties_MechanitorResurrectMech Props => (CompProperties_MechanitorResurrectMech)props;
        public int currentMaxResurrectCharges => Mathf.Max((int)(this.GetHediff().Severity - 1), 0) * ZIRIMisc_MechResurrector_Settings.MechResurrectChargePointMultiplier + ZIRIMisc_MechResurrector_Settings.MechResurrectChargePointBaseFactor;
        //Mathf.Max(resurrectCharges, 0)

        public void ResetCharges()
        {
           //Log.Message("ResetCharges called");
            this.resurrectCharges = this.currentMaxResurrectCharges;
           //Log.Message("ResetCharges end");
        }



        public Hediff GetHediff()
        {
           //Log.Message("GetHediff called");
            return this.parent.pawn.health.hediffSet.GetFirstHediffOfDef(ApocritonMechResurrector_HediffDefOf.ApocritonMechResurrector);
        }

        public override void Initialize(AbilityCompProperties props)
        {
           //Log.Message("Initialize called");
            base.Initialize(props);
            this.ResetCharges();
            costsByWeightClass = new Dictionary<MechWeightClass, int>();
           //Log.Message("Initialize costsByWeightClass");
            for (int i = 0; i < this.Props.costs.Count; i++)
            {
                costsByWeightClass[this.Props.costs[i].weightClass] = this.Props.costs[i].cost;
               //Log.Message("Initialize costsByWeightClass: " + this.Props.costs[i].weightClass + " " + this.Props.costs[i].cost);
            }
           //Log.Warning("Initialize end");
        }

        private int RemainedBandwith(Pawn pawn)
        {
           //Log.Message("RemainedBandwith called");
            try
            {
                return pawn.mechanitor.TotalBandwidth - pawn.mechanitor.UsedBandwidth;
            }
            catch when (pawn.mechanitor == null)
            {
                Log.Message("Mechanitor not found");
                return -99;
            }

        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
           //Log.Message("CanApplyOn called");
            Pawn mechanitorPawn = this.parent.pawn;
            int remainedBandwith = this.RemainedBandwith(mechanitorPawn);
            Pawn thisPawn = target.Thing as Pawn;
            Corpse thisCorpse = target.Thing as Corpse;
            bool ifCorpse = (target.Thing is Corpse corpse);
            bool checkIfBandwithEnough = thisCorpse.InnerPawn.GetStatValue(StatDef.Named("BandwidthCost")) <= remainedBandwith;

            //display the fail resurrection mech message
            if (thisCorpse.InnerPawn.Faction != this.parent.pawn.Faction)
            {
                Messages.Message("Can only resurrect allied mechs", thisPawn, MessageTypeDefOf.NegativeEvent);
            }
            else if (thisCorpse.timeOfDeath <= Find.TickManager.TicksGame - this.Props.maxCorpseAgeTicks)
            {
                Messages.Message("Target mech has been dead too long", thisPawn, MessageTypeDefOf.NegativeEvent);
            }
            else if (!checkIfBandwithEnough)
            {
                Messages.Message(mechanitorPawn.Name.ToStringSafe() + " has insufficient bandwidth", thisPawn, MessageTypeDefOf.NegativeEvent);
            }
            else if (thisCorpse.InnerPawn.RaceProps.mechWeightClass >= MechWeightClass.UltraHeavy)
            {
                Messages.Message("Cannot resurrect SuperHeavy Mech", thisPawn, MessageTypeDefOf.NegativeEvent);
            }
            else if (!TryGetResurrectCost(thisCorpse, out var cost) || cost > resurrectCharges)
            {
                Messages.Message("Insufficient Resurrection Points", MessageTypeDefOf.NegativeEvent);
            }


            //mechResurrect can only be applied by following conditions
            if (!base.CanApplyOn(target, dest))
            {
               //Log.Message("base.CanApplyOn(target, dest) is false");
                return false;
            }
            if (!target.HasThing || !(ifCorpse))
            {
               //Log.Message("target.HasThing || !(ifCorpse) is false");
                return false;
            }
            if (!CanResurrect(thisCorpse))
            {
               //Log.Message("CanResurrect(thisCorpse) is false");
                return false;
            }
            if (!checkIfBandwithEnough)
            {
               //Log.Message("checkIfBandwithEnough is false");
                return false;
            }


            return true;
        }

        private bool TryGetResurrectCost(Corpse corpse, out int cost)
        {
           //Log.Message("TryGetResurrectCost called");
            if (costsByWeightClass.ContainsKey(corpse.InnerPawn.RaceProps.mechWeightClass))
            {
                cost = costsByWeightClass[corpse.InnerPawn.RaceProps.mechWeightClass];
               //Log.Message("TryGetResurrectCost value: " + cost);
                return true;
            }
           //Log.Message("TryGetResurrectCost null");
            cost = -1;
            return false;
        }

        private bool CanResurrect(Corpse corpse)
        {
           //Log.Message("CanResurrect called");
            if (!corpse.InnerPawn.RaceProps.IsMechanoid || corpse.InnerPawn.RaceProps.mechWeightClass >= MechWeightClass.UltraHeavy)
            {
               //Log.Message("corpse.InnerPawn.RaceProps.IsMechanoid || corpse.InnerPawn.RaceProps.mechWeightClass >= MechWeightClass.UltraHeavy is false");
                return false;
            }
            if (corpse.InnerPawn.Faction != this.parent.pawn.Faction)
            {
               //Log.Message("corpse.InnerPawn.Faction != this.parent.pawn.Faction is false");
                return false;
            }
            if (corpse.InnerPawn.kindDef.abilities != null && corpse.InnerPawn.kindDef.abilities.Contains(AbilityDefOf.ResurrectionMech))
            {
               //Log.Message("corpse.InnerPawn.kindDef.abilities != null && corpse.InnerPawn.kindDef.abilities.Contains(AbilityDefOf.ResurrectionMech) is false");
                return false;
            }
            if (corpse.timeOfDeath < Find.TickManager.TicksGame - Props.maxCorpseAgeTicks)
            {
               //Log.Message("corpse.timeOfDeath < Find.TickManager.TicksGame - Props.maxCorpseAgeTicks is false");
                return false;
            }
            if (corpse.timeOfDeath < Find.TickManager.TicksGame - this.Props.maxCorpseAgeTicks)
            {
               //Log.Message("corpse.timeOfDeath < Find.TickManager.TicksGame - this.Props.maxCorpseAgeTicks is false");
                return false;
            }
            if (!TryGetResurrectCost(corpse, out var cost) || cost > resurrectCharges)
            {
               //Log.Message("TryGetResurrectCost(corpse, out var cost) || cost > resurrectCharges is false");
                return false;
            }
           //Log.Message("CanResurrect return true");
            return true;

        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {

           //Log.Message("Apply called");
            base.Apply(target, dest);
            Corpse corpse = (Corpse)target.Thing;
            if (CanResurrect(corpse) && TryGetResurrectCost(corpse, out var cost))
            {
                Pawn innerPawn = corpse.InnerPawn;
                resurrectCharges -= cost;
                //for resurrection
                ResurrectionUtility.TryResurrect(innerPawn);
                //for immediate control
                if (Props.appliedEffecterDef != null && MechanitorUtility.CanControlMech(parent.pawn, innerPawn))
                {
                    Effecter effecter = Props.appliedEffecterDef.SpawnAttached(innerPawn, innerPawn.MapHeld);
                    effecter.Trigger(innerPawn, innerPawn);
                    effecter.Cleanup();
                    parent.pawn.relations.AddDirectRelation(PawnRelationDefOf.Overseer, innerPawn);//if resurrection successful, immediately takes control of resurrected mech
                    //Log.Message("AddDirectRelation successful");//error is resurrectcharges -> fixed

                }
                innerPawn.stances.stagger.StaggerFor(60);
                
            }
           //Log.Message("Apply end");
        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = null;
            return false;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra() //charge display
        {
            //Log.Message("CompGetGizmosExtra called");
            if (gizmo == null)
            {
                gizmo = new Gizmo_MechanitorMechResurrectionCharges(this);
            }
            yield return gizmo;
            if (DebugSettings.ShowDevGizmos)
            {
                Command_Action add = new Command_Action
                {
                    defaultLabel = "DEV: Add charge",
                    action = delegate
                    {
                        resurrectCharges++;
                        resurrectCharges = Mathf.Min(resurrectCharges, this.currentMaxResurrectCharges);
                    }
                };
                yield return add;

                Command_Action remove = new Command_Action
                {
                    defaultLabel = "DEV: Remove charge",
                    action = delegate
                    {
                        resurrectCharges--;
                        resurrectCharges = Mathf.Max(resurrectCharges, 0);
                    }
                };
                yield return remove;

                Command_Action reset = new Command_Action
                {
                    defaultLabel = "DEV: Reset charge",
                    action = delegate
                    {
                        this.ResetCharges();
                    }
                };
                yield return reset;
            }


        }

        public override IEnumerable<Mote> CustomWarmupMotes(LocalTargetInfo target)
        {
            //Log.Message("CustomWarmupMotes called");
            foreach (LocalTargetInfo affectedTarget in this.parent.GetAffectedTargets(target))
            {
                Thing thing = affectedTarget.Thing;
                yield return MoteMaker.MakeAttachedOverlay(thing, ThingDefOf.Mote_MechResurrectWarmupOnTarget, Vector3.zero);
            }
            yield break;
        }

        public override void PostApplied(List<LocalTargetInfo> targets, Map map)
        {
           //Log.Message("PostApplied called");
            Vector3 zero = Vector3.zero;
            foreach (LocalTargetInfo target in targets)
            {
                zero += target.Cell.ToVector3Shifted();
            }
            zero /= (float)targets.Count();
            IntVec3 intVec = zero.ToIntVec3();

            EffecterDefOf.ApocrionAoeResolve.Spawn(intVec, map).EffectTick(new TargetInfo(intVec, map), new TargetInfo(intVec, map));
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref resurrectCharges, "resurrectCharges");
        }
    }

    [StaticConstructorOnStartup]
    public class Gizmo_MechanitorMechResurrectionCharges : Gizmo
    {
        private static readonly Texture2D FullShieldBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.2f, 0.24f));

        private static readonly Texture2D EmptyShieldBarTex = SolidColorMaterials.NewSolidColorTexture(Color.clear);

        private static readonly float Width = 110f;

        private CompAbilityEffect_MechanitorResurrectMech ability;

        public Gizmo_MechanitorMechResurrectionCharges(CompAbilityEffect_MechanitorResurrectMech ability)
        {
            this.ability = ability;
            Order = -100f;
            
        }

        public override float GetWidth(float maxWidth)
        {
            return Width;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Rect rect2 = rect.ContractedBy(6f);
            Widgets.DrawWindowBackground(rect);
            Rect rect3 = rect2;
            rect3.height = rect.height / 2f;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.Label(rect3, "MechResurrectionCharges".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            Rect rect4 = rect;
            rect4.y += rect3.height - 5f;
            rect4.height = rect.height / 2f;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect4, ability.ChargesRemaining.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            return new GizmoResult(GizmoState.Clear);
        }
    }

    //LTS mech resurrect end

    public class CompProperties_UseEffectAddHediffAndRecharge : CompProperties_UseEffect
    {
        public HediffDef hediffDef;

        public HediffDef hediffDefAnother;

        public bool allowRepeatedUse;

        public AbilityDef haveAbilityDef;

        public CompProperties_UseEffectAddHediffAndRecharge()
        {
            compClass = typeof(CompUseEffect_AddHediffAndResetMechResurrectCharge);
        }
    }

    public class CompUseEffect_AddHediffAndResetMechResurrectCharge : CompUseEffect
    {
        public CompProperties_UseEffectAddHediffAndRecharge Props => (CompProperties_UseEffectAddHediffAndRecharge)props;



        public override void DoEffect(Pawn user)
        {
          //Log.Message("DoEffect has been call by " + user.Name);
            AbilityDef abilityDef = Props.haveAbilityDef;

            //find the ability of RemoteResurrect from the user
            //Ability remoteResurrectAbility = user.abilities.GetAbility(abilityDef);
            Ability remoteResurrectAbility = user.abilities.GetAbility(abilityDef);

            if (remoteResurrectAbility == null)// find mech resurrect ability, if not found, add both input hediffs
            {
               //Log.Message("Didn't find RemoteResurrect ability");
               //Log.Message("AddHediff: " + Props.hediffDefAnother);
                user.health.AddHediff(Props.hediffDef);
                user.health.AddHediff(Props.hediffDefAnother);
                return;
            }
            else
            {
              //Log.Message("Find Abilitiy: " + remoteResurrectAbility.def.defName);
              //Log.Message("AddHediff: " + Props.hediffDef);
                //user.health.AddHediff(Props.hediffDef);
                //find the CompAbilityEffect_MechanitorResurrectMech from the ability(might has better ways to look through)
                CompAbilityEffect_MechanitorResurrectMech comp = remoteResurrectAbility.EffectComps.Find(x => x is CompAbilityEffect_MechanitorResurrectMech) as CompAbilityEffect_MechanitorResurrectMech;

                if (comp != null)
                {
                  //Log.Message("Find CompAbilityEffect_MechanitorResurrectMech!");
                    comp.ResetCharges();
                }
                return;
            }
        }

        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            if (!Props.allowRepeatedUse && (p.health.hediffSet.HasHediff(Props.hediffDef) || p.health.hediffSet.HasHediff(Props.hediffDefAnother)))
            {
                return "AlreadyHasHediff".Translate(Props.hediffDef.label);
            }
            return true;
        }
    }


    //Social: Hate Human for mechResurrect
    public class ThoughtWorker_disdainOrganism_Hediff : ThoughtWorker
    {
        protected override ThoughtState CurrentSocialStateInternal(Pawn p, Pawn other)
        {

          //Log.Message("CurrentSocialStateInternal called by: " + p.Name);

            Hediff firstHediffOfDef = p.health.hediffSet.GetFirstHediffOfDef(def.hediff);
            if (firstHediffOfDef?.def.stages == null)
            {
              //Log.Message("Hediff not found in ThoughtWorker_disdainOrganism_Hediff." + p.Name);
                return ThoughtState.Inactive;
            }
            if (MechanitorUtility.IsMechanitor(other))
            {
              //Log.Message("Other Mechanitor found in ThoughtWorker_disdainOrganism_Hediff." + other.Name);
                return ThoughtState.ActiveAtStage(def.stages.Count - 1);
            }
          //Log.Message("Hediff found in ThoughtWorker_disdainOrganism_Hediff." + p.Name);
            return ThoughtState.ActiveAtStage(Mathf.Min(firstHediffOfDef.CurStageIndex, firstHediffOfDef.def.stages.Count - 1, def.stages.Count - 1));

        }
    }






    public class HediffCompProperties_pawnExpload : HediffCompProperties
    {
        public IntRange wickTicks = new IntRange(140, 150);
        public float explosiveRadius = 1.9f;
        public bool explodeOnKilled;
        public DamageDef explosiveDamageType;
        public float propagationSpeed = 1f;
        public float chanceNeverExplodeFromDamage;
        public string extraInspectStringKey;
        public SoundDef soundDef;

        public HediffCompProperties_pawnExpload()
        {
            compClass = typeof(HediffComp_pawnExpload);
        }
    }

    public class HediffComp_pawnExpload : HediffComp
    {
        public HediffCompProperties_pawnExpload Props => (HediffCompProperties_pawnExpload)props;

        CompProperties_Explosive compProperties_Explosive = new CompProperties_Explosive();

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
           //Log.Message("Notify_PawnDied called");

            if (this.parent.pawn == null)
            {
               //Log.Message("Pawn is null");
                return;
            }

            GenExplosion.DoExplosion(
                    this.parent.pawn.PositionHeld, //IntVec3 center, 
                    this.parent.pawn.MapHeld, //Map map, 
                    Props.explosiveRadius, //float radius, 
                    Props.explosiveDamageType, //DamageDef damType, 
                    this.Pawn, //Thing instigator,
                    -1,  //int damAmount = -1, 
                    -1f,//float armorPenetration = -1f, 
                    Props.soundDef,//SoundDef explosionSound = null,
                    null,//ThingDef weapon = null,
                    null,//ThingDef projectile = null,
                    null,//Thing intendedTarget = null, 
                    null,//ThingDef postExplosionSpawnThingDef = null, 
                    0f,//float postExplosionSpawnChance = 0f,
                    1,//int postExplosionSpawnThingCount = 1, 
                    null,//GasType? postExplosionGasType = null, 
                    true,//bool applyDamageToExplosionCellsNeighbors = false
                    null,//ThingDef preExplosionSpawnThingDef = null, 
                    0f,//float preExplosionSpawnChance = 0f, 
                    1,//int preExplosionSpawnThingCount = 1, 
                    0f, //float chanceToStartFire = 0f,
                    false,//bool damageFalloff = false, 
                    null,//float? direction = null,
                    null,//List< Thing > ignoredThings = null, 
                    null,//FloatRange? affectedAngle = null,
                    true,//bool doVisualEffects = true, 
                    Props.propagationSpeed,//float propagationSpeed = 1f, 
                    0f,//float excludeRadius = 0f, 
                    true,//bool doSoundEffects = true, 
                    null,//ThingDef postExplosionSpawnThingDefWater = null, 
                    1f,//float screenShakeFactor = 1f, 
                    null,//SimpleCurve flammabilityChanceCurve = null, 
                    null //List<IntVec3> overrideCells = null
                    );
        }


    }
    public class CompUseEffect_InstallApocritonMechResurrector : CompUseEffect_InstallImplant
    {
        public override TaggedString ConfirmMessage(Pawn p)
        {

            Hediff hediff = this.GetExistingImplant(p);
            if (hediff == null)
            {
                return "ConfirmInstallApocritonMechResurrector_LevelInit".Translate();
            }

            int level = (int)hediff.Severity;


            if (level == 1)
            {
                return "ConfirmInstallApocritonMechResurrector_Level1".Translate();
            }
            else if (level == 2)
            {
                return "ConfirmInstallApocritonMechResurrector_Level2".Translate();
            }
            return null;

        }

        public override void DoEffect(Pawn user)
        {
           //Log.Message("DoEffect has been call by " + user.Name);
            

            BodyPartRecord bodyPartRecord = user.RaceProps.body.GetPartsWithDef(Props.bodyPart).FirstOrFallback();
            if (bodyPartRecord != null)
            {
                Hediff firstHediffOfDef = user.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
                if (firstHediffOfDef == null && !Props.requiresExistingHediff)
                {
                    user.health.AddHediff(Props.hediffDef, bodyPartRecord);
                }
                else if (Props.canUpgrade)
                {
                   //Log.Message("Current input charge point before chnage level: " + ((int)user.GetStatValue(StatDefOf.MechResurrectMaxChargePoint, true, -1)));
                    ((Hediff_Level)firstHediffOfDef).ChangeLevel(1);
                   //Log.Message("Current input charge point after chnage level: " + ((int)user.GetStatValue(StatDefOf.MechResurrectMaxChargePoint, true, -1)));

                    this.UpdateUserAbilityOfMaxChargePoints(user, ApocritonMechResurrector_AbilityDefOf.ApocritonMechResurrector);

                }
            }


        }

        private async void UpdateUserAbilityOfMaxChargePoints(Pawn p, AbilityDef abilityDef)
        {
            CompAbilityEffect_MechanitorResurrectMech comp = p.abilities.GetAbility(abilityDef).EffectComps.Find((x => x is CompAbilityEffect_MechanitorResurrectMech)) as CompAbilityEffect_MechanitorResurrectMech;
            if (comp != null)
            {
               //Log.Message("CompAbilityEffect_MechanitorResurrectMech found in : " + comp.ToStringSafe());
                try
                {
                  //Log.Message("Resetting charges...");
                    await Task.Delay(50);//have to wait for value assigned to the ability, then reset the charges
                    comp.ResetCharges();//other way: just read hediff level and assign to the charges, becuase this way is less reliable
                    return;
                }
                catch (System.Exception ex)
                {
                  Log.Error($"Error resetting charges: {ex.Message}");
                }
                
                
            }

           //Log.Message("CompAbilityEffect_MechanitorResurrectMech Not found!!!");

        }
    }

    //mod setting menu for mech resurrect charge point
    [StaticConstructorOnStartup]
    public class ZIRIMisc_MechResurrector_Settings : ModSettings
    {
        public static int MechResurrectChargePointBaseFactor = 30;

        public static int MechResurrectChargePointMultiplier = 10;

        public static void DoSettingsWindowContents(Rect inRect)
        {
            Rect rect = new Rect(inRect.x, inRect.y, inRect.width * 0.6f, inRect.height);
            rect = rect.CenteredOnXIn(inRect);
            Listing_Standard listing_Standard = new Listing_Standard
            {
                ColumnWidth = rect.width
            };

            listing_Standard.Begin(rect);

            listing_Standard.Gap(6f);
            MechResurrectChargePointBaseFactor = (int)listing_Standard.SliderLabeled("ZIRIMisc_MechResurrector_MechResurrectChargePointBaseFactor".Translate(MechResurrectChargePointBaseFactor.ToString()), (float)MechResurrectChargePointBaseFactor, 10f, 40f);

            listing_Standard.Gap(12f);
            MechResurrectChargePointMultiplier = (int)listing_Standard.SliderLabeled("ZIRIMisc_MechResurrector_MechResurrectChargePointMultiplier".Translate(MechResurrectChargePointMultiplier.ToString()), (float)MechResurrectChargePointMultiplier, 10f, 40f);

            listing_Standard.Gap(6f);
            if (listing_Standard.ButtonText("Reset".Translate()))
            {
                Reset();
            }
            listing_Standard.Gap(6f);
            listing_Standard.Label("ZIRIMisc_MechResurrector_Hint".Translate());
            listing_Standard.End();
        }

        protected static void Reset()
        {
            MechResurrectChargePointBaseFactor = 30;
            MechResurrectChargePointMultiplier = 10;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref MechResurrectChargePointBaseFactor, "MechResurrectChargePointBaseFactor", 30);
            Scribe_Values.Look(ref MechResurrectChargePointMultiplier, "MechResurrectChargePointMultiplier", 10);

        }
    }

    [StaticConstructorOnStartup]
    public class ZIRIMisc_MechResurrector_Mod : Mod
    {
        public static ZIRIMisc_MechResurrector_Settings _settings;

        public ZIRIMisc_MechResurrector_Mod(ModContentPack content)
            : base(content)
        {
            _settings = GetSettings<ZIRIMisc_MechResurrector_Settings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            ZIRIMisc_MechResurrector_Settings.DoSettingsWindowContents(inRect);
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "ZIRIMisc_MechResurrector".Translate();
        }
    }




}