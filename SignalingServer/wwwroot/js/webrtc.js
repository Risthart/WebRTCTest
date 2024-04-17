"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/signals", {withCredentials: false}).build();


const configuration = {
    'iceServers': [{
        'urls': 'stun:stun.l.google.com:19302'
    }]
};
const peerConn = new RTCPeerConnection(configuration);
const remoteVideo = document.getElementById('remoteVideo');
const serialIdInput = document.getElementById('serialIdInput');
const joinButton = document.getElementById('joinButton');

peerConn.ontrack = function (event) {
    console.log('icecandidate ontrack event:', event);
    remoteVideo.srcObject = event.streams[0];
};

connection.start({withCredentials: false}).then(async function () {
    connection.on('RtcMessage', async function (message) {
        console.log('Client received message:', message);
        message = JSON.parse(message);
        await peerConn.setRemoteDescription(message);
    });

    await grabUserAudio();
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

$(joinButton).click(async function () {
    let serialId = serialIdInput.value;
    await connection.invoke("Join", serialId);
    
    let offer = await peerConn.createOffer({offerToReceiveAudio: true, offerToReceiveVideo: true});
    console.log('offer created:', offer);
    await peerConn.setLocalDescription(offer);
    await connection.invoke("RtcMessage", JSON.stringify(offer), null);
});