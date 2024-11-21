namespace Dahs2BlazorApp.Models;

public interface IVerificationId
{
    int PipeId { get; set; }
    string Sid { get; set; }
    string DeviceCode { get; set; }
    DateTime ExecuteDate { get; set; }
    DateTime GetReportDate { get; set; }        
}

public interface IVerificationLog :IVerificationId
{
    bool Valid { get; set; }
}