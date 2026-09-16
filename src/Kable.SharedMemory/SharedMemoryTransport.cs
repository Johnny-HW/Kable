namespace Kable.SharedMemory;

using System;
using System.Threading;
using System.Threading.Tasks;
using Kable.Core;
using Kable.SharedMemory.Memory;

/// <summary>
/// MMF IPC 채널의 서버 및 클라이언트 엔드포인트 팩토리.
/// 채널 이름(channelName)을 기반으로 송수신 2개의 RingBuffer(ServerToClient, ClientToServer)를 결합합니다.
/// </summary>
public static class SharedMemoryTransport
{
    /// <summary>
    /// 서버(데몬/호스트) 측에서 MMF 채널을 생성하고 ConnectionContext를 반환합니다.
    /// </summary>
    public static SharedMemoryConnectionContext CreateServerEndpoint(string channelName, int bufferCapacityPowerOfTwo = 64 * 1024)
    {
        string s2cName = $"{channelName}_s2c";
        string c2sName = $"{channelName}_c2s";

        // 서버 관점: s2c는 내가 쓰고(outbound), c2s는 상대방이 써서 내가 읽음(inbound)
        var s2c = SharedMemoryRingBuffer.Create(s2cName, bufferCapacityPowerOfTwo);
        var c2s = SharedMemoryRingBuffer.Create(c2sName, bufferCapacityPowerOfTwo);

        return new SharedMemoryConnectionContext(
            inboundBuffer: c2s,
            outboundBuffer: s2c,
            endpointDescription: $"SharedMemory:Server:{channelName}");
    }

    /// <summary>
    /// 클라이언트(UI/오케스트레이터) 측에서 기생성된 MMF 채널에 접속하고 ConnectionContext를 반환합니다.
    /// </summary>
    public static SharedMemoryConnectionContext ConnectClientEndpoint(string channelName)
    {
        string s2cName = $"{channelName}_s2c";
        string c2sName = $"{channelName}_c2s";

        // 클라이언트 관점: c2s는 내가 쓰고(outbound), s2c는 서버가 써서 내가 읽음(inbound)
        var s2c = SharedMemoryRingBuffer.Open(s2cName);
        var c2s = SharedMemoryRingBuffer.Open(c2sName);

        return new SharedMemoryConnectionContext(
            inboundBuffer: s2c,
            outboundBuffer: c2s,
            endpointDescription: $"SharedMemory:Client:{channelName}");
    }
}
