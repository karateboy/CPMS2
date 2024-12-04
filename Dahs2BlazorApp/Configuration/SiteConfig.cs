using Dahs2BlazorApp.Db;

namespace Dahs2BlazorApp.Configuration;

public enum MonitorTypeCode
{
    A22,
    A23,
    A231,
    A232,
    A24,
    A26,
    E36,
    E37,
    F48,
    G11,
    W00,
    T59,
    T60,
    Press,
    RoomTemp,

    // pollutions prevention codes
    SecondTemp,
    BFTemp,
    BFPressDiff,
    BFWeightMod,
    WashFlow,
    WaterQuantity,
    BlowerSpeed,
    OpTemp,
    BurnerTemp,
    BlowerSpeed1,
    BlowerSpeed2,
    BlowerSpeed3,
    BlowerSpeed4,
    EmExit,
    WashTowerPressDiff,
    PH
}

public record TypeInfo(string Name, string Unit, string Unit1);

public record AdjustFactor(bool Water, bool O2, decimal? Ppm2Kg = null)
{
    public override string ToString()
    {
        string ozoneSetting = O2 ? "修氧" : "不修氧";
        string waterSetting = Water ? "修水" : "不修水";
        return $"{ozoneSetting}{waterSetting}";
    }
}

// ReSharper disable once ClassNeverInstantiated.Global
public record InterpolationFactor(decimal ReadMin, decimal ReadMax)
{
    public override string ToString()
    {
        return $"4mA: {ReadMin}, 20mA: {ReadMax}";
    }
}

// ReSharper disable once ClassNeverInstantiated.Global
public record CalculateFunction(List<string> InputSids, Func<List<decimal?>, decimal?> CalculateFunc);

public interface ITypeDefinition
{
    MonitorTypeCode Sid { get; init; }
    decimal RangeMin { get; init; }
    decimal RangeMax { get; init; }
    AdjustFactor AdjustFactor { get; init; }
    InterpolationFactor? InterpolationFactor { get; init; }
    decimal Offset { get; init; }
    decimal Multiplier { get; init; }
    CalculateFunction? Calculation { get; init; }
}

public record MonitorTypeOutputConfig(string Name, decimal OutputFactor);

public record TypeDefinition(
    MonitorTypeCode Sid,
    decimal RangeMin,
    decimal RangeMax,
    AdjustFactor AdjustFactor,
    InterpolationFactor? InterpolationFactor = null,
    decimal Multiplier = 1m,
    decimal Offset = 0m,
    CalculateFunction? Calculation = null) : ITypeDefinition
{
    public decimal CheckRange(decimal value)
    {
        return value;
    }
}

public static class SiteConfig
{
    public static string SiteName => "易增CPMS";

    private static readonly DateTime CommitDate = DateTime.Parse(ThisAssembly.Git.CommitDate);

    public static string Version =>
        $"{ThisAssembly.Git.Branch} v{ThisAssembly.Git.BaseTag}-{ThisAssembly.Git.Commits} ({ThisAssembly.Git.Commit} {CommitDate:g})";

    // 預設水分 10%
    public static decimal DefaultWater => 10m;

    public static readonly IList<Pipe> DefaultPipes = new List<Pipe>
    {
        new()
        {
            Id = 1, Name = "P001", EpaCode = "P001", Area = 0.74m, BaseO2 = 11m, LightDiameter = 1.3m,
            EmissionDiameter = 1.3m, LastNormalOzone = 20m, NormalOzoneTime = DateTime.Now,
            LastNormalTemp = 100m, NormalTempTime = DateTime.Now, UpperSource = ""
        },
    };

    public static List<MonitorTypeCode> CemsMonitorTypeCodes = new()
    {
        MonitorTypeCode.A22,
        MonitorTypeCode.A23,
        MonitorTypeCode.A231,
        MonitorTypeCode.A232,
        MonitorTypeCode.A24,
        MonitorTypeCode.A26,
        MonitorTypeCode.E36,
        MonitorTypeCode.E37,
        MonitorTypeCode.F48,
        MonitorTypeCode.G11,
        MonitorTypeCode.W00,
        MonitorTypeCode.T59,
        MonitorTypeCode.T60,
        MonitorTypeCode.Press,
        MonitorTypeCode.RoomTemp
    };

