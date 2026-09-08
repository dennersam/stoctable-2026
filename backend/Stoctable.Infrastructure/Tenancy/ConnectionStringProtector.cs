using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Stoctable.Infrastructure.Tenancy;

public interface IConnectionStringProtector
{
    byte[] Protect(string connectionString);
    string Unprotect(byte[] payload);
}

/// <summary>
/// Cifra as connection strings dos tenants com AES-GCM.
///
/// Uma chave só, guardada no Key Vault como <c>TenantConnectionEncryptionKey</c>
/// e lida uma vez no startup, protege as linhas de todas as empresas. É o que
/// torna viável ter milhares de tenants sem um segredo por tenant no vault —
/// ver Company.ConnectionStringEncrypted para o resto do raciocínio.
///
/// Formato do payload: [nonce 12 bytes][tag 16 bytes][ciphertext]. O nonce é
/// sorteado a cada operação: repetir nonce com a mesma chave em GCM quebra a
/// confidencialidade, então ele nunca é derivado nem reutilizado.
/// </summary>
public class ConnectionStringProtector : IConnectionStringProtector
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] _key;

    public ConnectionStringProtector(IConfiguration configuration)
    {
        var configured = configuration["TenantConnectionEncryptionKey"];

        if (string.IsNullOrWhiteSpace(configured))
        {
            // Sem chave configurada não dá para decifrar nada gravado antes, e
            // gerar uma na memória silenciosamente produziria linhas ilegíveis
            // no próximo restart. Falhar no startup é o comportamento correto.
            throw new InvalidOperationException(
                "TenantConnectionEncryptionKey não configurada. "
                + "Gere 32 bytes aleatórios em base64 e grave no Key Vault.");
        }

        _key = Convert.FromBase64String(configured);

        if (_key.Length != 32)
            throw new InvalidOperationException(
                $"TenantConnectionEncryptionKey precisa ter 32 bytes (256 bits); tem {_key.Length}.");
    }

    public byte[] Protect(string connectionString)
    {
        var plaintext = Encoding.UTF8.GetBytes(connectionString);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[NonceSize + TagSize + ciphertext.Length];
        nonce.CopyTo(payload, 0);
        tag.CopyTo(payload, NonceSize);
        ciphertext.CopyTo(payload, NonceSize + TagSize);
        return payload;
    }

    public string Unprotect(byte[] payload)
    {
        if (payload.Length < NonceSize + TagSize)
            throw new CryptographicException("Payload cifrado menor que o cabeçalho — dado corrompido.");

        var nonce = payload.AsSpan(0, NonceSize);
        var tag = payload.AsSpan(NonceSize, TagSize);
        var ciphertext = payload.AsSpan(NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }
}
