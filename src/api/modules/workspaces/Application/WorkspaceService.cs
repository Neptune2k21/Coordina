using System.Security.Cryptography;
using System.Text;
using Coordina.Api.Core.Results;
using Coordina.Api.Modules.Workspaces.Contracts;
using Coordina.Api.Modules.Workspaces.Domain;
using Coordina.Api.Shared.Enums;
using Coordina.Api.Shared.Validation;

namespace Coordina.Api.Modules.Workspaces.Application;

public sealed class WorkspaceService(
  IWorkspaceStore workspaces,
  IEnumerable<IWorkspaceDepartureHandler> departureHandlers,
  TimeProvider clock) : IWorkspaceService
{
  private const string LastOwnerMessage =
    "A workspace needs at least one owner. Make another member an owner first.";
  private const string InvalidInviteMessage =
    "Invitation code is invalid or has already been used.";
  private const string AlreadyMemberMessage =
    "You already belong to this workspace.";
  private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

  public async Task<Result<WorkspaceResponse>> CreateAsync(
    CreateWorkspaceRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var errors = new ValidationErrors().Required(
      "name",
      request.Name,
      "Workspace name",
      minLength: 2,
      maxLength: 80);

    if (errors.HasErrors)
    {
      return errors.ToError();
    }

    var workspace = await workspaces.CreateAsync(
      request.Name.Trim(),
      userId,
      clock.GetUtcNow(),
      cancellationToken);

    return Result.Success(ToResponse(workspace));
  }

  public async Task<IReadOnlyCollection<WorkspaceResponse>> ListAsync(
    Guid userId,
    CancellationToken cancellationToken)
  {
    var userWorkspaces = await workspaces.ListForUserAsync(
      userId,
      cancellationToken);

    return userWorkspaces.Select(ToResponse).ToArray();
  }

  public async Task<Result<WorkspaceResponse>> GetAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var workspace = await workspaces.FindForUserAsync(
      workspaceId,
      userId,
      cancellationToken);

    return workspace is null
      ? Error.NotFound()
      : Result.Success(ToResponse(workspace));
  }

  public async Task<Result<WorkspaceResponse>> JoinAsync(
    JoinWorkspaceRequest request,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var inviteCode = NormalizeInviteCode(request.InviteCode);

    if (inviteCode.Length != 12)
    {
      return Error.Validation(
        nameof(request.InviteCode),
        "Enter a valid invitation code.");
    }

    var invite = await workspaces.FindUsableInviteAsync(
      HashInviteCode(inviteCode),
      clock.GetUtcNow(),
      cancellationToken);

    if (invite is null)
    {
      return Error.NotFound(InvalidInviteMessage);
    }

    var existingMembership = await workspaces.FindForUserAsync(
      invite.WorkspaceId,
      userId,
      cancellationToken);

    if (existingMembership is not null)
    {
      return Error.Conflict(AlreadyMemberMessage);
    }

    var outcome = await workspaces.ConsumeInviteAsync(
      invite.Id,
      userId,
      clock.GetUtcNow(),
      cancellationToken);

    return outcome.Status switch
    {
      ConsumeInviteStatus.Consumed => Result.Success(ToResponse(outcome.Workspace!)),
      ConsumeInviteStatus.AlreadyMember => Error.Conflict(AlreadyMemberMessage),
      _ => Error.NotFound(InvalidInviteMessage)
    };
  }

  public async Task<Result<CreateWorkspaceInviteResponse>> CreateInviteAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    if (await EnsureOwnerAsync(
      workspaceId,
      userId,
      "Only workspace owners can create invitation codes.",
      cancellationToken) is { } error)
    {
      return error;
    }

    var code = GenerateInviteCode();
    var now = clock.GetUtcNow();
    var expiresAt = now.Add(InviteLifetime);

    await workspaces.CreateInviteAsync(
      workspaceId,
      userId,
      HashInviteCode(code),
      now,
      expiresAt,
      cancellationToken);

    return Result.Success(new CreateWorkspaceInviteResponse(code, expiresAt));
  }

  public async Task<Result<IReadOnlyCollection<WorkspaceMemberResponse>>> ListMembersAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    var role = await workspaces.FindUserRoleAsync(
      workspaceId,
      userId,
      cancellationToken);

    if (role is null)
    {
      return Error.NotFound();
    }

    var members = await workspaces.ListMembersAsync(
      workspaceId,
      cancellationToken);

    return Result.Success<IReadOnlyCollection<WorkspaceMemberResponse>>(
      members.Select(ToResponse).ToArray());
  }

  public async Task<Result<WorkspaceMemberResponse>> ChangeMemberRoleAsync(
    Guid workspaceId,
    Guid memberUserId,
    ChangeMemberRoleRequest request,
    Guid currentUserId,
    CancellationToken cancellationToken)
  {
    if (!EnumCode.TryParse<WorkspaceRole>(request.Role, out var role))
    {
      return Error.Validation(
        "role",
        $"Role must be {EnumCode.Describe<WorkspaceRole>()}.");
    }

    // The checks run under the workspace lock: two owners demoting each other
    // at the same time would otherwise both see a second owner and leave none.
    return await workspaces.RunExclusiveAsync<Result<WorkspaceMemberResponse>>(
      workspaceId,
      async token =>
      {
        if (await EnsureOwnerAsync(
          workspaceId,
          currentUserId,
          "Only workspace owners can change member roles.",
          token) is { } error)
        {
          return error;
        }

        var members = await workspaces.ListMembersAsync(workspaceId, token);
        var member = members.SingleOrDefault(candidate => candidate.UserId == memberUserId);

        if (member is null)
        {
          return Error.NotFound();
        }

        if (member.Role == role)
        {
          return Result.Success(ToResponse(member));
        }

        if (member.Role == WorkspaceRole.Owner && OtherOwners(members, memberUserId).Length == 0)
        {
          return Error.Conflict(LastOwnerMessage);
        }

        await workspaces.UpdateMemberRoleAsync(workspaceId, memberUserId, role, token);

        return Result.Success(ToResponse(member with { Role = role }));
      },
      cancellationToken);
  }

  public async Task<Result> LeaveAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    return await workspaces.RunExclusiveAsync<Result>(
      workspaceId,
      async token =>
      {
        var members = await workspaces.ListMembersAsync(workspaceId, token);

        if (members.All(member => member.UserId != userId))
        {
          return Error.NotFound();
        }

        // What the leaver owned goes to the owner who has been there longest.
        var successor = OtherOwners(members, userId).FirstOrDefault();

        if (successor is null)
        {
          return Error.Conflict(members.Count == 1
            ? "You are the only member. Delete the workspace instead of leaving it."
            : LastOwnerMessage);
        }

        await DepartAsync(new WorkspaceDeparture(workspaceId, userId, successor.UserId), token);

        return Result.Success();
      },
      cancellationToken);
  }

  public async Task<Result> RemoveMemberAsync(
    Guid workspaceId,
    Guid memberUserId,
    Guid currentUserId,
    CancellationToken cancellationToken)
  {
    return await workspaces.RunExclusiveAsync<Result>(
      workspaceId,
      async token =>
      {
        if (await EnsureOwnerAsync(
          workspaceId,
          currentUserId,
          "Only workspace owners can remove members.",
          token) is { } error)
        {
          return error;
        }

        if (memberUserId == currentUserId)
        {
          return Error.Validation(
            nameof(memberUserId),
            "Owners cannot remove themselves. Leave the workspace instead.");
        }

        var memberRole = await workspaces.FindUserRoleAsync(
          workspaceId,
          memberUserId,
          token);

        if (memberRole is null)
        {
          return Error.NotFound();
        }

        if (memberRole == WorkspaceRole.Owner)
        {
          return Error.Forbidden("Owner members cannot be removed. Make them a member first.");
        }

        // The owner who removes a member takes over what that member owned.
        await DepartAsync(new WorkspaceDeparture(workspaceId, memberUserId, currentUserId), token);

        return Result.Success();
      },
      cancellationToken);
  }

  public async Task<Result> DeleteAsync(
    Guid workspaceId,
    Guid userId,
    CancellationToken cancellationToken)
  {
    if (await EnsureOwnerAsync(
      workspaceId,
      userId,
      "Only workspace owners can delete a workspace.",
      cancellationToken) is { } error)
    {
      return error;
    }

    await workspaces.DeleteAsync(workspaceId, cancellationToken);

    return Result.Success();
  }

  /// <summary>
  /// Hides the workspace from non-members (404) and rejects members that are
  /// not owners (403).
  /// </summary>
  private async Task<Error?> EnsureOwnerAsync(
    Guid workspaceId,
    Guid userId,
    string forbiddenMessage,
    CancellationToken cancellationToken)
  {
    var role = await workspaces.FindUserRoleAsync(
      workspaceId,
      userId,
      cancellationToken);

    return role switch
    {
      null => Error.NotFound(),
      WorkspaceRole.Owner => null,
      _ => Error.Forbidden(forbiddenMessage)
    };
  }

  /// <summary>
  /// Removes the member, then lets the other modules hand over or drop what
  /// pointed at them, in the same transaction.
  /// </summary>
  private async Task DepartAsync(WorkspaceDeparture departure, CancellationToken cancellationToken)
  {
    await workspaces.RemoveMemberAsync(departure.WorkspaceId, departure.UserId, cancellationToken);

    foreach (var handler in departureHandlers)
    {
      await handler.HandleAsync(departure, cancellationToken);
    }
  }

  /// <summary>Owners other than <paramref name="userId"/>, longest-standing first.</summary>
  private static WorkspaceMember[] OtherOwners(
    IEnumerable<WorkspaceMember> members,
    Guid userId) =>
    members
      .Where(member => member.Role == WorkspaceRole.Owner && member.UserId != userId)
      .OrderBy(member => member.JoinedAt)
      .ThenBy(member => member.UserId)
      .ToArray();

  private static string GenerateInviteCode()
  {
    return Convert.ToHexString(RandomNumberGenerator.GetBytes(6));
  }

  private static string NormalizeInviteCode(string value) =>
    value.Trim().Replace("-", string.Empty).ToUpperInvariant();

  private static string HashInviteCode(string value)
  {
    var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
    return Convert.ToHexString(bytes);
  }

  private static WorkspaceResponse ToResponse(Workspace workspace)
  {
    return new WorkspaceResponse(
      workspace.Id.ToString(),
      workspace.Name,
      EnumCode.Format(workspace.CurrentUserRole),
      workspace.CreatedAt);
  }

  private static WorkspaceMemberResponse ToResponse(WorkspaceMember member)
  {
    return new WorkspaceMemberResponse(
      member.UserId.ToString(),
      member.Name,
      member.Email,
      EnumCode.Format(member.Role),
      member.JoinedAt);
  }
}
