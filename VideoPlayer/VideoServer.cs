using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.DirectoryServices.ActiveDirectory;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace VideoPlayer
{
    internal class VideoServer
    {


        /// <summary>
        /// Whether or not the thing is terminating
        /// </summary>
        static bool _Terminate = false;
        /// <summary>
        /// The default IP for servers
        /// </summary>
        static string DefaultIP = "localhost";
        /// <summary>
        /// The default port for servers
        /// </summary>
        static int DefaultPort = 1234;
        /// <summary>
        /// The total number of busy threads
        /// </summary>
        static int BusyThreads = 0;
        /// <summary>
        /// Whether or not we are using HTTPS (probably not, since localhost)
        /// </summary>
        static bool IsHTTPS = false;

        /// <summary>
        /// A blocking collection of tasks
        /// </summary>
        static BlockingCollection<(HttpServer server, HttpListenerContext context)> Tasks = new();

        /// <summary>
        /// A collection of active servers
        /// </summary>
        static Dictionary<string, HttpServer> ActiveServers = [];
        
        /// <summary>
        /// The number of running servers
        /// </summary>
        static int AliveServerCount = 0;

        /// <summary>
        /// The number of target servers
        /// </summary>
        static int MaxThreads = 16;

        /// <summary>
        /// The number of alive threads
        /// </summary>
        static int AliveThreads = 0;

        /// <summary>
        /// A dictionary mapping streamable files to their URL paths
        /// </summary>
        static Dictionary<string, string> StreamableFiles = new();
        /// <summary>
        /// A dictionary mapping URL paths to the streamable files
        /// </summary>
        static Dictionary<string, string> UrlRelators = new();

        /// <summary>
        /// The server handle
        /// </summary>
        class HttpServer : IDisposable
        {
            /// <summary>
            /// A flag noting whether this server is alive.
            /// </summary>
            public bool Alive
            {
                get => _alive; set
                {
                    if (!_alive && value) throw new Exception("Cannot Realive Unalived Server!");
                    _alive = value;
                    if (!_alive)
                    {
                        lock (ActiveServers) ActiveServers.Remove(URL);
                        Listener?.Stop();
                    }
                }
            }
            /// <summary> chained to <see cref="Alive"/></summary>
            private bool _alive = true;

            /// <summary>
            /// The IP address for this server
            /// </summary>
            public readonly string? IP = DefaultIP;

            /// <summary>
            /// The port for this server
            /// </summary>
            public readonly int Port = DefaultPort;

            /// <summary>
            /// The HTTP Listener
            /// </summary>
            public HttpListener? Listener;

            /// <summary>
            /// The URL for this server
            /// </summary>
            public string URL => string.Format("http{0}://{1}:{2}/", IsHTTPS ? "s" : "", IP, Port);
            /// <summary>
            /// Create a new server at the given port
            /// </summary>
            /// <param name="ip"></param>
            /// <param name="port"></param>
            public HttpServer(string? ip = "localhost", int port = 1234)
            {
                IP = ip;
                if (IP == null) IP = DefaultIP;
                this.Port = port;
                if (this.Port < 0) this.Port = DefaultPort;

                lock (ActiveServers)
                {
                    if (ActiveServers.ContainsKey(URL)) return;
                    ActiveServers.Add(URL, this);
                    //start a thread to host this server
                    new Thread(Enter).Start();
                    if (AliveThreads <= 0) new Thread(ThreadHost).Start();
                }
            }

            public void Enter()
            {
                Interlocked.Increment(ref AliveServerCount);
                if (Listener == null)
                {
                    Listener = new HttpListener();
                    Console.WriteLine("Starting HTTP Server on: " + URL);
                    Listener.Prefixes.Add(URL);
                    Listener.Start();
                }
                while (Alive)
                {
                    try
                    {
                        var context = Listener?.GetContext();
                        if (context != null) Tasks.Add((this, context));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("ERROR: " + e.Message);
                        Alive = false;
                    }
                }

                //and notify that we're dead
                lock (ActiveServers)
                {
                    ActiveServers.Remove(URL);
                }
                Interlocked.Decrement(ref AliveServerCount);
            }

            /// <summary>
            /// Lets us "using" the server
            /// </summary>
            public void Dispose()
            {
                Alive = false;
            }
        }


        /// <summary>
        /// Kills all of the active servers.
        /// </summary>
        public static void KillMediaServer()
        {
            _Terminate = true;
            lock(ActiveServers)
            {
                foreach(var s in ActiveServers)
                {
                    s.Value.Dispose();
                }
            }
        }

        /// <summary>
        /// Gears up the streaming engine to play the given file
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string StartStreamingFile(string path)
        {
            lock(StreamableFiles)
            {
                if (StreamableFiles.ContainsKey(path)) return StreamableFiles[path];
                else
                {
                    HttpServer s = new HttpServer("localhost", 1234);
                    string str = Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
                    StreamableFiles.TryAdd(path, s.URL + "/v=" + str);
                    UrlRelators.TryAdd(str, path);
                    return s.URL + "/v=" + str;
                }
            }
        }

        /// <summary>
        /// Seeks to close the file given
        /// </summary>
        /// <param name="path"></param>
        public static void TryCloseFile(string path)
        {
            lock(StreamableFiles)
            {
                if (StreamableFiles.ContainsKey(path))
                {
                    string s = StreamableFiles[path];
                    UrlRelators.Remove(s);
                    StreamableFiles.Remove(path);
                    if (StreamableFiles.Count == 0)
                    {
                        foreach (var server in ActiveServers) server.Value.Dispose();
                        ActiveServers.Clear();
                    }
                }
            }
        }

        static void ThreadHost()
        {
            try
            {
                Interlocked.Increment(ref AliveThreads);
                while (!_Terminate)
                {
                    //get the task
                    var work = Tasks.Take();
                    
                    //This informs whether we're possibly going to need to scale up or down
                    int n = Interlocked.Increment(ref BusyThreads);

                    //If we're in danger of running out of threads, make some new ones to be safe
                    //Thread creation is generally fast enough that this won't really matter for latency
                    if (BusyThreads >= 4 * AliveThreads / 5 
                    || AliveThreads < MaxThreads && Tasks.Count > 1) 
                        new Thread(ThreadHost).Start();

                    //now we can process the HTTP context
                    if (work.server.Alive)
                    {
                        ProcessContext(work.context);
                    }

                    //and notify that we're done
                    Interlocked.Decrement(ref BusyThreads);
                    //If we've gone past the goal then we should die
                    if (AliveThreads > MaxThreads)
                    {
                        if (Interlocked.Decrement(ref AliveThreads) > MaxThreads)
                            break;
                        else Interlocked.Increment(ref AliveThreads);
                    }
                }
            }
            finally
            {
                Interlocked.Decrement(ref AliveThreads);
            }
        }


        static void ProcessContext(HttpListenerContext c)
        {

            HttpStatusCode statusCode = HttpStatusCode.OK;
            Uri? url;
            try
            {
                if (c == null) return;
                url = c.Request.Url;
                if (url == null)
                {
                    c.Response.OutputStream.Close();
                    return;
                }
            }
            catch
            {
                return;
            }

            string requestDetails = url.AbsolutePath;
            var parts = requestDetails.Split(';');

            string? path = null;
            string? key = null;

            //Try to read the URL pieces
            foreach(var p in parts)
            {
                int n;
                if ((n = p.IndexOf("v=")) > 0) UrlRelators.TryGetValue(p.Substring(n + 2), out path);
                if ((n = p.IndexOf("k=")) > 0) key = p.Substring(n + 2);
            }

            HttpStatusCode result = HttpStatusCode.OK;
            if (path != null) result = StreamFile(c, path, key);

            // finish and wrap everything up
            c.Response.StatusCode = (int)statusCode;
            if (statusCode == HttpStatusCode.OK)
            {
                c.Response.AddHeader("Date", DateTime.Now.ToString("r"));
                c.Response.AddHeader("Last-Modified", DateTime.MinValue.ToString("r"));
            }
            c.Response.OutputStream.Close();
        }

        static HttpStatusCode StreamFile(HttpListenerContext context, string? filename, string? key)
        {
            HttpStatusCode statusCode;
            if (File.Exists(filename))
            {
                try
                {
                    Stream? stream = null;
                    try
                    {
                        if (!Path.Exists(filename))
                        {
                            Console.WriteLine("File not Found: " + filename);
                            return HttpStatusCode.NotFound;
                        }

                        string? mime;
                        bool encrypted = false; ;
                        if (!MimeHelper.GetMime(filename, out mime, out encrypted))
                        {
                            Console.WriteLine("Unrecogized Media Type: " + filename);
                            return HttpStatusCode.NotFound;
                        }

                        //Now generate the streams
                        stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        if (encrypted)
                        {
                            var k = AesHelper.DeriveKeyFromPassword(key ?? "default");
                            stream = new CryptoStream(stream, k.Key, k.IV);
                        }

                        // get mime type
                        long fileLength = stream.Length;
                        context.Response.ContentType = mime;

                        string? rangeHeader = context.Request.Headers["Range"];
                        if (string.IsNullOrEmpty(rangeHeader))
                        {
                            //The range was not given, so we're sending the entire file at once
                            context.Response.ContentLength64 = fileLength;
                            stream.CopyTo(context.Response.OutputStream);
                        }
                        else
                        {
                            //Range was specified, so we're going to read the ranges and send them
                            const string bytesUnit = "bytes="; //e.g bytes=2456-2347592
                            if (rangeHeader.StartsWith(bytesUnit, StringComparison.OrdinalIgnoreCase))
                            {
                                //let's try to read everything after the bytes=
                                string range = rangeHeader.Substring(bytesUnit.Length);
                                string[] ranges = range.Split('-');
                                //and parse the bytes out of the first
                                long start = long.Parse(ranges[0]);
                                //Nowt try to read the end
                                long end = ranges.Length > 1 && !string.IsNullOrEmpty(ranges[1]) ? long.Parse(ranges[1]) : fileLength - 1;
                                if (end >= fileLength) end = fileLength - 1;

                                //so we need to read this many bytes
                                long contentLength = end - start + 1;
                                // Set response headers for partial content
                                context.Response.StatusCode = (int)HttpStatusCode.PartialContent;
                                context.Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{fileLength}");
                                context.Response.ContentLength64 = contentLength;

                                // Write the requested range to the output stream
                                byte[] buffer = new byte[128 * 1024]; // 64KB buffer? Probably not a big deal what the buffer really is
                                stream.Seek(start, SeekOrigin.Begin);
                                while (contentLength > 0)
                                {
                                    int bytesRead = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, contentLength));
                                    context.Response.OutputStream.Write(buffer, 0, bytesRead);
                                    contentLength -= bytesRead;
                                }
                            }
                            else
                            {
                                // Invalid range header
                                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                            }
                        }
                        context.Response.OutputStream.Flush();

                    }
                    finally
                    {
                        //Now we can close the streams
                        stream?.Dispose();
                    }
                    statusCode = HttpStatusCode.OK;
                }
                catch (Exception e)
                {
                    statusCode = HttpStatusCode.InternalServerError;
                }
            }
            else
            {
                statusCode = HttpStatusCode.NotFound;
            }


            return statusCode;
        }









    }
}
