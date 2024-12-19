using Microsoft.Data.SqlClient;

namespace Dahs2BlazorApp.Db;

public class SiteInfoIo
{
    private readonly SysConfigIo _sysConfigIo;
    private readonly ILogger<SiteInfoIo> _logger;

    public SiteInfoIo(SysConfigIo sysConfigIo, ILogger<SiteInfoIo> logger)
    {
        _sysConfigIo = sysConfigIo;
        _logger = logger;
    }

    public interface ISiteInfo
    {
        string Name { get; set; }
        string SiteCode { get; set; }
        string Address { get; set; }
        string Owner { get; set; }
        string PlaceNumber { get; set; }
        string Industry { get; set; }
        string IndustryCode { get; set; }
        string Phone { get; set; }
    }

    public class SiteInfo : ISiteInfo
    {
        public required string Name { get; set; }
        public required string SiteCode { get; set; }
        public required string Address { get; set; }
        public required string Owner { get; set; }
        public required string PlaceNumber { get; set; }
        public required string Industry { get; set; }
        public required string IndustryCode { get; set; }
        public required string Phone { get; set; }
    }

    public async Task<SiteInfo> GetSiteInfo()
    {
        try
        {
            var siteName = await _sysConfigIo.GetSysConfig($"SiteInfo-{nameof(SiteInfo.Name)}");
            var siteCode = await _sysConfigIo.GetSysConfig($"SiteInfo-{nameof(SiteInfo.SiteCode)}");
            var address = await _sysConfigIo.GetSysConfig($"SiteInfo-{nameof(SiteInfo.Address)}");
            var owner = await _sysConfigIo.GetSysConfig($"SiteInfo-{nameof(SiteInfo.Owner)}");
            var placeNumber = await _sysConfigIo.GetSysConfig($"SiteInfo-{nameof(SiteInfo.PlaceNumber)}");
            var industry = await _sysConfigIo.GetSysConfig($"SiteInfo-{nameof(SiteInfo.Industry)}");
            var industryCode = await _sysConfigIo.GetSysConfig($"SiteInfo-{nameof(SiteInfo.IndustryCode)}");
            var phone = await _sysConfigIo.GetSysConfig($"SiteInfo-{nameof(SiteInfo.Phone)}");
            
            return new SiteInfo
            {
                Name = siteName,
                SiteCode = siteCode,
                Address = address,
                Owner = owner,
                PlaceNumber = placeNumber,
                Industry = industry,
                IndustryCode = industryCode,
                Phone = phone
            };
        }
        catch (Exception e)
        {
            _logger.LogError(e, "GetSiteInfo");
            throw;
        }
    }

    public async Task UpsertSiteInfo(ISiteInfo siteInfo)
    {
        try
        {
            var t1 = _sysConfigIo.UpsertSysConfig(new SysConfigIo.SysConfig($"SiteInfo-{nameof(SiteInfo.Name)}",
                siteInfo.Name));
            var t2 = _sysConfigIo.UpsertSysConfig(new SysConfigIo.SysConfig($"SiteInfo-SiteCode",
                siteInfo.SiteCode));
            var t3 = _sysConfigIo.UpsertSysConfig(new SysConfigIo.SysConfig($"SiteInfo-Address",
                siteInfo.Address));
            var t4 = _sysConfigIo.UpsertSysConfig(new SysConfigIo.SysConfig($"SiteInfo-{nameof(SiteInfo.Owner)}",
                siteInfo.Owner));
            var t5 = _sysConfigIo.UpsertSysConfig(new SysConfigIo.SysConfig($"SiteInfo-{nameof(SiteInfo.PlaceNumber)}",
                siteInfo.PlaceNumber));
            var t6 = _sysConfigIo.UpsertSysConfig(new SysConfigIo.SysConfig($"SiteInfo-{nameof(SiteInfo.Industry)}",
                siteInfo.Industry));
            var t7 = _sysConfigIo.UpsertSysConfig(new SysConfigIo.SysConfig($"SiteInfo-{nameof(SiteInfo.IndustryCode)}",
                siteInfo.IndustryCode));
            var t8 = _sysConfigIo.UpsertSysConfig(new SysConfigIo.SysConfig($"SiteInfo-{nameof(SiteInfo.Phone)}",
                siteInfo.Phone));
            
            await Task.WhenAll(t1, t2, t3, t4, t5, t6, t7, t8);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "UpsertSiteInfo");
            throw;
        }
    }
}