using System.Collections.Generic;
using NUnit.Framework;
using Worldhalla.Plugin.Patches;

namespace Worldhalla.Plugin.Tests;

/*
 * The keepalive during world generation hands vanilla's ReceivePing a request of its own making and relies
 * on the reply resetting the other side's timeout. Both halves are vanilla's ping protocol, pinned here.
 */
public class KeepaliveTests
{
    [Test, RequiresValheim]
    public void PingRequestIsAnsweredWithAReplyThatResetsTheTimeout()
    {
        var clientSocket = new RecordingSocket();
        new ZRpc(clientSocket).ReceivePing(ZRpcPatch.PingRequest());

        Assert.That(clientSocket.sent, Has.Count.EqualTo(1), "no reply sent");

        var reply = clientSocket.sent[0];
        Assert.That(reply.ReadInt(), Is.Zero, "reply is not a ping");

        var serverSocket = new RecordingSocket();
        var server = new ZRpc(serverSocket) { m_timeSinceLastPing = 25f };
        server.ReceivePing(reply);

        Assert.That(server.m_timeSinceLastPing, Is.Zero, "reply did not reset the timeout");
        Assert.That(serverSocket.sent, Is.Empty, "reply was taken for a request");
    }

    private sealed class RecordingSocket : ISocket
    {
        public readonly List<ZPackage> sent = new();

        public void Send(ZPackage pkg) => sent.Add(new ZPackage(pkg.GetArray()));

        public bool IsConnected() => true;
        public ZPackage Recv() => null;
        public int GetSendQueueSize() => 0;
        public int GetCurrentSendRate() => 0;
        public bool IsHost() => false;
        public void Dispose() { }
        public bool GotNewData() => false;
        public void Close() { }
        public string GetEndPointString() => "test";
        public void GetAndResetStats(out int totalSent, out int totalRecv) => totalSent = totalRecv = 0;
        public void GetConnectionQuality(out float localQuality, out float remoteQuality, out int ping, out float outByteSec, out float inByteSec)
            => (localQuality, remoteQuality, ping, outByteSec, inByteSec) = (0f, 0f, 0, 0f, 0f);
        public ISocket Accept() => null;
        public int GetHostPort() => 0;
        public bool Flush() => true;
        public string GetHostName() => "test";
        public void VersionMatch() { }
    }
}
