using FeruzaShopProject.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Infrastructre.Authorization
{
    public class BranchAccessHandler : AuthorizationHandler<BranchAccessRequirement>
    {
        private readonly ILogger<BranchAccessHandler> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BranchAccessHandler(ILogger<BranchAccessHandler> logger, IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            BranchAccessRequirement requirement)
        {
            var branchId = requirement.BranchId; // ✅ take from requirement first

            // fallback: check HttpContext.Items if requirement.BranchId is null
            if (!branchId.HasValue)
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var branchRequirement = httpContext?.Items["BranchAccessRequirement"] as BranchAccessRequirement;
                branchId = branchRequirement?.BranchId;
            }

            var userName = context.User.Identity?.Name ?? "Anonymous";
            var roles = string.Join(", ", context.User.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value));

            _logger.LogInformation(
                $"Checking BranchAccess for user: {userName}, Roles: {roles}, Requested BranchId: {branchId}");

            // Manager/Finance can access all branches
            if (context.User.HasClaim(c => c.Type == ClaimTypes.Role &&
                (c.Value == Role.Manager.ToString() || c.Value == Role.Finance.ToString())))
            {
                _logger.LogInformation("Granting access to Manager/Finance role");
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Sales can only access their own branch
            if (context.User.HasClaim(c => c.Type == ClaimTypes.Role && c.Value == Role.Sales.ToString()))
            {
                var userBranchClaim = context.User.FindFirst("BranchId")?.Value;
                if (!branchId.HasValue)
                {
                    _logger.LogWarning("BranchId not provided for Sales role");
                    context.Fail(new AuthorizationFailureReason(this, "BranchId is required for Sales role"));
                    return Task.CompletedTask;
                }

                if (string.IsNullOrEmpty(userBranchClaim) || !Guid.TryParse(userBranchClaim, out Guid userBranchId))
                {
                    _logger.LogWarning($"Invalid BranchId claim for user: {userName}");
                    context.Fail(new AuthorizationFailureReason(this, "Invalid or missing BranchId claim"));
                    return Task.CompletedTask;
                }

                if (userBranchId == branchId.Value)
                {
                    _logger.LogInformation("BranchAccess granted for Sales user");
                    context.Succeed(requirement);
                }
                else
                {
                    _logger.LogWarning($"BranchAccess denied: User BranchId={userBranchClaim}, Requested BranchId={branchId}");
                    context.Fail(new AuthorizationFailureReason(this, $"BranchId mismatch: User BranchId={userBranchClaim}, Requested BranchId={branchId}"));
                }
            }
            else
            {
                _logger.LogWarning($"User lacks required role. Roles: {roles}");
                context.Fail(new AuthorizationFailureReason(this, "User must be Sales, Manager, or Finance"));
            }

            return Task.CompletedTask;
        }

    }

    public class BranchAccessRequirement : IAuthorizationRequirement
    {
        public Guid? BranchId { get; }

        public BranchAccessRequirement(Guid? branchId = null)
        {
            BranchId = branchId;
        }
    }
}
