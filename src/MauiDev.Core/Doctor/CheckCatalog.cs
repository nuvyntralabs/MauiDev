using MauiDev.Checks;

namespace MauiDev;

public static class CheckCatalog
{
    public static IReadOnlyList<ICheck> DoctorChecks { get; } =
    [
        new DotNetSdkCheck(),
        new MauiWorkloadCheck(),
        new AndroidSdkCheck(),
        new JdkCheck(),
        new XcodeCheck(),
        new CocoaPodsCheck(),
        new MauiCliCheck(),
        new TargetFrameworkCheck(),
        new AndroidMinSdkCheck(),
        new PermissionsCheck(),
        new MauiResourceDuplicateCheck(),
        new UseMauiCheck(),
        new SigningCheck()
    ];
}
