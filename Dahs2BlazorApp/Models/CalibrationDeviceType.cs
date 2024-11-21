using System.Collections.Immutable;

namespace Dahs2BlazorApp.Models;

public record CalibrationDeviceType(int Type, string Name);

public static class CalibrationDeviceTypes{
    /*
     * 1－標準氣體鋼瓶、2－氣體匣、3－濾光片、4－儀用空氣、5－模擬訊號、6－其他
     */

    public static readonly List<CalibrationDeviceType> DeviceTypeList = new() {
        new CalibrationDeviceType(1, "標準氣體鋼瓶"),
        new CalibrationDeviceType(2, "氣體匣"),
        new CalibrationDeviceType(3, "濾光片"),
        new CalibrationDeviceType(4, "儀用空氣"),
        new CalibrationDeviceType(5, "模擬訊號"),
        new CalibrationDeviceType(6, "其他")
    };

    public static readonly ImmutableDictionary<int, string> DeviceTypeNameMap =
        DeviceTypeList.ToImmutableDictionary(t => t.Type, t => t.Name);
}