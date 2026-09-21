using System;
using System.Globalization;
using Kable.Exceptions;
using Kable.Localization;
using Xunit;

namespace Kable.Tests.Cases.Localization;

public sealed class KableLocalizationTests
{
    [Fact]
    public void Localizer_ShouldTranslateToEnglishByDefault()
    {
        var localizer = KableLocalizer.Instance;
        localizer.SetCulture(new CultureInfo("en-US"));

        var msg = localizer.GetErrorMessage(KableErrorCode.DeviceTimeout, "READ", 3000);
        Assert.Contains("Command 'READ' timed out after 3000ms", msg);
    }

    [Fact]
    public void Localizer_ShouldTranslateToKorean()
    {
        var localizer = KableLocalizer.Instance;
        localizer.SetCulture(new CultureInfo("ko-KR"));

        var msg = localizer.GetErrorMessage(KableErrorCode.DeviceTimeout, "READ", 3000);
        Assert.Contains("명령어 'READ'이(가) 3000ms 동안 응답이 없어 시간 초과되었습니다", msg);

        var discMsg = localizer.GetErrorMessage(KableErrorCode.DeviceDisconnected);
        Assert.Contains("하드웨어 연결이 끊어졌습니다", discMsg);
    }

    [Fact]
    public void Localizer_ShouldTranslateToChinese()
    {
        var localizer = KableLocalizer.Instance;
        localizer.SetCulture(new CultureInfo("zh-CN"));

        var msg = localizer.GetErrorMessage(KableErrorCode.DeviceTimeout, "READ", 3000);
        Assert.Contains("命令 'READ' 在 3000ms 后响应超时", msg);

        var protoMsg = localizer.GetErrorMessage(KableErrorCode.ProtocolViolation, "Invalid header");
        Assert.Contains("发生协议违规: Invalid header", protoMsg);
    }

    [Fact]
    public void LocalizedException_ReturnsLocalizedMessage()
    {
        var localizer = KableLocalizer.Instance;
        localizer.SetCulture(new CultureInfo("ko-KR"));

        var ex = new DeviceTimeoutException("LP_A", TimeSpan.FromSeconds(3));
        Assert.Equal(KableErrorCode.DeviceTimeout, ex.ErrorCode);
        Assert.Equal("LP_A", ex.Command);
        Assert.Equal(TimeSpan.FromSeconds(3), ex.Timeout);

        var localized = ex.GetLocalizedMessage();
        Assert.Contains("명령어 'LP_A'이(가) 3000ms 동안 응답이 없어 시간 초과되었습니다", localized);
    }
}
