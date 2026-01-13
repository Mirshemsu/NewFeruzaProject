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

                var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
                if (!Guid.TryParse(userIdClaim, out var createdBy))
                {
                    _logger.LogWarning("Invalid user ID in JWT: {UserIdClaim}", userIdClaim);
                    return ApiResponse<PurchaseOrderDto>.Fail("Invalid user ID in JWT");
                }

                var purchaseOrder = new PurchaseOrder
                {
                    Id = Guid.NewGuid(),
                    BranchId = dto.BranchId,
                    CreatedBy = createdBy,
                    Status = PurchaseOrderStatus.PendingAdminAcceptance,
                    Items = new List<PurchaseOrderItem>()
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
                        QuantityOrdered = itemDto.QuantityOrdered,
                        QuantityReceived = null,
                        QuantityApproved = null,
                        UnitPrice = itemDto.UnitPrice,
                    };
                    purchaseOrder.Items.Add(poItem);
                }

                _context.PurchaseOrders.Add(purchaseOrder);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Sales created purchase order {PurchaseOrderId} for branch {BranchId}",
                    purchaseOrder.Id, dto.BranchId);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Purchase order created successfully. Waiting for admin acceptance.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating purchase order: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error creating purchase order");
            }
        }

        public async Task<ApiResponse<PurchaseOrderDto>> AcceptByAdminAsync(Guid purchaseOrderId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    _logger.LogWarning("Invalid user ID in JWT: {UserIdClaim}", userIdClaim);
                    return ApiResponse<PurchaseOrderDto>.Fail("Invalid user ID in JWT");
                }

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Branch)
                    .Include(po => po.Items)
                    .FirstOrDefaultAsync(po => po.Id == purchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", purchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                if (purchaseOrder.Status != PurchaseOrderStatus.PendingAdminAcceptance)
                {
                    _logger.LogWarning("Purchase order not in PendingAdminAcceptance status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order must be in PendingAdminAcceptance status to be accepted by admin");
                }

                // Admin accepts the sales request
                purchaseOrder.Status = PurchaseOrderStatus.PendingReceiving;
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Admin accepted purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Purchase order accepted by admin. Ready to receive goods.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting purchase order by admin: {PurchaseOrderId}", purchaseOrderId);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error accepting purchase order by admin");
            }
        }

        public async Task<ApiResponse<PurchaseOrderDto>> ReceivePurchaseOrderAsync(ReceivePurchaseOrderDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    _logger.LogWarning("Invalid user ID in JWT: {UserIdClaim}", userIdClaim);
                    return ApiResponse<PurchaseOrderDto>.Fail("Invalid user ID in JWT");
                }

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .Include(po => po.Branch)
                    .FirstOrDefaultAsync(po => po.Id == dto.Id && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", dto.Id);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                // Can receive if in PendingReceiving, PartiallyReceived, or CompletelyReceived
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingReceiving &&
                    purchaseOrder.Status != PurchaseOrderStatus.PartiallyReceived &&
                    purchaseOrder.Status != PurchaseOrderStatus.CompletelyReceived)
                {
                    _logger.LogWarning("Purchase order not in receivable status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order must be in PendingReceiving, PartiallyReceived, or CompletelyReceived status to receive goods");
                }

                foreach (var itemDto in dto.Items)
                {
                    var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.Id);
                    if (item == null)
                    {
                        _logger.LogWarning("Purchase order item not found: {ItemId}", itemDto.Id);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order item not found: {itemDto.Id}");
                    }

                    var newReceived = (item.QuantityReceived ?? 0) + itemDto.QuantityReceived;
                    if (newReceived > item.QuantityOrdered)
                    {
                        _logger.LogWarning("Total received quantity exceeds ordered: {NewReceived} > {Ordered}", newReceived, item.QuantityOrdered);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Total received quantity exceeds ordered for item: {item.Id}");
                    }

                    item.SetQuantityReceived(itemDto.QuantityReceived);

                    _context.PurchaseHistory.Add(new PurchaseHistory
                    {
                        Id = Guid.NewGuid(),
                        PurchaseOrderId = purchaseOrder.Id,
                        Action = "Received",
                        PerformedByUserId = userId,
                        Details = $"Received {itemDto.QuantityReceived} items for ProductId: {item.ProductId}",
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    });
                }

                // Update status based on received quantities
                var allItemsFullyReceived = purchaseOrder.Items.All(i => i.QuantityReceived == i.QuantityOrdered);
                var anyItemsReceived = purchaseOrder.Items.Any(i => i.QuantityReceived > 0);

                if (allItemsFullyReceived)
                {
                    purchaseOrder.Status = PurchaseOrderStatus.CompletelyReceived;
                }
                else if (anyItemsReceived)
                {
                    purchaseOrder.Status = PurchaseOrderStatus.PartiallyReceived;
                }
                else
                {
                    purchaseOrder.Status = PurchaseOrderStatus.PendingReceiving;
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Received purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Purchase order received successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving purchase order: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail(ex.InnerException?.Message ?? ex.Message);
            }
        }

        public async Task<ApiResponse<PurchaseOrderDto>> CheckoutByFinanceAsync(Guid purchaseOrderId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    _logger.LogWarning("Invalid user ID in JWT: {UserIdClaim}", userIdClaim);
                    return ApiResponse<PurchaseOrderDto>.Fail("Invalid user ID in JWT");
                }

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Branch)
                    .FirstOrDefaultAsync(po => po.Id == purchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", purchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                if (purchaseOrder.Status != PurchaseOrderStatus.CompletelyReceived)
                {
                    _logger.LogWarning("Purchase order not in CompletelyReceived status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order must be in CompletelyReceived status to be checked out by finance");
                }

                purchaseOrder.Status = PurchaseOrderStatus.PendingFinanceCheckout;
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                // Add to purchase history
                _context.PurchaseHistory.Add(new PurchaseHistory
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderId = purchaseOrder.Id,
                    Action = "CheckedOutByFinance",
                    PerformedByUserId = userId,
                    Details = "Finance department processed payment for the purchase order",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Finance checked out purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Purchase order payment processed by finance. Waiting for admin final approval.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking out purchase order by finance: {PurchaseOrderId}", purchaseOrderId);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error checking out purchase order by finance");
            }
        }

        public async Task<ApiResponse<PurchaseOrderDto>> FinalApproveByAdminAsync(Guid purchaseOrderId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    _logger.LogWarning("Invalid user ID in JWT: {UserIdClaim}", userIdClaim);
                    return ApiResponse<PurchaseOrderDto>.Fail("Invalid user ID in JWT");
                }

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .Include(po => po.Branch)
                    .FirstOrDefaultAsync(po => po.Id == purchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", purchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                if (purchaseOrder.Status != PurchaseOrderStatus.PendingFinanceCheckout)
                {
                    _logger.LogWarning("Purchase order not in PendingFinanceCheckout status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order must be in PendingFinanceCheckout status for final approval");
                }

                // Update product prices and stock
                foreach (var item in purchaseOrder.Items)
                {
                    if (!item.QuantityReceived.HasValue)
                    {
                        _logger.LogWarning("Item not received, cannot approve: {ItemId}", item.Id);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Item not received, cannot approve: {item.Id}");
                    }

                    // Update product price
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        product.UnitPrice = item.UnitPrice;
                        product.UpdatedAt = DateTime.UtcNow;
                        _context.Products.Update(product);
                    }

                    // Update stock
                    // Update stock and create stock movement
                    var stock = await _context.Stocks
                        .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.BranchId == purchaseOrder.BranchId);

                    decimal currentStockQuantity = 0;

                    if (stock == null)
                    {
                        // No existing stock, this is the first entry
                        currentStockQuantity = 0;
                        stock = new Stock
                        {
                            Id = Guid.NewGuid(),
                            ProductId = item.ProductId,
                            BranchId = purchaseOrder.BranchId,
                            Quantity = item.QuantityReceived.Value,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        _context.Stocks.Add(stock);
                    }
                    else
                    {
                        currentStockQuantity = stock.Quantity;
                        stock.Quantity += item.QuantityReceived.Value;
                        stock.UpdatedAt = DateTime.UtcNow;
                    }

                    // Create stock movement using helper method
                    await CreateStockMovementForPurchaseAsync(
                        item.ProductId,
                        purchaseOrder.BranchId,
                        purchaseOrder.Id,
                        item.QuantityReceived.Value,
                        currentStockQuantity,
                        userId);

                    item.QuantityApproved = item.QuantityReceived;
                }

                purchaseOrder.Status = PurchaseOrderStatus.FullyApproved;
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                // Add to purchase history
                _context.PurchaseHistory.Add(new PurchaseHistory
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderId = purchaseOrder.Id,
                    Action = "FinalApprovedByAdmin",
                    PerformedByUserId = userId,
                    Details = "Admin gave final approval and items added to inventory",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Admin finally approved purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Purchase order fully approved. Items added to inventory.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.InnerException.Message, "Error final approving purchase order by admin: {PurchaseOrderId}", purchaseOrderId);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error final approving purchase order by admin");
            }
        }

        public async Task<ApiResponse<PurchaseOrderDto>> RejectPurchaseOrderAsync(Guid purchaseOrderId, string reason)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    _logger.LogWarning("Invalid user ID in JWT: {UserIdClaim}", userIdClaim);
                    return ApiResponse<PurchaseOrderDto>.Fail("Invalid user ID in JWT");
                }

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Branch)
                    .FirstOrDefaultAsync(po => po.Id == purchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", purchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                purchaseOrder.Status = PurchaseOrderStatus.Rejected;
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                // Add to purchase history
                _context.PurchaseHistory.Add(new PurchaseHistory
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderId = purchaseOrder.Id,
                    Action = "Rejected",
                    PerformedByUserId = userId,
                    Details = $"Purchase order rejected. Reason: {reason}",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Purchase order {PurchaseOrderId} rejected", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Purchase order rejected successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting purchase order: {PurchaseOrderId}", purchaseOrderId);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error rejecting purchase order");
            }
        }

        public async Task<ApiResponse<PurchaseOrderDto>> UpdatePurchaseOrderAsync(UpdatePurchaseOrderDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .FirstOrDefaultAsync(po => po.Id == dto.Id && po.IsActive && po.Status == PurchaseOrderStatus.PendingAdminAcceptance);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found, inactive, or not in PendingAdminAcceptance status: {PurchaseOrderId}", dto.Id);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found, inactive, or not in PendingAdminAcceptance status");
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                _context.PurchaseOrderItems.RemoveRange(purchaseOrder.Items);
                purchaseOrder.Items.Clear();

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
                        QuantityOrdered = itemDto.QuantityOrdered,
                        QuantityReceived = null,
                        QuantityApproved = null,
                        UnitPrice = itemDto.UnitPrice,
                    };
                    purchaseOrder.Items.Add(poItem);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Updated purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Purchase order updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating purchase order: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error updating purchase order");
            }
        }

        public async Task<ApiResponse<PurchaseOrderDto>> ApprovePurchaseOrderAsync(ApprovePurchaseOrderDto dto)
        {
            // This method is kept for backward compatibility but might not be used in new flow
            return await FinalApproveByAdminAsync(dto.Id);
        }

        public async Task<ApiResponse<PurchaseOrderDto>> UpdatePurchaseOrderStatusAsync(Guid id, PurchaseOrderStatus status)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items).ThenInclude(pi => pi.Product)
                    .Include(po => po.Branch)
                    .FirstOrDefaultAsync(po => po.Id == id && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", id);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                if (!IsValidStatusTransition(purchaseOrder.Status, status))
                {
                    _logger.LogWarning("Invalid status transition from {CurrentStatus} to {NewStatus}", purchaseOrder.Status, status);
                    return ApiResponse<PurchaseOrderDto>.Fail($"Invalid status transition from {purchaseOrder.Status} to {status}");
                }

                purchaseOrder.Status = status;
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Updated status of purchase order {PurchaseOrderId} to {Status}", id, status);
                return ApiResponse<PurchaseOrderDto>.Success(result, $"Purchase order status updated to {status}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating purchase order status {PurchaseOrderId} to {Status}", id, status);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error updating purchase order status");
            }
        }

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

        public async Task<ApiResponse<bool>> CancelPurchaseOrderAsync(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .FirstOrDefaultAsync(po => po.Id == id && po.IsActive &&
                    (po.Status == PurchaseOrderStatus.PendingAdminAcceptance ||
                     po.Status == PurchaseOrderStatus.PendingReceiving));

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found, inactive, or not in cancellable status: {PurchaseOrderId}", id);
                    return ApiResponse<bool>.Fail("Purchase order not found, inactive, or not in cancellable status");
                }

                purchaseOrder.Status = PurchaseOrderStatus.Cancelled;
                purchaseOrder.Deactivate();
                foreach (var item in purchaseOrder.Items)
                {
                    item.Deactivate();
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Cancelled purchase order {PurchaseOrderId}", id);
                return ApiResponse<bool>.Success(true, "Purchase order cancelled successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling purchase order {PurchaseOrderId}", id);
                await transaction.RollbackAsync();
                return ApiResponse<bool>.Fail("Error cancelling purchase order");
            }
        }
        private async Task CreateStockMovementForPurchaseAsync(
            Guid productId,
            Guid branchId,
            Guid purchaseOrderId,
            decimal quantity,
            decimal previousQuantity,
            Guid performedByUserId)
        {
            try
            {
                var newQuantity = previousQuantity + quantity;

                var stockMovement = new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    BranchId = branchId,
                    // Option 1: Set to null if allowed
                    //TransactionId = null,
                    // Option 2: Add a new field for purchase order reference
                    //PurchaseOrderId = purchaseOrderId,
                    MovementType = StockMovementType.Purchase,
                    Quantity = quantity,
                    PreviousQuantity = previousQuantity,
                    NewQuantity = newQuantity,
                    Reason = $"Purchase Order #{purchaseOrderId} - Stock Added",
                    MovementDate = DateTime.UtcNow,
                    IsActive = true
                };

                _context.StockMovements.Add(stockMovement);

                _logger.LogInformation(
                    "Created stock movement for Product {ProductId} in Branch {BranchId}: {Quantity} units added (Previous: {Previous}, New: {New})",
                    productId, branchId, quantity, previousQuantity, newQuantity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating stock movement for purchase");
                throw;
            }
        }
        private bool IsValidStatusTransition(PurchaseOrderStatus currentStatus, PurchaseOrderStatus newStatus)
        {
            var validTransitions = new Dictionary<PurchaseOrderStatus, List<PurchaseOrderStatus>>
            {
                {
                    PurchaseOrderStatus.PendingAdminAcceptance,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.PendingReceiving,
                        PurchaseOrderStatus.Rejected,
                        PurchaseOrderStatus.Cancelled
                    }
                },
                {
                    PurchaseOrderStatus.PendingReceiving,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.PartiallyReceived,
                        PurchaseOrderStatus.CompletelyReceived,
                        PurchaseOrderStatus.Rejected,
                        PurchaseOrderStatus.Cancelled
                    }
                },
                {
                    PurchaseOrderStatus.PartiallyReceived,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.CompletelyReceived,
                        PurchaseOrderStatus.Rejected
                    }
                },
                {
                    PurchaseOrderStatus.CompletelyReceived,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.PendingFinanceCheckout,
                        PurchaseOrderStatus.Rejected
                    }
                },
                {
                    PurchaseOrderStatus.PendingFinanceCheckout,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.FullyApproved,
                        PurchaseOrderStatus.Rejected
                    }
                }
            };

            return validTransitions.ContainsKey(currentStatus) &&
                   validTransitions[currentStatus].Contains(newStatus);
        }
    }
}