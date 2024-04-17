using FFmpeg.AutoGen;
using Microsoft.AspNetCore.SignalR.Client;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;

ffmpeg.RootPath = Path.Combine(Environment.CurrentDirectory, @"..\..\..\..\SIPSorceryMedia.FFmpeg\lib\x64");

var config = new RTCConfiguration
{
    iceServers = new List<RTCIceServer> { new RTCIceServer { urls = "stun:stun.l.google.com:19302" } }
};

HubConnection hubConnection = new HubConnectionBuilder()
                           .WithUrl("http://localhost:8080/signals")
                           .Build();

await hubConnection.StartAsync();
hubConnection.On<string, string>("RtcMessage", HandleRtcMessage);

var serialId = Guid.NewGuid().ToString("N");
Console.WriteLine($"Serial id: {serialId}");

await hubConnection.SendAsync("Register", serialId);

Console.ReadKey();

async Task HandleRtcMessage(string offerJson, string connectionId)
{
    RTCPeerConnection peerConnection = CreatePeerConnection();
    
    Console.WriteLine($"Received offer: {offerJson}");
    RTCSessionDescriptionInit.TryParse(offerJson, out RTCSessionDescriptionInit? offer);
    peerConnection.setRemoteDescription(offer);
    RTCSessionDescriptionInit answer = peerConnection.createAnswer();
    await hubConnection.SendAsync("RtcMessage", answer.toJSON(), connectionId);
}

RTCPeerConnection CreatePeerConnection()
{
    var connection = new RTCPeerConnection(config);

    var videoSource = new FFmpegFileSource("MzCA8MQv.mp4", true, new AudioEncoder());
    // var videoSource = new FFmpegFileSource("1.264", true, new AudioEncoder());
    // var videoSource = new FFmpegFileSource("3.264", true, new AudioEncoder());
    videoSource.SetVideoSourceFormat(new VideoFormat(VideoCodecsEnum.VP8, 100));

    var videoTrack = new MediaStreamTrack(videoSource.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);
    var audioTrack = new MediaStreamTrack(videoSource.GetAudioSourceFormats(), MediaStreamStatusEnum.SendRecv);

    connection.addTrack(videoTrack);
    connection.addTrack(audioTrack);
    videoSource.OnVideoSourceEncodedSample += connection.SendVideo;
    videoSource.OnAudioSourceEncodedSample += connection.SendAudio;
    
    connection.OnVideoFormatsNegotiated += videoFormats => videoSource.SetVideoSourceFormat(videoFormats.First());
    connection.OnAudioFormatsNegotiated += audioFormats => videoSource.SetAudioSourceFormat(audioFormats.First());
    
    connection.onconnectionstatechange += async (state) =>
    {
        Console.WriteLine($"Peer connection state change to {state}.");

        switch (state)
        {
            case RTCPeerConnectionState.connected:
                await videoSource.Start();

                break;
            case RTCPeerConnectionState.failed:
                connection.Close("ice disconnection");
                break;
            case RTCPeerConnectionState.closed:
                break;
        }
    };

    return connection;
}