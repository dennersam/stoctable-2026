using Stoctable.Application.Results;
using Stoctable.Communication.Requests.Users;
using Stoctable.Communication.Responses.Users;
using Stoctable.Domain.Contracts.Repositories;
using Stoctable.Domain.Entities;
using Stoctable.Domain.Enums;
using Stoctable.Exceptions;

namespace Stoctable.Application.Services.Users;

public class UserService(IUserRepository userRepository)
{
    public async Task<Result<IEnumerable<UserResponse>>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await userRepository.GetAllAsync(ct);
        return Result<IEnumerable<UserResponse>>.Success(users.Select(MapToResponse));
    }

    public async Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(id, ct);
        if (user is null)
            return Result<UserResponse>.NotFound(ErrorMessages.User.NotFound);

        return Result<UserResponse>.Success(MapToResponse(user));
    }

    public async Task<Result<UserResponse>> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        if (await userRepository.UsernameExistsAsync(request.Username, ct))
            return Result<UserResponse>.Conflict(ErrorMessages.User.UsernameAlreadyExists);

        if (await userRepository.EmailExistsAsync(request.Email, ct))
            return Result<UserResponse>.Conflict(ErrorMessages.User.EmailAlreadyExists);

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            return Result<UserResponse>.Failure($"Role inválido: {request.Role}.");

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            FullName = request.FullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role,
            IsActive = true
        };

        await userRepository.AddAsync(user, ct);
        return Result<UserResponse>.Success(MapToResponse(user), 201);
    }

    public async Task<Result<UserResponse>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(id, ct);
        if (user is null)
            return Result<UserResponse>.NotFound(ErrorMessages.User.NotFound);

        if (request.Email is not null && request.Email != user.Email &&
            await userRepository.EmailExistsAsync(request.Email, ct))
            return Result<UserResponse>.Conflict(ErrorMessages.User.EmailAlreadyExists);

        if (request.FullName is not null) user.FullName = request.FullName;
        if (request.Email is not null) user.Email = request.Email;
        if (request.Password is not null) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        if (request.IsActive is not null) user.IsActive = request.IsActive.Value;

        if (request.Role is not null)
        {
            if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
                return Result<UserResponse>.Failure($"Role inválido: {request.Role}.");
            user.Role = role;
        }

        await userRepository.UpdateAsync(user, ct);
        return Result<UserResponse>.Success(MapToResponse(user));
    }

    public async Task<Result<UserResponse>> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return Result<UserResponse>.NotFound(ErrorMessages.User.NotFound);

        return Result<UserResponse>.Success(MapToResponse(user));
    }

    public async Task<Result<UserResponse>> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return Result<UserResponse>.NotFound(ErrorMessages.User.NotFound);

        if (request.NewPassword is not null)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
                return Result<UserResponse>.Failure(ErrorMessages.User.IncorrectPassword);

            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
                return Result<UserResponse>.Failure(ErrorMessages.User.IncorrectPassword);

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        }

        if (request.FullName is not null) user.FullName = request.FullName;
        if (request.AvatarUrl is not null) user.AvatarUrl = request.AvatarUrl;

        await userRepository.UpdateAsync(user, ct);
        return Result<UserResponse>.Success(MapToResponse(user));
    }

    private static UserResponse MapToResponse(User u) => new(
        Id: u.Id,
        Username: u.Username,
        Email: u.Email,
        FullName: u.FullName,
        Role: u.Role.ToString().ToLower(),
        IsActive: u.IsActive,
        AvatarUrl: u.AvatarUrl,
        LastLoginAt: u.LastLoginAt,
        CreatedAt: u.CreatedAt);
}
