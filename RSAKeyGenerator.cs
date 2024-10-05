using System.Security.Cryptography;

namespace FamilyApp.API
{
    public class RSAKeyGenerator
    {
        public static void Main(string[] args)
        {
            using (var rsa = RSA.Create(2048))
            {
                var privateKey = rsa.ExportRSAPrivateKey();
                var publicKey = rsa.ExportRSAPublicKey();

                System.IO.File.WriteAllBytes("private.key", privateKey);
                System.IO.File.WriteAllBytes("public.key", publicKey);

                Console.WriteLine("Keys generated and saved.");
            }
        }
    }
}
