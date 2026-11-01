using NUnit.Framework;
using UnityEngine;

public class PlayerPerksTests
{
    [TestCase(.1f, ChestReward.Cash)]
    [TestCase(.5f, ChestReward.FreeUpgrade)]
    [TestCase(.65f, ChestReward.NoAttacks)]
    [TestCase(.75f, ChestReward.Mobility)]
    [TestCase(.85f, ChestReward.InfiniteStamina)]
    [TestCase(.95f, ChestReward.DoubleSellCash)]
    public void PoolIncludesEveryReward(float roll, ChestReward expected)
    {
        Assert.That(PlayerPerks.Roll(roll), Is.EqualTo(expected));
    }
    [Test] public void CashAndVoucherRewardsChangeOnlyTheirOwnBalance()
    {
        var data = new PlayerData { cash = 20 };
        Assert.IsTrue(PlayerPerks.Grant(data, ChestReward.Cash, 500, 300, 1000));
        Assert.That(data.cash, Is.EqualTo(520));
        Assert.IsTrue(PlayerPerks.Grant(data, ChestReward.FreeUpgrade, 500, 300, 1000));
        Assert.That(data.freeUpgradeVouchers, Is.EqualTo(1));
        Assert.That(data.cash, Is.EqualTo(520));
    }
    [TestCase(ChestReward.NoAttacks)]
    [TestCase(ChestReward.Mobility)]
    [TestCase(ChestReward.InfiniteStamina)]
    [TestCase(ChestReward.DoubleSellCash)]
    public void TimedRewardsPersistAndExtendWithoutChangingStrength(ChestReward reward)
    {
        var data = new PlayerData();
        Assert.IsTrue(PlayerPerks.Grant(data, reward, 500, 300, 1000));
        Assert.That(Expiry(data, reward), Is.EqualTo(1300));
        data = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(data));
        Assert.That(Expiry(data, reward), Is.EqualTo(1300));
        PlayerPerks.Grant(data, reward, 500, 300, 1050);
        Assert.That(Expiry(data, reward), Is.EqualTo(1600));
        Assert.IsTrue(PlayerPerks.Active(1600, 1599));
        Assert.IsFalse(PlayerPerks.Active(1600, 1600));
    }
    [Test] public void DoubleSalesPayExactlyTwiceTheDisplayedRoundedBaseValue()
    {
        Assert.That(PlayerPerks.SellCash(1.6, 2), Is.EqualTo(2 * PlayerPerks.SellCash(1.6, 1)));
        Assert.That(PlayerPerks.SellCash(int.MaxValue, 2), Is.EqualTo(int.MaxValue));
    }
    [Test] public void LegacySavesStartWithoutPerks()
    {
        var data = JsonUtility.FromJson<PlayerData>("{\"cash\":100}");
        Assert.That(data.noAttacksUntilUtc, Is.Zero);
        Assert.That(data.mobilityUntilUtc, Is.Zero);
        Assert.That(data.doubleSellCashUntilUtc, Is.Zero);
        Assert.IsFalse(PlayerPerks.Active(0, 1000));
    }
    private static long Expiry(PlayerData data, ChestReward reward)
    {
        switch (reward)
        {
            case ChestReward.NoAttacks: return data.noAttacksUntilUtc;
            case ChestReward.Mobility: return data.mobilityUntilUtc;
            case ChestReward.InfiniteStamina: return data.infiniteStaminaUntilUtc;
            default: return data.doubleSellCashUntilUtc;
        }
    }
}
