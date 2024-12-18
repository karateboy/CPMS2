using System.Diagnostics;
using Dahs2BlazorApp.Configuration;
using Dahs2BlazorApp.Db;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Dahs2BlazorApp.Models;

public class ExcelUtility
{
    private readonly ILogger<ExcelUtility> _logger;
    private readonly PipeIo _pipeIo;
    private readonly MonitorTypeIo _monitorTypeIo;
    private readonly IWebHostEnvironment _env;

    public ExcelUtility(ILogger<ExcelUtility> logger,
        PipeIo pipeIo,
        MonitorTypeIo monitorTypeIo,
        IWebHostEnvironment environment)
    {
        _logger = logger;
        _pipeIo = pipeIo;
        _monitorTypeIo = monitorTypeIo;
        _env = environment;
    }

    void AppendCellValue(ICell cell, string value)
    {
        var prompt = cell.StringCellValue;
        cell.SetCellValue($"{prompt}{value}");
    }

    void PrependCellValue(ICell cell, string value)
    {
        var prompt = cell.StringCellValue;
        cell.SetCellValue($"{value}{prompt}");
    }

    void FormatCellValue(ICell cell, params object[] args)
    {
        var fmt = cell.StringCellValue;
        cell.SetCellValue(string.Format(fmt, args));
    }

    public string ExportDailyReport(DateTime dateTime, Dictionary<DateTime, Dictionary<string, Record>> dailyRecord)
    {
        var start = dateTime.Date;
        var src = Path.Combine(_env.WebRootPath, "ReportTemplate", "DailyReport.xlsx");
        var dest = Path.GetTempFileName();

        using var stream = new FileStream(src, FileMode.Open, FileAccess.Read);
        var workBook = new XSSFWorkbook(stream);

        #region 封面

        ISheet sheet = workBook.GetSheetAt(0);
        ICell cell = sheet.GetRow(3).GetCell(14);
        FormatCellValue(cell, start.Year - 1911, start.Month, start.Day);
        var mtList = new[]
        {
            MonitorTypeCode.OpTemp.ToString(),
            MonitorTypeCode.BurnerTemp.ToString(),
            MonitorTypeCode.SecondTemp.ToString(),
            MonitorTypeCode.A24.ToString(),
            MonitorTypeCode.E36.ToString(),
            MonitorTypeCode.BFTemp.ToString(),
            MonitorTypeCode.BFPressDiff.ToString(),
            MonitorTypeCode.BFWeightMod.ToString(),
            MonitorTypeCode.WashFlow.ToString(),
            MonitorTypeCode.PH.ToString(),
            MonitorTypeCode.WashTowerPressDiff.ToString(),
            MonitorTypeCode.F48.ToString(),
            MonitorTypeCode.WaterQuantity.ToString(),
        };
        foreach (var (mt, mtIndex) in mtList.Select((mt, idx) => (mt, idx)))
        {
            foreach (var (dt, hourIndex) in Helper.GetTimeSeries(start, start.AddDays(1), TimeSpan.FromHours(1))
                         .Select((time, idx) => (time, idx)))
            {
                cell = sheet.GetRow(8 + hourIndex).GetCell(2 + mtIndex);
                if (dailyRecord.TryGetValue(dt, out var recordMap) && recordMap.TryGetValue(mt, out var record))
                {
                    cell.SetCellValue(Convert.ToDouble(record.Value.GetValueOrDefault(0)));
                }
            }

            cell = sheet.GetRow(32).GetCell(2 + mtIndex);
            cell.SetCellValue(dailyRecord.Values.Count(dict => dict.ContainsKey(mt)));
            var overCount = dailyRecord.Values.Count(dict => dict.ContainsKey(mt) && dict[mt].Status == "11");
            sheet.GetRow(33).GetCell(2 + mtIndex).SetCellValue(overCount);
            var validStatuses = new[] { "00", "01", "02", "10", "11" };
            var validCount =
                dailyRecord.Values.Count(dict => dict.ContainsKey(mt) && validStatuses.Contains(dict[mt].Status));
            sheet.GetRow(34).GetCell(2 + mtIndex).SetCellValue(validCount);
            var values = dailyRecord.Values.Where(dict => dict.ContainsKey(mt) && dict[mt].Value.HasValue)
                .Select(dict => dict[mt].Value!.Value).ToArray();

            if (values.Length > 0)
            {
                sheet.GetRow(35).GetCell(2 + mtIndex).SetCellValue(Convert.ToDouble(values.Average()));
                sheet.GetRow(36).GetCell(2 + mtIndex).SetCellValue(Convert.ToDouble(values.Max()));
                sheet.GetRow(37).GetCell(2 + mtIndex).SetCellValue(Convert.ToDouble(values.Min()));
                sheet.GetRow(38).GetCell(2 + mtIndex).SetCellValue((double)validCount / values.Length);
            }
            else
            {
                sheet.GetRow(35).GetCell(2 + mtIndex).SetCellValue("-");
                sheet.GetRow(36).GetCell(2 + mtIndex).SetCellValue("-");
                sheet.GetRow(37).GetCell(2 + mtIndex).SetCellValue("-");
                sheet.GetRow(38).GetCell(2 + mtIndex).SetCellValue("-");
            }
        }

        #endregion

        workBook.SetActiveSheet(0);
        workBook.SetForceFormulaRecalculation(true);
        using var outputStream = new FileStream(dest, FileMode.Create, FileAccess.Write);
        workBook.Write(outputStream);
        outputStream.Close();

        return dest;
    }
    