    public static readonly Dictionary<MonitorTypeCode, TypeInfo> TypeCodeNameMap = new()
    {
        { MonitorTypeCode.A22, new TypeInfo("二氧化硫", "ppm", "ppm") },
        { MonitorTypeCode.A23, new TypeInfo("氮氧化物", "ppm", "ppm") },
        { MonitorTypeCode.A231, new TypeInfo("一氧化氮", "ppm", "ppm") },
        { MonitorTypeCode.A232, new TypeInfo("二氧化氮", "ppm", "ppm") },
        { MonitorTypeCode.A24, new TypeInfo("一氧化碳", "ppm", "ppm") },
        { MonitorTypeCode.A26, new TypeInfo("氯化氫", "ppm", "ppm") },
        { MonitorTypeCode.E36, new TypeInfo("氧氣", "%", "%") },
        { MonitorTypeCode.E37, new TypeInfo("二氧化碳", "%", "%") },
        { MonitorTypeCode.F48, new TypeInfo("流速/流率", "m/sec", "Nm\u00b3/min") },
        { MonitorTypeCode.G11, new TypeInfo("不透光率", "%", "%") },
        { MonitorTypeCode.W00, new TypeInfo("水份", "%", "%") },
        { MonitorTypeCode.T59, new TypeInfo("溫度", "℃", "℃") },
        { MonitorTypeCode.T60, new TypeInfo("棧房溫度", "℃", "℃") },
        { MonitorTypeCode.Press, new TypeInfo("壓力", "mBar", "mBar") },
        { MonitorTypeCode.RoomTemp, new TypeInfo("室溫", "℃", "℃") },
        { MonitorTypeCode.SecondTemp, new TypeInfo("二次出口溫度", "℃", "℃") },
        { MonitorTypeCode.BFTemp, new TypeInfo("BF入口溫度", "℃", "℃") },
        { MonitorTypeCode.BFPressDiff, new TypeInfo("BF壓差", "mmH2O", "mmH2O") },
        { MonitorTypeCode.BFWeightMod, new TypeInfo("BF稱種模組", "kg", "kg") },
        { MonitorTypeCode.WashFlow, new TypeInfo("洗滌流率", "l/h", "l/h") },
        { MonitorTypeCode.PH, new TypeInfo("PH值", "-", "-") },
        { MonitorTypeCode.WaterQuantity, new TypeInfo("換水量", "噸", "噸") },
        { MonitorTypeCode.BlowerSpeed, new TypeInfo("鼓風機轉速", "rpm", "rpm") },
        { MonitorTypeCode.OpTemp, new TypeInfo("操作溫度", "℃", "℃") },
        { MonitorTypeCode.BurnerTemp, new TypeInfo("焚化爐出口溫度", "℃", "℃") },
        { MonitorTypeCode.BlowerSpeed1, new TypeInfo("鼓風機1轉速", "rpm", "rpm") },
        { MonitorTypeCode.BlowerSpeed2, new TypeInfo("鼓風機2轉速", "rpm", "rpm") },
        { MonitorTypeCode.BlowerSpeed3, new TypeInfo("鼓風機3轉速", "rpm", "rpm") },
        { MonitorTypeCode.BlowerSpeed4, new TypeInfo("鼓風機4轉速", "rpm", "rpm") },
        { MonitorTypeCode.EmExit, new TypeInfo("緊急排放口", "℃", "℃") },
        { MonitorTypeCode.WashTowerPressDiff, new TypeInfo("洗滌塔壓差", "mmH2O", "mmH2O") },
    };

    private static decimal? GetGteZero(decimal? value)
    {
        if (value is null)
            return null;

        return value < 0 ? 0 : value;
    }

    private static readonly CalculateFunction A23Calculation = new(
        new List<string> { "A231", "A232" },
        values => GetGteZero(values[0]) + GetGteZero(values[1]));
    
