using Dahs2BlazorApp.Db;

namespace Dahs2BlazorApp.Configuration;

public enum MonitorTypeCode
{
    MT1,
    MT2,
    MT3,
    MT4,
    MT5,
    MT6,
    MT7,
    MT8,
    MT9,
    MT10,
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
        if (value < RangeMin)
            return RangeMin;

        if (value > RangeMax)
            return RangeMax;

        return value;
    }
}

public static class SiteConfig
{
    public static string SiteName => "CPMS核心系統";

    private static readonly DateTime CommitDate = DateTime.Parse(ThisAssembly.Git.CommitDate);

    public static string Version =>
        $"{ThisAssembly.Git.Branch} v{ThisAssembly.Git.BaseTag}-{ThisAssembly.Git.Commits} ({ThisAssembly.Git.Commit} {CommitDate:g})";
    
    public static readonly IList<Pipe> DefaultPipes = new List<Pipe>
    {
        new()
        {
            Id = 1, Name = "P001", EpaCode = "P001", Area = 0.74m, BaseO2 = 11m, LightDiameter = 1.3m,
            EmissionDiameter = 1.3m, LastNormalOzone = 20m, NormalOzoneTime = DateTime.Now,
            LastNormalTemp = 100m, NormalTempTime = DateTime.Now, UpperSource = ""
        },
    };

    public static readonly List<MonitorTypeCode> CemsMonitorTypeCodes = new()
    {
        MonitorTypeCode.MT1,
        MonitorTypeCode.MT2,
        MonitorTypeCode.MT3,
        MonitorTypeCode.MT4,
        MonitorTypeCode.MT5,
    };

    public static readonly Dictionary<MonitorTypeCode, TypeInfo> TypeCodeNameMap = new()
    {
        { MonitorTypeCode.MT1, new TypeInfo("測項1", "ppm", "ppm") },
        { MonitorTypeCode.MT2, new TypeInfo("測項2", "ppm", "ppm") },
        { MonitorTypeCode.MT3, new TypeInfo("測項3", "ppm", "ppm") },
        { MonitorTypeCode.MT4, new TypeInfo("測項4", "ppm", "ppm") },
        { MonitorTypeCode.MT5, new TypeInfo("測項5", "ppm", "ppm") },
        { MonitorTypeCode.MT6, new TypeInfo("測項6", "ppm", "ppm") },
        { MonitorTypeCode.MT7, new TypeInfo("測項7", "ppm", "ppm") },
        { MonitorTypeCode.MT8, new TypeInfo("測項8", "ppm", "ppm") },
        { MonitorTypeCode.MT9, new TypeInfo("測項9", "ppm", "ppm") },
        { MonitorTypeCode.MT10, new TypeInfo("測項10", "ppm", "ppm") }
    };
    
    public static readonly Dictionary<int, List<TypeDefinition>> PipeMonitorTypes =
        new()
        {
            // Pipe 1
            {
                1, new List<TypeDefinition>
                {
                    new(
                        Sid: MonitorTypeCode.MT1,
                        RangeMin: 0m,
                        RangeMax: 100000m,
                        AdjustFactor: new AdjustFactor(false, false)
                    ),  // 0-100000
                    new(
                        Sid: MonitorTypeCode.MT2,
                        RangeMin: 0m,
                        RangeMax: 100000m,
                        AdjustFactor: new AdjustFactor(false, false)
                    ),  // 0-100000
                    new(
                        Sid: MonitorTypeCode.MT3,
                        RangeMin: 0m,
                        RangeMax: 100000m,
                        AdjustFactor: new AdjustFactor(false, false)
                    ),  // 0-100000
                    new(
                        Sid: MonitorTypeCode.MT4,
                        RangeMin: 0m,
                        RangeMax: 100000m,
                        AdjustFactor: new AdjustFactor(false, false)
                    ),  // 0-100000
                    new(
                        Sid: MonitorTypeCode.MT5,
                        RangeMin: 0m,
                        RangeMax: 100000m,
                        AdjustFactor: new AdjustFactor(false, false)
                    ),  // 0-100000
                    new(
                        Sid: MonitorTypeCode.MT6,
                        RangeMin: 0m,
                        RangeMax: 100000m,
                        AdjustFactor: new AdjustFactor(false, false)
                    ),  // 0-100000
                    new(
                        Sid: MonitorTypeCode.MT7,
                        RangeMin: 0m,
                        RangeMax: 100000m,
                        AdjustFactor: new AdjustFactor(false, false)
                    ),  // 0-100000
                    new(
                        Sid: MonitorTypeCode.MT8,
                        RangeMin: 0m,
                        RangeMax: 100000m,
                        AdjustFactor: new AdjustFactor(false, false)
                    ),  // 0-100000
                    new(
                        Sid: MonitorTypeCode.MT9,
                        RangeMin: 0m,
                        RangeMax: 100000m,
                        AdjustFactor: new AdjustFactor(false, false)
                    ),  // 0-100000
                    new(
                        Sid: MonitorTypeCode.MT10,
                        RangeMin: 0m,
                        RangeMax: 100000m,
                        AdjustFactor: new AdjustFactor(false, false)
                    ),  // 0-100000
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