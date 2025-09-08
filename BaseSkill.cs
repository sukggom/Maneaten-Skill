using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UG.Framework;
using System;
using ActorDictionary = System.Collections.Generic.Dictionary<long, UG.Framework.UActorObject>;
using SKillTrunk = UG.Framework.UDataRecord<SkillRecord>;

public abstract partial class BaseSkill : IWork
{
    protected FWeakPtr<BaseActor> CastingRealOwner; //������ ����������, ���� �����ϵ��� �����ߴ� ���. ���ڽ��̵ɼ��� ���̵ɼ�������
    public FWeakPtr<BaseActor> Caster; // ��ų�� ������ ��ü

    public int SkillIndex { get; private set; }
    protected int CurrentLevel;
    public eSkillCategory SkillCateogry { get; private set; } = eSkillCategory.NONE;
    public eSkillType SkillType { get; private set; } = eSkillType.Active;
    protected SKillTrunk DataTrunk;

    //protected SkillDataRecord DataRecord;

    ////�ӽ�..
    //protected SkillActionData[] PrivateSkillData;
    //protected SkillActionData CurrentSkillData;

    protected bool Pause = false;
    protected bool NeedUpdate = false;
    protected bool UpdateEnd = false;
    protected float UpdateTimer = 0.0f;
    protected float Frame = 0f;
    protected float Elipse = 0.001f;
    protected bool bCheckCoolTime = false;
    protected eSkillTriggerType ReceiveTriggerType = eSkillTriggerType.None;

    protected float CoolTimeChecker = 0f; //��Ÿ�Ӵ���

    private Action<SkillKey> EndCallBack = null;
    public SkillKey MyPoolKey = SkillKey.None;
    private ISKillEndEvent EndEvent;

    protected List<OneOffSkillActionData> StartFrameList = new List<OneOffSkillActionData>();
    protected List<bool> CurrentStartFrameCheckList = new List<bool>();
    protected bool bRepeat = false;
    //private Dictionary<OneOffSkillActionData, bool> StartFrameList = new Dictionary<OneOffSkillActionData, bool>();

    public void RegistSkill(int InSkillIndex, int InLevel = 1)
    {
        DataTrunk = GameRecord.GetSkillRecord(InSkillIndex);

        if (null != DataTrunk)
        {
            var Skill = DataTrunk.GetValue();
            SkillIndex = Skill.GetSkillIndex();
            CurrentLevel = InLevel;
            SkillCateogry = Skill.GetSkillCategory();
            SkillType = Skill.GetSkillDataRecord().GetSkillType();
            bRepeat = Skill.GetSkillDataRecord().GetRepeat();
            ReceiveTriggerType = Skill.GetSkillDataRecord().CreateTriggerType();

            RegistStartFrameDataList();

            SetCurrentLevelData();
        }
    }

    protected abstract void ClearCustomStatus();

    public void ClearStatus()
    {
        ClearCustomStatus();
        Pause = false;
        NeedUpdate = false;
        UpdateTimer = 0f;
        Frame = 0f;
        UpdateEnd = false;

        Caster.Clear();
        CastingRealOwner.Clear();

        CoolTimeChecker = 0f;
        bCheckCoolTime = false;
        MyPoolKey = SkillKey.None;
        EndCallBack = null;
        CurrentStartFrameCheckList.Clear();
        EndEvent = null;
        DamagedTargetList.Clear();
        CalActiveRadius = 1f;

    }

    public void Release()
    {
        DataTrunk.Dispose();
        ClearStatus();

        if (UFramework.GetWorker().HasAction(this))
        {
            UFramework.GetWorker().Remove(this);
        }
    }

    protected void PlayRepeat()
    {
        PlaySkill(MyPoolKey, Caster.Value, EndCallBack, EndEvent, CastingRealOwner.Value);
    }

    public void PlaySkill(SkillKey InKey,  BaseActor InCaster , Action<SkillKey> InEndCallBack , ISKillEndEvent InEnvet, BaseActor InOwner = null)
    {
        ClearStatus();
        EndCallBack = InEndCallBack;
        CreateCheckList();
        NeedUpdate = true;
        MyPoolKey = InKey;
        EndEvent = InEnvet;

        Caster.Set(InCaster);
        if (InOwner != null)
        {
            CastingRealOwner.Set(InOwner);
        }
        else
        {
            CastingRealOwner.Set(Caster.Value);
        }

        PlayStart();
    }

    private void CreateCheckList()
    {
        for(int i =0; i < StartFrameList.Count; ++i)
        {
            CurrentStartFrameCheckList.Add(false);
        }
    }

    private void RegistStartFrameDataList()
    {
        StartFrameList.Clear();
        RegistStartFrameDatas(GetCurrentSkillDataRecord().GetAnimationDatas());
        RegistStartFrameDatas(GetCurrentSkillDataRecord().GetSoundDatas());
        RegistStartFrameDatas(GetCurrentSkillDataRecord().GetSpawnFXDatas());
    }

    protected SkillDataRecord GetCurrentSkillDataRecord()
    {
        return DataTrunk.GetValue().GetSkillDataRecord();
    }

    private void RegistStartFrameDatas<T>(T[] InDatas) where T : OneOffSkillActionData
    {
        if(InDatas != null && InDatas.Length > 0)
        {
            foreach (var Data in InDatas)
            {
                StartFrameList.Add(Data);
            }
        }
    }


