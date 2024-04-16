"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/signals", {withCredentials: false}).build();

const configuration = {
    'iceServers': [{
        'urls': 'stun:stun.l.google.com:19302'
    }]
};
const peerConn = new RTCPeerConnection(configuration);
const remoteVideo = document.getElementById('remoteVideo');

connection.start({withCredentials: false}).then(async function () {

    connection.on('signal', async function (message) {
        console.log('Client received message:', message);
        message = JSON.parse(message);
        await peerConn.setRemoteDescription(message);
    });

    connection.on('iceCandidate', function (message) {
        console.log('Client received iceCandidate:', message);
        let iceCandidate = JSON.parse(message);
        if (peerConn.remoteDescription !== null) {
            peerConn.addIceCandidate(iceCandidate);
        }
    })

    //setup my video here.
    await grabUserAudio();
    await createPeerConnection();
}).catch(function (err) {
    return console.error(err.toString());
});

async function grabUserAudio() {
    console.log('Getting user media (video) ...');
    let stream = await navigator.mediaDevices.getUserMedia({
        audio: true,
        video: false
    });
    console.log('getUserMedia video stream URL:', stream);
    stream.getTracks().forEach(track => peerConn.addTrack(track, stream));
}

async function createPeerConnection() {
    console.log('Creating Peer connection');

    // send any ice candidates to the other peer
    peerConn.onicecandidate = function (event) {
        console.log('icecandidate event:', event);
        connection.invoke("IceCandidate", JSON.stringify(event.candidate)).catch(function (err) {
            return console.error(err.toString());
        });
    };

    peerConn.ontrack = function (event) {
        console.log('icecandidate ontrack event:', event);
        remoteVideo.srcObject = event.streams[0];
    };

    let offer = await peerConn.createOffer({offerToReceiveAudio: true, offerToReceiveVideo: true});
    console.log('offer created:', offer);
    await peerConn.setLocalDescription(offer);
    await connection.invoke("Signal", JSON.stringify(offer));
}