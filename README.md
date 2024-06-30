
[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](https://choosealicense.com/licenses/mit/)

# Project Title

A simple C#.NET media player, which encrypts the file via AES256, then plays it back via an internal HTTP file server using VLCSharp. While this is not necessarily practically useful in and of itself, it incorporates a number of important principles;

- Custom Scalable UI elements
- Content delivery over HTTP(S)
- Cryptography and Streams

### HTTP Server

The internal HTTP code can easily be expanded (including to HTTPS with the right certificates) to create a more comprehensive media server, file server, or web server. 

TODO: Encorporating REST API is also not difficult. For example, while the application is running, http://localhost:1234/controls will provide a widget with simple control over the media player.

### Media Player

The Media Player is relatively straight forward, using VLCSharp to play the media files. Unfortunately, WinForms has a very annoying graphics interface, so we have to use an overlayed form with TransparencyKey to provide the interface above the video.

### Encryption

AES provides very good encryption, but standard approaches do not provide seekable streams. To resolve this, we need to force the AES stream to use Counter based encryption.

This approach does the job, but is relatively inefficient due to the looping and XORing. Performance could be improved significantly by using SSE/AVX to vectorize the final XOR step, or better; we could just chunk the file ourselves and rely on the AES intrinsics exposed by .NET.