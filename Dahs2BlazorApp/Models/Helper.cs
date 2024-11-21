using Dahs2BlazorApp.Db;

namespace Dahs2BlazorApp.Models;

public static class Helper
{
    public static DateTime GetRoundedNow(TimeSpan ts)
    {
        var nowTicks = DateTime.Now.Ticks;
        var floorTicks = nowTicks / ts.Ticks;
        var ceilingTicks = (nowTicks + ts.Ticks - 1) / ts.Ticks;
        // return the closest time
        if (nowTicks - floorTicks * ts.Ticks < ceilingTicks * ts.Ticks - nowTicks)
            return new DateTime(floorTicks * ts.Ticks);

        return new DateTime(ceilingTicks * ts.Ticks);
    }

    public static DateTime GetAlignedNextTime(TimeSpan ts)
    {
        var nowTicks = DateTime.Now.Ticks;
        var ceilingTicks = (nowTicks + ts.Ticks - 1) / ts.Ticks;
        return new DateTime(ceilingTicks * ts.Ticks);
    }


    public static IEnumerable<T> GetEnumValues<T>()
    {
        return Enum.GetValues(typeof(T)).Cast<T>();
    }

    public static IEnumerable<DateTime> GetTimeSeries(DateTime start, DateTime end, TimeSpan timeSpan)
    {
        for (var t = start; t < end; t = t.Add(timeSpan))
            yield return t;
    }

    public static (decimal, decimal, decimal) GetFixOzone(bool fFixWater, decimal value,
        decimal water, decimal baseO2, decimal dryMaxO2, decimal baf) // 乾基氧氣
    {
        decimal
            waterFactor =
                fFixWater ? 100 / (100 - water) : 1; // 乾基含氧修正值-修正水氣對含氧率的影響 : O2(乾基) = O2(原始) x 100 / (100 - 水份參考值[%])
        decimal dCalc = value * waterFactor * baf;
        if (dCalc >= dryMaxO2)
            dCalc = dryMaxO2; // 如果氧氣乾基超過20%以20%來計算

        decimal dFixO2_20 = (dCalc >= 20) ? 20 : dCalc; // 如果氧氣乾基超過20%以20%來計算 add code by jack 20211011

        decimal ozoneFactor = (21 - baseO2) / (21 - dFixO2_20);
        decimal
            flowOzoneFactor = (21 - dFixO2_20) / (21 - baseO2); // 排放流率修正值-參數之一，預先計算保留 :(21 - 氧氣乾基值) / (21 - 氧氣修正標準百分比)
        return (Math.Round(dCalc, 2, MidpointRounding.AwayFromZero), ozoneFactor, flowOzoneFactor);
    }

    public static decimal GetFlowFixValue(bool fixO2, bool fixWater, decimal value,
        decimal water, decimal temp, decimal area, decimal baf, decimal flowOzoneFactor)
    {
        //Log.Debug($"GetFlowFixValue fixO2={fixO2} fixWater={fixWater} raw={value} water={water}, temp={temp} area={area} baf={baf}");
        // 排放流率修正值-參數之一，預先計算保留 : (100 - 水份參考值[%]) / 100
        decimal waterFactor = fixWater ? (100 - water) / 100 : 1;
        decimal tempFactor = 273 / (273 + temp);
        decimal ozoneFactor = fixO2 ? flowOzoneFactor : 1;
        decimal flow1 = value * area * 3600 * tempFactor; // * dFlowFixWaterFactor * dFlowFixO2 * dFlowFixTemperature;
        decimal dcRet = flow1 * ozoneFactor * waterFactor * baf;
        // 排放流率修正值:排放流率原始值raw[0, 6] * 3600 * 煙囪截面積areaM2 * 水分修正 * 氧氣修正 * 溫度修正 * 偏移校正因子BAF
        //Log.Debug($"Flow={dcRet:f2}");
        return Math.Round(dcRet, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal GetOtherFixValue(string mt, bool fFixO2, bool fFixWater,
        decimal value, decimal water, decimal baf, decimal ozoneFactor)
    {
        if (mt != "E36" && mt != "F48" && mt != "T59" && mt != "W00")
        {
            decimal
                dWaterFactor =
                    fFixWater
                        ? 100 / (100 - water)
                        : 1; // 乾基含氧修正值-修正水氣對含氧率的影響 : O2(乾基) = O2(原始) x 100 / (100 - 水份參考值[%])

            decimal dFixO2 = fFixO2 ? ozoneFactor : 1;
            return Math.Round(value * dWaterFactor * dFixO2 * baf, 2, MidpointRounding.AwayFromZero);
        }

        return Math.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal RoundDecimal(decimal value, int digits = 2)
    {
        return Math.Round(value, digits, MidpointRounding.AwayFromZero);
    }

    public static void CheckTask(Task task, ILogger logger, string? message, params object?[] args)
    {
        if (task is not { IsFaulted: true, Exception: not null }) return;

        logger.LogError(task.Exception, message, args);
        throw task.Exception;
    }

    public enum RecordStatus
    {
        Normal,
        OverRange,
        Fixing,
        Calibration,
        Abnormal,
        Other,
    }

    public static RecordStatus ToRecordStatus(this Record? record)
    {
        if (record is null)
            return RecordStatus.Abnormal;

        return record.Status switch
        {
            "00" => RecordStatus.Normal,
            "01" => RecordStatus.Normal,
            "02" => RecordStatus.Normal,
            "10" => RecordStatus.Normal,
            "11" => RecordStatus.OverRange,
            "20" => RecordStatus.Calibration,
            "31" => RecordStatus.Fixing,
            "32" => RecordStatus.Abnormal,
            _ => RecordStatus.Other
        };
    }

    public static RecordStatus ToRecordStatus(this string status)
    {
        return status switch
        {
            "00" => RecordStatus.Normal,
            "01" => RecordStatus.Normal,
            "10" => RecordStatus.Normal,
            "11" => RecordStatus.OverRange,
            "20" => RecordStatus.Calibration,
            "31" => RecordStatus.Fixing,
            "32" => RecordStatus.Abnormal,
            _ => RecordStatus.Other
        };
    }

    public static string ToBlinkCss(this RecordStatus recordStatus)
    {
        return recordStatus switch
        {
            RecordStatus.Normal => "normal normal_status",
            RecordStatus.OverRange => "text-white blink abnormal_status",
            RecordStatus.Fixing => "normal fixing_status",
            RecordStatus.Calibration => "normal calibration_status",
            RecordStatus.Abnormal => "blink abnormal_status",
            RecordStatus.Other => "normal other_status",
            _ => "normal other_status"
        };
    }

    public static string ToCss(this RecordStatus recordStatus)
    {
        return recordStatus switch
        {
            RecordStatus.Normal => "normal normal_status",
            RecordStatus.OverRange => "text-white abnormal_status",
            RecordStatus.Fixing => "normal fixing_status",
            RecordStatus.Calibration => "normal calibration_status",
            RecordStatus.Abnormal => "abnormal_status",
            RecordStatus.Other => "other_status",
            _ => "normal other_status"
        };
    }

    public static string FormatTaiwanDayStr(DateTime dt)
    {
        return $"{dt.Year - 1911:000}{dt.Month:00}{dt.Day:00}";
    }
}