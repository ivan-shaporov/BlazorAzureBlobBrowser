function b64ToArray(base64_string) {
    return Uint8Array.from(atob(base64_string), c => c.charCodeAt(0))
}

function arrayBufferToBase64(arrayBuffer) {
    var byteArray = new Uint8Array(arrayBuffer);
    var byteString = '';
    for (var i = 0; i < byteArray.byteLength; i++) {
        byteString += String.fromCharCode(byteArray[i]);
    }
    var b64 = window.btoa(byteString);

    return b64;
}

export async function sha256(message, keyb64) {

    key = b64ToArray(keyb64)

    key = await crypto.subtle.importKey(
        'raw',
        key,
        { name: 'HMAC', hash: { name: 'SHA-256' } },
        false,
        ["sign"]
    );

    const encoder = new TextEncoder();
    message = encoder.encode(message);

    var result = await window.crypto.subtle.sign({ name: 'HMAC', hash: 'SHA-256' }, key, message);
    result = arrayBufferToBase64(result)
    return result;
}
