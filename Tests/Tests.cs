using VideoPlayer;

namespace Tests
{
    public class Tests
    {
        [Fact]
        public void Test_AESCryptoStream()
        {
            //pre-init a few things
            string s = "This is a testing string!";
            byte[] t = System.Text.Encoding.UTF8.GetBytes(s);
            string keyString = "password";
            var key = AesHelper.DeriveKeyFromPassword(keyString);

            //encrypt it
            using var encryptBytes = new MemoryStream();
            using var encryptStream = new AesCtrStream(encryptBytes, key.Key, key.IV);
            encryptStream.Write(t, 0, t.Length);

            //now decrypt
            using var decryptBytes = new MemoryStream();
            using var decryptStream = new AesCtrStream(decryptBytes, key.Key, key.IV);
            decryptStream.Write(encryptBytes.ToArray());

            string result = System.Text.Encoding.UTF8.GetString(decryptBytes.ToArray());

            //now check that the encryption matches
            Assert.Equal(s, result);



        }

        /// <summary>
        /// Tests the main Mime types to ensure that they're returning the expected outputs
        /// </summary>
        [Fact]
        public void Test_MimeTypes()
        {
            //go through the main list of types that we're almost certainly going to need
            //in any kind of fleshed out page
            //Add other essential MIME types to the below collection
            Dictionary<string, (string mime, bool encrypted)> inputs = new()
            {
                {".mp4", ("video/mpeg", false) },
                {".emp4", ("video/mpeg", true) }, //test the encrypted video return
                {".html", ("text/html", false) },
                {".css", ("text/css", false) },
                {".js", ("text/javascript", false) },
                {".png", ("image/png", false) },
                {".jpg", ("image/jpb", false) }
            };

            foreach(var k in inputs)
            { 
                //get the mimes
                MimeHelper.GetMime("X:\\Directory\\whatever" + k.Key, out var resultMime, out var resultEncrypt);
                //and check them
                Assert.Equal(resultMime, k.Value.mime);
                Assert.Equal(resultEncrypt, k.Value.encrypted);
            }



        }

    }
}