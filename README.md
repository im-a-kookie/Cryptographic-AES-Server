
[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](https://choosealicense.com/licenses/mit/)

# Project Title

A simple C#.NET media player, which encrypts the file via AES256, then plays it back via an internal HTTP file server using VLCSharp. This is not necessarily practical in and of itself, but demonstrates a variety of important and foundational software concepts;

- Custom Scalable UI elements
- Content delivery over HTTP(S) using low level HTTP listeners
- Multithreading to improve performance and responsiveness
- Streams and Cryptography

### HTTP Server

The internal HTTP code can easily be expanded (including to HTTPS with the right certificates) to create a more comprehensive media server, file server, or web server. Encorporating REST API is also not difficult. For example, while the application is running, http://localhost:1234/controls will provide a widget with simple control over the media player.

Beyond this point, just use ASP.NET.

### Media Player

The Media Player is relatively straight forward, using VLCSharp to play the media files. Unfortunately, WinForms has a very annoying graphics interface especially with transpareency, so we use an overlayed form with TransparencyKey to provide the interface above the video. I could use WPF but lighting myself on fire might be more fun.

### Encryption

AES provides strong encryption, but a basic "encrypt file" level approach is generally not suitable when dealing with large files that require random or seekable access. Generally speaking, this is easy to solve via ECB and CTR driven AES encryption, whereby we can generate the cipher for any given block based on the position of the block.

This approach does the job, but is relatively inefficient due to the manual looping, XORing, and random IO. The obvious solution is therefore to encrypt the file into smaller chunks, such that we can simply and efficienty decrypt (and cache) the chunk containing the desired data. Example streams are provided, using this approach, and using AES CTR mode.