    public string Export5MinDailyReport(DateTime dateTime, string monitorType, 
        Dictionary<DateTime, Dictionary<string, Record>> dailyRecord)
    {
        var start = dateTime.Date;
        var src = Path.Combine(_env.WebRootPath, "ReportTemplate", "FiveMinDailyReport.xlsx");
        var dest = Path.GetTempFileName();

        using var stream = new FileStream(src, FileMode.Open, FileAccess.Read);
        var workBook = new XSSFWorkbook(stream);

        #region 封面

        ISheet sheet = workBook.GetSheetAt(0);
        ICell cell = sheet.GetRow(3).GetCell(12);
        FormatCellValue(cell, start.Year - 1911, start.Month, start.Day);
        cell = sheet.GetRow(3).GetCell(2);
        cell.SetCellValue(_monitorTypeIo.PipeMonitorTypeMap[1][monitorType].Name);
        
       
        foreach (var (hour, hourIndex) in Helper.GetTimeSeries(start, start.AddDays(1), TimeSpan.FromHours(1))
            .Select((time, idx) => (time, idx)))
        {
            foreach (var (dt, minIndex) in Helper.GetTimeSeries(hour, hour.AddHours(1), TimeSpan.FromMinutes(5))
                         .Select((time, idx) => (time, idx)))
            {
                if (dt > DateTime.Now)
                    break;
                
                cell = sheet.GetRow(6 + hourIndex).GetCell(2+minIndex);
                if (dailyRecord.TryGetValue(dt, out var recordMap) && recordMap.TryGetValue(monitorType, out var record))
                {
                    if(record.Value.HasValue)
                        cell.SetCellValue(Convert.ToDouble(record.Value.Value));
                }
            }
        }
        cell = sheet.GetRow(30).GetCell(2);
        if(_monitorTypeIo.PipeMonitorTypeMap[1][monitorType].Standard.HasValue)
            cell.SetCellValue(Convert.ToDouble(_monitorTypeIo.PipeMonitorTypeMap[1][monitorType].Standard!.Value));
        
        
        var values = dailyRecord.Values.Where(dict => dict.ContainsKey(monitorType) && dict[monitorType].Value.HasValue)
            .Select(dict => dict[monitorType].Value!.Value).ToArray();
        if(values.Length > 0)
        {
            cell = sheet.GetRow(31).GetCell(2);
            cell.SetCellValue(Convert.ToDouble(values.Max()));
            cell = sheet.GetRow(32).GetCell(2);
            cell.SetCellValue(Convert.ToDouble(values.Min()));
            cell = sheet.GetRow(33).GetCell(2);
            cell.SetCellValue(Convert.ToDouble(values.Average()));
            cell = sheet.GetRow(34).GetCell(2);
            int overCount = dailyRecord.Values.Count(dict => dict.ContainsKey(monitorType) && dict[monitorType].Status == "11");
            cell.SetCellValue(overCount);
        }
        #endregion

        workBook.SetActiveSheet(0);
        workBook.SetForceFormulaRecalculation(true);
        using var outputStream = new FileStream(dest, FileMode.Create, FileAccess.Write);
        workBook.Write(outputStream);
        outputStream.Close();

        return dest;
    }
    
