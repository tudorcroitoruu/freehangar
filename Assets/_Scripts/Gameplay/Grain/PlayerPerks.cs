using System;
using UnityEngine;

public enum ChestReward { Cash, FreeUpgrade, NoAttacks, Mobility, InfiniteStamina, DoubleSellCash }

/// <summary>Saved UTC-expiry perks. Repeated rewards extend duration, never multiply their strength.</summary>
public static class PlayerPerks
{
    public const int DefaultDurationSeconds=300;
    public static long Now => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    public static bool NoAttacksActive => Active(SaveSystem.Data?.noAttacksUntilUtc ?? 0,Now);
    public static float MobilityMultiplier =>
        (1f + StaminaUpgradeSettings.MobilityBonusPercent(SaveSystem.Data?.staminaUpgradeLevel ?? 0) / 100f) *
        (Active(SaveSystem.Data?.mobilityUntilUtc ?? 0,Now) ? 1.25f : 1f);
    public static int SellMultiplier => Active(SaveSystem.Data?.doubleSellCashUntilUtc ?? 0,Now)?2:1;
    public static int SellCash(double baseValue,int multiplier) => (int)Math.Min(int.MaxValue,(long)EconomyNumberFormat.SaleCash(baseValue)*Math.Max(1,multiplier));
    public static bool Active(long until,long now) => until>now;
    public static long Extend(long until,long now,int seconds) => Math.Min(long.MaxValue-Math.Max(1,seconds),Math.Max(now,until))+Math.Max(1,seconds);
    public static ChestReward Roll(float value)
    {
        value=Mathf.Clamp01(value);
        if(value<.45f)return ChestReward.Cash;
        if(value<.60f)return ChestReward.FreeUpgrade;
        if(value<.70f)return ChestReward.NoAttacks;
        if(value<.80f)return ChestReward.Mobility;
        if(value<.90f)return ChestReward.InfiniteStamina;
        return ChestReward.DoubleSellCash;
    }
    public static bool Grant(PlayerData data,ChestReward reward,int cash,int seconds,long now)
    {
        if(data==null)return false;
        switch(reward)
        {
            case ChestReward.Cash:
                if(cash<=0 || data.cash>int.MaxValue-cash)return false;
                data.cash+=cash;break;
            case ChestReward.FreeUpgrade:
                if(data.freeUpgradeVouchers==int.MaxValue)return false;
                data.freeUpgradeVouchers++;break;
            case ChestReward.NoAttacks:data.noAttacksUntilUtc=Extend(data.noAttacksUntilUtc,now,seconds);break;
            case ChestReward.Mobility:data.mobilityUntilUtc=Extend(data.mobilityUntilUtc,now,seconds);break;
            case ChestReward.InfiniteStamina:data.infiniteStaminaUntilUtc=Extend(data.infiniteStaminaUntilUtc,now,seconds);break;
            case ChestReward.DoubleSellCash:data.doubleSellCashUntilUtc=Extend(data.doubleSellCashUntilUtc,now,seconds);break;
            default:return false;
        }
        return true;
    }
    public static string Receipt(ChestReward reward,int cash,int seconds)
    {
        string duration=seconds%60==0?$"{seconds/60} MIN":$"{seconds} SEC";
        switch(reward)
        {
            case ChestReward.Cash:return $"+${EconomyNumberFormat.Accounting(cash)}";
            case ChestReward.FreeUpgrade:return "FREE UPGRADE VOUCHER!";
            case ChestReward.NoAttacks:return "NO ATTACKS - "+duration;
            case ChestReward.Mobility:return "+25% MOVEMENT SPEED - "+duration;
            case ChestReward.InfiniteStamina:return "INFINITE STAMINA - "+duration;
            default:return "2X SELL CASH - "+duration;
        }
    }
}
