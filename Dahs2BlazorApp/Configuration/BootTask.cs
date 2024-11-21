using Dahs2BlazorApp.Db;
using Dahs2BlazorApp.Models;
using Microsoft.AspNetCore.Identity;

namespace Dahs2BlazorApp.Configuration
{
    internal sealed class BootTask : IHostedService
    {
        private readonly ILogger _logger;
        private readonly PipeIo _pipeIo;
        private readonly MonitorTypeIo _monitorTypeIo;
        private readonly RecordIo _recordIo;
        private readonly DeviceIo _deviceIo;
        private readonly DeviceMeasuringIo _deviceMeasuringIo;
        private readonly DeviceOutputIo _deviceOutputIo;
        private readonly DeviceSignalIo _deviceSignalIo;
        private readonly SysConfigIo _sysConfigIo;
        private readonly IHostEnvironment _env;
        readonly IServiceScopeFactory _scopeFactory;

        public BootTask(ILogger<BootTask> logger,
            PipeIo pipeIo,
            MonitorTypeIo monitorTypeIo,
            RecordIo recordIo,
            DeviceIo deviceIo,
            DeviceMeasuringIo deviceMeasuringIo,
            DeviceOutputIo deviceOutputIo,
            DeviceSignalIo deviceSignalIo,
            SysConfigIo sysConfigIo,
            IHostEnvironment env,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _pipeIo = pipeIo;
            _monitorTypeIo = monitorTypeIo;
            _recordIo = recordIo;
            _deviceIo = deviceIo;
            _deviceMeasuringIo = deviceMeasuringIo;
            _deviceOutputIo = deviceOutputIo;
            _deviceSignalIo = deviceSignalIo;
            _sysConfigIo = sysConfigIo;
            _env = env;
            _scopeFactory = scopeFactory;
        }

        private async Task InitRoles()
        {
            var roles = new List<string> { Role.Admin, Role.Operator, Role.User };
            using var scope = _scopeFactory.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }

        private record UserProfile(string Username, string Password, string Email, string Role);

        private async Task CreateDefaultUsers()
        {
            var users = new List<UserProfile>
            {
                new("admin@atlas", "admin", "admin@atlas", Role.Admin),
                new("operator@atlas", "operator", "operator@atlas", Role.Operator),
                new("user@atlas", "user", "user@atlas", Role.User)
            };
            using var scope = _scopeFactory.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            foreach (var profile in users)
            {
                var user = await userManager.FindByNameAsync(profile.Username);

                if (user == null)
                {
                    user = new IdentityUser(profile.Username)
                    {
                        Id = Guid.NewGuid().ToString(),
                        Email = profile.Email
                    };
                    var ret = await userManager.CreateAsync(user, profile.Password);
                    if (!ret.Succeeded)
                    {
                        _logger.LogError("Create user {Username} failed, {Errors}",
                            profile.Username, ret.Errors.Select(x => x.Description));
                        continue;
                    }

                    await userManager.AddToRoleAsync(user, profile.Role);
                }

                var confirmed = await userManager.IsEmailConfirmedAsync(user);
                if (confirmed == false)
                {
                    _logger.LogInformation("Confirm email for {Username}", profile.Username);
                    var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
                    await userManager.ConfirmEmailAsync(user, token);
                }
            }
        }

        private Task SetupUploadFolder()
        {
            return Task.Run(async () =>
            {
                try
                {
                    EpaUploadSetting.EpaRootFolder =
                        Path.Combine(Path.GetPathRoot(_env.ContentRootPath) ?? string.Empty, "AtlasCPMS", "EPA");
                    EpaUploadSetting.UploadFolder = await _sysConfigIo.GetSysConfig(SysConfigIo.UploadPathKey);
                    EpaUploadSetting.EnsureFolder();
                    EpaUploadSetting.IsTestUpload = await _sysConfigIo.GetTestUpload();
                    _logger.LogInformation("EpaRootFolder: {EpaRootFolder}", EpaUploadSetting.EpaRootFolder);
                    _logger.LogInformation("UploadFolder: {UploadFolder}", EpaUploadSetting.UploadFolder);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "SetupUploadFolder failed");
                }
            });
            
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("BootTask init...");
                await _pipeIo.Init();
                await _monitorTypeIo.Init();
                await _recordIo.Init();
                await _deviceIo.Init();
                await _deviceMeasuringIo.Init();
                await _deviceOutputIo.Init();
                await _deviceSignalIo.Init();

                await InitRoles();
                await CreateDefaultUsers();
                _ = SetupUploadFolder();
                // Init Device Manager
                DeviceManager.Init(_deviceIo, _deviceMeasuringIo, _deviceSignalIo);
                _logger.LogInformation("BootTask init successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BootTask init failed");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}