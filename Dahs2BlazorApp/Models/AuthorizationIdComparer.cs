namespace Dahs2BlazorApp.Models;

public class AuthorizationIdComparer : IComparer<IVerificationId>, IEqualityComparer<IVerificationId>
{
    public int Compare(IVerificationId? x, IVerificationId? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (ReferenceEquals(x, null)) return 1;
        if (ReferenceEquals(y, null)) return -1;
        
        if (Equals(x, y))
            return 0;
        
        if (x.GetReportDate > y.GetReportDate)
            return 1;
        if (x.GetReportDate < y.GetReportDate)
            return -1;
        
        if (x.GetReportDate == y.GetReportDate)
            return 0;
        
        return 1;
    }

    public bool Equals(IVerificationId? x, IVerificationId? y)
    {
        if (ReferenceEquals(x, y)) return true;
        if (ReferenceEquals(x, null)) return false;
        if (ReferenceEquals(y, null)) return false;

        return x.PipeId == y.PipeId && x.Sid == y.Sid &&
               x.DeviceCode == y.DeviceCode && x.ExecuteDate.Equals(y.ExecuteDate);
    }

    public int GetHashCode(IVerificationId obj)
    {
        return HashCode.Combine(obj.PipeId, obj.Sid, obj.DeviceCode, obj.ExecuteDate, obj.GetReportDate);
    }
}