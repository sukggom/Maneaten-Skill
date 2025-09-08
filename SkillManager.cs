using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UG.Framework;
using System;

public struct SkillKey
{
    public static SkillKey None = new SkillKey(-1);

    private int Key;

    public SkillKey(int InKey)
    {
        Key = InKey;
    }

    public int GetKey()
    {
        return Key;
    }

    public bool isValid()
    {
        return Key > 0;
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (!(obj is SkillKey))
        {
            return false;
        }

        SkillKey other = (SkillKey)obj;
        return this.Key == other.Key;
    }

    public static bool operator ==(SkillKey a, SkillKey b)
    {
        if (a.Key.Equals(b.Key))
        {
            return true;
        }

        return false;
    }

    public static bool operator !=(SkillKey a, SkillKey b)
    {
        if (a.Key.Equals(b.Key))
        {
            return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        return Key.GetHashCode();
    }
}

public class SkillPool
{
    private Queue<BaseSkill> PoolList = new Queue<BaseSkill>();

    public BaseSkill GetSkill()
    {
        if (isValidPool())
        {
            return PoolList.Dequeue();
        }
        else
        {
            return null;
        }
    }

    public void Release()
    {
        while(PoolList.Count > 0)
        {
            var Skill = PoolList.Dequeue();
            Skill.Release();
            Skill = null;
        }
    }


    public void AddPool(BaseSkill InSkill)
    {
        PoolList.Enqueue(InSkill);
    }


    private bool isValidPool()
    {
        return PoolList.Count > 0;
    }
}


public interface ISKillEndEvent
{
    void End(BaseSkill InSkill);
}


public class SkillManager : ISKillEndEvent , IStaticUnInitializer
{
    private static SkillManager manager;
    public static SkillManager Manager
    {
        get
        {
            if (null != manager)
            {
                return manager;
            }
            else
            {
                manager = new SkillManager();
                manager.Initialize();
                UFramework.AddStaticManager(manager);
                return manager;
            }
        }
    }


    private static int SkillKeyCode = 100;

    public static float UnityFrame = 30.0f;

    private Dictionary<int, SkillPool> SkillList = new Dictionary<int, SkillPool>();

    private Dictionary<SkillKey, BaseSkill> SkillPlayList = new Dictionary<SkillKey, BaseSkill>();

    private Queue<SkillKey> KeyPool = new Queue<SkillKey>(256);

    public void Initialize()
    {
    }

    public void StopPlayList()
    {
        List<SkillKey> KeyList = new List<SkillKey>(SkillPlayList.Keys);

        for (int i = KeyList.Count - 1; i >= 0; i--)
        {
            SkillPlayList[KeyList[i]].End();
        }

        SkillPlayList.Clear();
    }

    public void PreUnInitialize()
    {
        StopPlayList();

        var SkillEnumerator = SkillList.GetEnumerator();
        while (SkillEnumerator.MoveNext())
        {
            SkillEnumerator.Current.Value.Release();
        }

        SkillList.Clear();
        KeyPool.Clear();
    }


    public void UnInitialized()
    {
        manager = null;
    }

    public static SkillKey GenerateKey()
    {
        return new SkillKey(++SkillKeyCode);
    }

    public bool IsPlaySkill(SkillKey InKey)
    {
        if (SkillPlayList.ContainsKey(InKey))
        {
            return true;
        }

        return false;
    }

    public void CastSkill(
        BaseActor InCaster,
        int InIndex, 
        int InLevel,
        Action<SkillKey, int> InPlayCallBack,
        Action<SkillKey> InEndCallBack,
        BaseActor InOwner = null)
    {
        var Skill = GetSkill(InIndex, InLevel);

        if(Skill != null)
        {
            SkillKey Key = GetKey();
            SkillPlayList.Add(Key, Skill);
            InPlayCallBack?.Invoke(Key, InIndex);
            UFramework.GetWorker().Do(Skill);

            Skill.PlaySkill(Key, InCaster, InEndCallBack, this, InOwner);

        }
    }

    private SkillKey GetKey()
    {
        SkillKey Key;
        if(KeyPool.Count > 0)
        {
            Key = KeyPool.Dequeue();
        }
        else
        {
            Key = GenerateKey();
        }

        return Key;
    }

    private BaseSkill GetSkill(int InIndex, int InLevel)
    {
        SkillPool Pool = null;
        if (SkillList.TryGetValue(InIndex, out Pool))
        {
            if (Pool == null)
            {
                Pool = new SkillPool();
            }
        }
        else
        {
            //ULogger.Log($"Alloc SkillPool Dictionary {InIndex}");

            Pool = new SkillPool();
            SkillList.Add(InIndex, Pool);
        }

        BaseSkill Skill = Pool.GetSkill();

        if(null == Skill)
        {
            return CreateSkill(InIndex, InLevel);
        }
        else
        {
            Skill.SetLevel(InLevel);
        }

        return Skill;
    }

