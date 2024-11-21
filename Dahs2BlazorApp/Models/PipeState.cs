namespace Dahs2BlazorApp.Models
{
    public record PipeStateModel(string Code, string Name);


    public static class PipeState
    {
        public static List<PipeStateModel> PipeStateModelList = new()
        {
            new PipeStateModel("A", "A - 正常運轉-故障、維修"),
            new PipeStateModel("N", "N - 污染源正常運轉"),
            new PipeStateModel("B", "B - 起火-故障、維修"),
            new PipeStateModel("C", "C - 起火-污染源正常運轉"),
            new PipeStateModel("D", "D - 停車-故障、維修"),
            new PipeStateModel("E", "E - 停車-正常運轉"),
            new PipeStateModel("F", "F - 污染源暫停運轉"),
            new PipeStateModel("G", "G - 歲(檢)修"),
            new PipeStateModel("P", "P - 停工"),
        };

        public static IDictionary<string, string> PipeStateCssMap => PipeStateModelList.ToDictionary(x => x.Code, x => x.Code switch
        {
            "A" => "pollution_prevention_broken_status",
            "N" => "",
            "B" => "pollution_prevention_broken_status",
            "C" => "not_operating_pipe_status",
            "D" => "pollution_prevention_broken_status",
            "E" => "not_operating_pipe_status",
            "F" => "not_operating_pipe_status",
            "G" => "not_operating_pipe_status",
            "P" => "not_operating_pipe_status",
            _ => ""
        });
        
        public static IDictionary<string, string> PipeStateNameMap => PipeStateModelList.ToDictionary(x => x.Code, x => x.Name);

        public static IDictionary<string, string> PipeStateFullNameMap => PipeStateModelList.ToDictionary(x => x.Code, x => $"{x.Code} - {x.Name}");

        public static string GetPipeStateFullName(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";

            return PipeStateFullNameMap.TryGetValue(code, out var fullName) ? fullName : "";
        }
    }
}
