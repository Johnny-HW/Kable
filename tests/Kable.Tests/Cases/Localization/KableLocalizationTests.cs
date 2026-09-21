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
    public void Localizer_ShouldTranslateToChineseSimplified()
    {
        var localizer = KableLocalizer.Instance;
        localizer.SetCulture(new CultureInfo("zh-CN"));

        var msg = localizer.GetErrorMessage(KableErrorCode.DeviceTimeout, "READ", 3000);
        Assert.Contains("命令 'READ' 在 3000ms 后响应超时", msg);

        var protoMsg = localizer.GetErrorMessage(KableErrorCode.ProtocolViolation, "Invalid header");
        Assert.Contains("发生协议违规: Invalid header", protoMsg);
    }

    [Fact]
    public void Localizer_ShouldTranslateToChineseTraditional()
    {
        var localizer = KableLocalizer.Instance;
        localizer.SetCulture(new CultureInfo("zh-TW"));

        var msg = localizer.GetErrorMessage(KableErrorCode.DeviceTimeout, "READ", 3000);
        Assert.Contains("指令 'READ' 在 3000ms 後回應逾時", msg);

        var discMsg = localizer.GetErrorMessage(KableErrorCode.DeviceDisconnected);
        Assert.Contains("硬體連線已中斷", discMsg);
    }

    [Fact]
    public void Localizer_ShouldTranslateToJapanese()
    {
        var localizer = KableLocalizer.Instance;
        localizer.SetCulture(new CultureInfo("ja-JP"));

        var msg = localizer.GetErrorMessage(KableErrorCode.DeviceTimeout, "READ", 3000);
        Assert.Contains("コマンド 'READ' の応答が 3000ms 待機後にタイムアウトしました", msg);

        var discMsg = localizer.GetErrorMessage(KableErrorCode.DeviceDisconnected);
        Assert.Contains("ハードウェア接続が切断されました", discMsg);
    }

    [Fact]
    public void Localizer_ShouldTranslateToGerman()
    {
        var localizer = KableLocalizer.Instance;
        localizer.SetCulture(new CultureInfo("de-DE"));

        var msg = localizer.GetErrorMessage(KableErrorCode.DeviceTimeout, "READ", 3000);
        Assert.Contains("Befehl 'READ' hat nach 3000ms eine Zeitüberschreitung verursacht", msg);

        var discMsg = localizer.GetErrorMessage(KableErrorCode.DeviceDisconnected);
        Assert.Contains("Hardware-Verbindung wurde getrennt", discMsg);
    }

    [Fact]
    public void Localizer_ShouldTranslateToFrench()
    {
        var localizer = KableLocalizer.Instance;
        localizer.SetCulture(new CultureInfo("fr-FR"));

        var msg = localizer.GetErrorMessage(KableErrorCode.DeviceTimeout, "READ", 3000);
        Assert.Contains("La commande 'READ' a expiré après 3000ms", msg);

        var discMsg = localizer.GetErrorMessage(KableErrorCode.DeviceDisconnected);
        Assert.Contains("La connexion matérielle a été interrompue", discMsg);
    }

    [Fact]
    public void Localizer_ShouldTranslateToSpanish()
    {
        var localizer = KableLocalizer.Instance;
        localizer.SetCulture(new CultureInfo("es-ES"));

        var msg = localizer.GetErrorMessage(KableErrorCode.DeviceTimeout, "READ", 3000);
        Assert.Contains("El comando 'READ' agotó el tiempo de espera tras 3000ms", msg);

        var discMsg = localizer.GetErrorMessage(KableErrorCode.DeviceDisconnected);
        Assert.Contains("La conexión de hardware se ha desconectado", discMsg);
    }

    [Fact]
    public void Localizer_ShouldTranslateUiStringsDynamicallyAcrossLanguages()
    {
        var localizer = KableLocalizer.Instance;

        // Korean
        localizer.SetCulture(new CultureInfo("ko-KR"));
        Assert.Equal("💬 수시 명령 통신 (Aperiodic)", localizer.GetString("Header_CommandConsole"));
        Assert.Equal("순번(#)", localizer.GetString("Col_Seq"));
        Assert.Equal("수동 명령 전송", localizer.GetString("Btn_SendManual"));

        // English
        localizer.SetCulture(new CultureInfo("en-US"));
        Assert.Equal("💬 Aperiodic Commands", localizer.GetString("Header_CommandConsole"));
        Assert.Equal("#", localizer.GetString("Col_Seq"));
        Assert.Equal("Send Manual Command", localizer.GetString("Btn_SendManual"));

        // Spanish
        localizer.SetCulture(new CultureInfo("es-ES"));
        Assert.Equal("💬 Comandos Aperiódicos", localizer.GetString("Header_CommandConsole"));
        Assert.Equal("#", localizer.GetString("Col_Seq"));
        Assert.Equal("Enviar Comando Manual", localizer.GetString("Btn_SendManual"));
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
