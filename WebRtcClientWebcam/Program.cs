using Microsoft.AspNetCore.SignalR.Client;
using SIPSorcery.Net;
using SIPSorceryMedia.Encoders;
using SIPSorceryMedia.Windows;

var config = new RTCConfiguration
{
    iceServers = new List<RTCIceServer> { new RTCIceServer { urls = "stun:stun.l.google.com:19302" } }
};

HubConnection connection = new HubConnectionBuilder()
                           .WithUrl("http://localhost:8080/signals")
                           .Build();

await connection.StartAsync();
connection.On<string, string>("RtcMessage", HandleRtcMessage);

var serialId = Guid.NewGuid().ToString("N");
Console.WriteLine($"Serial id: {serialId}");

await connection.SendAsync("Register", serialId);

Console.ReadKey();

async Task HandleRtcMessage(string offerJson, string connectionId)
{
    RTCPeerConnection peerConnection = await CreatePeerConnection();

    Console.WriteLine($"Received offer: {offerJson}");
    RTCSessionDescriptionInit.TryParse(offerJson, out RTCSessionDescriptionInit? offer);
    peerConnection.setRemoteDescription(offer);
    RTCSessionDescriptionInit answer = peerConnection.createAnswer();
    await connection.SendAsync("RtcMessage", answer.toJSON(), connectionId);
}

async Task<RTCPeerConnection> CreatePeerConnection()
{
    var peerConnection = new RTCPeerConnection(config);

    var winVideoEp = new WindowsVideoEndPoint(new VpxVideoEncoder(), "HD Pro Webcam C920");
    MediaStreamTrack videoTrack =
        new MediaStreamTrack(winVideoEp.GetVideoSourceFormats(), MediaStreamStatusEnum.SendRecv);

    peerConnection.addTrack(videoTrack);
    winVideoEp.OnVideoSourceEncodedSample += peerConnection.SendVideo;

    peerConnection.OnVideoFormatsNegotiated += (videoFormats) =>
        winVideoEp.SetVideoSourceFormat(videoFormats.First());

    peerConnection.onconnectionstatechange += async (state) =>
    {
        Console.WriteLine($"Peer connection state change to {state}.");

        switch (state)
        {
            case RTCPeerConnectionState.connected:
                await winVideoEp.InitialiseVideoSourceDevice();
                await winVideoEp.StartVideo();
                break;
            case RTCPeerConnectionState.failed:
                peerConnection.Close("ice disconnection");
                break;
            case RTCPeerConnectionState.closed:
                break;
        }
    };

    return peerConnection;
}