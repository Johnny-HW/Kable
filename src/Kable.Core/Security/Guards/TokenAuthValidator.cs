namespace Kable.Core.Security.Guards;

using System;
using Kable.Core.Security;

/// <summary>
/// SecurityTransportOptions에 기반하여 토큰 인증을 수행하는 표준 검증기입니다.
/// </summary>
public sealed class TokenAuthValidator : ITokenAuthValidator
{
    private const string BearerPrefix = "Bearer ";
    private readonly SecurityTransportOptions _options;

    public TokenAuthValidator(SecurityTransportOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public bool ValidateToken(string? token)
    {
        if (!_options.RequireTokenAuthentication)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var trimmed = token!.Trim();

        // 1. 커스텀 검증기가 지정된 경우 우선 수행
        if (_options.CustomTokenValidator != null)
        {
            return _options.CustomTokenValidator(trimmed);
        }

        // 2. 정적 토큰 목록 검증
        return _options.ValidTokens.Contains(trimmed);
    }

    public bool ValidateHeader(string? rawHeaderValue)
    {
        if (!_options.RequireTokenAuthentication)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(rawHeaderValue))
        {
            return false;
        }

        var trimmed = rawHeaderValue!.Trim();

        // Bearer 접두어 처리
        if (trimmed.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var tokenPart = trimmed.Substring(BearerPrefix.Length).Trim();
            return ValidateToken(tokenPart);
        }

        return ValidateToken(trimmed);
    }
}
