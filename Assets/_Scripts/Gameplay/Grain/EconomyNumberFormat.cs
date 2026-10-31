using System;
using System.Globalization;
using System.Collections.Generic;

/// <summary>Stable comma thousands grouping, independent of the machine's regional settings.</summary>
public static class EconomyNumberFormat
{
    public static string Accounting(double value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>The integer amount credited by a sale; fractions of a dollar are discarded.</summary>
    public static int SaleCash(double value) => !double.IsNaN(value) && !double.IsInfinity(value) &&
        value >= 1 && value < (double)int.MaxValue + 1 ? (int)Math.Floor(value) : 0;

    public static double CropValue(float kilograms, float unitPrice)
    {
        if (!float.IsFinite(kilograms) || !float.IsFinite(unitPrice) || kilograms < 0 || unitPrice < 0) return double.NaN;
        // Decimal conversion removes float representation noise such as 50 * 2.6f = 129.999995.
        try { return (double)((decimal)kilograms * (decimal)unitPrice); }
        catch (OverflowException) { return double.PositiveInfinity; }
    }

    public static int SaleTotal(IEnumerable<double> cropValues)
    {
        if (cropValues == null) return 0;
        long total = 0;
        foreach (double value in cropValues)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value >= (double)int.MaxValue + 1) return 0;
            total += SaleCash(value);
            if (total > int.MaxValue) return 0;
        }
        return (int)total;
    }
}
