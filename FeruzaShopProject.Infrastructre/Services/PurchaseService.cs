using AutoMapper;
using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;
using FeruzaShopProject.Infrastructre.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FeruzaShopProject.Infrastructre.Services
{
    public class PurchaseService : IPurchaseService
    {
        private readonly ShopDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<PurchaseService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PurchaseService(
            ShopDbContext context,
            IMapper mapper,
            ILogger<PurchaseService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        #region Helper Methods

        private async Task<Guid> GetCurrentUserIdAsync()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Invalid user ID in JWT: {UserIdClaim}", userIdClaim);
                throw new UnauthorizedAccessException("Invalid user ID in JWT");
            }
            return userId;
        }

        private async Task AddPurchaseHistoryAsync(Guid purchaseOrderId, string action, Guid userId, string details, Guid? itemId = null)
        {
            _context.PurchaseHistory.Add(new PurchaseHistory
            {
                Id = Guid.NewGuid(),
                PurchaseOrderId = purchaseOrderId,
                Action = action,
                PerformedByUserId = userId,
                Details = details,
                PurchaseOrderItemId = itemId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
        }

        private async Task<decimal> GetDefaultMarkupPercentageAsync()
        {
            // This could come from settings table
            return 30m; // 30% default markup
        }

        #endregion

        #region Step 1: Sales Creates Purchase Order

        public async Task<ApiResponse<PurchaseOrderDto>> CreatePurchaseOrderAsync(CreatePurchaseOrderDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                // Validate branch
                var branch = await _context.Branches.FindAsync(dto.BranchId);
                if (branch == null || !branch.IsActive)
                {
                    _logger.LogWarning("Invalid or inactive branch: {BranchId}", dto.BranchId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Invalid or inactive branch");
                }

                // Create purchase order
                var purchaseOrder = new PurchaseOrder
                {
                    Id = Guid.NewGuid(),
                    BranchId = dto.BranchId,
                    CreatedBy = userId,
                    Status = PurchaseOrderStatus.PendingFinanceVerification,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Items = new List<PurchaseOrderItem>()
                };

                // Validate and add items
                foreach (var itemDto in dto.Items)
                {
                    var product = await _context.Products.FindAsync(itemDto.ProductId);
                    if (product == null || !product.IsActive)
                    {
                        _logger.LogWarning("Invalid or inactive product: {ProductId}", itemDto.ProductId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Invalid or inactive product: {itemDto.ProductId}");
                    }

                    var poItem = new PurchaseOrderItem
                    {
                        Id = Guid.NewGuid(),
                        PurchaseOrderId = purchaseOrder.Id,
                        ProductId = itemDto.ProductId,
                        Quantity = itemDto.Quantity,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    purchaseOrder.Items.Add(poItem);
                }

                _context.PurchaseOrders.Add(purchaseOrder);
                await AddPurchaseHistoryAsync(purchaseOrder.Id, "Created", userId, $"Purchase order created with {purchaseOrder.Items.Count} items");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = await GetPurchaseOrderByIdAsync(purchaseOrder.Id);
                _logger.LogInformation("Sales created purchase order {PurchaseOrderId} for branch {BranchId}", purchaseOrder.Id, dto.BranchId);
                return ApiResponse<PurchaseOrderDto>.Success(result.Data, "Purchase order created successfully. Waiting for finance verification.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating purchase order: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error creating purchase order");
            }
        }

        #endregion

        #region Step 2: Finance Verification

        public async Task<ApiResponse<PurchaseOrderDto>> FinanceVerificationAsync(FinanceVerificationDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .Include(po => po.Branch)
                    .FirstOrDefaultAsync(po => po.Id == dto.PurchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                // Verify status
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingFinanceVerification)
                {
                    _logger.LogWarning("Purchase order not in PendingFinanceVerification status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order must be in PendingFinanceVerification status. Current status: {purchaseOrder.Status}");
                }

                // Set invoice number
                purchaseOrder.InvoiceNumber = dto.InvoiceNumber;

                // Process each item
                foreach (var itemDto in dto.Items)
                {
                    var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.ItemId);
                    if (item == null)
                    {
                        _logger.LogWarning("Purchase order item not found: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order item not found: {itemDto.ItemId}");
                    }

                    // Set buying price
                    item.BuyingPrice = itemDto.BuyingPrice;

                    // Set selling price (auto-calculate if not provided)
                    if (itemDto.SellingPrice.HasValue)
                    {
                        item.UnitPrice = itemDto.SellingPrice.Value;
                    }
                    else
                    {
                        // Auto-calculate with default markup
                        var markupPercentage = await GetDefaultMarkupPercentageAsync();
                        item.UnitPrice = itemDto.BuyingPrice * (1 + markupPercentage / 100);
                    }

                    // Set supplier name if provided
                    if (!string.IsNullOrWhiteSpace(itemDto.SupplierName))
                    {
                        item.SupplierName = itemDto.SupplierName;
                    }

                    item.UpdatedAt = DateTime.UtcNow;
                }

                // Update purchase order status
                purchaseOrder.Status = PurchaseOrderStatus.PendingManagerApproval;
                purchaseOrder.FinanceVerifiedAt = DateTime.UtcNow;
                purchaseOrder.FinanceVerifiedBy = userId;
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "FinanceVerified", userId,
                    $"Finance verified purchase order with invoice #{dto.InvoiceNumber}. Items: {purchaseOrder.Items.Count}");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = await GetPurchaseOrderByIdAsync(purchaseOrder.Id);
                _logger.LogInformation("Finance verified purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result.Data, "Purchase order verified successfully. Waiting for manager approval.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in finance verification: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error in finance verification");
            }
        }

        #endregion

        #region Step 3: Manager Approval

        public async Task<ApiResponse<PurchaseOrderDto>> ManagerApprovalAsync(ManagerApprovalDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .Include(po => po.Branch)
                    .FirstOrDefaultAsync(po => po.Id == dto.PurchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                // Verify status
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingManagerApproval)
                {
                    _logger.LogWarning("Purchase order not in PendingManagerApproval status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order must be in PendingManagerApproval status. Current status: {purchaseOrder.Status}");
                }

                // Check if all items have prices
                if (purchaseOrder.Items.Any(i => !i.BuyingPrice.HasValue || !i.UnitPrice.HasValue))
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} has items without prices", purchaseOrder.Id);
                    return ApiResponse<PurchaseOrderDto>.Fail("All items must have prices before approval");
                }

                // Update status based on approval/rejection
                if (dto.IsApproved)
                {
                    purchaseOrder.Status = PurchaseOrderStatus.Approved;
                    purchaseOrder.ApprovedAt = DateTime.UtcNow;
                    purchaseOrder.ApprovedBy = userId;

                    await AddPurchaseHistoryAsync(purchaseOrder.Id, "Approved", userId,
                        $"Manager approved purchase order. Total value: {purchaseOrder.TotalBuyingCost:C2}");
                }
                else
                {
                    purchaseOrder.Status = PurchaseOrderStatus.Rejected;
                    purchaseOrder.UpdatedAt = DateTime.UtcNow;

                    await AddPurchaseHistoryAsync(purchaseOrder.Id, "Rejected", userId,
                        $"Manager rejected purchase order. Reason: {dto.Remarks ?? "Not specified"}");
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = await GetPurchaseOrderByIdAsync(purchaseOrder.Id);
                var message = dto.IsApproved
                    ? "Purchase order approved successfully"
                    : "Purchase order rejected";

                _logger.LogInformation("Manager {Action} purchase order {PurchaseOrderId}",
                    dto.IsApproved ? "approved" : "rejected", purchaseOrder.Id);

                return ApiResponse<PurchaseOrderDto>.Success(result.Data, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in manager approval: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error in manager approval");
            }
        }

        #endregion

        #region Sales Edit Operations

        public async Task<ApiResponse<PurchaseOrderDto>> EditPurchaseOrderBySalesAsync(EditPurchaseOrderBySalesDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .FirstOrDefaultAsync(po => po.Id == dto.PurchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                // Verify sales can edit (only PendingFinanceVerification status)
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingFinanceVerification)
                {
                    _logger.LogWarning("Sales cannot edit purchase order in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order can only be edited when in PendingFinanceVerification status");
                }

                // Verify that the current user is the creator
                if (purchaseOrder.CreatedBy != userId)
                {
                    _logger.LogWarning("User {UserId} is not the creator of purchase order {PurchaseOrderId}", userId, dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("You can only edit purchase orders that you created");
                }

                // Update branch if changed
                if (dto.BranchId.HasValue && dto.BranchId.Value != purchaseOrder.BranchId)
                {
                    var branch = await _context.Branches.FindAsync(dto.BranchId.Value);
                    if (branch == null || !branch.IsActive)
                    {
                        _logger.LogWarning("Invalid or inactive branch: {BranchId}", dto.BranchId.Value);
                        return ApiResponse<PurchaseOrderDto>.Fail("Invalid or inactive branch");
                    }
                    purchaseOrder.BranchId = dto.BranchId.Value;
                }

                // Update items
                if (dto.Items != null && dto.Items.Any())
                {
                    var newItems = new List<PurchaseOrderItem>();
                    var requestedItemIds = dto.Items.Where(i => i.ItemId.HasValue).Select(i => i.ItemId.Value).ToHashSet();

                    // Update existing items
                    foreach (var itemDto in dto.Items.Where(i => i.ItemId.HasValue))
                    {
                        var existingItem = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.ItemId.Value);
                        if (existingItem != null)
                        {
                            existingItem.ProductId = itemDto.ProductId;
                            existingItem.Quantity = itemDto.Quantity;
                            existingItem.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    // Add new items
                    foreach (var itemDto in dto.Items.Where(i => !i.ItemId.HasValue))
                    {
                        var product = await _context.Products.FindAsync(itemDto.ProductId);
                        if (product == null || !product.IsActive)
                        {
                            _logger.LogWarning("Invalid or inactive product: {ProductId}", itemDto.ProductId);
                            return ApiResponse<PurchaseOrderDto>.Fail($"Invalid or inactive product: {itemDto.ProductId}");
                        }

                        var newItem = new PurchaseOrderItem
                        {
                            Id = Guid.NewGuid(),
                            PurchaseOrderId = purchaseOrder.Id,
                            ProductId = itemDto.ProductId,
                            Quantity = itemDto.Quantity,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        purchaseOrder.Items.Add(newItem);
                    }

                    // Remove items not in the list
                    var itemsToRemove = purchaseOrder.Items
                        .Where(i => i.IsActive && !requestedItemIds.Contains(i.Id))
                        .ToList();

                    foreach (var item in itemsToRemove)
                    {
                        item.IsActive = false;
                        item.UpdatedAt = DateTime.UtcNow;
                    }
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "EditedBySales", userId,
                    $"Sales edited purchase order. Items: {purchaseOrder.Items.Count(i => i.IsActive)}");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = await GetPurchaseOrderByIdAsync(purchaseOrder.Id);
                _logger.LogInformation("Sales edited purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result.Data, "Purchase order updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing purchase order by sales: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error editing purchase order");
            }
        }

        public async Task<ApiResponse<bool>> DeletePurchaseOrderBySalesAsync(Guid purchaseOrderId, string? reason = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .FirstOrDefaultAsync(po => po.Id == purchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", purchaseOrderId);
                    return ApiResponse<bool>.Fail("Purchase order not found or inactive");
                }

                // Verify status
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingFinanceVerification)
                {
                    _logger.LogWarning("Sales cannot delete purchase order in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<bool>.Fail("Purchase order can only be deleted when in PendingFinanceVerification status");
                }

                // Verify user is creator
                if (purchaseOrder.CreatedBy != userId)
                {
                    _logger.LogWarning("User {UserId} is not the creator of purchase order {PurchaseOrderId}", userId, purchaseOrderId);
                    return ApiResponse<bool>.Fail("You can only delete purchase orders that you created");
                }

                // Soft delete
                purchaseOrder.IsActive = false;
                purchaseOrder.Status = PurchaseOrderStatus.Cancelled;
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                foreach (var item in purchaseOrder.Items)
                {
                    item.IsActive = false;
                    item.UpdatedAt = DateTime.UtcNow;
                }

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "DeletedBySales", userId,
                    $"Sales deleted purchase order. Reason: {reason ?? "Not specified"}");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Sales deleted purchase order {PurchaseOrderId}", purchaseOrderId);
                return ApiResponse<bool>.Success(true, "Purchase order deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting purchase order by sales: {PurchaseOrderId}", purchaseOrderId);
                await transaction.RollbackAsync();
                return ApiResponse<bool>.Fail("Error deleting purchase order");
            }
        }

        public async Task<ApiResponse<PurchaseOrderDto>> DeleteItemFromPurchaseOrderBySalesAsync(Guid purchaseOrderId, Guid itemId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .FirstOrDefaultAsync(po => po.Id == purchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", purchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                // Verify status
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingFinanceVerification)
                {
                    _logger.LogWarning("Sales cannot delete items from purchase order in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Items can only be deleted when order is in PendingFinanceVerification status");
                }

                // Verify user is creator
                if (purchaseOrder.CreatedBy != userId)
                {
                    _logger.LogWarning("User {UserId} is not the creator of purchase order {PurchaseOrderId}", userId, purchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("You can only delete items from purchase orders that you created");
                }

                var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemId && i.IsActive);
                if (item == null)
                {
                    _logger.LogWarning("Item not found: {ItemId}", itemId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Item not found");
                }

                // Check if finance has already verified (shouldn't happen due to status check, but double-check)
                if (item.BuyingPrice.HasValue || item.UnitPrice.HasValue)
                {
                    _logger.LogWarning("Cannot delete item that has prices set: {ItemId}", itemId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Cannot delete item that has already been processed by finance");
                }

                // Delete related history first
                var itemHistory = await _context.PurchaseHistory
                    .Where(h => h.PurchaseOrderItemId == itemId)
                    .ToListAsync();

                if (itemHistory.Any())
                {
                    _context.PurchaseHistory.RemoveRange(itemHistory);
                }

                // Delete the item
                _context.PurchaseOrderItems.Remove(item);
                purchaseOrder.Items.Remove(item);

                // If no items left, cancel the whole order
                if (!purchaseOrder.Items.Any(i => i.IsActive))
                {
                    purchaseOrder.IsActive = false;
                    purchaseOrder.Status = PurchaseOrderStatus.Cancelled;

                    var orderHistory = await _context.PurchaseHistory
                        .Where(h => h.PurchaseOrderId == purchaseOrderId)
                        .ToListAsync();

                    if (orderHistory.Any())
                    {
                        _context.PurchaseHistory.RemoveRange(orderHistory);
                    }
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "ItemDeletedBySales", userId,
                    $"Sales deleted item from purchase order");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (!purchaseOrder.Items.Any(i => i.IsActive))
                {
                    return ApiResponse<PurchaseOrderDto>.Success(null, "Purchase order deleted as all items were removed");
                }

                var result = await GetPurchaseOrderByIdAsync(purchaseOrder.Id);
                _logger.LogInformation("Sales deleted item {ItemId} from purchase order {PurchaseOrderId}", itemId, purchaseOrderId);
                return ApiResponse<PurchaseOrderDto>.Success(result.Data, "Item deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item from purchase order: {PurchaseOrderId}, Item: {ItemId}", purchaseOrderId, itemId);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error deleting item from purchase order");
            }
        }

        #endregion

        #region Finance Edit Operations

        public async Task<ApiResponse<PurchaseOrderDto>> EditPurchaseOrderByFinanceAsync(EditPurchaseOrderByFinanceDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .FirstOrDefaultAsync(po => po.Id == dto.PurchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                // Verify finance can edit (before manager approval)
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingFinanceVerification &&
                    purchaseOrder.Status != PurchaseOrderStatus.PendingManagerApproval)
                {
                    _logger.LogWarning("Finance cannot edit purchase order in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order can only be edited when in PendingFinanceVerification or PendingManagerApproval status");
                }

                // Update invoice number if provided
                if (!string.IsNullOrWhiteSpace(dto.InvoiceNumber))
                {
                    purchaseOrder.InvoiceNumber = dto.InvoiceNumber;
                }

                // Update items if provided
                if (dto.Items != null && dto.Items.Any())
                {
                    foreach (var itemDto in dto.Items)
                    {
                        var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.ItemId && i.IsActive);
                        if (item == null)
                        {
                            _logger.LogWarning("Purchase order item not found: {ItemId}", itemDto.ItemId);
                            return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order item not found: {itemDto.ItemId}");
                        }

                        // Update supplier name if provided
                        if (!string.IsNullOrWhiteSpace(itemDto.SupplierName))
                        {
                            item.SupplierName = itemDto.SupplierName;
                        }

                        // Update buying price if provided
                        if (itemDto.BuyingPrice.HasValue)
                        {
                            item.BuyingPrice = itemDto.BuyingPrice.Value;
                        }

                        // Update selling price if provided
                        if (itemDto.SellingPrice.HasValue)
                        {
                            item.UnitPrice = itemDto.SellingPrice.Value;
                        }

                        item.UpdatedAt = DateTime.UtcNow;
                    }
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "EditedByFinance", userId,
                    $"Finance edited purchase order details");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = await GetPurchaseOrderByIdAsync(purchaseOrder.Id);
                _logger.LogInformation("Finance edited purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result.Data, "Purchase order updated successfully by finance");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing purchase order by finance: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error editing purchase order");
            }
        }

        #endregion

        #region Reject/Cancel Operations

        public async Task<ApiResponse<RejectResponseDto>> RejectPurchaseOrderAsync(RejectPurchaseOrderDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .FirstOrDefaultAsync(po => po.Id == dto.PurchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<RejectResponseDto>.Fail("Purchase order not found or inactive");
                }

                // Cannot reject already approved orders
                if (purchaseOrder.Status == PurchaseOrderStatus.Approved)
                {
                    _logger.LogWarning("Cannot reject approved purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<RejectResponseDto>.Fail("Cannot reject an approved purchase order");
                }

                purchaseOrder.Status = PurchaseOrderStatus.Rejected;
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "Rejected", userId,
                    $"Purchase order rejected. Reason: {dto.Reason}");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var response = new RejectResponseDto
                {
                    PurchaseOrderId = purchaseOrder.Id,
                    NewStatus = purchaseOrder.Status,
                    Message = $"Purchase order rejected. Reason: {dto.Reason}"
                };

                _logger.LogInformation("Purchase order {PurchaseOrderId} rejected", purchaseOrder.Id);
                return ApiResponse<RejectResponseDto>.Success(response, "Purchase order rejected successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting purchase order: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<RejectResponseDto>.Fail("Error rejecting purchase order");
            }
        }

        public async Task<ApiResponse<bool>> CancelPurchaseOrderAsync(CancelPurchaseOrderDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .FirstOrDefaultAsync(po => po.Id == dto.PurchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<bool>.Fail("Purchase order not found or inactive");
                }

                // Can only cancel in PendingFinanceVerification status
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingFinanceVerification)
                {
                    _logger.LogWarning("Cannot cancel purchase order in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<bool>.Fail($"Cannot cancel purchase order in {purchaseOrder.Status} status");
                }

                purchaseOrder.IsActive = false;
                purchaseOrder.Status = PurchaseOrderStatus.Cancelled;
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                foreach (var item in purchaseOrder.Items)
                {
                    item.IsActive = false;
                    item.UpdatedAt = DateTime.UtcNow;
                }

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "Cancelled", userId,
                    $"Purchase order cancelled. Reason: {dto.Reason ?? "Not specified"}");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Cancelled purchase order {PurchaseOrderId}", dto.PurchaseOrderId);
                return ApiResponse<bool>.Success(true, "Purchase order cancelled successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling purchase order {PurchaseOrderId}", dto.PurchaseOrderId);
                await transaction.RollbackAsync();
                return ApiResponse<bool>.Fail("Error cancelling purchase order");
            }
        }

        #endregion

        #region Query Methods

        public async Task<ApiResponse<PurchaseOrderDto>> GetPurchaseOrderByIdAsync(Guid id)
        {
            try
            {
                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Branch)
                    .Include(po => po.Creator)
                    .Include(po => po.FinanceVerifier)
                    .Include(po => po.Approver)
                    .Include(po => po.Items).ThenInclude(pi => pi.Product)
                    .Include(po => po.History).ThenInclude(h => h.PerformedByUser)
                    .FirstOrDefaultAsync(po => po.Id == id);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found: {PurchaseOrderId}", id);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found");
                }

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                return ApiResponse<PurchaseOrderDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving purchase order {PurchaseOrderId}", id);
                return ApiResponse<PurchaseOrderDto>.Fail(ex.Message);
            }
        }

        public async Task<ApiResponse<List<PurchaseOrderDto>>> GetAllPurchaseOrdersAsync()
        {
            try
            {
                var purchaseOrders = await _context.PurchaseOrders
                    .Include(po => po.Branch)
                    .Include(po => po.Creator)
                    .Include(po => po.FinanceVerifier)
                    .Include(po => po.Approver)
                    .Include(po => po.Items).ThenInclude(pi => pi.Product)
                    .OrderByDescending(po => po.CreatedAt)
                    .ToListAsync();

                var result = _mapper.Map<List<PurchaseOrderDto>>(purchaseOrders);
                return ApiResponse<List<PurchaseOrderDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all purchase orders");
                return ApiResponse<List<PurchaseOrderDto>>.Fail("Error retrieving purchase orders");
            }
        }

        public async Task<ApiResponse<List<PurchaseOrderDto>>> GetPurchaseOrdersByStatusAsync(PurchaseOrderStatus status)
        {
            try
            {
                var purchaseOrders = await _context.PurchaseOrders
                    .Include(po => po.Branch)
                    .Include(po => po.Creator)
                    .Include(po => po.FinanceVerifier)
                    .Include(po => po.Approver)
                    .Include(po => po.Items).ThenInclude(pi => pi.Product)
                    .Where(po => po.Status == status && po.IsActive)
                    .OrderByDescending(po => po.CreatedAt)
                    .ToListAsync();

                var result = _mapper.Map<List<PurchaseOrderDto>>(purchaseOrders);
                return ApiResponse<List<PurchaseOrderDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving purchase orders by status {Status}", status);
                return ApiResponse<List<PurchaseOrderDto>>.Fail($"Error retrieving purchase orders by status: {status}");
            }
        }

        public async Task<ApiResponse<List<PurchaseOrderDto>>> GetPurchaseOrdersByBranchAsync(Guid branchId)
        {
            try
            {
                var purchaseOrders = await _context.PurchaseOrders
                    .Include(po => po.Branch)
                    .Include(po => po.Creator)
                    .Include(po => po.FinanceVerifier)
                    .Include(po => po.Approver)
                    .Include(po => po.Items).ThenInclude(pi => pi.Product)
                    .Where(po => po.BranchId == branchId && po.IsActive)
                    .OrderByDescending(po => po.CreatedAt)
                    .ToListAsync();

                var result = _mapper.Map<List<PurchaseOrderDto>>(purchaseOrders);
                return ApiResponse<List<PurchaseOrderDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving purchase orders for branch {BranchId}", branchId);
                return ApiResponse<List<PurchaseOrderDto>>.Fail($"Error retrieving purchase orders for branch: {branchId}");
            }
        }

        public async Task<ApiResponse<List<PurchaseOrderDto>>> GetPurchaseOrdersByCreatorAsync(Guid createdBy)
        {
            try
            {
                var purchaseOrders = await _context.PurchaseOrders
                    .Include(po => po.Branch)
                    .Include(po => po.Creator)
                    .Include(po => po.FinanceVerifier)
                    .Include(po => po.Approver)
                    .Include(po => po.Items).ThenInclude(pi => pi.Product)
                    .Where(po => po.CreatedBy == createdBy && po.IsActive)
                    .OrderByDescending(po => po.CreatedAt)
                    .ToListAsync();

                var result = _mapper.Map<List<PurchaseOrderDto>>(purchaseOrders);
                return ApiResponse<List<PurchaseOrderDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving purchase orders by creator {CreatedBy}", createdBy);
                return ApiResponse<List<PurchaseOrderDto>>.Fail($"Error retrieving purchase orders by creator: {createdBy}");
            }
        }

        public async Task<ApiResponse<List<PurchaseOrderDto>>> GetPurchaseOrdersByDateRangeAsync(DateTime fromDate, DateTime toDate, Guid? branchId = null)
        {
            try
            {
                var query = _context.PurchaseOrders
                    .Include(po => po.Branch)
                    .Include(po => po.Creator)
                    .Include(po => po.FinanceVerifier)
                    .Include(po => po.Approver)
                    .Include(po => po.Items).ThenInclude(pi => pi.Product)
                    .Where(po => po.CreatedAt.Date >= fromDate.Date && po.CreatedAt.Date <= toDate.Date);

                if (branchId.HasValue)
                {
                    query = query.Where(po => po.BranchId == branchId.Value);
                }

                var purchaseOrders = await query
                    .OrderByDescending(po => po.CreatedAt)
                    .ToListAsync();

                var result = _mapper.Map<List<PurchaseOrderDto>>(purchaseOrders);
                return ApiResponse<List<PurchaseOrderDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving purchase orders by date range");
                return ApiResponse<List<PurchaseOrderDto>>.Fail("Error retrieving purchase orders by date range");
            }
        }

        public async Task<ApiResponse<PurchaseOrderStatsDto>> GetPurchaseOrderStatsAsync(Guid? branchId = null)
        {
            try
            {
                var query = _context.PurchaseOrders
                    .Include(po => po.Items)
                    .Where(po => po.IsActive)
                    .AsQueryable();

                if (branchId.HasValue)
                {
                    query = query.Where(po => po.BranchId == branchId.Value);
                }

                // Get all data first, then calculate in memory
                var purchaseOrders = await query.ToListAsync();

                var stats = new PurchaseOrderStatsDto
                {
                    TotalPurchaseOrders = purchaseOrders.Count,

                    PendingFinanceVerification = purchaseOrders.Count(po => po.Status == PurchaseOrderStatus.PendingFinanceVerification),
                    PendingManagerApproval = purchaseOrders.Count(po => po.Status == PurchaseOrderStatus.PendingManagerApproval),
                    Approved = purchaseOrders.Count(po => po.Status == PurchaseOrderStatus.Approved),
                    Rejected = purchaseOrders.Count(po => po.Status == PurchaseOrderStatus.Rejected),
                    Cancelled = purchaseOrders.Count(po => po.Status == PurchaseOrderStatus.Cancelled),

                    // Calculate totals in memory
                    TotalBuyingCost = purchaseOrders.Sum(po => po.Items?
                        .Where(i => i.BuyingPrice.HasValue)
                        .Sum(i => i.BuyingPrice.Value * i.Quantity) ?? 0),

                    TotalSellingValue = purchaseOrders.Sum(po => po.Items?
                        .Where(i => i.UnitPrice.HasValue)
                        .Sum(i => i.UnitPrice.Value * i.Quantity) ?? 0),

                    TotalItemsOrdered = purchaseOrders.Sum(po => po.Items?.Count ?? 0),

                    BranchId = branchId
                };

                if (branchId.HasValue)
                {
                    var branch = await _context.Branches.FindAsync(branchId.Value);
                    stats.BranchName = branch?.Name;
                }

                // Calculate average profit margin
                var allItems = purchaseOrders.SelectMany(po => po.Items ?? new List<PurchaseOrderItem>())
                    .Where(i => i.ProfitMargin.HasValue)
                    .ToList();

                stats.AverageProfitMargin = allItems.Any()
                    ? allItems.Average(i => i.ProfitMargin.Value)
                    : 0;

                // Calculate monthly stats
                var now = DateTime.UtcNow;
                var firstDayThisMonth = new DateTime(now.Year, now.Month, 1);
                var firstDayLastMonth = firstDayThisMonth.AddMonths(-1);

                stats.OrdersThisMonth = purchaseOrders.Count(po => po.CreatedAt >= firstDayThisMonth);
                stats.OrdersLastMonth = purchaseOrders.Count(po =>
                    po.CreatedAt >= firstDayLastMonth && po.CreatedAt < firstDayThisMonth);

                if (stats.OrdersLastMonth > 0)
                {
                    stats.MonthlyGrowthPercentage = ((decimal)(stats.OrdersThisMonth - stats.OrdersLastMonth) / stats.OrdersLastMonth) * 100;
                }

                return ApiResponse<PurchaseOrderStatsDto>.Success(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving purchase order statistics");
                return ApiResponse<PurchaseOrderStatsDto>.Fail("Error retrieving purchase order statistics");
            }
        }
        public async Task<ApiResponse<PurchaseOrderDashboardDto>> GetPurchaseOrderDashboardAsync(Guid? branchId = null)
        {
            try
            {
                var query = _context.PurchaseOrders
                    .Include(po => po.Branch)
                    .Include(po => po.Creator)
                    .Include(po => po.Items)
                    .Where(po => po.IsActive)
                    .AsQueryable();

                if (branchId.HasValue)
                {
                    query = query.Where(po => po.BranchId == branchId.Value);
                }

                // Fetch all data first
                var purchaseOrders = await query.ToListAsync();

                var dashboard = new PurchaseOrderDashboardDto();

                // Summary cards
                dashboard.TotalPending = purchaseOrders.Count(po =>
                    po.Status == PurchaseOrderStatus.PendingFinanceVerification ||
                    po.Status == PurchaseOrderStatus.PendingManagerApproval);
                dashboard.TotalCompleted = purchaseOrders.Count(po => po.Status == PurchaseOrderStatus.Approved);
                dashboard.TotalRejected = purchaseOrders.Count(po => po.Status == PurchaseOrderStatus.Rejected);
                dashboard.TotalCancelled = purchaseOrders.Count(po => po.Status == PurchaseOrderStatus.Cancelled);

                // Status breakdown
                dashboard.OrdersByStatus = new Dictionary<string, int>();
                foreach (PurchaseOrderStatus status in Enum.GetValues(typeof(PurchaseOrderStatus)))
                {
                    var count = purchaseOrders.Count(po => po.Status == status);
                    dashboard.OrdersByStatus.Add(status.ToString(), count);
                }

                // Branch breakdown (only if no branch filter)
                if (!branchId.HasValue)
                {
                    dashboard.OrdersByBranch = new Dictionary<string, int>();
                    dashboard.ValueByBranch = new Dictionary<string, decimal>();

                    var branchGroups = purchaseOrders
                        .GroupBy(po => po.Branch?.Name ?? "Unknown")
                        .Select(g => new {
                            BranchName = g.Key,
                            Count = g.Count(),
                            Value = g.Sum(po => po.Items?
                                .Where(i => i.BuyingPrice.HasValue)
                                .Sum(i => i.BuyingPrice.Value * i.Quantity) ?? 0)
                        })
                        .ToList();

                    foreach (var item in branchGroups)
                    {
                        dashboard.OrdersByBranch.Add(item.BranchName, item.Count);
                        dashboard.ValueByBranch.Add(item.BranchName, item.Value);
                    }
                }

                // Financial summary
                dashboard.TotalPurchaseValue = purchaseOrders.Sum(po => po.Items?
                    .Where(i => i.BuyingPrice.HasValue)
                    .Sum(i => i.BuyingPrice.Value * i.Quantity) ?? 0);

                dashboard.TotalProfit = purchaseOrders.Sum(po => po.Items?
                    .Where(i => i.BuyingPrice.HasValue && i.UnitPrice.HasValue)
                    .Sum(i => (i.UnitPrice.Value - i.BuyingPrice.Value) * i.Quantity) ?? 0);

                var itemsWithMargin = purchaseOrders
                    .SelectMany(po => po.Items ?? new List<PurchaseOrderItem>())
                    .Where(i => i.ProfitMargin.HasValue)
                    .ToList();

                dashboard.AverageProfitMargin = itemsWithMargin.Any()
                    ? itemsWithMargin.Average(i => i.ProfitMargin.Value)
                    : 0;

                // Recent orders
                dashboard.RecentOrders = purchaseOrders
                    .OrderByDescending(po => po.CreatedAt)
                    .Take(10)
                    .Select(po => new RecentPurchaseOrderDto
                    {
                        Id = po.Id,
                        OrderNumber = po.Id.ToString().Substring(0, 8),
                        BranchName = po.Branch?.Name ?? "Unknown",
                        Status = po.Status.ToString(),
                        CreatedAt = po.CreatedAt,
                        ItemCount = po.Items?.Count ?? 0,
                        TotalValue = po.Items?
                            .Where(i => i.BuyingPrice.HasValue)
                            .Sum(i => i.BuyingPrice.Value * i.Quantity) ?? 0,
                        CreatedByName = po.Creator?.Name ?? "Unknown"
                    })
                    .ToList();

                // Recent activity
                var historyQuery = _context.PurchaseHistory
                    .Include(h => h.PurchaseOrder)
                    .Include(h => h.PerformedByUser)
                    .Where(h => !branchId.HasValue || h.PurchaseOrder.BranchId == branchId.Value)
                    .OrderByDescending(h => h.CreatedAt)
                    .Take(20);

                dashboard.RecentActivity = await historyQuery
                    .Select(h => new RecentPurchaseHistoryDto
                    {
                        PurchaseOrderId = h.PurchaseOrderId,
                        OrderNumber = h.PurchaseOrder.Id.ToString().Substring(0, 8),
                        Action = h.Action,
                        PerformedBy = h.PerformedByUser.Name,
                        CreatedAt = h.CreatedAt,
                        Details = h.Details
                    })
                    .ToListAsync();

                // Monthly trends
                var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
                var monthlyGroups = purchaseOrders
                    .Where(po => po.CreatedAt >= sixMonthsAgo)
                    .GroupBy(po => new { po.CreatedAt.Year, po.CreatedAt.Month })
                    .Select(g => new MonthlyTrendDto
                    {
                        Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                        OrderCount = g.Count(),
                        TotalValue = g.Sum(po => po.Items?
                            .Where(i => i.BuyingPrice.HasValue)
                            .Sum(i => i.BuyingPrice.Value * i.Quantity) ?? 0),
                        TotalProfit = g.Sum(po => po.Items?
                            .Where(i => i.BuyingPrice.HasValue && i.UnitPrice.HasValue)
                            .Sum(i => (i.UnitPrice.Value - i.BuyingPrice.Value) * i.Quantity) ?? 0)
                    })
                    .OrderBy(m => DateTime.ParseExact(m.Month, "MMM yyyy", System.Globalization.CultureInfo.InvariantCulture))
                    .ToList();

                dashboard.MonthlyTrends = monthlyGroups;

                // Top products
                var allItems = purchaseOrders
                    .SelectMany(po => po.Items ?? new List<PurchaseOrderItem>())
                    .Where(i => i.Product != null)
                    .GroupBy(i => new { i.ProductId, i.Product.Name })
                    .Select(g => new TopProductDto
                    {
                        ProductId = g.Key.ProductId,
                        ProductName = g.Key.Name,
                        QuantityOrdered = g.Sum(i => i.Quantity),
                        TotalValue = g.Sum(i => i.BuyingPrice.GetValueOrDefault() * i.Quantity),
                        TotalProfit = g.Sum(i => (i.UnitPrice.GetValueOrDefault() - i.BuyingPrice.GetValueOrDefault()) * i.Quantity)
                    })
                    .OrderByDescending(p => p.TotalValue)
                    .Take(10)
                    .ToList();

                dashboard.TopProducts = allItems;

                // Top suppliers
                var supplierGroups = purchaseOrders
                    .SelectMany(po => po.Items ?? new List<PurchaseOrderItem>())
                    .Where(i => !string.IsNullOrWhiteSpace(i.SupplierName))
                    .GroupBy(i => i.SupplierName)
                    .Select(g => new TopSupplierDto
                    {
                        SupplierName = g.Key,
                        OrderCount = g.Select(i => i.PurchaseOrderId).Distinct().Count(),
                        ItemCount = g.Sum(i => i.Quantity),
                        TotalValue = g.Sum(i => i.BuyingPrice.GetValueOrDefault() * i.Quantity)
                    })
                    .OrderByDescending(s => s.TotalValue)
                    .Take(10)
                    .ToList();

                dashboard.TopSuppliers = supplierGroups;

                return ApiResponse<PurchaseOrderDashboardDto>.Success(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving purchase order dashboard");
                return ApiResponse<PurchaseOrderDashboardDto>.Fail("Error retrieving purchase order dashboard");
            }
        }
        #endregion

        #region Helper/Convenience Methods

        public async Task<ApiResponse<RejectResponseDto>> RejectPurchaseOrderAsync(Guid purchaseOrderId, string reason)
        {
            var dto = new RejectPurchaseOrderDto
            {
                PurchaseOrderId = purchaseOrderId,
                Reason = reason
            };

            return await RejectPurchaseOrderAsync(dto);
        }

        public async Task<ApiResponse<bool>> CancelPurchaseOrderAsync(Guid purchaseOrderId, string? reason = null)
        {
            var dto = new CancelPurchaseOrderDto
            {
                PurchaseOrderId = purchaseOrderId,
                Reason = reason
            };

            return await CancelPurchaseOrderAsync(dto);
        }

        public async Task<ApiResponse<bool>> CanUserEditAsync(Guid purchaseOrderId, Guid userId)
        {
            try
            {
                var purchaseOrder = await _context.PurchaseOrders
                    .FirstOrDefaultAsync(po => po.Id == purchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    return ApiResponse<bool>.Success(false, "Purchase order not found");
                }

                // Admin can always edit (except approved)
                var isAdmin = true; // Check user role from claims
                if (isAdmin && purchaseOrder.Status != PurchaseOrderStatus.Approved)
                {
                    return ApiResponse<bool>.Success(true, "Admin can edit");
                }

                // Finance can edit if not approved
                var isFinance = true; // Check user role from claims
                if (isFinance && purchaseOrder.Status != PurchaseOrderStatus.Approved)
                {
                    return ApiResponse<bool>.Success(true, "Finance can edit");
                }

                // Sales can only edit their own orders in PendingFinanceVerification
                if (purchaseOrder.CreatedBy == userId &&
                    purchaseOrder.Status == PurchaseOrderStatus.PendingFinanceVerification)
                {
                    return ApiResponse<bool>.Success(true, "Sales can edit");
                }

                return ApiResponse<bool>.Success(false, "User cannot edit this purchase order");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking edit permission");
                return ApiResponse<bool>.Fail("Error checking edit permission");
            }
        }

        #endregion
    }
}