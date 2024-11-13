﻿//using HarmonyLib;
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

        public int currentMaxResurrectCharges => ((int)this.parent.pawn.GetStatValue(StatDefOf.MechResurrectMaxChargePoint, true, -1));


        public void ResetCharges()
        {
           //Log.Message("ResetCharges called");
           //Log.Message("Old ResetCharges: " + resurrectCharges);
            this.resurrectCharges = this.currentMaxResurrectCharges;
           //Log.Message("new ResetCharges: " + resurrectCharges);

            Hediff hediff = this.parent.pawn.health.hediffSet.GetFirstHediffOfDef(ApocritonMechResurrector_HediffDefOf.ApocritonMechResurrector);
           //Log.Message("Show hediff Severity: " + hediff.Severity);
        }

        public override void Initialize(AbilityCompProperties props)
        {
            base.Initialize(props);
            this.ResetCharges();
            costsByWeightClass = new Dictionary<MechWeightClass, int>();
            for (int i = 0; i < Props.costs.Count; i++)
            {
                costsByWeightClass[Props.costs[i].weightClass] = Props.costs[i].cost;
            }
        }

        private int RemainedBandwith(Pawn pawn)
        {
            try
            {
                return pawn.mechanitor.TotalBandwidth - pawn.mechanitor.UsedBandwidth;
            }
            catch when (pawn.mechanitor == null)
            {
                //Log.Message("Mechanitor not found");
                return -99;
            }

        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
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
                Messages.Message("Insufficient Resurrection Charge Points", MessageTypeDefOf.NegativeEvent);
            }


            //mechResurrect can only be applied by following conditions
            if (!base.CanApplyOn(target, dest))
            {
                return false;
            }
            if (!target.HasThing || !(ifCorpse))
            {
                return false;
            }
            if (!CanResurrect(thisCorpse))
            {
                return false;
            }
            if (!checkIfBandwithEnough)
            {
                return false;
            }


            return true;
        }

        private bool TryGetResurrectCost(Corpse corpse, out int cost)
        {
            if (costsByWeightClass.ContainsKey(corpse.InnerPawn.RaceProps.mechWeightClass))
            {
                cost = costsByWeightClass[corpse.InnerPawn.RaceProps.mechWeightClass];
                return true;
            }
            cost = -1;
            return false;
        }

        private bool CanResurrect(Corpse corpse)
        {
            if (!corpse.InnerPawn.RaceProps.IsMechanoid || corpse.InnerPawn.RaceProps.mechWeightClass >= MechWeightClass.UltraHeavy)
            {
                return false;
            }
            if (corpse.InnerPawn.Faction != this.parent.pawn.Faction)
            {
                return false;
            }
            if (corpse.InnerPawn.kindDef.abilities != null && corpse.InnerPawn.kindDef.abilities.Contains(AbilityDefOf.ResurrectionMech))
            {
                return false;
            }
            if (corpse.timeOfDeath < Find.TickManager.TicksGame - Props.maxCorpseAgeTicks)
            {
                return false;
            }
            if (corpse.timeOfDeath < Find.TickManager.TicksGame - this.Props.maxCorpseAgeTicks)
            {
                return false;
            }
            if (!TryGetResurrectCost(corpse, out var cost) || cost > resurrectCharges)
            {
                return false;
            }
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
                ResurrectionUtility.TryResurrect(innerPawn);
                if (Props.appliedEffecterDef != null)
                {
                    Effecter effecter = Props.appliedEffecterDef.SpawnAttached(innerPawn, innerPawn.MapHeld);
                    effecter.Trigger(innerPawn, innerPawn);
                    effecter.Cleanup();
                    parent.pawn.relations.AddDirectRelation(PawnRelationDefOf.Overseer, innerPawn);//if resurrection successful, immediately takes control of resurrected mech
                    //Log.Message("resurrectCharges check: " + resurrectCharges);//error is resurrectcharges -> fixed

                }
                innerPawn.stances.stagger.StaggerFor(60);
            }

        }

        public override bool GizmoDisabled(out string reason)
        {
            reason = null;
            return false;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra() //charge display
        {
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
            foreach (LocalTargetInfo affectedTarget in this.parent.GetAffectedTargets(target))
            {
                Thing thing = affectedTarget.Thing;
                yield return MoteMaker.MakeAttachedOverlay(thing, ThingDefOf.Mote_MechResurrectWarmupOnTarget, Vector3.zero);
            }
            //yield break;
        }

        public override void PostApplied(List<LocalTargetInfo> targets, Map map)
        {
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

            if (remoteResurrectAbility == null)// find mech resurrect ability, if not found, add hediffAnother and stop, otherwise add hediff
            {
               //Log.Message("Didn't find RemoteResurrect ability");
               //Log.Message("AddHediff: " + Props.hediffDefAnother);
                user.health.AddHediff(Props.hediffDefAnother);
                return;
            }
            else
            {
               //Log.Message("Find Abilitiy: " + remoteResurrectAbility.def.defName);
               //Log.Message("AddHediff: " + Props.hediffDef);
                user.health.AddHediff(Props.hediffDef);
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
            Log.Message("CurrentSocialStateInternal called by: " + p.Name);

            Hediff firstHediffOfDef = p.health.hediffSet.GetFirstHediffOfDef(def.hediff);



            if (!p.RaceProps.Humanlike)
            {
                return false;
            }
            if (!RelationsUtility.PawnsKnowEachOther(p, other))
            {
                return false;
            }
            if (other.def != p.def)
            {
                return false;
            }
            //new condition for find Hediff
            if (firstHediffOfDef == null)
            {
                //Log.Message("Hediff not found in ThoughtWorker_disdainOrganism_Hediff.");
                return false;
            }

            bool ifOtherIsMechanitor = IfMechanitor(other);

            if (ifOtherIsMechanitor)
            {
                return ThoughtState.ActiveAtStage(0);
            }


            int level = (int)firstHediffOfDef.Severity;
            if (level == 0)
            {
                //Log.Message("Hediff found but Severity is 0.");
                return false;
            }
            else if (level == 1)//((int)p.GetStatValue(StatDefOf.MechResurrectMaxChargePoint, true, -1) == 1)
            {
                //Log.Message("display message level:1");
                return ThoughtState.ActiveAtStage(1);
            }

            else if (level == 2)
            {
                //Log.Message("display message level:2");
                return ThoughtState.ActiveAtStage(2);
            }

            else if (level == 3)
            {
                //Log.Message("display message level:3");
                return ThoughtState.ActiveAtStage(3);
            }

            return true;
        }

        public bool IfMechanitor(Pawn pawn)
        {
            if (ModsConfig.BiotechActive && pawn.health?.hediffSet != null)
            {
                
                return pawn.health.hediffSet.HasHediff(HediffDefOf.MechlinkImplant);
            }
            return false;
        }




        public override string PostProcessDescription(Pawn p, string description)
        {
            string text = base.PostProcessDescription(p, description);
            Hediff firstHediffOfDef = p.health.hediffSet.GetFirstHediffOfDef(def.hediff);
            if (firstHediffOfDef == null || !firstHediffOfDef.Visible)
            {
                return text;
            }
            return text + "\n\n" + "CausedBy".Translate() + ": " + firstHediffOfDef.LabelBase.CapitalizeFirst();
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

                await Task.Delay(50);//have to wait for value assigned to the ability, then reset the charges
                comp.ResetCharges();
                return;
            }

            //Log.Message("CompAbilityEffect_MechanitorResurrectMech Not found!!!");

        }
    }





}