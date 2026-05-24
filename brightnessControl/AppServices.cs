using brightnessControl.Services;

namespace brightnessControl;

public sealed class AppServices
{
    private AppServices(
        AppLogger logger,
        ProfileService profileService,
        GammaService gammaService,
        GameDiscoveryService gameDiscoveryService,
        INvidiaColorService nvidiaColorService,
        RecoveryService recoveryService,
        ProcessMonitorService processMonitorService)
    {
        Logger = logger;
        ProfileService = profileService;
        GammaService = gammaService;
        GameDiscoveryService = gameDiscoveryService;
        NvidiaColorService = nvidiaColorService;
        RecoveryService = recoveryService;
        ProcessMonitorService = processMonitorService;
    }

    public AppLogger Logger { get; }

    public ProfileService ProfileService { get; }

    public GammaService GammaService { get; }

    public GameDiscoveryService GameDiscoveryService { get; }

    public INvidiaColorService NvidiaColorService { get; }

    public RecoveryService RecoveryService { get; }

    public ProcessMonitorService ProcessMonitorService { get; }

    public static AppServices Create()
    {
        var logger = new AppLogger();
        var profileService = new ProfileService(logger);
        var gammaService = new GammaService(logger);
        var gameDiscoveryService = new GameDiscoveryService(logger);
        INvidiaColorService nvidiaColorService = CreateNvidiaColorService(logger);
        var recoveryService = new RecoveryService(
            logger,
            gammaService,
            nvidiaColorService,
            () => profileService.DisplayTarget);
        var processMonitorService = new ProcessMonitorService(logger);

        return new AppServices(
            logger,
            profileService,
            gammaService,
            gameDiscoveryService,
            nvidiaColorService,
            recoveryService,
            processMonitorService);
    }

    private static INvidiaColorService CreateNvidiaColorService(AppLogger logger)
    {
        try
        {
            var nvApi = new NvApi(logger);
            logger.Info("NVAPI initialized. NVIDIA scanout color service is active.");
            return new NvApiNvidiaColorService(logger, nvApi);
        }
        catch (Exception ex)
        {
            logger.Warning($"NVAPI unavailable. Using Windows gamma fallback only. {ex.Message}");
            return new DummyNvidiaColorService(logger);
        }
    }
}
