using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UG.Framework;

[System.Serializable]
public class BombSkillData : SkillActionData
{
    public float Search_Radius;
    public float Active_Radius;
    public float SpeedRate;
    public float Height = 5f;
    public string SpawnFX_Name;
}


public class BombSkill : BaseSkill
{
    BombSkillData CurrentSkillData;

    Vector3 TargetPos = Vector3.zero;
    float Distance = float.MaxValue;
    Vector3 StartPos;

    Vector3 Direction;

    float Spin = 0f;
    float SpinMax = 360f;

    protected override void ClearCustomStatus()
    {
    }

    protected override void FixedUpdate(float InDeltaTime)
    {
        if (Frame > 0)
        {
            float VelocityFrame = Frame / GetTotalFrame();
            float parabolicT = VelocityFrame * 2 - 1;
            float yOffset = CurrentSkillData.Height * (1 - parabolicT * parabolicT);
            Vector3 Pos = Vector3.Lerp(StartPos, TargetPos, VelocityFrame);
            Caster.Value.SetPosition(new Vector3(Pos.x, Pos.y + yOffset, Pos.z));

            if (Spin > SpinMax)
            {
                Spin -= SpinMax;
            }

            Spin += 45f;

            Caster.Value.SetLocalEulerAngles(Direction * Spin);
        }
        else
        {
            Caster.Value.SetPosition(StartPos);
        }

#if DRAW_DEBUG

        UDebugHelper.DrawCircle_XZ(Caster.Value.GetPosition(), CurrentSkillData.Active_Radius * Caster.Value.GetStatRef().GetActiveRadius());

#endif

        if (Frame >= GetTotalFrame())
        {
            UFX.Manager.SpawnFX(
                new UAssetID(CurrentSkillData.SpawnFX_Name), 
                null,
                Caster.Value.GetPosition(), 
                Quaternion.identity, 
                Vector3.one * Caster.Value.GetStatRef().GetActiveRadius());
            BaseSearchTarget(CalActiveRadius);
        }
    }

    public override bool BaseScanTarget(BaseActor InTarget, float InTargetRadius, float InCasterRadius)
    {
        return UIntersect.IntersectSphereSphere(Caster.Value.GetPosition()
                    , CalActiveRadius
                    , InTarget.GetPosition()
                    , InTargetRadius);
    }

    protected override void BaseAttackTarget(BaseActor InTarget)
    {
        UScene.Manager.GetInstance<FormulaManager>().DoImpact(MyPoolKey, Caster.Value, CurrentSkillData.GetImpactDatas(), InTarget, SkillCateogry);
    }

    protected override void PlayStart()
    {
        CalActiveRadius = CurrentSkillData.Active_Radius * GetMyStatusAddActiveRadius(Caster.Value);

        Distance = CurrentSkillData.Search_Radius * CurrentSkillData.Search_Radius;

#if DRAW_DEBUG
        UDebugHelper.DrawCircle_XZ(Caster.Value.GetPosition(), CurrentSkillData.Search_Radius);
#endif

        float InitRanRadius = CurrentSkillData.Active_Radius * 5;
        TargetPos = new Vector3(
            Caster.Value.GetPosition().x + Random.Range(-InitRanRadius, InitRanRadius), 
            0.1f, 
            Caster.Value.GetPosition().z + Random.Range(-InitRanRadius, InitRanRadius));
        Direction = Vector3.one;
        StartPos = Caster.Value.GetPosition();


        var Actors = GetActors();
        while (Actors.MoveNext())
        {
            var TargetActor = CheckTarget(Actors.Current);

            if (TargetActor == null)
            {
                continue;
            }

            float CurrentDistance = (StartPos - TargetActor.GetPosition()).sqrMagnitude;

            if (CurrentDistance < Distance)
            {
                Distance = CurrentDistance;
                TargetPos = TargetActor.GetPosition();

                Direction = (Caster.Value.GetPosition() - TargetPos).normalized;
            }
        }

    }

    protected override void SetCurrentLevelData()
    {
        CurrentSkillData = GetCurrentSkillDataRecord().GetCurrentLevelSkillData<BombSkillData>(CurrentLevel);
    }

    public override void End()
    {
        base.End();
    }

    protected override float GetTotalFrame()
    {
        return base.GetTotalFrame() / CurrentSkillData.SpeedRate;
    }
}
