using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Amard.App.Services
{
    public static class MyFunction
    {
        #region Crypto
        private static byte[] GetKey(string password)
        {
            using (var sha = SHA256.Create())
                return sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        }

        public static string Encrypt(string plainText, string password = "tahlil@123")
        {
            using (var aes = Aes.Create())
            {
                aes.Key = GetKey(password);
                aes.GenerateIV(); // هر بار IV تصادفی جدید

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    // IV رو اول خروجی می‌نویسیم تا موقع دیکریپت بخونیم
                    ms.Write(aes.IV, 0, aes.IV.Length);

                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        var data = Encoding.UTF8.GetBytes(plainText);
                        cs.Write(data, 0, data.Length);
                    }

                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public static string Decrypt(string cipherText, string password = "tahlil@123")
        {
            var fullCipher = Convert.FromBase64String(cipherText);

            using (var aes = Aes.Create())
            {
                aes.Key = GetKey(password);

                // ۱۶ بایت اول = IV
                var iv = new byte[16];
                Array.Copy(fullCipher, 0, iv, 0, 16);
                aes.IV = iv;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(fullCipher, 16, fullCipher.Length - 16))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var reader = new StreamReader(cs, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static readonly Regex UrlRegex = new Regex(@"https?://[^\s]+", RegexOptions.IgnoreCase);

        private const string ParamName = "data";

        public static string EncryptUrlsInText(string text, string password = "tahlil@123")
        {
            return UrlRegex.Replace(text, m =>
            {
                string url = m.Value;
                int q = url.IndexOf('?');

                // لینک بدون پارامتر رو دست نمی‌زنیم
                if (q < 0 || q == url.Length - 1)
                    return url;

                string baseUrl = url.Substring(0, q);
                string query = url.Substring(q + 1);

                string encrypted = ToUrlSafe(Encrypt(query, password));
                return baseUrl + "?" + ParamName + "=" + encrypted;
            });
        }

        // برعکس عمل بالا: پارامترها رو دوباره دیکریپت می‌کنه
        public static string DecryptUrlsInText(string text, string password = "tahlil@123")
        {
            string prefix = ParamName + "=";

            return UrlRegex.Replace(text, m =>
            {
                string url = m.Value;
                int q = url.IndexOf('?');

                if (q < 0) return url;

                string baseUrl = url.Substring(0, q);
                string query = url.Substring(q + 1);

                if (!query.StartsWith(prefix)) return url;

                try
                {
                    string base64 = FromUrlSafe(query.Substring(prefix.Length));
                    return baseUrl + "?" + Decrypt(base64, password);
                }
                catch
                {
                    return url; // اگه دیکریپت نشد لینک همون‌طور بمونه
                }
            });
        }

        // Base64 -> URL-safe Base64
        private static string ToUrlSafe(string base64)
        {
            return base64.Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        // URL-safe Base64 -> Base64
        private static string FromUrlSafe(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return s;
        }
        #endregion


        /// <summary>
        /// فراخوانی یک URL بدون باز شدن TAB  
        /// </summary>
        /// <param name="url"></param>
        /// <returns>پاسخ برگردانده شده از url</returns>
        public static string CallUrl(string url)
        {
            try
            {
                if (WebRequest.Create(url) is not HttpWebRequest request)
                {
                    return string.Empty;
                }
                //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                var response = request.GetResponse();
                var reader = new StreamReader(response.GetResponseStream());
                return reader.ReadToEnd(); // it takes the response from your url. now you can use as your need  

            }
            catch (Exception e)
            {
                return string.Empty;
            }
        }

    }
}
