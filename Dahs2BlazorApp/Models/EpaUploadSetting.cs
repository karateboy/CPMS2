namespace Dahs2BlazorApp.Models;

public static class EpaUploadSetting
{
    public static string EpaRootFolder { get; set; } = @"D:\DAHS2\Epa\";
    public static string TempFolder => Path.Combine(EpaRootFolder, "Temp");
    public static string BackupFolder => Path.Combine(EpaRootFolder, "Backup");
    
    public static void EnsureFolder()
    {
        Directory.CreateDirectory(EpaRootFolder);
        Directory.CreateDirectory(TempFolder);
        Directory.CreateDirectory(BackupFolder);
        Directory.CreateDirectory(UploadFolder);
    }
    
    private static string _uploadFolder = @"C:\AtlasCEMS\Upload\";
    public static string UploadFolder { get => _uploadFolder;
    set
    {
        if (!string.IsNullOrEmpty(value))
            _uploadFolder = value;
    } } 
    public static bool IsTestUpload { get; set; } = true;
}