    public string Export5MinDailyReportWithStatus(DateTime dateTime, string monitorType, 
        Dictionary<DateTime, Dictionary<string, Record>> dailyRecord)
    {
        var start = dateTime.Date;
        var src = Path.Combine(_env.WebRootPath, "ReportTemplate", "FiveMinDailyReportWithStatus.xlsx");
        var dest = Path.GetTempFileName();

        using var stream = new FileStream(src, FileMode.Open, FileAccess.Read);
        var workBook = new XSSFWorkbook(stream);

        #region 封面

        ISheet sheet = workBook.GetSheetAt(0);
        ICell cell = sheet.GetRow(3).GetCell(11);
        FormatCellValue(cell, start.Year - 1911, start.Month, start.Day);
        cell = sheet.GetRow(3).GetCell(2);
        cell.SetCellValue(_monitorTypeIo.PipeMonitorTypeMap[1][monitorType].Name);
        
       
        foreach (var (hour, hourIndex) in Helper.GetTimeSeries(start, start.AddDays(1), TimeSpan.FromHours(1))
            .Select((time, idx) => (time, idx)))
        {
            foreach (var (dt, minIndex) in Helper.GetTimeSeries(hour, hour.AddHours(1), TimeSpan.FromMinutes(5))
                         .Select((time, idx) => (time, idx)))
            {
                if (dt > DateTime.Now)
                    break;
                
                if (dailyRecord.TryGetValue(dt, out var recordMap) && recordMap.TryGetValue(monitorType, out var record))
                {
                    cell = sheet.GetRow(6 + hourIndex).GetCell(2 + 2 * minIndex);
                    if(record.Value.HasValue)
                        cell.SetCellValue(Convert.ToDouble(record.Value.Value));
                    cell = sheet.GetRow(6 + hourIndex).GetCell(3 + 2 * minIndex);
                    cell.SetCellValue(record.Status);
                }
            }
        }
        cell = sheet.GetRow(30).GetCell(2);
        if(_monitorTypeIo.PipeMonitorTypeMap[1][monitorType].Standard.HasValue)
            cell.SetCellValue(Convert.ToDouble(_monitorTypeIo.PipeMonitorTypeMap[1][monitorType].Standard!.Value));
        
        
        var values = dailyRecord.Values.Where(dict => dict.ContainsKey(monitorType) && dict[monitorType].Value.HasValue)
            .Select(dict => dict[monitorType].Value!.Value).ToArray();
        if(values.Length > 0)
        {
            cell = sheet.GetRow(31).GetCell(2);
            cell.SetCellValue(Convert.ToDouble(values.Max()));
            cell = sheet.GetRow(32).GetCell(2);
            cell.SetCellValue(Convert.ToDouble(values.Min()));
            cell = sheet.GetRow(33).GetCell(2);
            cell.SetCellValue(Convert.ToDouble(values.Average()));
            cell = sheet.GetRow(34).GetCell(2);
            int overCount = dailyRecord.Values.Count(dict => dict.ContainsKey(monitorType) && dict[monitorType].Status == "11");
            cell.SetCellValue(overCount);
        }
        #endregion

        workBook.SetActiveSheet(0);
        workBook.SetForceFormulaRecalculation(true);
        using var outputStream = new FileStream(dest, FileMode.Create, FileAccess.Write);
        workBook.Write(outputStream);
        outputStream.Close();

        return dest;
    }
}