    public static readonly Dictionary<int, List<TypeDefinition>> PipeMonitorTypes =
        new()
        {
            // Pipe 1
            {
                1, new List<TypeDefinition>
                {
                    new(
                        Sid: MonitorTypeCode.A26,
                        RangeMin: 0m,
                        RangeMax: 500m,
                        AdjustFactor: new AdjustFactor(false, true)
                    ),
                    new(
                        Sid: MonitorTypeCode.A231,
                        RangeMin: 0m,
                        RangeMax: 500m,
                        AdjustFactor: new AdjustFactor(false, true)
                    ),
                    new(
                        Sid: MonitorTypeCode.A232,
                        RangeMin: 0m,
                        RangeMax: 500m,
                        AdjustFactor: new AdjustFactor(false, true)
                    ),
                    new(
                        Sid: MonitorTypeCode.A23,
                        RangeMin: 0m,
                        RangeMax: 1000m,
                        AdjustFactor: new AdjustFactor(false, true),
                        Calculation: A23Calculation
                    ),
                    new(
                        Sid: MonitorTypeCode.A22,
                        RangeMin: 0m,
                        RangeMax: 500m,
                        new AdjustFactor(false, true)
                    ),
                    new(
                        Sid: MonitorTypeCode.A24,
                        RangeMin: 0m,
                        RangeMax: 500m,
                        new AdjustFactor(false, true)
                    ),
                    new(
                        Sid: MonitorTypeCode.W00,
                        RangeMin: 0m,
                        RangeMax: 25m,
                        new AdjustFactor(false, false)
                    ),
                    new(
                        Sid: MonitorTypeCode.E36,
                        RangeMin: 0m,
                        RangeMax: 25m,
                        AdjustFactor: new AdjustFactor(false, false)
                    ),
                    new(
                        Sid: MonitorTypeCode.E37,
                        RangeMin: 0m,
                        RangeMax: 25m,
                        AdjustFactor: new AdjustFactor(false, false)
                    ),
                    new(
                        Sid: MonitorTypeCode.F48,
                        RangeMin: 0m,
                        RangeMax: 40m,
                        AdjustFactor: new AdjustFactor(false, false),
                        Multiplier: decimal.Divide(40m, 65535m)
                    ),
                    new(
                        Sid: MonitorTypeCode.G11,
                        RangeMin: 0m,
                        RangeMax: 100m,
                        AdjustFactor: new AdjustFactor(false, false),
                        Multiplier: decimal.Divide(100m, 65535m)
                    ),
                    new(
                        Sid: MonitorTypeCode.T59,
                        RangeMin: 0m,
                        RangeMax: 300m,
                        AdjustFactor: new AdjustFactor(false, false),
                        Multiplier: decimal.Divide(300m, 65535m)
                    ),
                    new(
                        Sid: MonitorTypeCode.T60,
                        RangeMin: 0m,
                        RangeMax: 50m,
                        AdjustFactor: new AdjustFactor(false, false),
                        Multiplier: decimal.Divide(50m, 65535m)
                    ),
                    new(Sid: MonitorTypeCode.SecondTemp, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.BFTemp, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.BFPressDiff, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.BFWeightMod, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.WashFlow, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.WaterQuantity, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.BlowerSpeed, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.OpTemp, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.BurnerTemp, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.BlowerSpeed1, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.BlowerSpeed2, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.BlowerSpeed3, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.BlowerSpeed4, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.EmExit, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.WashTowerPressDiff, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false)),
                    new(Sid: MonitorTypeCode.PH, RangeMin: 0m, RangeMax: 3000m,
                        AdjustFactor: new AdjustFactor(false, false))
                }
            },
        };


    public static Dictionary<int, Dictionary<string, TypeDefinition>> PipeMonitorTypeMap =>
        PipeMonitorTypes.ToDictionary(
            x => x.Key,
            x => x.Value.ToDictionary(
                y => y.Sid.ToString(), y => y));

    public delegate decimal? Generator(int pipeId, IReadOnlyDictionary<string, Record> recordMap);

    public record DeviceOutputConfig(int Id, string Description, Generator OutputGenerator);

    private static decimal? ExportA23(int pipeId, IReadOnlyDictionary<string, Record> recordMap)
    {
        if (!recordMap.TryGetValue("A23", out var noxRecord))
            return null;

        var noxTypeDef = PipeMonitorTypeMap[pipeId]["A23"];

        return noxRecord.Value / 1500m * 4096m;
    }

    public static readonly List<DeviceOutputConfig> DeviceOutputConfigs = new()
    {
        new DeviceOutputConfig(
            Id: 1,
            Description: "修正後NOx依量測範圍轉成0-1500數值輸出",
            OutputGenerator: ExportA23)
    };

    public static readonly Dictionary<int, DeviceOutputConfig> DeviceOutputConfigMap =
        DeviceOutputConfigs.ToDictionary(x => x.Id, x => x);

    public static string GetDeviceOutputDescription(int id)
    {
        return DeviceOutputConfigMap.TryGetValue(id, out var deviceOutputConfig) ? deviceOutputConfig.Description : "";
    }
}