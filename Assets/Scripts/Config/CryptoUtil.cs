using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

public static class CryptoUtil
{
    private static readonly string KeyPath = Path.Combine(Application.persistentDataPath, "crypto_key.bin");
    private static readonly string IvPath  = Path.Combine(Application.persistentDataPath, "crypto_iv.bin");

    private static byte[] GetOrCreate(string path, int len)
    {
        if (File.Exists(path))
            return File.ReadAllBytes(path);
        var buf = new byte[len];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(buf);
        File.WriteAllBytes(path, buf);
        return buf;
    }

    private static (byte[] key, byte[] iv) Material()
    {
        var key = GetOrCreate(KeyPath, 32); // AES-256
        var iv  = GetOrCreate(IvPath, 16);  // 128-bit IV
        return (key, iv);
    }

    public static string EncryptToB64(string plain)
    {
        if (string.IsNullOrEmpty(plain)) return "";
        var (key, iv) = Material();
        using var aes = Aes.Create();
        aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
        using var enc = aes.CreateEncryptor();
        var bytes = Encoding.UTF8.GetBytes(plain);
        var cipher = enc.TransformFinalBlock(bytes, 0, bytes.Length);
        return Convert.ToBase64String(cipher);
    }

    public static string DecryptFromB64(string b64)
    {
        if (string.IsNullOrEmpty(b64)) return "";
        try
        {
            var (key, iv) = Material();
            using var aes = Aes.Create();
            aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            using var dec = aes.CreateDecryptor();
            var cipher = Convert.FromBase64String(b64);
            var plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            Debug.LogWarning("[CryptoUtil] Decrypt failed (returning empty).");
            return "";
        }
    }
}
