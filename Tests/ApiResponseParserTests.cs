using XUnlock.Models;
using XUnlock.Services;

namespace XUnlock.Tests;

public class ApiResponseParserTests
{
    [Fact]
    public void State_1_IsGranted()
    {
        var r = ApiResponseParser.ParseState("{\"code\":1}", TimeSpan.FromMilliseconds(10), null);
        Assert.Equal(PermissionState.Granted, r.State);
    }

    [Fact]
    public void State_2_IsApplyAvailable()
    {
        var r = ApiResponseParser.ParseState("{\"code\":2}", TimeSpan.FromMilliseconds(10), null);
        Assert.Equal(PermissionState.ApplyAvailable, r.State);
    }

    [Fact]
    public void State_NestedCode_IsSupported()
    {
        var r = ApiResponseParser.ParseState("{\"data\":{\"code\":4}}", TimeSpan.FromMilliseconds(10), null);
        Assert.Equal(PermissionState.NotEligible, r.State);
    }

    [Fact]
    public void UnknownCode_IsApiChanged()
    {
        var r = ApiResponseParser.ParseState("{\"code\":999}", TimeSpan.Zero, null);
        Assert.Equal(PermissionState.ApiChanged, r.State);
    }

    [Fact]
    public void InvalidJson_IsApiChanged()
    {
        var r = ApiResponseParser.ParseState("not-json", TimeSpan.Zero, null);
        Assert.Equal(PermissionState.ApiChanged, r.State);
    }

    [Fact]
    public void Apply_1_IsSuccessful()
    {
        var r = ApiResponseParser.ParseApply("{\"code\":1}", TimeSpan.Zero, null);
        Assert.Equal(ApplyState.Successful, r.State);
    }

    [Fact]
    public void Apply_5_IsMinuteRetry()
    {
        var r = ApiResponseParser.ParseApply("{\"code\":5}", TimeSpan.Zero, null);
        Assert.Equal(ApplyState.TryAgainMinute, r.State);
    }
}
