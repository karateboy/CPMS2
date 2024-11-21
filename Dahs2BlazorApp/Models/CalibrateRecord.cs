namespace Dahs2BlazorApp.Models;

public class CalibrateRecord : IComparable
{
    public string? name;
    public DateTime start;
    public DateTime? end;
    public double? zeroValue;
    public double? zeroLaw;
    public double? zeroOffsetValue;
    public double? zeroOffsetPercent;
    public int? zeroStatus;
    public double? spanLaw;
    public double? spanValue;
    public double? spanOffsetValue;
    public double? spanOffsetPercent;
    public int? spanStatus;
    public double? mirrorValue;
    public int status;

    public int CompareTo(object? obj)
    {
        CalibrateRecord? r1 = (CalibrateRecord)obj!;
        if (name != r1.name)
            return string.CompareOrdinal(name, r1.name);
        else
            return DateTime.Compare(start, r1.start);
    }
}