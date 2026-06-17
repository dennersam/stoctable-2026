using Microsoft.Extensions.Logging;
using Stoctable.Domain.Contracts.Services;

namespace Stoctable.Infrastructure.Email;

/// <summary>
/// Implementação de desenvolvimento do envio de email: apenas registra o conteúdo
/// (incluindo links de convite/redefinição de senha) no log. Pode ser substituída
/// por um provedor real (SMTP/SendGrid/Azure Communication) sem alterar os serviços.
/// </summary>
public class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[EMAIL DEV] Para: {To} | Assunto: {Subject}\n{Body}",
            to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
