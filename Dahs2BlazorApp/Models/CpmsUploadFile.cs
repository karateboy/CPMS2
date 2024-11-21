using System.Text;
using Serilog;
using System;
using System.Diagnostics;

namespace Dahs2BlazorApp.Models;

public class CpmsUploadFile
{
    private readonly string _filename;
    private readonly List<string> _currentRecord = new();
    private readonly List<string> _recordList = new();

    public CpmsUploadFile(string controlNo, DateTime date, string nnn)
    {
        _filename = $"E1{date.Month:00}{date.Day:00}{date.Hour:00}{date.Minute:00}{date.Second:00}.{nnn}";
        AddEntry($"100{controlNo}E01");   
    }

    public void AddEntry(string entry)
    {
        _recordList.Add(entry);
    }
    
    public void EndFile()
    {
        _recordList.Add("\u0004");
    }
    
    public void AddEntry(string fmt, string equip, DateTime date, string value, string status)
    {
        Debug.Assert(fmt.Length==3);
        var paddedEquip = equip.PadRight(6, ' ');
        var paddedValue = value.PadRight(10, ' ');
        
        _recordList.Add($"{fmt}{paddedEquip}{date.Year-1911}{date:MMddHHmm}{paddedValue}{status}");
    }

    public async Task<string> Flush()
    {
        string tempFolder = EpaUploadSetting.EpaRootFolder;
        Directory.CreateDirectory(tempFolder);

        string outputPath = Path.Combine(tempFolder, _filename);

        string content = string.Join("\n", _recordList) + "\n\u0004";
        var big5 = Encoding.GetEncoding("big5");
        
        var encodedText = big5.GetBytes(content);

        await using var fs = File.Create(outputPath);
        await fs.WriteAsync(encodedText);
        return outputPath;
    }

    public static void UploadAndBackup(string tempFullPath, DateTime date)
    {
        var uploadFilename = Path.GetFileName(tempFullPath);
        var backupFolder =
            Path.Combine(EpaUploadSetting.BackupFolder, $"{date.Year - 1911:000}{date.Month:00}{date.Day:00}"); 
            
        var backupPath = Path.Combine($"{backupFolder}\\", uploadFilename);
        var uploadFullPath = Path.Combine(EpaUploadSetting.UploadFolder, uploadFilename);
            
        try
        {
            Directory.CreateDirectory(backupFolder);
            File.Copy(tempFullPath, uploadFullPath, true);
            if (!File.Exists(backupPath))
                File.Move(tempFullPath, backupPath);
            else
                File.Delete(tempFullPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "UploadAndBackup failed for {UploadFilename}", uploadFilename);
            throw;
        }
    }
}
