using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace VideoPlayer
{
    internal class MimeHelper
    {

        /// <summary>
        /// A mapping of common media file MIME types
        /// </summary>
        private static Dictionary<string, string> mimeTypes = new(StringComparer.InvariantCultureIgnoreCase)
        {
            {".asf", "video/x-ms-asf"},
            {".asx", "video/x-ms-asf"},
            {".avi", "video/x-msvideo"},
            {".flv", "video/x-flv"},
            {".gif", "image/gif"},
            {".jng", "image/x-jng"},
            {".jpeg", "image/jpeg"},
            {".jpg", "image/jpeg"},
            {".m4v", "audio/mpeg"},
            {".mng", "video/x-mng"},
            {".mov", "video/quicktime"},
            {".mp3", "audio/mpeg"},
            {".mp4", "video/mpeg"},
            {".mkv", "video/mpeg"},
            {".mpeg", "video/mpeg"},
            {".mpg", "video/mpeg"},
            {".png", "image/png"},
            {".ra", "audio/x-realaudio"},
            {".swf", "application/x-shockwave-flash"},
            {".wbmp", "image/vnd.wap.wbmp"},
            {".wmv", "video/x-ms-wmv"},
            {".xml", "text/xml"},
            {".zip", "application/zip"},
            {".html", "text/html" }
        };

        static HashSet<string> encryptedTypes = [];


        /// <summary>
        /// Create the mime helper and insert the encrypted file forms
        /// </summary>
        static MimeHelper()
        {
            foreach(var k in mimeTypes.ToList())
            {
                mimeTypes.TryAdd(k.Key.Replace(".", ".e"), k.Value);
                encryptedTypes.Add(k.Key.Replace(".", ".e"));
            }
        }

        /// <summary>
        /// Gets the MIME type from the given path
        /// </summary>
        /// <param name="extension"></param>
        /// <param name="mime"></param>
        /// <param name="encrypted"></param>
        /// <returns></returns>
        public static bool GetMime(string file, out string? mime, out bool encrypted)
        {
            string s = Path.GetExtension(file).ToLower();
            encrypted = encryptedTypes.Contains(s);
            return mimeTypes.TryGetValue(s, out mime);
        }




    }
}
