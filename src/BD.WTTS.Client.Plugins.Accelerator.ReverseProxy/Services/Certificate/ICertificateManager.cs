using dotnetCampus.Ipc.CompilerServices.Attributes;
using Const = BD.WTTS.Constants;

// ReSharper disable once CheckNamespace
namespace BD.WTTS.Services;

/// <summary>
/// 证书管理(安装/卸载)
/// </summary>
[IpcPublic(Timeout = AssemblyInfo.IpcTimeout, IgnoresIpcException = false)]
public interface ICertificateManager
{
    static class Constants
    {
        public static ICertificateManager Instance => Ioc.Get<ICertificateManager>(); // 因为 Ipc 服务接口的原因，不能将此属性放在非嵌套类上

        internal static bool IsCertificateInstalled(
            IPCPlatformService platformService,
            X509CertificatePackable packable)
        {
            X509Certificate2? certificate2 = packable;
            if (certificate2 == null)
                return false;
            if (certificate2.NotAfter <= DateTime.Now)
                return false;

            if (OperatingSystem.IsAndroid() ||
                OperatingSystem.IsLinux() ||
                OperatingSystem.IsMacOS())
            {
                return platformService.IsCertificateInstalled(packable);
            }
            else
            {
                using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                return store.Certificates.Contains(certificate2);
            }
        }

        internal static bool IsRootCertificateInstalled(
            ICertificateManager certificateManager,
            IPCPlatformService platformService,
            X509CertificatePackable packable)
        {
            if (EqualityComparer<X509CertificatePackable>.Default.Equals(packable, default))
            {
                var filePath = certificateManager.GetCerFilePathGeneratedWhenNoFileExists();
                if (filePath == null)
                    return false;
            }

            var isInstalled = IsCertificateInstalled(platformService, packable);
            return isInstalled;
        }

        internal static void TrustRootCertificate(
            Func<string?> getCerFilePath,
            IPCPlatformService platformService,
            X509Certificate2 certificate2)
        {
            if (OperatingSystem.IsWindows())
            {
                using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
                try
                {
                    store.Open(OpenFlags.ReadWrite);

                    var findCerts = store.Certificates.Find(X509FindType.FindByThumbprint, certificate2.Thumbprint, true);
                    if (!findCerts.Any())
                    {
                        store.Add(certificate2);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(nameof(ICertificateManager), e,
                        "Please manually install the CA certificate to a trusted root certificate authority.");
                }
            }
            else if (OperatingSystem.IsMacOS())
            {
                var cerFilePath = getCerFilePath();
                if (cerFilePath == null)
                    return;

                void TrustRootCertificateMacOS()
                {
                    var result = platformService.TrustRootCertificateAsync(cerFilePath);
                    if (result.HasValue && !result.Value)
                    {
                        TrustRootCertificateMacOS();
                    }
                }
                TrustRootCertificateMacOS();
            }
            else if (OperatingSystem.IsLinux())
            {
                var cerFilePath = getCerFilePath();
                if (cerFilePath == null)
                    return;

                void TrustRootCertificateLinux()
                {
                    var result = platformService.TrustRootCertificateAsync(cerFilePath);
                    try
                    {
                        // 部分系统还是只能手动导入浏览器
                        Browser2.Open(Const.Urls.OfficialWebsite_LiunxSetupCer);
                    }
                    catch
                    {

                    }
                    if (result.HasValue && !result.Value)
                        getCerFilePath();
                }
                TrustRootCertificateLinux();
            }
        }
    }

    /// <summary>
    /// 证书密码的 Utf8String
    /// </summary>
    byte[]? PfxPassword { get; }

    #region Path

    /// <summary>
    /// PFX 证书文件路径
    /// </summary>
    string PfxFilePath => CertificateConstants.DefaultPfxFilePath;

    /// <summary>
    /// CER 证书文件路径
    /// </summary>
    string CerFilePath => CertificateConstants.DefaultCerFilePath;

    #endregion

    /// <summary>
    /// 获取当前 Root 证书，<see cref="X509CertificatePackable"/> 类型可隐式转换为 <see cref="X509Certificate2"/>
    /// </summary>
    X509CertificatePackable RootCertificatePackable { get; set; }

    /// <summary>
    /// 获取 Cer 证书路径，当不存在时生成文件后返回路径
    /// </summary>
    /// <returns></returns>
    string? GetCerFilePathGeneratedWhenNoFileExists();

    /// <summary>
    /// 信任根证书，有 Root 权限将尝试执行信任，否则则 UI 引导，跳转网页或弹窗
    /// </summary>
    void TrustRootCertificate();

    /// <summary>
    /// 安装根证书，如果没有证书将生成一个新的
    /// </summary>
    /// <returns>返回根证书是否受信任</returns>
    bool SetupRootCertificate();

    /// <summary>
    /// 删除根证书，如果没有证书将返回 <see langword="true"/>
    /// </summary>
    /// <returns></returns>
    bool DeleteRootCertificate();

    /// <summary>
    /// 当前根证书是否已安装并信任
    /// </summary>
    [Obsolete("use IsRootCertificateInstalled2")]
    bool IsRootCertificateInstalled { get; }

    /// <summary>
    /// 当前根证书是否已安装并信任
    /// </summary>
    bool? IsRootCertificateInstalled2 { get; }

    /// <summary>
    /// (✔️🔒)生成 Root 证书
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
    bool? GenerateCertificate();
}