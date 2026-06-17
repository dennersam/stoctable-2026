namespace Stoctable.Communication.Requests.Auth;

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Token, string NewPassword);
