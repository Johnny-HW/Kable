namespace Kable.Tests.Cases.Security;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core.Security;
using Kable.Core.Security.Guards;
using Xunit;

public sealed class ConcurrencyGuardTests
{
    [Fact]
    public async Task TryAcquireAsync_WhenNotBusy_SucceedsAndReleasesCleanly()
    {
        await using var guard = new SingleFlightConcurrencyGuard(new ConcurrencyOptions
        {
            Mode = BusyHandlingMode.RejectImmediately
        });

        const string deviceKey = "PLC_DEV_01";
        Assert.False(guard.IsBusy(deviceKey));

        var handle = await guard.TryAcquireAsync(deviceKey);
        Assert.NotNull(handle);
        Assert.True(guard.IsBusy(deviceKey));

        await handle!.DisposeAsync();
        Assert.False(guard.IsBusy(deviceKey));
    }

    [Fact]
    public async Task TryAcquireAsync_RejectImmediately_ReturnsNullOnConcurrentAccess()
    {
        await using var guard = new SingleFlightConcurrencyGuard(new ConcurrencyOptions
        {
            Mode = BusyHandlingMode.RejectImmediately
        });

        const string deviceKey = "PLC_DEV_02";

        var firstHandle = await guard.TryAcquireAsync(deviceKey);
        Assert.NotNull(firstHandle);

        // 두 번째 요청은 즉시 거부(null)되어야 함
        var secondHandle = await guard.TryAcquireAsync(deviceKey);
        Assert.Null(secondHandle);

        // 첫 번째 작업 해제 후에는 재획득 가능
        await firstHandle!.DisposeAsync();
        var thirdHandle = await guard.TryAcquireAsync(deviceKey);
        Assert.NotNull(thirdHandle);
        await thirdHandle!.DisposeAsync();
    }

    [Fact]
    public async Task AcquireAsync_RejectImmediately_ThrowsDeviceBusyException()
    {
        await using var guard = new SingleFlightConcurrencyGuard(new ConcurrencyOptions
        {
            Mode = BusyHandlingMode.RejectImmediately
        });

        const string deviceKey = "PLC_DEV_03";

        var handle = await guard.AcquireAsync(deviceKey);
        Assert.NotNull(handle);

        var ex = await Assert.ThrowsAsync<DeviceBusyException>(async () =>
        {
            await guard.AcquireAsync(deviceKey);
        });

        Assert.Equal(deviceKey, ex.ResourceKey);
        await handle.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireAsync_EnqueueFifo_ExecutesSequentially()
    {
        await using var guard = new SingleFlightConcurrencyGuard(new ConcurrencyOptions
        {
            Mode = BusyHandlingMode.EnqueueFifo,
            MaxQueueCapacity = 10,
            LockAcquisitionTimeout = TimeSpan.FromSeconds(3)
        });

        const string deviceKey = "ROBOT_ARM_01";
        var firstHandle = await guard.AcquireAsync(deviceKey);

        var secondEntered = false;
        var secondTask = Task.Run(async () =>
        {
            var handle = await guard.AcquireAsync(deviceKey);
            secondEntered = true;
            await handle.DisposeAsync();
        });

        // 100ms 대기 후에도 두 번째 작업은 대기 상태여야 함
        await Task.Delay(100);
        Assert.False(secondEntered);

        // 첫 번째 핸들 해제
        await firstHandle.DisposeAsync();

        // 두 번째 작업이 진입 완료되는지 확인
        await secondTask;
        Assert.True(secondEntered);
        Assert.False(guard.IsBusy(deviceKey));
    }

    [Fact]
    public async Task TryAcquireAsync_EnqueueFifo_RejectsWhenQueueExceeded()
    {
        await using var guard = new SingleFlightConcurrencyGuard(new ConcurrencyOptions
        {
            Mode = BusyHandlingMode.EnqueueFifo,
            MaxQueueCapacity = 1, // 대기열 1명만 허용
            LockAcquisitionTimeout = TimeSpan.FromSeconds(2)
        });

        const string deviceKey = "ROBOT_ARM_02";
        var first = await guard.AcquireAsync(deviceKey);

        var waitTask = Task.Run(async () =>
        {
            var h = await guard.AcquireAsync(deviceKey);
            await h.DisposeAsync();
        });

        // 큐에 1명이 들어갈 때까지 약간 대기
        await Task.Delay(50);

        // 대기열 용량(1) 초과 시도시 즉시 null 거부
        var third = await guard.TryAcquireAsync(deviceKey);
        Assert.Null(third);

        await first.DisposeAsync();
        await waitTask;
    }

    [Fact]
    public async Task ConcurrencyDisabled_AllowsConcurrentPassThrough()
    {
        await using var guard = new SingleFlightConcurrencyGuard(new ConcurrencyOptions
        {
            PreventDuplicateExecution = false
        });

        const string deviceKey = "NO_LOCK_DEV";
        var handle1 = await guard.TryAcquireAsync(deviceKey);
        var handle2 = await guard.TryAcquireAsync(deviceKey);

        Assert.NotNull(handle1);
        Assert.NotNull(handle2);

        await handle1!.DisposeAsync();
        await handle2!.DisposeAsync();
    }
}
