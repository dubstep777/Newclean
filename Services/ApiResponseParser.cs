using System.Text.Json;
using XUnlock.Models;

namespace XUnlock.Services;

public static class ApiResponseParser
{
    public static ApiStateResult ParseState(string json, TimeSpan rtt, DateTimeOffset? serverDate)
    {
        if (!TryGetCode(json, out var code))
            return new(PermissionState.ApiChanged, null, "Ответ API не содержит ожидаемый code", rtt, serverDate, Shape(json));

        return code switch
        {
            -1 => new(PermissionState.TemporaryError, code, "API Error", rtt, serverDate, Shape(json)),
            1 => new(PermissionState.Granted, code, "Разрешение уже выдано", rtt, serverDate, Shape(json)),
            2 => new(PermissionState.ApplyAvailable, code, "Можно подавать заявку", rtt, serverDate, Shape(json)),
            3 => new(PermissionState.NotEligible, code, "Ошибка аккаунта / условия аккаунта не выполнены", rtt, serverDate, Shape(json)),
            4 => new(PermissionState.NotEligible, code, "Аккаунт должен быть зарегистрирован более 30 дней", rtt, serverDate, Shape(json)),
            _ => new(PermissionState.ApiChanged, code, $"Неизвестный code={code}", rtt, serverDate, Shape(json))
        };
    }

    public static ApplyResult ParseApply(string json, TimeSpan rtt, DateTimeOffset? serverDate)
    {
        if (!TryGetCode(json, out var code))
            return new(ApplyState.ApiChanged, null, "Ответ API не содержит ожидаемый code", rtt, serverDate, Shape(json));

        return code switch
        {
            -1 => new(ApplyState.TemporaryError, code, "API Error", rtt, serverDate, Shape(json)),
            1 => new(ApplyState.Successful, code, "Заявка успешно принята", rtt, serverDate, Shape(json)),
            2 => new(ApplyState.AccountError, code, "Ошибка аккаунта", rtt, serverDate, Shape(json)),
            3 => new(ApplyState.QuotaReached, code, "Достигнут лимит заявок", rtt, serverDate, Shape(json)),
            4 => new(ApplyState.Failed, code, "Заявка отклонена API", rtt, serverDate, Shape(json)),
            5 => new(ApplyState.TryAgainMinute, code, "API просит повторить через минуту", rtt, serverDate, Shape(json)),
            6 => new(ApplyState.TryAgainLater, code, "API просит повторить позже", rtt, serverDate, Shape(json)),
            _ => new(ApplyState.ApiChanged, code, $"Неизвестный code={code}", rtt, serverDate, Shape(json))
        };
    }

    private static bool TryGetCode(string json, out int code)
    {
        code = 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return TryGetCode(doc.RootElement, out code);
        }
        catch (JsonException) { return false; }
    }

    private static bool TryGetCode(JsonElement e, out int code)
    {
        code = 0;
        if (e.ValueKind == JsonValueKind.Object)
        {
            if (e.TryGetProperty("code", out var p) && p.TryGetInt32(out code)) return true;
            if (e.TryGetProperty("data", out var d) && TryGetCode(d, out code)) return true;
            if (e.TryGetProperty("result", out var r) && TryGetCode(r, out code)) return true;
        }
        return false;
    }

    private static string Shape(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return string.Join(',', doc.RootElement.EnumerateObject().Select(p => p.Name).Take(20));
        }
        catch { return "invalid-json"; }
    }
}
