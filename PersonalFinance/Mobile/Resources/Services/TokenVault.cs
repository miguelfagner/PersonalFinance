using Android.App;
using Android.Content;
using Android.Security;
using Java.Security;
using Javax.Crypto;
using PersonalFinance.Contracts;
using System.Text.Json;

namespace PersonalFinance.Resources.Services;

// Only ciphertext is stored in preferences. The private key stays in AndroidKeyStore.
public static class TokenVault
{
    private sealed record Session(string AccessToken, DateTime ExpiresAtUtc, string? RefreshToken);
    private const string Alias="PersonalFinance.Sync";
    private static ISharedPreferences Preferences=>Application.Context.GetSharedPreferences("finance-sync-token",FileCreationMode.Private)!;
    private static KeyStore Store()
    {
        var store=KeyStore.GetInstance("AndroidKeyStore")!;store.Load(null);
        if(!store.ContainsAlias(Alias))
        {
#pragma warning disable CS0618
            var spec=new KeyPairGeneratorSpec.Builder(Application.Context).SetAlias(Alias)!.SetSubject(new Javax.Security.Auth.X500.X500Principal("CN=PersonalFinanceSync"))!.SetSerialNumber(Java.Math.BigInteger.One)!.SetStartDate(new Java.Util.Date())!.SetEndDate(new Java.Util.Date(DateTimeOffset.UtcNow.AddYears(25).ToUnixTimeMilliseconds()))!.Build()!;
#pragma warning restore CS0618
            var generator=KeyPairGenerator.GetInstance("RSA","AndroidKeyStore")!;generator.Initialize(spec);generator.GenerateKeyPair();
        }
        return store;
    }
    public static void Save(LoginResponse login) => SaveEncrypted(JsonSerializer.Serialize(new Session(login.Token, login.ExpiresAt, login.RefreshToken)));
    public static void Save(string token) => SaveEncrypted(token);
    private static void SaveEncrypted(string token)
    {
        // RSA encrypts a random AES key, avoiding a limit on JWT length.
        using var aes=System.Security.Cryptography.Aes.Create();aes.GenerateKey();var nonce=System.Security.Cryptography.RandomNumberGenerator.GetBytes(12);var tag=new byte[16];
        var cipher=Cipher.GetInstance("RSA/ECB/PKCS1Padding")!;cipher.Init(CipherMode.EncryptMode,Store().GetCertificate(Alias)!.PublicKey);
        var encryptedKey=cipher.DoFinal(aes.Key)!;
        var plain=System.Text.Encoding.UTF8.GetBytes(token);var encrypted=new byte[plain.Length];using var gcm=new System.Security.Cryptography.AesGcm(aes.Key,16);gcm.Encrypt(nonce,plain,encrypted,tag);
        Preferences.Edit()!.PutString("key",Convert.ToBase64String(encryptedKey))!.PutString("iv",Convert.ToBase64String(nonce))!.PutString("tag",Convert.ToBase64String(tag))!.PutString("token",Convert.ToBase64String(encrypted))!.Apply();
    }
    private static string? ReadEncrypted()
    {
        var token=Preferences.GetString("token",null);if(token is null)return null;
        try{var cipher=Cipher.GetInstance("RSA/ECB/PKCS1Padding")!;cipher.Init(CipherMode.DecryptMode,Store().GetKey(Alias,null));using var aes=System.Security.Cryptography.Aes.Create();aes.Key=cipher.DoFinal(Convert.FromBase64String(Preferences.GetString("key","")!))!;var encrypted=Convert.FromBase64String(token);var plain=new byte[encrypted.Length];using var gcm=new System.Security.Cryptography.AesGcm(aes.Key,16);gcm.Decrypt(Convert.FromBase64String(Preferences.GetString("iv","")!),encrypted,Convert.FromBase64String(Preferences.GetString("tag","")!),plain);return System.Text.Encoding.UTF8.GetString(plain);}
        catch{Clear();return null;}
    }
    public static string? Read()
    {
        var value = ReadEncrypted();
        if (value is null) return null;
        try { return JsonSerializer.Deserialize<Session>(value)?.AccessToken; }
        catch (JsonException) { return value; }
    }
    public static string? ReadRefresh()
    {
        var value = ReadEncrypted();
        if (value is null) return null;
        try { return JsonSerializer.Deserialize<Session>(value)?.RefreshToken; }
        catch (JsonException) { return null; }
    }
    public static DateTime? ReadExpiry()
    {
        var value = ReadEncrypted();
        if (value is null) return null;
        try { return JsonSerializer.Deserialize<Session>(value)?.ExpiresAtUtc; }
        catch (JsonException) { return null; }
    }
    public static void Clear()=>Preferences.Edit()!.Clear()!.Apply();
}
