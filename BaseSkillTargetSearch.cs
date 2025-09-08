using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UG.Framework;
using ActorDictionary = System.Collections.Generic.Dictionary<long, UG.Framework.UActorObject>;

public abstract partial class BaseSkill
{
    protected float CalActiveRadius = 1f;

    protected List<long> DamagedTargetList = new List<long>();

    public IEnumerator<BaseActor> GetActors() 
    {
        return UActor.Manager.Acquire<BaseActor>();
    }

    public virtual bool BaseScanTarget(BaseActor InTarget, float InTargetRadius, float InCasterRadius)
    {
#if DRAW_DEBUG
        UDebugHelper.DrawCircle_XZ(Caster.Value.GetPosition(), InCasterRadius);
#endif
        return UIntersect.IntersectSphereSphere(Caster.Value.GetPosition()
                  , InCasterRadius
                  , InTarget.GetPosition()
                  , InTargetRadius);
    }

    protected BaseActor CheckTarget(BaseActor TargetActor)
    {
        if (TargetActor == null)
        {
            return null;
        }

        if (TargetActor == Caster.Value)
        {
            return null;
        }

        if ((GetMyCollideeLayer() & TargetActor.GetColliderLayer()) <= 0)
        {
            return null;
        }

        if (TargetActor.IsInValidTarget())
        {
            return null;
        }

        if (TargetActor.EnableController() == false)
        {
            return null;
        }

        return TargetActor;
    }

    protected void BaseSearchTarget(float CasterRadius)
    {
        var Actors = GetActors();
        while (Actors.MoveNext())
        {
            var TargetActor = CheckTarget(Actors.Current);

            if(TargetActor == null)
            {
                continue;
            }

            if (DamagedTargetList.Contains(TargetActor.GetActorID()))
            {
                continue;
            }

            float TargetRadius = TargetActor.GetController().GetRadius();

            if (BaseScanTarget(TargetActor, TargetRadius, CasterRadius))
            {
                DamagedTargetList.Add(TargetActor.GetActorID());
                BaseAttackTarget(TargetActor);

                if (SearchEnd())
                {
                    return;
                }
            }
        }
    }




    protected virtual int GetMyCollideeLayer()
    {
        return Caster.Value.GetCollideeLayer();
    }

    protected virtual void BaseAttackTarget(BaseActor InTarget)
    {

    }

    protected float GetMyStatusAddActiveRadius(BaseActor InActor)
    {
        return InActor.GetStatRef().GetActiveRadius();
    }

    protected virtual bool SearchEnd()
    {
        return false;
    }

    public bool GetTriggerRate(float InValue)
    {
        return InValue > Random.Range(0, 10000);

    }

    public bool UseTriggerFlag()
    {
        return (eSkillTriggerType.None & ReceiveTriggerType) == 0;
    }

}
