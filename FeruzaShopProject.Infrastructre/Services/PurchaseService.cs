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

namespace ShopMgtSys.Infrastructure.Services
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

        // ========== HELPER METHODS ==========
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
            await _context.SaveChangesAsync();
        }

        private async Task<decimal> GetDefaultMarkupPercentageAsync()
        {
            // You can store this in a configuration table or app settings
            return 30m; // 30% default markup
        }

        private async Task UpdateStockAndCreateMovementAsync(
            Guid productId,
            Guid branchId,
            Guid purchaseOrderId,
            int quantity,
            decimal buyingPrice,
            decimal sellingPrice,
            Guid userId)
        {
            try
            {
                var stock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == productId && s.BranchId == branchId);

                decimal previousQuantity = 0;

                if (stock == null)
                {
                    stock = new Stock
                    {
                        Id = Guid.NewGuid(),
                        ProductId = productId,
                        BranchId = branchId,
                        Quantity = quantity,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    _context.Stocks.Add(stock);
                }
                else
                {
                    previousQuantity = stock.Quantity;
                    stock.Quantity += quantity;
                    stock.UpdatedAt = DateTime.UtcNow;
                }

                var stockMovement = new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    BranchId = branchId,
                    PurchaseOrderId = purchaseOrderId,
                    MovementType = StockMovementType.Purchase,
                    Quantity = quantity,
                    PreviousQuantity = previousQuantity,
                    NewQuantity = previousQuantity + quantity,
                    Reason = $"Purchase Order #{purchaseOrderId} - Cost: ${buyingPrice}, Sell: ${sellingPrice}",
                    MovementDate = DateTime.UtcNow,
                    IsActive = true
                };

                _context.StockMovements.Add(stockMovement);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock and creating movement for Product {ProductId}", productId);
                throw;
            }
        }

        // ========== STEP 1: SALES CREATES PURCHASE ORDER ==========
        public async Task<ApiResponse<PurchaseOrderDto>> CreatePurchaseOrderAsync(CreatePurchaseOrderDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var branch = await _context.Branches.FindAsync(dto.BranchId);
                if (branch == null || !branch.IsActive)
                {
                    _logger.LogWarning("Invalid or inactive branch: {BranchId}", dto.BranchId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Invalid or inactive branch");
                }

                var createdBy = await GetCurrentUserIdAsync();

                var purchaseOrder = new PurchaseOrder
                {
                    Id = Guid.NewGuid(),
                    BranchId = dto.BranchId,
                    CreatedBy = createdBy,
                    Status = PurchaseOrderStatus.PendingFinanceVerification,
                    Items = new List<PurchaseOrderItem>(),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

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
                await _context.SaveChangesAsync();

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "Created", createdBy,
                    $"Purchase order created with {purchaseOrder.Items.Count} items. Status: PendingFinanceVerification");

                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Sales created purchase order {PurchaseOrderId} for branch {BranchId}",
                    purchaseOrder.Id, dto.BranchId);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Purchase order created successfully. Waiting for finance verification.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating purchase order: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error creating purchase order");
            }
        }

        // ========== STEP 2: FINANCE VERIFICATION ==========
        public async Task<ApiResponse<PurchaseOrderDto>> FinanceVerificationAsync(FinanceVerificationDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                        .ThenInclude(i => i.Product)
                    .Include(po => po.Branch)
                    .FirstOrDefaultAsync(po => po.Id == dto.PurchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                // Only PendingFinanceVerification status allowed
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingFinanceVerification)
                {
                    _logger.LogWarning("Purchase order not in PendingFinanceVerification status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order must be in PendingFinanceVerification status for finance verification");
                }

                bool allItemsVerified = true;
                bool anyItemsRejected = false;
                var verifiedItems = new List<Guid>();

                foreach (var itemDto in dto.Items)
                {
                    var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.ItemId);
                    if (item == null)
                    {
                        _logger.LogWarning("Purchase order item not found: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order item not found: {itemDto.ItemId}");
                    }

                    if (item.IsApproved)
                    {
                        _logger.LogWarning("Cannot modify approved item: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Item is already approved and cannot be modified");
                    }

                    if (itemDto.IsVerified)
                    {
                        // Validate buying price
                        if (itemDto.BuyingPrice <= 0)
                        {
                            _logger.LogWarning("Buying price must be greater than zero: {Price}", itemDto.BuyingPrice);
                            return ApiResponse<PurchaseOrderDto>.Fail($"Buying price must be greater than zero for item: {itemDto.ItemId}");
                        }

                        item.BuyingPrice = itemDto.BuyingPrice;

                        // Handle selling price
                        if (itemDto.SellingPrice.HasValue)
                        {
                            if (itemDto.SellingPrice.Value <= 0)
                            {
                                _logger.LogWarning("Selling price must be greater than zero: {Price}", itemDto.SellingPrice.Value);
                                return ApiResponse<PurchaseOrderDto>.Fail($"Selling price must be greater than zero for item: {itemDto.ItemId}");
                            }

                            if (itemDto.SellingPrice.Value <= itemDto.BuyingPrice)
                            {
                                _logger.LogWarning("Selling price must be higher than buying price: {Selling} <= {Buying}",
                                    itemDto.SellingPrice.Value, itemDto.BuyingPrice);
                                return ApiResponse<PurchaseOrderDto>.Fail($"Selling price must be higher than buying price for item: {itemDto.ItemId}");
                            }

                            item.UnitPrice = itemDto.SellingPrice.Value;
                        }
                        else
                        {
                            // Auto-calculate selling price with markup
                            var defaultMarkupPercentage = await GetDefaultMarkupPercentageAsync();
                            item.UnitPrice = itemDto.BuyingPrice * (1 + defaultMarkupPercentage / 100);
                        }

                        // Set supplier name
                        if (!string.IsNullOrWhiteSpace(itemDto.SupplierName))
                        {
                            item.SupplierName = itemDto.SupplierName;
                        }

                        item.FinanceVerified = true;
                        item.FinanceVerifiedAt = DateTime.UtcNow;
                        item.FinanceVerifiedBy = userId;
                        item.PriceSetAt = DateTime.UtcNow;
                        item.PriceSetBy = userId;
                        item.PriceEditCount = 0;
                        item.UpdatedAt = DateTime.UtcNow;

                        verifiedItems.Add(item.Id);
                    }
                    else
                    {
                        // Item is rejected
                        item.FinanceVerified = false;
                        item.UpdatedAt = DateTime.UtcNow;
                        anyItemsRejected = true;
                        allItemsVerified = false;
                    }
                }

                // Update purchase order status
                if (allItemsVerified)
                {
                    // All items verified - move to PendingManagerApproval
                    purchaseOrder.Status = PurchaseOrderStatus.PendingManagerApproval;
                }
                else if (anyItemsRejected && verifiedItems.Any())
                {
                    // Some items verified, some rejected - stay in PendingFinanceVerification
                    purchaseOrder.Status = PurchaseOrderStatus.PendingFinanceVerification;
                }
                else if (!verifiedItems.Any())
                {
                    // All items rejected
                    purchaseOrder.Status = PurchaseOrderStatus.Rejected;
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var verifiedCount = verifiedItems.Count;
                var rejectedCount = dto.Items.Count - verifiedCount;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "FinanceVerification", userId,
                    $"Finance verification completed. Verified: {verifiedCount}, Rejected: {rejectedCount}. Status: {purchaseOrder.Status}");

                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Finance completed verification for purchase order {PurchaseOrderId}", purchaseOrder.Id);

                var message = verifiedCount > 0
                    ? $"Finance verification completed. {verifiedCount} items verified, {rejectedCount} rejected."
                    : "All items were rejected by finance.";

                return ApiResponse<PurchaseOrderDto>.Success(result, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in finance verification: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error in finance verification");
            }
        }

        // ========== STEP 3: MANAGER APPROVAL ==========
        public async Task<ApiResponse<PurchaseOrderDto>> ManagerApprovalAsync(ManagerApprovalDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                        .ThenInclude(i => i.Product)
                    .Include(po => po.Branch)
                    .FirstOrDefaultAsync(po => po.Id == dto.PurchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                // Only PendingManagerApproval status allowed
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingManagerApproval)
                {
                    _logger.LogWarning("Purchase order not in PendingManagerApproval status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order must be in PendingManagerApproval status for manager approval");
                }

                // Determine which items to approve
                var itemsToApprove = dto.ItemIds != null && dto.ItemIds.Any()
                    ? purchaseOrder.Items.Where(i => dto.ItemIds.Contains(i.Id) && i.FinanceVerified == true && !i.IsApproved).ToList()
                    : purchaseOrder.Items.Where(i => i.FinanceVerified == true && !i.IsApproved).ToList();

                if (!itemsToApprove.Any())
                {
                    _logger.LogWarning("No items to approve for purchase order {PurchaseOrderId}", dto.PurchaseOrderId);

                    // Check if all items are already approved
                    if (purchaseOrder.Items.All(i => i.IsApproved || i.FinanceVerified == false))
                    {
                        // If all verified items are approved, mark as approved
                        if (purchaseOrder.Items.Any(i => i.FinanceVerified == true))
                        {
                            purchaseOrder.Status = PurchaseOrderStatus.Approved;
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();

                            var orderResult = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                            return ApiResponse<PurchaseOrderDto>.Success(orderResult, "All items are already approved");
                        }
                    }

                    return ApiResponse<PurchaseOrderDto>.Fail("No verified items found to approve");
                }

                // Check if any items are not fully priced
                foreach (var item in itemsToApprove)
                {
                    if (!item.BuyingPrice.HasValue || !item.UnitPrice.HasValue)
                    {
                        _logger.LogWarning("Item {ItemId} missing price information", item.Id);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Item {item.Product?.Name ?? item.Id.ToString()} is missing price information");
                    }
                }

                // Update stock and prices for approved items
                foreach (var item in itemsToApprove)
                {
                    // Update product prices
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.BuyingPrice = item.BuyingPrice.Value;
                        product.UnitPrice = item.UnitPrice.Value;
                        product.UpdatedAt = DateTime.UtcNow;
                        _context.Products.Update(product);
                    }

                    // Update stock and create stock movement
                    await UpdateStockAndCreateMovementAsync(
                        item.ProductId,
                        purchaseOrder.BranchId,
                        purchaseOrder.Id,
                        item.Quantity,
                        item.BuyingPrice.Value,
                        item.UnitPrice.Value,
                        userId);

                    item.ApprovedAt = DateTime.UtcNow;
                    item.ApprovedBy = userId;
                    item.UpdatedAt = DateTime.UtcNow;
                }

                // Check if all finance-verified items are now approved
                var allVerifiedItemsApproved = purchaseOrder.Items
                    .Where(i => i.FinanceVerified == true)
                    .All(i => i.IsApproved);

                // Update purchase order status
                purchaseOrder.Status = allVerifiedItemsApproved
                    ? PurchaseOrderStatus.Approved
                    : PurchaseOrderStatus.PendingManagerApproval;

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                var approvedCount = itemsToApprove.Count;
                var totalApproved = purchaseOrder.Items.Count(i => i.IsApproved);
                var remainingToApprove = purchaseOrder.Items.Count(i => i.FinanceVerified == true && !i.IsApproved);
                var rejectedItems = purchaseOrder.Items.Count(i => i.FinanceVerified == false);

                var totalValue = purchaseOrder.Items
                    .Where(i => i.IsApproved && i.BuyingPrice.HasValue)
                    .Sum(i => i.BuyingPrice.Value * i.Quantity);

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "ManagerApproval", userId,
                    $"Manager approved {approvedCount} items. Total approved: {totalApproved}. Total value: ${totalValue}");

                await transaction.CommitAsync();

                var finalResult = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Manager approved {ApprovedCount} items for purchase order {PurchaseOrderId}",
                    approvedCount, purchaseOrder.Id);

                // Determine the correct message
                string statusMessage;
                if (purchaseOrder.Status == PurchaseOrderStatus.Approved)
                {
                    statusMessage = $"Purchase order fully approved. All {totalApproved} items added to inventory with updated prices.";
                }
                else
                {
                    statusMessage = $"{approvedCount} items approved. " +
                                   $"Total approved: {totalApproved}, " +
                                   $"Remaining to approve: {remainingToApprove}, " +
                                   $"Rejected items: {rejectedItems}.";
                }

                return ApiResponse<PurchaseOrderDto>.Success(finalResult, statusMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in manager approval: {PurchaseOrderId}", dto.PurchaseOrderId);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error in manager approval");
            }
        }

        // ========== SALES EDIT OPERATIONS ==========
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

                // Sales can only edit in PendingFinanceVerification status
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
                    // Get IDs of items that should remain
                    var requestedItemIds = dto.Items
                        .Where(i => i.ItemId.HasValue)
                        .Select(i => i.ItemId.Value)
                        .ToHashSet();

                    // Find items to remove (existing items not in the request)
                    var itemsToRemove = purchaseOrder.Items
                        .Where(i => i.IsActive && !requestedItemIds.Contains(i.Id))
                        .ToList();

                    // Remove items
                    if (itemsToRemove.Any())
                    {
                        _context.PurchaseOrderItems.RemoveRange(itemsToRemove);
                        _logger.LogInformation("Removing {Count} items from purchase order", itemsToRemove.Count);
                    }

                    // Process each item from the DTO
                    foreach (var itemDto in dto.Items)
                    {
                        var product = await _context.Products.FindAsync(itemDto.ProductId);
                        if (product == null || !product.IsActive)
                        {
                            _logger.LogWarning("Invalid or inactive product: {ProductId}", itemDto.ProductId);
                            return ApiResponse<PurchaseOrderDto>.Fail($"Invalid or inactive product: {itemDto.ProductId}");
                        }

                        if (itemDto.ItemId.HasValue)
                        {
                            // Update existing item
                            var existingItem = await _context.PurchaseOrderItems
                                .FirstOrDefaultAsync(i => i.Id == itemDto.ItemId.Value && i.PurchaseOrderId == purchaseOrder.Id);

                            if (existingItem != null)
                            {
                                existingItem.ProductId = itemDto.ProductId;
                                existingItem.Quantity = itemDto.Quantity;
                                existingItem.UpdatedAt = DateTime.UtcNow;
                                _context.PurchaseOrderItems.Update(existingItem);
                            }
                        }
                        else
                        {
                            // Create new item
                            var newItem = new PurchaseOrderItem
                            {
                                Id = Guid.NewGuid(),
                                PurchaseOrderId = purchaseOrder.Id,
                                ProductId = itemDto.ProductId,
                                Quantity = itemDto.Quantity,
                                CreatedAt = DateTime.UtcNow,
                                IsActive = true
                            };
                            await _context.PurchaseOrderItems.AddAsync(newItem);
                        }
                    }
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;
                _context.PurchaseOrders.Update(purchaseOrder);

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "EditedBySales", userId,
                    $"Sales edited purchase order. Items: {purchaseOrder.Items.Count(i => i.IsActive)}");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Refresh the purchase order to get latest state
                var updatedOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .Include(po => po.Branch)
                    .Include(po => po.Creator)
                    .FirstOrDefaultAsync(po => po.Id == purchaseOrder.Id);

                var result = _mapper.Map<PurchaseOrderDto>(updatedOrder);
                _logger.LogInformation("Sales edited purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Purchase order updated successfully");
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Concurrency error editing purchase order by sales: {@Dto}", dto);
                return ApiResponse<PurchaseOrderDto>.Fail("The purchase order was modified by another user. Please refresh and try again.");
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

                // Sales can only delete in PendingFinanceVerification status
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingFinanceVerification)
                {
                    _logger.LogWarning("Sales cannot delete purchase order in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<bool>.Fail("Purchase order can only be deleted when in PendingFinanceVerification status");
                }

                // Verify that the current user is the creator
                if (purchaseOrder.CreatedBy != userId)
                {
                    _logger.LogWarning("User {UserId} is not the creator of purchase order {PurchaseOrderId}", userId, purchaseOrderId);
                    return ApiResponse<bool>.Fail("You can only delete purchase orders that you created");
                }

                // Soft delete
                purchaseOrder.Deactivate();
                purchaseOrder.Status = PurchaseOrderStatus.Cancelled;

                foreach (var item in purchaseOrder.Items)
                {
                    item.Deactivate();
                }

                await _context.SaveChangesAsync();

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "DeletedBySales", userId,
                    $"Sales deleted purchase order. Reason: {reason ?? "Not specified"}");

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
                    .Include(po => po.History)
                    .FirstOrDefaultAsync(po => po.Id == purchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", purchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                // Sales can only delete items in PendingFinanceVerification status
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingFinanceVerification)
                {
                    _logger.LogWarning("Sales cannot delete items from purchase order in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Items can only be deleted when order is in PendingFinanceVerification status");
                }

                // Verify that the current user is the creator
                if (purchaseOrder.CreatedBy != userId)
                {
                    _logger.LogWarning("User {UserId} is not the creator of purchase order {PurchaseOrderId}", userId, purchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("You can only delete items from purchase orders that you created");
                }

                // Find the item
                var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemId);
                if (item == null)
                {
                    _logger.LogWarning("Item not found: {ItemId}", itemId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Item not found");
                }

                // Delete any purchase history records that reference this item
                var itemHistory = await _context.PurchaseHistory
                    .Where(h => h.PurchaseOrderItemId == itemId)
                    .ToListAsync();

                if (itemHistory.Any())
                {
                    _context.PurchaseHistory.RemoveRange(itemHistory);
                }

                // Delete the item itself
                _context.PurchaseOrderItems.Remove(item);
                purchaseOrder.Items.Remove(item);

                // If no items left, delete the entire order and its history
                if (!purchaseOrder.Items.Any())
                {
                    var orderHistory = await _context.PurchaseHistory
                        .Where(h => h.PurchaseOrderId == purchaseOrderId)
                        .ToListAsync();

                    if (orderHistory.Any())
                    {
                        _context.PurchaseHistory.RemoveRange(orderHistory);
                    }

                    _context.PurchaseOrders.Remove(purchaseOrder);
                }
                else
                {
                    purchaseOrder.UpdatedAt = DateTime.UtcNow;

                    var history = new PurchaseHistory
                    {
                        Id = Guid.NewGuid(),
                        PurchaseOrderId = purchaseOrder.Id,
                        Action = "ItemDeleted",
                        PerformedByUserId = userId,
                        Details = $"Item {item.ProductId} permanently deleted from purchase order",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    _context.PurchaseHistory.Add(history);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (!purchaseOrder.Items.Any())
                {
                    _logger.LogInformation("Purchase order {PurchaseOrderId} deleted as all items were removed", purchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Success(null, "Purchase order deleted as all items were removed");
                }

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Sales permanently deleted item {ItemId} from purchase order {PurchaseOrderId}", itemId, purchaseOrderId);

                return ApiResponse<PurchaseOrderDto>.Success(result, "Item permanently deleted from purchase order");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting item from purchase order: {PurchaseOrderId}, Item: {ItemId}", purchaseOrderId, itemId);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error deleting item from purchase order");
            }
        }

        // ========== FINANCE EDIT OPERATIONS ==========
        public async Task<ApiResponse<PurchaseOrderDto>> EditPricesByFinanceAsync(EditPricesByFinanceDto dto)
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

                // Finance can only edit prices in PendingFinanceVerification status
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingFinanceVerification)
                {
                    _logger.LogWarning("Finance cannot edit prices in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Prices can only be edited when order is in PendingFinanceVerification status");
                }

                foreach (var itemDto in dto.Items)
                {
                    var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.ItemId);
                    if (item == null)
                    {
                        _logger.LogWarning("Purchase order item not found: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order item not found: {itemDto.ItemId}");
                    }

                    if (item.IsApproved)
                    {
                        _logger.LogWarning("Cannot edit prices for approved item: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Cannot edit prices for approved item: {itemDto.ItemId}");
                    }

                    if (itemDto.BuyingPrice <= 0)
                    {
                        _logger.LogWarning("Buying price must be greater than zero: {Price}", itemDto.BuyingPrice);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Buying price must be greater than zero for item: {itemDto.ItemId}");
                    }

                    if (itemDto.SellingPrice.HasValue && itemDto.SellingPrice.Value <= itemDto.BuyingPrice)
                    {
                        _logger.LogWarning("Selling price must be higher than buying price: {Selling} <= {Buying}",
                            itemDto.SellingPrice.Value, itemDto.BuyingPrice);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Selling price must be higher than buying price for item: {itemDto.ItemId}");
                    }

                    // Update prices with edit tracking
                    item.BuyingPrice = itemDto.BuyingPrice;

                    if (itemDto.SellingPrice.HasValue)
                    {
                        item.UnitPrice = itemDto.SellingPrice.Value;
                    }
                    else
                    {
                        var defaultMarkupPercentage = await GetDefaultMarkupPercentageAsync();
                        item.UnitPrice = itemDto.BuyingPrice * (1 + defaultMarkupPercentage / 100);
                    }

                    item.PriceSetAt = DateTime.UtcNow;
                    item.PriceSetBy = userId;
                    item.PriceEditCount += 1;
                    item.UpdatedAt = DateTime.UtcNow;
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "PricesEditedByFinance", userId,
                    $"Finance edited prices for {dto.Items.Count} items");

                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Finance edited prices for purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Prices updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing prices by finance: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error editing prices");
            }
        }

        // ========== REJECT OPERATIONS ==========
        public async Task<ApiResponse<RejectResponseDto>> RejectPurchaseOrderAsync(RejectPurchaseOrderDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Branch)
                    .Include(po => po.Items)
                    .FirstOrDefaultAsync(po => po.Id == dto.PurchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<RejectResponseDto>.Fail("Purchase order not found or inactive");
                }

                // Cannot reject approved orders
                if (purchaseOrder.Status == PurchaseOrderStatus.Approved)
                {
                    _logger.LogWarning("Cannot reject approved purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<RejectResponseDto>.Fail("Cannot reject an approved purchase order");
                }

                int rejectedCount = 0;

                // If specific items are provided, reject only those items
                if (dto.ItemIds != null && dto.ItemIds.Any())
                {
                    foreach (var itemId in dto.ItemIds)
                    {
                        var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemId);
                        if (item != null && !item.IsApproved)
                        {
                            item.FinanceVerified = false;
                            item.UpdatedAt = DateTime.UtcNow;
                            rejectedCount++;

                            await AddPurchaseHistoryAsync(
                                purchaseOrder.Id,
                                "ItemRejected",
                                userId,
                                $"Item rejected: {dto.Reason}",
                                item.Id
                            );
                        }
                    }

                    // Update status based on remaining items
                    var hasVerifiedItems = purchaseOrder.Items.Any(i => i.FinanceVerified == true);
                    var hasUnverifiedItems = purchaseOrder.Items.Any(i => i.FinanceVerified == null);

                    if (!hasVerifiedItems && !hasUnverifiedItems)
                    {
                        purchaseOrder.Status = PurchaseOrderStatus.Rejected;
                    }
                    else if (!hasVerifiedItems && hasUnverifiedItems)
                    {
                        purchaseOrder.Status = PurchaseOrderStatus.PendingFinanceVerification;
                    }
                }
                else
                {
                    // Reject entire order
                    foreach (var item in purchaseOrder.Items.Where(i => !i.IsApproved))
                    {
                        item.FinanceVerified = false;
                        item.UpdatedAt = DateTime.UtcNow;
                    }
                    purchaseOrder.Status = PurchaseOrderStatus.Rejected;
                    rejectedCount = purchaseOrder.Items.Count;
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Add main rejection history if entire order rejected
                if (dto.ItemIds == null || !dto.ItemIds.Any())
                {
                    await AddPurchaseHistoryAsync(purchaseOrder.Id, "Rejected", userId,
                        $"Purchase order rejected: {dto.Reason}");
                }

                await transaction.CommitAsync();

                var result = new RejectResponseDto
                {
                    PurchaseOrderId = purchaseOrder.Id,
                    NewStatus = purchaseOrder.Status,
                    RejectedItems = rejectedCount,
                    Message = dto.ItemIds != null && dto.ItemIds.Any()
                        ? $"{rejectedCount} items rejected successfully"
                        : "Purchase order rejected successfully"
                };

                _logger.LogInformation("Purchase order {PurchaseOrderId} rejected. Items rejected: {RejectedCount}",
                    purchaseOrder.Id, rejectedCount);
                return ApiResponse<RejectResponseDto>.Success(result, result.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting purchase order: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<RejectResponseDto>.Fail("Error rejecting purchase order");
            }
        }

        // ========== CANCEL OPERATIONS ==========
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

                // Can cancel only if not approved
                if (purchaseOrder.Status == PurchaseOrderStatus.Approved)
                {
                    _logger.LogWarning("Cannot cancel approved purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<bool>.Fail("Cannot cancel an approved purchase order");
                }

                purchaseOrder.Status = PurchaseOrderStatus.Cancelled;
                purchaseOrder.Deactivate();

                foreach (var item in purchaseOrder.Items)
                {
                    item.Deactivate();
                }

                await _context.SaveChangesAsync();

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "Cancelled", userId,
                    $"Purchase order cancelled. Reason: {dto.Reason ?? "Not specified"}");

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

        // ========== QUERY METHODS ==========
        public async Task<ApiResponse<PurchaseOrderDto>> GetPurchaseOrderByIdAsync(Guid id)
        {
            try
            {
                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Branch)
                    .Include(po => po.Creator)
                    .Include(po => po.Items).ThenInclude(pi => pi.Product)
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
                    .AsQueryable();

                if (branchId.HasValue)
                {
                    query = query.Where(po => po.BranchId == branchId.Value);
                }

                var now = DateTime.UtcNow;
                var firstDayOfMonth = new DateTime(now.Year, now.Month, 1);
                var firstDayOfLastMonth = firstDayOfMonth.AddMonths(-1);

                var totalBuyingCost = await query
                    .SelectMany(po => po.Items)
                    .Where(i => i.BuyingPrice.HasValue)
                    .SumAsync(i => i.BuyingPrice.Value * i.Quantity);

                var totalSellingValue = await query
                    .SelectMany(po => po.Items)
                    .Where(i => i.UnitPrice.HasValue)
                    .SumAsync(i => i.UnitPrice.Value * i.Quantity);

                var stats = new PurchaseOrderStatsDto
                {
                    TotalPurchaseOrders = await query.CountAsync(),

                    PendingFinanceVerification = await query.CountAsync(po =>
                        po.Status == PurchaseOrderStatus.PendingFinanceVerification),
                    PendingManagerApproval = await query.CountAsync(po =>
                        po.Status == PurchaseOrderStatus.PendingManagerApproval),
                    Approved = await query.CountAsync(po =>
                        po.Status == PurchaseOrderStatus.Approved),
                    Rejected = await query.CountAsync(po =>
                        po.Status == PurchaseOrderStatus.Rejected),
                    Cancelled = await query.CountAsync(po =>
                        po.Status == PurchaseOrderStatus.Cancelled),

                    TotalBuyingCost = totalBuyingCost,
                    TotalSellingValue = totalSellingValue,

                    TotalItemsOrdered = await query
                        .SelectMany(po => po.Items)
                        .SumAsync(i => i.Quantity),

                    TotalItemsVerified = await query
                        .SelectMany(po => po.Items)
                        .CountAsync(i => i.FinanceVerified == true),

                    TotalItemsApproved = await query
                        .SelectMany(po => po.Items)
                        .CountAsync(i => i.ApprovedAt.HasValue),

                    OrdersThisMonth = await query
                        .CountAsync(po => po.CreatedAt >= firstDayOfMonth),

                    OrdersLastMonth = await query
                        .CountAsync(po =>
                            po.CreatedAt >= firstDayOfLastMonth &&
                            po.CreatedAt < firstDayOfMonth)
                };

                // Calculate average profit margin
                var itemsWithMargin = await query
                    .SelectMany(po => po.Items)
                    .Where(i => i.ProfitMargin.HasValue)
                    .ToListAsync();

                stats.AverageProfitMargin = itemsWithMargin.Any()
                    ? itemsWithMargin.Average(i => i.ProfitMargin.Value)
                    : 0;

                // Calculate monthly growth
                if (stats.OrdersLastMonth > 0)
                {
                    stats.MonthlyGrowthPercentage = ((decimal)stats.OrdersThisMonth - stats.OrdersLastMonth)
                        / stats.OrdersLastMonth * 100;
                }

                if (branchId.HasValue)
                {
                    stats.BranchId = branchId;
                    var branch = await _context.Branches.FindAsync(branchId);
                    stats.BranchName = branch?.Name;
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
                    .AsQueryable();

                if (branchId.HasValue)
                {
                    query = query.Where(po => po.BranchId == branchId.Value);
                }

                var dashboard = new PurchaseOrderDashboardDto
                {
                    TotalPending = await query.CountAsync(po =>
                        po.Status == PurchaseOrderStatus.PendingFinanceVerification ||
                        po.Status == PurchaseOrderStatus.PendingManagerApproval),

                    TotalCompleted = await query.CountAsync(po => po.Status == PurchaseOrderStatus.Approved),
                    TotalRejected = await query.CountAsync(po => po.Status == PurchaseOrderStatus.Rejected),
                    TotalCancelled = await query.CountAsync(po => po.Status == PurchaseOrderStatus.Cancelled),

                    OrdersByStatus = new Dictionary<string, int>(),
                    OrdersByBranch = new Dictionary<string, int>(),
                    ValueByBranch = new Dictionary<string, decimal>(),
                    RecentOrders = new List<RecentPurchaseOrderDto>(),
                    RecentActivity = new List<RecentPurchaseHistoryDto>(),
                    MonthlyTrends = new List<MonthlyTrendDto>(),
                    TopProducts = new List<TopProductDto>(),
                    TopSuppliers = new List<TopSupplierDto>()
                };

                // Status breakdown
                foreach (PurchaseOrderStatus status in Enum.GetValues(typeof(PurchaseOrderStatus)))
                {
                    var count = await query.CountAsync(po => po.Status == status);
                    dashboard.OrdersByStatus.Add(status.ToString(), count);
                }

                // Branch breakdown (if no branch filter)
                if (!branchId.HasValue)
                {
                    var branchData = await query
                        .GroupBy(po => po.Branch.Name)
                        .Select(g => new {
                            BranchName = g.Key,
                            Count = g.Count(),
                            Value = g.Sum(po => po.Items.Sum(i => i.BuyingPrice.GetValueOrDefault() * i.Quantity))
                        })
                        .ToListAsync();

                    foreach (var item in branchData)
                    {
                        dashboard.OrdersByBranch.Add(item.BranchName, item.Count);
                        dashboard.ValueByBranch.Add(item.BranchName, item.Value);
                    }
                }

                // Recent orders
                dashboard.RecentOrders = await query
                    .OrderByDescending(po => po.CreatedAt)
                    .Take(10)
                    .Select(po => new RecentPurchaseOrderDto
                    {
                        Id = po.Id,
                        OrderNumber = po.Id.ToString().Substring(0, 8),
                        BranchName = po.Branch.Name,
                        Status = po.Status.ToString(),
                        CreatedAt = po.CreatedAt,
                        ItemCount = po.Items.Count,
                        TotalValue = po.Items.Sum(i => i.BuyingPrice.GetValueOrDefault() * i.Quantity),
                        CreatedByName = po.Creator.Name
                    })
                    .ToListAsync();

                // Recent activity
                dashboard.RecentActivity = await _context.PurchaseHistory
                    .Include(ph => ph.PurchaseOrder)
                    .Include(ph => ph.PerformedByUser)
                    .Where(ph => !branchId.HasValue || ph.PurchaseOrder.BranchId == branchId.Value)
                    .OrderByDescending(ph => ph.CreatedAt)
                    .Take(20)
                    .Select(ph => new RecentPurchaseHistoryDto
                    {
                        PurchaseOrderId = ph.PurchaseOrderId,
                        OrderNumber = ph.PurchaseOrder.Id.ToString().Substring(0, 8),
                        Action = ph.Action,
                        PerformedBy = ph.PerformedByUser.Name,
                        CreatedAt = ph.CreatedAt,
                        Details = ph.Details
                    })
                    .ToListAsync();

                // Monthly trends (last 6 months)
                var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
                var monthlyData = await query
                    .Where(po => po.CreatedAt >= sixMonthsAgo)
                    .GroupBy(po => new { po.CreatedAt.Year, po.CreatedAt.Month })
                    .Select(g => new MonthlyTrendDto
                    {
                        Month = $"{new DateTime(g.Key.Year, g.Key.Month, 1):MMM yyyy}",
                        OrderCount = g.Count(),
                        TotalValue = g.SelectMany(po => po.Items)
                                     .Where(i => i.BuyingPrice.HasValue)
                                     .Sum(i => i.BuyingPrice.Value * i.Quantity),
                        TotalProfit = g.SelectMany(po => po.Items)
                                      .Where(i => i.UnitPrice.HasValue && i.BuyingPrice.HasValue)
                                      .Sum(i => (i.UnitPrice.Value - i.BuyingPrice.Value) * i.Quantity)
                    })
                    .OrderBy(m => m.Month)
                    .ToListAsync();

                dashboard.MonthlyTrends = monthlyData;

                // Top products
                dashboard.TopProducts = await query
                    .SelectMany(po => po.Items)
                    .GroupBy(i => new { i.ProductId, i.Product.Name })
                    .Select(g => new TopProductDto
                    {
                        ProductId = g.Key.ProductId,
                        ProductName = g.Key.Name,
                        QuantityOrdered = g.Sum(i => i.Quantity),
                        QuantityApproved = g.Count(i => i.ApprovedAt.HasValue),
                        TotalValue = g.Where(i => i.BuyingPrice.HasValue)
                                     .Sum(i => i.BuyingPrice.Value * i.Quantity),
                        TotalProfit = g.Where(i => i.UnitPrice.HasValue && i.BuyingPrice.HasValue)
                                      .Sum(i => (i.UnitPrice.Value - i.BuyingPrice.Value) * i.Quantity)
                    })
                    .OrderByDescending(p => p.TotalValue)
                    .Take(10)
                    .ToListAsync();

                // Top suppliers
                dashboard.TopSuppliers = await query
                    .SelectMany(po => po.Items)
                    .Where(i => i.SupplierName != null)
                    .GroupBy(i => i.SupplierName)
                    .Select(g => new TopSupplierDto
                    {
                        SupplierName = g.Key,
                        OrderCount = g.Select(i => i.PurchaseOrderId).Distinct().Count(),
                        ItemCount = g.Sum(i => i.Quantity),
                        TotalValue = g.Where(i => i.BuyingPrice.HasValue)
                                     .Sum(i => i.BuyingPrice.Value * i.Quantity)
                    })
                    .OrderByDescending(s => s.TotalValue)
                    .Take(10)
                    .ToListAsync();

                // Financial summary
                dashboard.TotalPurchaseValue = await query
                    .SelectMany(po => po.Items)
                    .Where(i => i.BuyingPrice.HasValue)
                    .SumAsync(i => i.BuyingPrice.Value * i.Quantity);

                dashboard.TotalProfit = await query
                    .SelectMany(po => po.Items)
                    .Where(i => i.UnitPrice.HasValue && i.BuyingPrice.HasValue)
                    .SumAsync(i => (i.UnitPrice.Value - i.BuyingPrice.Value) * i.Quantity);

                var itemsWithMargin = await query
                    .SelectMany(po => po.Items)
                    .Where(i => i.ProfitMargin.HasValue)
                    .ToListAsync();

                dashboard.AverageProfitMargin = itemsWithMargin.Any()
                    ? itemsWithMargin.Average(i => i.ProfitMargin.Value)
                    : 0;

                return ApiResponse<PurchaseOrderDashboardDto>.Success(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving purchase order dashboard");
                return ApiResponse<PurchaseOrderDashboardDto>.Fail("Error retrieving purchase order dashboard");
            }
        }

        // ========== HELPER/CONVENIENCE METHODS ==========
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

                // Check if user is finance (based on role claim - implement as needed)
                var isFinance = _httpContextAccessor.HttpContext?.User.IsInRole("Finance") ?? false;

                // Check if user is manager (based on role claim - implement as needed)
                var isManager = _httpContextAccessor.HttpContext?.User.IsInRole("Manager") ?? false;

                // Finance can edit in PendingFinanceVerification status
                if (isFinance && purchaseOrder.Status == PurchaseOrderStatus.PendingFinanceVerification)
                {
                    return ApiResponse<bool>.Success(true, "Finance can edit");
                }

                // Manager can edit in PendingManagerApproval status
                if (isManager && purchaseOrder.Status == PurchaseOrderStatus.PendingManagerApproval)
                {
                    return ApiResponse<bool>.Success(true, "Manager can edit");
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
    }
}