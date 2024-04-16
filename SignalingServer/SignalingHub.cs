using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using SIPSorcery.Media;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SIPSorceryMedia.FFmpeg;
using MediaStreamTrack = SIPSorcery.Net.MediaStreamTrack;
using RTCConfiguration = SIPSorcery.Net.RTCConfiguration;

namespace WebRtcClientMvp;

public class SignalingHub : Hub
{
    private readonly IHubContext<SignalingHub> _hubContext;
    private readonly ILogger<SignalingHub> _logger;
    private readonly RTCConfiguration _config;

    private static readonly ConcurrentDictionary<string, RTCPeerConnection> ConcurrentDictionary = new();

    public SignalingHub(IHubContext<SignalingHub> hubContext, ILogger<SignalingHub> logger)
    {
        _hubContext = hubContext;
        _logger = logger;

        _config = new RTCConfiguration
        {
            iceServers = new List<RTCIceServer> { new RTCIceServer { urls = "stun.l.google.com:19302" } }
        };
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var contextConnectionId = Context.ConnectionId;
        if (ConcurrentDictionary.TryGetValue(contextConnectionId, out RTCPeerConnection? pc))
        {
            if (!pc.IsClosed)
                pc.Close("Peer disconnected");
        }

        return Task.CompletedTask;
    }

    public async Task IceCandidate(string iceCandidateJson)
    {
        var contextConnectionId = Context.ConnectionId;
        if (RTCIceCandidateInit.TryParse(iceCandidateJson, out RTCIceCandidateInit iceCandidate)
            && ConcurrentDictionary.TryGetValue(contextConnectionId, out RTCPeerConnection? pc))
        {
            pc.addIceCandidate(iceCandidate);
        }
    }

    public async Task Signal(string remoteDescriptionJson)
    {
        var pc = new RTCPeerConnection(_config);
        var contextConnectionId = Context.ConnectionId;
        ConcurrentDictionary.AddOrUpdate(contextConnectionId, s => pc, (s, connection) =>
        {
            if (!connection.IsClosed)
                connection.Close("New Connection");
            return pc;
        });


        var videoSource = new FFmpegFileSource("MzCA8MQv.mp4", true, new AudioEncoder());
        // var videoSource = new FFmpegFileSource("1.264", true, new AudioEncoder());
        // var videoSource = new FFmpegFileSource("3.264", true, new AudioEncoder());
        videoSource.SetVideoSourceFormat(new VideoFormat(VideoCodecsEnum.VP8, 100));
        
        var videoTrack = new MediaStreamTrack(videoSource.GetVideoSourceFormats(), MediaStreamStatusEnum.SendOnly);
        var audioTrack = new MediaStreamTrack(videoSource.GetAudioSourceFormats(), MediaStreamStatusEnum.SendRecv);
        
        pc.addTrack(videoTrack);
        pc.addTrack(audioTrack);

        videoSource.OnVideoSourceEncodedSample += pc.SendVideo;
        videoSource.OnAudioSourceEncodedSample += pc.SendAudio;
        pc.OnVideoFormatsNegotiated += videoFormats => videoSource.SetVideoSourceFormat(videoFormats.First());
        pc.OnAudioFormatsNegotiated += audioFormats => videoSource.SetAudioSourceFormat(audioFormats.First());
        
        pc.onconnectionstatechange += async (state) =>
        {
            _logger.LogDebug($"Peer connection state change to {state}.");

            if (state == RTCPeerConnectionState.connected)
            {
                await videoSource.Start();
            }
                        
            if (state == RTCPeerConnectionState.failed)
            {
                pc.Close("ice disconnection");
            }
        };

        // Diagnostics.
        pc.OnReceiveReport += (re, media, rr) =>
            _logger.LogDebug($"RTCP Receive for {media} from {re}\n{rr.GetDebugSummary()}");
        pc.OnSendReport += (media, sr) => _logger.LogDebug($"RTCP Send for {media}\n{sr.GetDebugSummary()}");
        pc.GetRtpChannel().OnStunMessageReceived += (msg, ep, isRelay) =>
            _logger.LogDebug($"STUN {msg.Header.MessageType} received from {ep}.");
        pc.oniceconnectionstatechange += (state) => _logger.LogDebug($"ICE connection state change to {state}.");

        pc.onicecandidate += async candidate =>
        {
            await _hubContext.Clients.Client(contextConnectionId).SendAsync("iceCandidate", candidate.toJSON());
        };
        
        RTCSessionDescriptionInit.TryParse(remoteDescriptionJson, out RTCSessionDescriptionInit? remoteDescription);
        pc.setRemoteDescription(remoteDescription);

        RTCSessionDescriptionInit rtcSessionDescriptionInit = pc.createAnswer();
        await pc.setLocalDescription(rtcSessionDescriptionInit);

        await Clients.Caller.SendAsync("signal", rtcSessionDescriptionInit.toJSON());
    }
}