using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class EconomyNumberFormatTests
{
    [TestCase("ro-RO")]
    [TestCase("de-DE")]
    [TestCase("fr-FR")]
    public void AccountingUsesCommaThousandsAndNoDecimalsRegardlessOfLocale(string locale)
    {
        var previous=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo(locale);
            Assert.That(EconomyNumberFormat.Accounting(12500.25),Is.EqualTo("12,500"));
            Assert.That(EconomyNumberFormat.Accounting(-12500),Is.EqualTo("-12,500"));
        }
        finally { CultureInfo.CurrentCulture=previous; }
    }

    [Test]
    public void DisplayNeverRoundsAnIncompleteBagOrFractionalCashUp()
    {
        Assert.That(EconomyNumberFormat.Accounting(9.999f),Is.EqualTo("9"));
        Assert.That(EconomyNumberFormat.Accounting(10),Is.EqualTo("10"));
        Assert.That(EconomyNumberFormat.Accounting(12500.99),Is.EqualTo("12,500"));
        Assert.That(EconomyNumberFormat.Accounting(.99),Is.EqualTo("0"));
    }

    [Test]
    public void SaleTotalEqualsTheWholeDollarCropRowsWithoutHiddenFractionalCarry()
    {
        double first=EconomyNumberFormat.CropValue(12,2.4f),second=EconomyNumberFormat.CropValue(6,2.6f);
        Assert.That(EconomyNumberFormat.SaleCash(first),Is.EqualTo(28));
        Assert.That(EconomyNumberFormat.SaleCash(second),Is.EqualTo(15));
        Assert.That(EconomyNumberFormat.SaleTotal(new[]{first,second}),Is.EqualTo(43));
        Assert.That(EconomyNumberFormat.SaleCash(EconomyNumberFormat.CropValue(50,2.6f)),Is.EqualTo(130));
    }

    [Test]
    public void InvalidOrOverflowingQuotesCannotBePartiallyPaid()
    {
        Assert.That(EconomyNumberFormat.SaleTotal(new[]{10d,(double)int.MaxValue}),Is.Zero);
        Assert.That(EconomyNumberFormat.SaleTotal(new[]{10d,(double)int.MaxValue+1}),Is.Zero);
        Assert.That(EconomyNumberFormat.SaleTotal(new[]{10d,double.NaN}),Is.Zero);
        Assert.That(EconomyNumberFormat.SaleTotal(new[]{10d,double.PositiveInfinity}),Is.Zero);
        Assert.That(EconomyNumberFormat.SaleTotal(new[]{10d,-1d}),Is.Zero);
        Assert.That(EconomyNumberFormat.SaleCash(.99),Is.Zero);
    }

    [Test]
    public void SellingRowUsesAccountingAndTheActualWholeDollarPayout()
    {
        var root=new GameObject("Accounting sale row");
        try
        {
            var row=root.AddComponent<SellGrainElement>();
            Text quantity=ChildText(root,"Quantity"),price=ChildText(root,"Price"),value=ChildText(root,"Value");
            Set(row,"grainQuantity",quantity);Set(row,"grainPrice",price);Set(row,"grainValue",value);
            row.SetData(new GrainElementSettings{grainType=GrainType.Corn,grainPrice=2.6f},1000);
            Assert.That(quantity.text,Is.EqualTo("1,000 kg"));
            Assert.That(price.text,Is.EqualTo("$ 2.60 / kg"));
            Assert.That(value.text,Is.EqualTo("$ 2,600"));
            row.SetData(new GrainElementSettings{grainType=GrainType.Corn,grainPrice=2.6f},9.9f);
            Assert.That(quantity.text,Is.EqualTo("9 kg"));
            Assert.That(price.text,Is.EqualTo("$ 2.60 / kg"));
            Assert.That(value.text,Is.EqualTo("$ 25"));
        }
        finally { Object.DestroyImmediate(root); }
    }
    private static Text ChildText(GameObject root,string name)
    {
        var child=new GameObject(name,typeof(RectTransform),typeof(Text));child.transform.SetParent(root.transform);return child.GetComponent<Text>();
    }
    private static void Set(object target,string name,object value) => target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
}
