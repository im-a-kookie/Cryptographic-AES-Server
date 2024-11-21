
[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](https://choosealicense.com/licenses/mit/)

# Project Title

A simple C#.NET media player, which encrypts the file via AES256, then plays it back via an internal HTTP file server using VLCSharp. This is not necessarily practical in and of itself, but demonstrates a variety of important and foundational software concepts;

- Custom Scalable UI elements
- Content delivery over HTTP(S) using low level HTTP listeners
- Multithreaded message processing
- Streams and Cryptography

### HTTP Server

The internal HTTP code can easily be expanded to create a more comprehensive media server, file server, or web server. Encorporating REST API is also not difficult. For example, while the application is running, http://localhost:1234/controls will provide a widget with simple control over the media player.

Incoming requests are proceessed via manual threadpool implementation, using highly scalable non-blocking Message Pattern. For extremely high request loads, a TPL queuing pattern will provide higher throughput via ActionBlock.

But at this point just use ASP.NET, it's faster, easier, this project is demonstrational only.

### Media Player

The Media Player is relatively straight forward, using VLCSharp to play the media files. Unfortunately, WinForms has a very annoying graphics interface especially with transpareency, so we use an overlayed form with TransparencyKey to provide the interface above the video. In retrospect, WPF is more appropriate for this task.

### Encryption

AES provides strong encryption, but a basic "encrypt file" level approach is generally not suitable when dealing with large files that require random or seekable access. Generally speaking, this is easy to solve via ECB and CTR AES modes, however this can be very inefficient due to the necessary manual looping, XORing, and file access overhead. A more performant solution is provided, better leveraging SeqIO and the extremely high throughput of AES-NI, by encrypting and decrypting the file into large virtually mapped chunks.
