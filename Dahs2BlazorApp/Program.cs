using Dahs2BlazorApp.Areas.Identity;
using Dahs2BlazorApp.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.EntityFrameworkCore;
using Syncfusion.Blazor;
using System.Globalization;
using Dahs2BlazorApp.Configuration;
using Dahs2BlazorApp.Shared;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Localization;
using Dahs2BlazorApp.Db;
using Dapper;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using Dahs2BlazorApp.Models;
using Serilog.Core;
using Syncfusion.Blazor.Popups;

var builder = WebApplication.CreateBuilder(args);

// Register the Syncfusion license
Syncfusion.Licensing.SyncfusionLicenseProvider
    .RegisterLicense(
        "Mgo+DSMBMAY9C3t2VlhhQlJCfV5AQmBIYVp/TGpJfl96cVxMZVVBJAtUQF1hSn9TdkFjUH1WdXZUQGRV;MjczOTg5NUAzMjMzMmUzMDJlMzBLYzRRL3BKRTZCTzJ1TFJid1NBV2VmRkVUemdTa3ZwNW0zOTFrZ1BqcHVVPQ==");

// Enable Hsl
if (!HslCommunication.Authorization.SetAuthorizationCode("b23b00e2-ce46-4bfc-b33c-71c47c2c11c2"))
{
    Console.WriteLine(@"Hsl Communication active failed");
    Console.ReadLine();
}

// Create Serilog logger
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();
// Workaround for Dapper TimeOnly mapping
SqlMapper.AddTypeHandler(new TimeOnlyHandler());

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("Authentication") ??
                       throw new InvalidOperationException("Connection string 'Authentication' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequiredLength = 4;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();
builder.Services.AddScoped<SfDialogService>();
builder.Services.AddSyncfusionBlazor();
builder.Services.AddSingleton(typeof(ISyncfusionStringLocalizer), typeof(SyncfusionLocalizer));

builder.Services.AddRazorPages();
builder.Services.AddSignalR(e => { e.MaximumReceiveMessageSize = 102400000; });
builder.Services.AddServerSideBlazor();
builder.Services
    .AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();

builder.Services.AddSingleton<ISqlServer, SqlServer>();
builder.Services.AddSingleton<PipeIo>();
builder.Services.AddSingleton<MonitorTypeIo>();
builder.Services.AddSingleton<RecordIo>();
builder.Services.AddSingleton<DeviceIo>();
builder.Services.AddSingleton<DeviceMeasuringIo>();
builder.Services.AddSingleton<DeviceOutputIo>();
builder.Services.AddSingleton<DeviceSignalIo>();
builder.Services.AddSingleton<SysConfigIo>();
builder.Services.AddSingleton<SiteInfoIo>();
builder.Services.AddSingleton<MeasuringAdjust>();
builder.Services.AddSingleton<FuelIo>();
builder.Services.AddSingleton<DataCollectManager>();
builder.Services.AddSingleton<RecordIo>();
builder.Services.AddSingleton<AlarmIo>();
builder.Services.AddSingleton<ISyncfusionStringLocalizer, SyncfusionLocalizer>();
builder.Services.AddHttpClient<ILineNotify, LineNotify>();
builder.Services.AddSingleton<ExcelUtility>();
builder.Services.AddHostedService<BootTask>();
builder.Services.AddHostedService<DataCollectManager>(p => p.GetRequiredService<DataCollectManager>());
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
var cultureStrings = new[] { "en-US", "zh-hant", "zh-hans" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(cultureStrings[1])
    .AddSupportedCultures(cultureStrings)
    .AddSupportedUICultures(cultureStrings);
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = cultureStrings.Select(x => new CultureInfo(x)).ToList();
    options.DefaultRequestCulture = new RequestCulture("zh-hant");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
});
builder.Services.AddControllersWithViews().AddViewLocalization().AddDataAnnotationsLocalization();
var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseRequestLocalization(localizationOptions);

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

Log.Information("DAHS2 started. {Version}", SiteConfig.Version);
app.Run();