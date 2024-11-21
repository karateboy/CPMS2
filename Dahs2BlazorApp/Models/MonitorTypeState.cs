namespace Dahs2BlazorApp.Models
{
    public record MonitorTypeStateModel
    {
        public string? Code { get; init; }

        public required string Name { get; init; }

        public string FullName => $"{Code} {Name}";
    }

    public static class MonitorTypeState
    {
        public static readonly List<MonitorTypeStateModel> States = new()
        {
            new MonitorTypeStateModel { Code = "", Name = "-" },
            new MonitorTypeStateModel { Code = "00", Name = "00 - 暫停運轉" },
            new MonitorTypeStateModel { Code = "01", Name = "01 - 昇載" },
            new MonitorTypeStateModel { Code = "02", Name = "02 - 降載" },
            new MonitorTypeStateModel { Code = "10", Name = "10 - 正常操作" },
            new MonitorTypeStateModel { Code = "11", Name = "11 - 不符合核定操作測值" },
            new MonitorTypeStateModel { Code = "20", Name = "20 - 測試" },
            new MonitorTypeStateModel { Code = "30", Name = "30 - 無效數據" },
            new MonitorTypeStateModel { Code = "31", Name = "31 - 維修保養" },
            new MonitorTypeStateModel { Code = "32", Name = "32 - 其他無效" },
        };

        private static readonly List<string?> OverrideStateCodes = new(){ "", "00", "01", "02", "10", "11", "20", "30", "31", "32" };
        public static List<MonitorTypeStateModel> OverrideStates => 
            States.Where(x => OverrideStateCodes.Contains(x.Code)).ToList();

        public static Dictionary<string, string> StateNameMap => States.ToDictionary(x => x.Code??"", x => x.Name);
    }
}