    public void StopSkill(SkillKey InKey)
    {
        BaseSkill Skill = null;
        if(SkillPlayList.TryGetValue(InKey, out Skill))
        {
            Skill.Stop();
        }
    }

    public void CoolTimeReset(SkillKey InKey)
    {
        BaseSkill Skill = null;
        if (SkillPlayList.TryGetValue(InKey, out Skill))
        {
            Skill.CoolTimeReset();
        }
    }

    public bool GetDisableSkillCheck(SkillKey InKey)
    {
        BaseSkill Skill = null;
        if (SkillPlayList.TryGetValue(InKey, out Skill))
        {
            if(Skill.IsCoolTime() == false)
            {
                return Skill.SkillType != eSkillType.Buff || Skill.SkillType != eSkillType.BossActive;
            }
        }

        return false;


    }

    public bool IsCoolTimeSkill(SkillKey InKey)
    {
        BaseSkill Skill = null;
        if (SkillPlayList.TryGetValue(InKey, out Skill))
        {
            return Skill.IsCoolTime();
        }

        return false;
    }

    public void Resume(SkillKey InKey)
    {
        BaseSkill Skill = null;
        if (SkillPlayList.TryGetValue(InKey, out Skill))
        {
            Skill.Resume();
        }
    }

    public void ForcedEndSkill(SkillKey InKey)
    {
        BaseSkill Skill = null;
        if (SkillPlayList.TryGetValue(InKey, out Skill))
        {
            Skill.End();
        }
    }

    public void CallTrigger(SkillKey InKey, eSkillTriggerType InTriggerType, int InValue, long InValue2)
    {
        BaseSkill CurrentSkill = null;
        if(SkillPlayList.TryGetValue(InKey, out CurrentSkill))
        {
            if(null != CurrentSkill)
            {
                CurrentSkill.ReceiveTrigger(InKey, InTriggerType, InValue, InValue2);
            }
        }
    }


    public void End(BaseSkill InSkill)
    {
        if (SkillPlayList.ContainsKey(InSkill.MyPoolKey))
        {
            SkillPlayList.Remove(InSkill.MyPoolKey);
        }

        EnqueueSkill(InSkill);
        KeyPool.Enqueue(InSkill.MyPoolKey);
        UFramework.GetWorker().Remove(InSkill);
    }

    private void EnqueueSkill(BaseSkill InSkill)
    {
        if (SkillList.ContainsKey(InSkill.SkillIndex))
        {
            var Pool = SkillList[InSkill.SkillIndex];

            if(null == Pool)
            {
                Pool = new SkillPool();
            }

            Pool.AddPool(InSkill);
        }
    }


    //사용할스킬을 미리만들어둔다.
    public void Apply_CreateSkill(int InSkillIndex, int InPoolCount)
    {
        for (int i = 0; i < InPoolCount; i++)
        {
            BaseSkill Skill = CreateSkill(InSkillIndex, 1);

            if (!SkillList.ContainsKey(InSkillIndex))
            {
                SkillList.Add(InSkillIndex, new SkillPool());
            }
            SkillList[InSkillIndex].AddPool(Skill);
        }
    }

    private BaseSkill CreateSkill(int InSkillIndex , int InLevel)
    {
        BaseSkill Skill = null;
        using (var DataTrunk = GameRecord.GetSkillRecord(InSkillIndex))
        {
            if (null != DataTrunk)
            {
                var SkillRecord = DataTrunk.GetValue();
                Type classType = Type.GetType(SkillRecord.GetClassName());
                Skill = Activator.CreateInstance(classType) as BaseSkill;
                Skill.RegistSkill(InSkillIndex, InLevel);
            }
            else
            {
                ULogger.Error($"{InSkillIndex} is SkillRecord Null");
            }
        }

        return Skill;
    }


#if UNITY_EDITOR
    public void DEBUG_CoolTimeReset()
    {
        List<SkillKey> KeyList = new List<SkillKey>(SkillPlayList.Keys);

        for (int i = KeyList.Count - 1; i >= 0; i--)
        {
            if (SkillPlayList[KeyList[i]].IsCoolTime())
            {
                SkillPlayList[KeyList[i]].CoolTimeReset();
            }
        }
    }

#endif

    public void ChangingScene()
    {
        StopPlayList();
    }
}
