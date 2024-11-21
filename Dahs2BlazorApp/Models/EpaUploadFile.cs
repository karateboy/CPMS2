using System.Text;
using Serilog;
using System;

namespace Dahs2BlazorApp.Models;

public enum FileType
{
    Law,
    Mon,
    Raw,
    Fst
}

public class EpaUploadFile
{
    private readonly string _filename;
    private readonly List<string> _currentRecord = new();
    private readonly List<string> _recordList = new();

    public EpaUploadFile(string header, FileType fileType, string filename, string controlNo, DateTime date)
    {
        this._filename = $"{header}{filename}";
        AddTransIdRecord(fileType, controlNo);

        if (fileType == FileType.Mon)
            AddYearMonthRecord(date);
    }

    private void AddTransIdRecord(FileType fileType, string controlNo)
    {
        if (controlNo.Length != 8)
            throw new Exception("管制編號長度必須為8");

        StartRecord("100");
        AddField(controlNo);
        switch (fileType)
        {
            case FileType.Law:
                AddField("LAW");
                break;
            case FileType.Mon:
                AddField("MON");
                break;
            case FileType.Fst:
                AddField("FST");
                break;
            case FileType.Raw:
                AddField("RAW");
                break;
            default:
                throw new Exception($"Unhandled FileType {fileType}");
        }

        AddField("V109");
        EndRecord();
    }

    private void AddYearMonthRecord(DateTime date)
    {
        StartRecord("101");
        AddField($"{date.Year - 1911:000}");
        AddField($"{date.Month:00}");
        EndRecord();
    }

    public void StartRecord(string formatCode)
    {
        if (_currentRecord.Count != 0)
            throw new Exception("previous record not end yet!");

        if (formatCode.Length != 3)
            throw new Exception("Format code length shall be 3!");

        AddField(formatCode);
    }

    public void AddField(string value)
    {
        _currentRecord.Add(value);
    }

    public void AddField(string value, int limit)
    {
        AddField(value);
    }

    public void EndRecord()
    {
        string record = string.Join(",", _currentRecord);
        _recordList.Add(record);
        _currentRecord.Clear();
    }

    public async Task<string> Flush()
    {
        if (_currentRecord.Count != 0)
            throw new Exception("Record not end!");

        string tempFolder = EpaUploadSetting.EpaRootFolder;
        Directory.CreateDirectory(tempFolder);

        string outputPath = Path.Combine(tempFolder, _filename);

        string content = string.Join("\n", _recordList) + "\r";
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