    public virtual void End()
    {
        NeedUpdate = false;
        UpdateEnd = true;
        EndEvent?.End(this);
        EndCallBack?.Invoke(MyPoolKey);
    }

    public virtual bool isRepeat()
    {
        return bRepeat;
    }

    public virtual void Stop()
    {
    }

    public void ReceiveTrigger(SkillKey InKey, eSkillTriggerType InTriggerType, int InValue, long InValue2)
    {
        if(InKey != MyPoolKey)
        {
            return;
        }

        if((InTriggerType & ReceiveTriggerType) != 0)
        {
            ReceiveTrigger(InValue, InValue2);
        }
    }

    protected virtual void ReceiveTrigger(int InValue, long InValue2)
    {

    }

    public bool isEnd()
    {
        return UpdateEnd;
    }


    public virtual void SetLevel(int InLevel)
    {
        CurrentLevel = InLevel;
        SetCurrentLevelData();
    }

    protected abstract void SetCurrentLevelData();


    protected abstract void PlayStart();

    public bool IsValid()
    {
        return NeedUpdate;
    }

    void IWork.FixedUpdate(float InDeltaTime)
    {
        if (CastingRealOwner.IsValid() == false || CastingRealOwner.Value.IsInValidTarget())
        {
            End();
            return;
        }

        if (Pause)
        {
            UpdatePauseTime(InDeltaTime);
            return;
        }

        if (!IsValid())
        {
            if (bCheckCoolTime)
            {
                if (GetCoolTime() > CoolTimeChecker)
                {
                    CoolTimeChecker += InDeltaTime;
                }
                else
                {
                    if (!isRepeat())
                    {
                        End();
                    }
                    else
                    {
                        PlayRepeat();
                    }
                }
            }
            
            return;
        }

        UpdateTimer += InDeltaTime;

        FixedUpdateStartFrameList();

        FixedUpdate(InDeltaTime);

        if (!IsInfinite())
        {
            if (Frame >= GetTotalFrame())
            {
                OnCoolTime();
            }
        }
    }

    protected void OnCoolTime()
    {
        NeedUpdate = false;
        bCheckCoolTime = true;
        StartCoolTime();
    }

    protected virtual void UpdatePauseTime(float InDeltaTime)
    {

    }
    public bool IsCoolTime()
    {
        return bCheckCoolTime;
    }

    public void CoolTimeReset()
    {
        if(bCheckCoolTime && isRepeat())
        {
            PlayRepeat();
        }
    }

    public virtual void Resume()
    {
        Pause = false;
    }

    protected virtual float GetCoolTime()
    {
        if (Caster.IsValid())
        {
            return DataTrunk.GetValue().GetSkillDataRecord().GetCoolTime() * Caster.Value.GetStatRef().SkillCoolTimeReduceRate();
        }
        else
        {
            return DataTrunk.GetValue().GetSkillDataRecord().GetCoolTime();
        }
    }

    protected virtual void FixedUpdateStartFrameList(float InAccelateValue = 1.0f , bool DoPlay = true)
    {
        Frame = UpdateTimer * SkillManager.UnityFrame + Elipse;

        for (int i = StartFrameList.Count - 1; i >= 0; i--)
        {
            if (CurrentStartFrameCheckList[i])
            {
                continue;
            }

            if (Frame > StartFrameList[i].GetStartFrame() / InAccelateValue)
            {
                if (DoPlay)
                {
                    StartFrameList[i].Play(Caster.Value, CurrentLevel, InAccelateValue);
                }
                CurrentStartFrameCheckList[i] = true;
            }
        }
    }

    protected void ResetStartFrameCheckList(int InOrder)
    {
        for(int i =0; i < StartFrameList.Count; ++i)
        {
            if(StartFrameList[i].GetOrderIndex() >= InOrder)
            {
                CurrentStartFrameCheckList[i] = false;
            }
        }
    }

    private bool IsInfinite()
    {
        switch (GetCurrentSkillDataRecord().GetSkillType()) 
        {
            case eSkillType.Passive:
            case eSkillType.Summon:
                {
                    return true;
                }
        }

        return false;
    }

    protected virtual float GetTotalFrame()
    {
        return GetCurrentSkillDataRecord().GetTotalFrame();
    }

    /// <summary>
    /// Woker�� FixedTime�� ����� �ð�������
    /// </summary>
    /// <param name="InDeltaTime"></param>
    protected abstract void FixedUpdate(float InDeltaTime);


    /// <summary>
    /// ĳ������ ������ ǥ���ҋ� ����� , Fixed�δ� ���ڿ������� ȭ���� �߻���
    /// ������ �����������
    /// </summary>
    /// <param name="InDeltaTime"></param>
    public virtual void Update(float InDeltaTime)
    {
    }

    protected virtual void StartCoolTime()
    {

    }

    protected void PlayCustomAudio(string InAudioID)
    {
        if (!string.IsNullOrEmpty(InAudioID))
        {
            UAudio.Manager.PlaySound(InAudioID);
        }
    }

    //���� ������ ���� ��ų
    protected virtual void DoImpact(BaseActor Target, ImpactData InImpactData)
    {
        UScene.Manager.GetInstance<FormulaManager>().DoImpact(MyPoolKey, Caster.Value, InImpactData, Target, SkillCateogry, 0);
    }
}
