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
                    Status = PurchaseOrderStatus.PendingAdminAcceptance,
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
                        QuantityRequested = itemDto.QuantityRequested,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    purchaseOrder.Items.Add(poItem);
                }

                _context.PurchaseOrders.Add(purchaseOrder);
                await AddPurchaseHistoryAsync(purchaseOrder.Id, "Created", createdBy, $"Purchase order created with {purchaseOrder.Items.Count} items");
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

        // ========== STEP 2: ADMIN ACCEPTS QUANTITIES ==========
        public async Task<ApiResponse<PurchaseOrderDto>> AcceptQuantitiesByAdminAsync(AcceptPurchaseQuantitiesDto dto)
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

                if (purchaseOrder.Status != PurchaseOrderStatus.PendingAdminAcceptance)
                {
                    _logger.LogWarning("Purchase order not in PendingAdminAcceptance status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order must be in PendingAdminAcceptance status for admin acceptance");
                }

                bool allItemsRejected = true;
                foreach (var itemDto in dto.Items)
                {
                    var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.ItemId);
                    if (item == null)
                    {
                        _logger.LogWarning("Purchase order item not found: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order item not found: {itemDto.ItemId}");
                    }

                    if (itemDto.QuantityAccepted > item.QuantityRequested)
                    {
                        _logger.LogWarning("Accepted quantity exceeds requested quantity: {Accepted} > {Requested}",
                            itemDto.QuantityAccepted, item.QuantityRequested);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Accepted quantity cannot exceed requested quantity for item: {itemDto.ItemId}");
                    }

                    item.QuantityAccepted = itemDto.QuantityAccepted;
                    item.AcceptedAt = DateTime.UtcNow;
                    item.AcceptedBy = userId;
                    item.UpdatedAt = DateTime.UtcNow;

                    if (itemDto.QuantityAccepted > 0)
                    {
                        allItemsRejected = false;
                    }
                }

                // Update status
                purchaseOrder.Status = allItemsRejected ? PurchaseOrderStatus.Rejected : PurchaseOrderStatus.AcceptedByAdmin;
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "QuantitiesAcceptedByAdmin", userId,
                    $"Admin accepted quantities. Status: {purchaseOrder.Status}");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Admin accepted quantities for purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Purchase quantities accepted by admin. Ready for purchase.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting purchase quantities by admin: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error accepting purchase quantities by admin");
            }
        }

        // ========== STEP 3: SALES REGISTERS RECEIVED QUANTITIES (MULTIPLE TIMES) ==========
        public async Task<ApiResponse<PurchaseOrderDto>> RegisterReceivedQuantitiesAsync(RegisterReceivedQuantitiesDto dto)
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

                // ALLOWED STATUSES FOR REGISTRATION:
                // - Before finance: AcceptedByAdmin, PartiallyRegistered, CompletelyRegistered
                // - After finance: PartiallyFinanceProcessed, FullyFinanceProcessed
                // - After admin approval: PartiallyApproved
                var allowedStatuses = new List<PurchaseOrderStatus>
        {
            PurchaseOrderStatus.AcceptedByAdmin,
            PurchaseOrderStatus.PartiallyRegistered,
            PurchaseOrderStatus.CompletelyRegistered,
            PurchaseOrderStatus.PartiallyFinanceProcessed,
            PurchaseOrderStatus.FullyFinanceProcessed,
            PurchaseOrderStatus.PartiallyApproved
        };

                if (!allowedStatuses.Contains(purchaseOrder.Status))
                {
                    _logger.LogWarning("Purchase order not in correct status for registration: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order must be in one of these statuses to register received quantities: AcceptedByAdmin, PartiallyRegistered, CompletelyRegistered, PartiallyFinanceProcessed, FullyFinanceProcessed, or PartiallyApproved");
                }

                // Check if order is fully approved - cannot register more
                if (purchaseOrder.Status == PurchaseOrderStatus.FullyApproved)
                {
                    _logger.LogWarning("Cannot register quantities for fully approved purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Cannot register quantities for a fully approved purchase order");
                }

                foreach (var itemDto in dto.Items)
                {
                    var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.ItemId);
                    if (item == null)
                    {
                        _logger.LogWarning("Purchase order item not found: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order item not found: {itemDto.ItemId}");
                    }

                    if (!item.QuantityAccepted.HasValue)
                    {
                        _logger.LogWarning("Item not accepted by admin: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Item not accepted by admin: {itemDto.ItemId}");
                    }

                    // Check if item is already approved
                    if (item.IsApproved)
                    {
                        _logger.LogWarning("Cannot register quantities for approved item: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Item is already approved and cannot receive more quantities");
                    }

                    // Calculate new total registered quantity
                    int newTotalRegistered = (item.QuantityRegistered ?? 0) + itemDto.QuantityRegistered;

                    if (newTotalRegistered > item.QuantityAccepted.Value)
                    {
                        _logger.LogWarning("Total registered quantity exceeds accepted quantity: {NewTotal} > {Accepted}",
                            newTotalRegistered, item.QuantityAccepted.Value);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Total registered quantity ({newTotalRegistered}) would exceed accepted quantity ({item.QuantityAccepted.Value}) for item: {itemDto.ItemId}");
                    }

                    // Update registered quantity (accumulate for multiple registrations)
                    item.QuantityRegistered = newTotalRegistered;
                    item.RegisteredAt = DateTime.UtcNow;
                    item.RegisteredBy = userId;

                    // Only increment edit count if this is an edit (not first registration)
                    if (item.RegistrationEditCount > 0 || newTotalRegistered > itemDto.QuantityRegistered)
                    {
                        item.RegistrationEditCount += 1;
                        item.LastRegistrationEditAt = DateTime.UtcNow;
                    }

                    item.UpdatedAt = DateTime.UtcNow;
                }

                // Update status based on registration progress
                UpdateRegistrationStatus(purchaseOrder);

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "QuantitiesRegisteredBySales", userId,
                    $"Sales registered quantities. Status: {purchaseOrder.Status}");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Sales registered received quantities for purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Received quantities registered successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering received quantities: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error registering received quantities");
            }
        }
        private void UpdateRegistrationStatus(PurchaseOrder purchaseOrder)
        {
            var allItemsRegistered = purchaseOrder.Items.All(i =>
                i.QuantityAccepted.HasValue &&
                i.QuantityRegistered.HasValue &&
                i.QuantityRegistered.Value >= i.QuantityAccepted.Value);

            var someItemsRegistered = purchaseOrder.Items.Any(i =>
                i.QuantityRegistered.HasValue &&
                i.QuantityRegistered.Value > 0);

            if (allItemsRegistered)
            {
                // If all items are fully registered, move to appropriate status
                if (purchaseOrder.Status == PurchaseOrderStatus.PartiallyFinanceProcessed ||
                    purchaseOrder.Status == PurchaseOrderStatus.FullyFinanceProcessed)
                {
                    purchaseOrder.Status = PurchaseOrderStatus.FullyFinanceProcessed;
                }
                else if (purchaseOrder.Status == PurchaseOrderStatus.PartiallyApproved)
                {
                    purchaseOrder.Status = PurchaseOrderStatus.PartiallyApproved; // Keep as partially approved until admin approves remaining
                }
                else
                {
                    purchaseOrder.Status = PurchaseOrderStatus.CompletelyRegistered;
                }
            }
            else if (someItemsRegistered)
            {
                purchaseOrder.Status = PurchaseOrderStatus.PartiallyRegistered;
            }
            else
            {
                purchaseOrder.Status = PurchaseOrderStatus.AcceptedByAdmin;
            }
        }

        // ========== STEP 4: FINANCE VERIFICATION (PARTIAL SUPPORTED) ==========
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

                // ALLOWED STATUSES FOR FINANCE VERIFICATION:
                // - Before registration: AcceptedByAdmin
                // - During registration: PartiallyRegistered, CompletelyRegistered
                // - After partial approval: PartiallyApproved
                // - After previous finance: PartiallyFinanceProcessed
                var allowedStatuses = new List<PurchaseOrderStatus>
        {
            PurchaseOrderStatus.AcceptedByAdmin,
            PurchaseOrderStatus.PartiallyRegistered,
            PurchaseOrderStatus.CompletelyRegistered,
            PurchaseOrderStatus.PartiallyFinanceProcessed,
            PurchaseOrderStatus.PartiallyApproved
        };

                if (!allowedStatuses.Contains(purchaseOrder.Status))
                {
                    _logger.LogWarning("Purchase order not in correct status for finance verification: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order must be in one of these statuses: AcceptedByAdmin, PartiallyRegistered, CompletelyRegistered, PartiallyFinanceProcessed, or PartiallyApproved");
                }

                // Cannot verify if order is fully approved
                if (purchaseOrder.Status == PurchaseOrderStatus.FullyApproved)
                {
                    _logger.LogWarning("Cannot verify fully approved purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Cannot verify a fully approved purchase order");
                }

                bool allItemsVerified = true;
                bool anyItemsVerified = false;

                foreach (var itemDto in dto.Items)
                {
                    var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.ItemId);
                    if (item == null)
                    {
                        _logger.LogWarning("Purchase order item not found: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order item not found: {itemDto.ItemId}");
                    }

                    // Check if item is already approved
                    if (item.IsApproved)
                    {
                        _logger.LogWarning("Cannot verify approved item: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Item is already approved and cannot be modified");
                    }

                    if (!item.QuantityRegistered.HasValue || item.QuantityRegistered.Value == 0)
                    {
                        _logger.LogWarning("Item quantity not registered: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Item quantity not registered: {itemDto.ItemId}");
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

                        // Set supplier name for this item (if provided)
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

                        anyItemsVerified = true;
                    }
                    else
                    {
                        allItemsVerified = false;
                    }
                }

                // Update status based on verification
                UpdateFinanceStatus(purchaseOrder, allItemsVerified, anyItemsVerified);

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "FinanceVerification", userId,
                    $"Finance verification completed. Verified items: {purchaseOrder.Items.Count(i => i.FinanceVerified == true)}/{purchaseOrder.Items.Count}");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Finance completed verification for purchase order {PurchaseOrderId}", purchaseOrder.Id);

                var message = anyItemsVerified
                    ? $"Finance verification completed. {purchaseOrder.Items.Count(i => i.FinanceVerified == true)} items verified."
                    : "Finance verification completed. No items verified.";

                return ApiResponse<PurchaseOrderDto>.Success(result, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in finance verification: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error in finance verification");
            }
        }
        private void UpdateFinanceStatus(PurchaseOrder purchaseOrder, bool allItemsVerified, bool anyItemsVerified)
        {
            // Check if ALL items are fully registered (QuantityRegistered >= QuantityAccepted)
            var allItemsFullyRegistered = purchaseOrder.Items.All(i =>
                i.QuantityRegistered.HasValue &&
                i.QuantityAccepted.HasValue &&
                i.QuantityRegistered.Value >= i.QuantityAccepted.Value);

            // Check if ALL items are finance verified
            var allItemsFinanceVerified = purchaseOrder.Items.All(i => i.FinanceVerified == true);

            // Check if any items are already approved
            var hasApprovedItems = purchaseOrder.Items.Any(i => i.IsApproved);

            if (allItemsFinanceVerified && allItemsFullyRegistered)
            {
                if (hasApprovedItems)
                {
                    // If some items are already approved, stay in PartiallyApproved
                    purchaseOrder.Status = PurchaseOrderStatus.PartiallyApproved;
                }
                else
                {
                    // Only when ALL items are BOTH fully registered AND finance verified
                    purchaseOrder.Status = PurchaseOrderStatus.FullyFinanceProcessed;
                }
            }
            else if (allItemsFinanceVerified && !allItemsFullyRegistered)
            {
                // All items are verified BUT not fully registered yet
                purchaseOrder.Status = hasApprovedItems
                    ? PurchaseOrderStatus.PartiallyApproved
                    : PurchaseOrderStatus.PartiallyFinanceProcessed;
            }
            else if (anyItemsVerified)
            {
                // Some items are verified
                purchaseOrder.Status = hasApprovedItems
                    ? PurchaseOrderStatus.PartiallyApproved
                    : PurchaseOrderStatus.PartiallyFinanceProcessed;
            }
            else
            {
                // No items verified yet - stay in registration or approval status
                if (hasApprovedItems)
                {
                    purchaseOrder.Status = PurchaseOrderStatus.PartiallyApproved;
                }
                else
                {
                    purchaseOrder.Status = allItemsFullyRegistered
                        ? PurchaseOrderStatus.CompletelyRegistered
                        : PurchaseOrderStatus.PartiallyRegistered;
                }
            }
        }
        private async Task<decimal> GetDefaultMarkupPercentageAsync()
        {
            // You can store this in a configuration table or app settings
            return 30m; // 30% default markup
        }

        // ========== STEP 5: ADMIN FINAL APPROVAL (PARTIAL SUPPORTED) ==========
        public async Task<ApiResponse<PurchaseOrderDto>> FinalApprovalByAdminAsync(FinalApprovePurchaseOrderDto dto)
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

                // ALLOWED STATUSES FOR FINAL APPROVAL:
                // - After finance: PartiallyFinanceProcessed, FullyFinanceProcessed
                // - After previous approvals: PartiallyApproved
                var allowedStatuses = new List<PurchaseOrderStatus>
        {
            PurchaseOrderStatus.PartiallyFinanceProcessed,
            PurchaseOrderStatus.FullyFinanceProcessed,
            PurchaseOrderStatus.PartiallyApproved  // Can approve more items even after partial approval
        };

                if (!allowedStatuses.Contains(purchaseOrder.Status))
                {
                    _logger.LogWarning("Purchase order not in correct status for final approval: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order must be in PartiallyFinanceProcessed, FullyFinanceProcessed, or PartiallyApproved status for final approval");
                }

                // Cannot approve if fully approved
                if (purchaseOrder.Status == PurchaseOrderStatus.FullyApproved)
                {
                    _logger.LogWarning("Purchase order already fully approved: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order is already fully approved");
                }

                // Determine which items to approve
                var itemsToApprove = dto.ItemIds != null && dto.ItemIds.Any()
                    ? purchaseOrder.Items.Where(i => dto.ItemIds.Contains(i.Id) && i.FinanceVerified == true && !i.IsApproved).ToList()
                    : purchaseOrder.Items.Where(i => i.FinanceVerified == true && !i.IsApproved).ToList();

                if (!itemsToApprove.Any())
                {
                    _logger.LogWarning("No items to approve for purchase order {PurchaseOrderId}", dto.PurchaseOrderId);

                    // Check if all items are already approved
                    if (purchaseOrder.Items.All(i => i.IsApproved))
                    {
                        purchaseOrder.Status = PurchaseOrderStatus.FullyApproved;
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        // FIX: Rename this to avoid conflict
                        var orderResult = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                        return ApiResponse<PurchaseOrderDto>.Success(orderResult, "All items are already approved");
                    }

                    return ApiResponse<PurchaseOrderDto>.Fail("No verified and unapproved items found to approve");
                }

                // Update stock and prices for approved items
                foreach (var item in itemsToApprove)
                {
                    if (!item.QuantityRegistered.HasValue || !item.BuyingPrice.HasValue || !item.UnitPrice.HasValue)
                    {
                        _logger.LogWarning("Item {ItemId} missing required data for approval", item.Id);
                        continue;
                    }

                    // Check if item is fully registered
                    if (item.QuantityRegistered.Value < item.QuantityAccepted.Value)
                    {
                        _logger.LogWarning("Item {ItemId} is not fully registered: {Registered}/{Accepted}",
                            item.Id, item.QuantityRegistered.Value, item.QuantityAccepted.Value);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Item is not fully registered. Registered: {item.QuantityRegistered.Value}, Accepted: {item.QuantityAccepted.Value}");
                    }

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
                        item.QuantityRegistered.Value,
                        item.BuyingPrice.Value,
                        item.UnitPrice.Value,
                        userId);

                    item.ApprovedAt = DateTime.UtcNow;
                    item.ApprovedBy = userId;
                    item.UpdatedAt = DateTime.UtcNow;
                }

                // Check current state
                var allItems = purchaseOrder.Items;
                var totalItems = allItems.Count();
                var acceptedItems = allItems.Count(i => i.QuantityAccepted > 0);
                var registeredItems = allItems.Count(i => i.QuantityRegistered > 0);
                var financeVerifiedItems = allItems.Count(i => i.FinanceVerified == true);
                var approvedItems = allItems.Count(i => i.IsApproved);

                // Check if ALL items are fully registered
                var allItemsFullyRegistered = allItems.All(i =>
                    !i.QuantityAccepted.HasValue || // Skip items not accepted
                    (i.QuantityRegistered.HasValue &&
                     i.QuantityAccepted.HasValue &&
                     i.QuantityRegistered.Value >= i.QuantityAccepted.Value));

                // Check if ALL finance-verified items are approved
                var allFinanceVerifiedItemsApproved = allItems
                    .Where(i => i.FinanceVerified == true)
                    .All(i => i.IsApproved);

                // Check if there are any unapproved finance-verified items
                var hasUnapprovedFinanceVerified = allItems
                    .Any(i => i.FinanceVerified == true && !i.IsApproved);

                // Update purchase order status
                if (allFinanceVerifiedItemsApproved && allItemsFullyRegistered)
                {
                    // All finance-verified items are approved AND all accepted items are fully registered
                    purchaseOrder.Status = PurchaseOrderStatus.FullyApproved;
                }
                else if (approvedItems > 0)
                {
                    // Some items are approved
                    purchaseOrder.Status = PurchaseOrderStatus.PartiallyApproved;
                }
                else if (financeVerifiedItems > 0)
                {
                    // Some items are finance verified but none approved yet
                    purchaseOrder.Status = allItemsFullyRegistered
                        ? PurchaseOrderStatus.FullyFinanceProcessed
                        : PurchaseOrderStatus.PartiallyFinanceProcessed;
                }
                else if (registeredItems > 0)
                {
                    // Some items are registered but not finance verified
                    purchaseOrder.Status = allItemsFullyRegistered
                        ? PurchaseOrderStatus.CompletelyRegistered
                        : PurchaseOrderStatus.PartiallyRegistered;
                }
                else
                {
                    // Only accepted, nothing else
                    purchaseOrder.Status = PurchaseOrderStatus.AcceptedByAdmin;
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                var approvedCount = purchaseOrder.Items.Count(i => i.IsApproved);
                var totalValue = purchaseOrder.Items
                    .Where(i => i.IsApproved && i.BuyingPrice.HasValue && i.QuantityRegistered.HasValue)
                    .Sum(i => i.BuyingPrice.Value * i.QuantityRegistered.Value);

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "FinalApprovedByAdmin", userId,
                    $"Admin approved {itemsToApprove.Count} new items. Total approved: {approvedCount}. Total value: ${totalValue}");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // FIX: Use a different variable name here
                var finalResult = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Admin gave final approval for purchase order {PurchaseOrderId}. New items approved: {NewApproved}, Total approved: {TotalApproved}",
                    purchaseOrder.Id, itemsToApprove.Count, approvedCount);

                // Determine the correct message
                string statusMessage;
                if (purchaseOrder.Status == PurchaseOrderStatus.FullyApproved)
                {
                    statusMessage = $"Purchase order fully approved. All {approvedCount} items added to inventory with updated prices.";
                }
                else
                {
                    var remainingItems = allItems.Count(i => i.FinanceVerified == true && !i.IsApproved);
                    statusMessage = $"Purchase order partially approved. {itemsToApprove.Count} new items approved. Total approved: {approvedCount}. Remaining items to approve: {remainingItems}.";
                }

                return ApiResponse<PurchaseOrderDto>.Success(finalResult, statusMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in final approval by admin: {PurchaseOrderId}", dto.PurchaseOrderId);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error in final approval by admin");
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

                // Check if sales can edit (only PendingAdminAcceptance status)
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingAdminAcceptance)
                {
                    _logger.LogWarning("Sales cannot edit purchase order in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order can only be edited when in PendingAdminAcceptance status");
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
                    // Create a list of new items to add
                    var newItems = new List<PurchaseOrderItem>();

                    foreach (var itemDto in dto.Items)
                    {
                        var product = await _context.Products.FindAsync(itemDto.ProductId);
                        if (product == null || !product.IsActive)
                        {
                            _logger.LogWarning("Invalid or inactive product: {ProductId}", itemDto.ProductId);
                            return ApiResponse<PurchaseOrderDto>.Fail($"Invalid or inactive product: {itemDto.ProductId}");
                        }

                        // Check if this item already exists
                        var existingItem = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.ItemId);

                        if (existingItem != null)
                        {
                            // Update existing item instead of creating new one
                            existingItem.ProductId = itemDto.ProductId;
                            existingItem.QuantityRequested = itemDto.QuantityRequested;
                            existingItem.UpdatedAt = DateTime.UtcNow;
                        }
                        else
                        {
                            // Create new item only if it doesn't exist
                            var poItem = new PurchaseOrderItem
                            {
                                Id = Guid.NewGuid(), // Always generate new ID for new items
                                PurchaseOrderId = purchaseOrder.Id,
                                ProductId = itemDto.ProductId,
                                QuantityRequested = itemDto.QuantityRequested,
                                CreatedAt = DateTime.UtcNow,
                                IsActive = true
                            };
                            newItems.Add(poItem);
                        }
                    }

                    // Add all new items at once
                    foreach (var newItem in newItems)
                    {
                        purchaseOrder.Items.Add(newItem);
                    }

                    // Find and deactivate items that are no longer in the list
                    var requestedItemIds = dto.Items.Where(i => i.ItemId.HasValue).Select(i => i.ItemId.Value).ToHashSet();
                    var itemsToRemove = purchaseOrder.Items
                        .Where(i => i.IsActive && !requestedItemIds.Contains(i.Id))
                        .ToList();

                    foreach (var item in itemsToRemove)
                    {
                        item.Deactivate();
                        item.UpdatedAt = DateTime.UtcNow;
                    }
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "EditedBySales", userId,
                    $"Sales edited purchase order. Items: {purchaseOrder.Items.Count(i => i.IsActive)}");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Sales edited purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Purchase order updated successfully");
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

                // Check if sales can delete (only PendingAdminAcceptance status)
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingAdminAcceptance)
                {
                    _logger.LogWarning("Sales cannot delete purchase order in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<bool>.Fail("Purchase order can only be deleted when in PendingAdminAcceptance status");
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

        public async Task<ApiResponse<PurchaseOrderDto>> EditRegisteredQuantitiesBySalesAsync(EditRegisteredQuantitiesBySalesDto dto)
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

                // Can edit registered quantities if not yet finance verified
                if (purchaseOrder.Items.Any(i => i.FinanceVerified == true))
                {
                    _logger.LogWarning("Cannot edit registered quantities after finance verification");
                    return ApiResponse<PurchaseOrderDto>.Fail("Cannot edit registered quantities after finance verification");
                }

                if (purchaseOrder.Status != PurchaseOrderStatus.PartiallyRegistered &&
                    purchaseOrder.Status != PurchaseOrderStatus.CompletelyRegistered)
                {
                    _logger.LogWarning("Cannot edit registered quantities in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Can only edit registered quantities in PartiallyRegistered or CompletelyRegistered status");
                }

                foreach (var itemDto in dto.Items)
                {
                    var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.ItemId);
                    if (item == null)
                    {
                        _logger.LogWarning("Purchase order item not found: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order item not found: {itemDto.ItemId}");
                    }

                    if (!item.QuantityAccepted.HasValue)
                    {
                        _logger.LogWarning("Item not accepted by admin: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Item not accepted by admin: {itemDto.ItemId}");
                    }

                    if (itemDto.QuantityRegistered > item.QuantityAccepted.Value)
                    {
                        _logger.LogWarning("Registered quantity exceeds accepted quantity: {Registered} > {Accepted}",
                            itemDto.QuantityRegistered, item.QuantityAccepted.Value);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Registered quantity cannot exceed accepted quantity for item: {itemDto.ItemId}");
                    }

                    // Update with edit tracking
                    item.QuantityRegistered = itemDto.QuantityRegistered;
                    item.RegisteredAt = DateTime.UtcNow;
                    item.RegisteredBy = userId;
                    item.RegistrationEditCount += 1;
                    item.LastRegistrationEditAt = DateTime.UtcNow;
                    item.UpdatedAt = DateTime.UtcNow;
                }

                // Recalculate status
                UpdateRegistrationStatus(purchaseOrder);
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "RegisteredQuantitiesEditedBySales", userId,
                    $"Sales edited registered quantities");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Sales edited registered quantities for purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Registered quantities updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing registered quantities by sales: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error editing registered quantities");
            }
        }
        // ========== ADMIN EDIT OPERATIONS ==========

        public async Task<ApiResponse<PurchaseOrderDto>> EditPurchaseOrderByAdminAsync(EditPurchaseOrderByAdminDto dto)
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

                // Admin cannot edit fully approved orders
                if (purchaseOrder.Status == PurchaseOrderStatus.FullyApproved)
                {
                    _logger.LogWarning("Admin cannot edit fully approved purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Cannot edit a fully approved purchase order");
                }

                // Update branch if provided
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
                    foreach (var itemDto in dto.Items)
                    {
                        if (itemDto.ItemId.HasValue)
                        {
                            // Update existing item
                            var existingItem = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.ItemId.Value);
                            if (existingItem != null)
                            {
                                // Only update if not approved
                                if (!existingItem.ApprovedAt.HasValue)
                                {
                                    if (itemDto.QuantityRequested.HasValue)
                                        existingItem.QuantityRequested = itemDto.QuantityRequested.Value;

                                    if (itemDto.QuantityAccepted.HasValue)
                                    {
                                        existingItem.QuantityAccepted = itemDto.QuantityAccepted.Value;
                                        existingItem.AcceptedAt = DateTime.UtcNow;
                                        existingItem.AcceptedBy = userId;
                                    }

                                    if (itemDto.QuantityRegistered.HasValue)
                                    {
                                        existingItem.QuantityRegistered = itemDto.QuantityRegistered.Value;
                                        existingItem.RegisteredAt = DateTime.UtcNow;
                                        existingItem.RegisteredBy = userId;
                                    }

                                    if (itemDto.BuyingPrice.HasValue)
                                        existingItem.BuyingPrice = itemDto.BuyingPrice.Value;

                                    if (itemDto.SellingPrice.HasValue)
                                        existingItem.UnitPrice = itemDto.SellingPrice.Value;

                                    if (itemDto.FinanceVerified.HasValue)
                                    {
                                        existingItem.FinanceVerified = itemDto.FinanceVerified.Value;
                                        if (itemDto.FinanceVerified.Value)
                                        {
                                            existingItem.FinanceVerifiedAt = DateTime.UtcNow;
                                            existingItem.FinanceVerifiedBy = userId;
                                        }
                                    }

                                    existingItem.UpdatedAt = DateTime.UtcNow;
                                }
                            }
                        }
                        else if (itemDto.ProductId.HasValue)
                        {
                            // Add new item
                            var product = await _context.Products.FindAsync(itemDto.ProductId.Value);
                            if (product == null || !product.IsActive)
                            {
                                _logger.LogWarning("Invalid or inactive product: {ProductId}", itemDto.ProductId.Value);
                                return ApiResponse<PurchaseOrderDto>.Fail($"Invalid or inactive product: {itemDto.ProductId.Value}");
                            }

                            var newItem = new PurchaseOrderItem
                            {
                                Id = Guid.NewGuid(),
                                PurchaseOrderId = purchaseOrder.Id,
                                ProductId = itemDto.ProductId.Value,
                                QuantityRequested = itemDto.QuantityRequested ?? 0,
                                QuantityAccepted = itemDto.QuantityAccepted,
                                QuantityRegistered = itemDto.QuantityRegistered,
                                BuyingPrice = itemDto.BuyingPrice,
                                UnitPrice = itemDto.SellingPrice,
                                FinanceVerified = itemDto.FinanceVerified,
                                CreatedAt = DateTime.UtcNow,
                                IsActive = true
                            };

                            if (itemDto.QuantityAccepted.HasValue)
                            {
                                newItem.AcceptedAt = DateTime.UtcNow;
                                newItem.AcceptedBy = userId;
                            }

                            if (itemDto.QuantityRegistered.HasValue)
                            {
                                newItem.RegisteredAt = DateTime.UtcNow;
                                newItem.RegisteredBy = userId;
                            }

                            if (itemDto.FinanceVerified == true)
                            {
                                newItem.FinanceVerifiedAt = DateTime.UtcNow;
                                newItem.FinanceVerifiedBy = userId;
                                newItem.PriceSetAt = DateTime.UtcNow;
                                newItem.PriceSetBy = userId;
                            }

                            purchaseOrder.Items.Add(newItem);
                        }
                    }
                }

                // Remove items
                if (dto.ItemIdsToRemove != null && dto.ItemIdsToRemove.Any())
                {
                    var itemsToRemove = purchaseOrder.Items
                        .Where(i => dto.ItemIdsToRemove.Contains(i.Id) && !i.ApprovedAt.HasValue)
                        .ToList();

                    foreach (var item in itemsToRemove)
                    {
                        item.Deactivate();
                    }
                }

                // Recalculate status
                purchaseOrder.Status = DeterminePurchaseOrderStatus(purchaseOrder);
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "EditedByAdmin", userId,
                    $"Admin edited purchase order. New status: {purchaseOrder.Status}");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Admin edited purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Purchase order updated successfully by admin");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing purchase order by admin: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error editing purchase order");
            }
        }
        public async Task<ApiResponse<PurchaseOrderDto>> EditAcceptedQuantitiesByAdminAsync(EditAcceptedQuantitiesByAdminDto dto)
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

                // Can edit accepted quantities if not yet registered
                if (purchaseOrder.Items.Any(i => i.QuantityRegistered.HasValue))
                {
                    _logger.LogWarning("Cannot edit accepted quantities after registration");
                    return ApiResponse<PurchaseOrderDto>.Fail("Cannot edit accepted quantities after items have been registered");
                }

                foreach (var itemDto in dto.Items)
                {
                    var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.ItemId);
                    if (item == null)
                    {
                        _logger.LogWarning("Purchase order item not found: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order item not found: {itemDto.ItemId}");
                    }

                    if (itemDto.QuantityAccepted > item.QuantityRequested)
                    {
                        _logger.LogWarning("Accepted quantity exceeds requested quantity: {Accepted} > {Requested}",
                            itemDto.QuantityAccepted, item.QuantityRequested);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Accepted quantity cannot exceed requested quantity for item: {itemDto.ItemId}");
                    }

                    item.QuantityAccepted = itemDto.QuantityAccepted;
                    item.AcceptedAt = DateTime.UtcNow;
                    item.AcceptedBy = userId;
                    item.UpdatedAt = DateTime.UtcNow;
                }

                // Recalculate status
                var allItemsRejected = purchaseOrder.Items.All(i => i.QuantityAccepted == 0);
                purchaseOrder.Status = allItemsRejected ? PurchaseOrderStatus.Rejected : PurchaseOrderStatus.AcceptedByAdmin;
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "AcceptedQuantitiesEditedByAdmin", userId,
                    $"Admin edited accepted quantities");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Admin edited accepted quantities for purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Accepted quantities updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing accepted quantities: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error editing accepted quantities");
            }
        }
        public async Task<ApiResponse<PurchaseOrderDto>> EditRegisteredQuantitiesByAdminAsync(EditRegisteredQuantitiesByAdminDto dto)
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

                // Can edit registered quantities if not yet finance verified
                if (purchaseOrder.Items.Any(i => i.FinanceVerified == true))
                {
                    _logger.LogWarning("Cannot edit registered quantities after finance verification");
                    return ApiResponse<PurchaseOrderDto>.Fail("Cannot edit registered quantities after finance verification");
                }

                foreach (var itemDto in dto.Items)
                {
                    var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.ItemId);
                    if (item == null)
                    {
                        _logger.LogWarning("Purchase order item not found: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order item not found: {itemDto.ItemId}");
                    }

                    if (!item.QuantityAccepted.HasValue)
                    {
                        _logger.LogWarning("Item not accepted by admin: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Item not accepted by admin: {itemDto.ItemId}");
                    }

                    if (itemDto.QuantityRegistered > item.QuantityAccepted.Value)
                    {
                        _logger.LogWarning("Registered quantity exceeds accepted quantity: {Registered} > {Accepted}",
                            itemDto.QuantityRegistered, item.QuantityAccepted.Value);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Registered quantity cannot exceed accepted quantity for item: {itemDto.ItemId}");
                    }

                    item.QuantityRegistered = itemDto.QuantityRegistered;
                    item.RegisteredAt = DateTime.UtcNow;
                    item.RegisteredBy = userId;
                    item.RegistrationEditCount += 1;
                    item.LastRegistrationEditAt = DateTime.UtcNow;
                    item.UpdatedAt = DateTime.UtcNow;
                }

                // Recalculate status
                UpdateRegistrationStatus(purchaseOrder);
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "RegisteredQuantitiesEditedByAdmin", userId,
                    $"Admin edited registered quantities");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Admin edited registered quantities for purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Registered quantities updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing registered quantities: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error editing registered quantities");
            }
        }
        public async Task<ApiResponse<PurchaseOrderDto>> EditPricesByAdminAsync(EditPricesByAdminDto dto)
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

                // Can edit prices if not yet approved
                if (purchaseOrder.Status == PurchaseOrderStatus.FullyApproved)
                {
                    _logger.LogWarning("Cannot edit prices after final approval");
                    return ApiResponse<PurchaseOrderDto>.Fail("Cannot edit prices after final approval");
                }

                foreach (var itemDto in dto.Items)
                {
                    var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemDto.ItemId);
                    if (item == null)
                    {
                        _logger.LogWarning("Purchase order item not found: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Purchase order item not found: {itemDto.ItemId}");
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
                        // Auto-calculate with markup
                        var defaultMarkupPercentage = await GetDefaultMarkupPercentageAsync();
                        item.UnitPrice = itemDto.BuyingPrice * (1 + defaultMarkupPercentage / 100);
                    }

                    item.PriceSetAt = DateTime.UtcNow;
                    item.PriceSetBy = userId;
                    item.PriceEditCount += 1;
                    item.UpdatedAt = DateTime.UtcNow;
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "PricesEditedByAdmin", userId,
                    $"Admin edited prices");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Admin edited prices for purchase order {PurchaseOrderId}", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Prices updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing prices: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error editing prices");
            }
        }
        public async Task<ApiResponse<bool>> DeletePurchaseOrderByAdminAsync(Guid purchaseOrderId, string reason)
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

                // Admin cannot delete fully approved orders
                if (purchaseOrder.Status == PurchaseOrderStatus.FullyApproved)
                {
                    _logger.LogWarning("Admin cannot delete fully approved purchase order: {PurchaseOrderId}", purchaseOrderId);
                    return ApiResponse<bool>.Fail("Cannot delete a fully approved purchase order. Use reverse transaction instead.");
                }

                // Soft delete
                purchaseOrder.Deactivate();
                purchaseOrder.Status = PurchaseOrderStatus.Cancelled;

                foreach (var item in purchaseOrder.Items)
                {
                    item.Deactivate();
                }

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "DeletedByAdmin", userId,
                    $"Admin deleted purchase order. Reason: {reason}");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Admin deleted purchase order {PurchaseOrderId}", purchaseOrderId);
                return ApiResponse<bool>.Success(true, "Purchase order deleted successfully by admin");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting purchase order by admin: {PurchaseOrderId}", purchaseOrderId);
                await transaction.RollbackAsync();
                return ApiResponse<bool>.Fail("Error deleting purchase order");
            }
        }

        // ========== REJECT/CANCEL OPERATIONS ==========

        public async Task<ApiResponse<PurchaseOrderDto>> RejectPurchaseOrderAsync(RejectPurchaseOrderDto dto)
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
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                // Can reject only in certain statuses
                var allowedStatuses = new List<PurchaseOrderStatus>
        {
            PurchaseOrderStatus.PendingAdminAcceptance,
            PurchaseOrderStatus.AcceptedByAdmin,
            PurchaseOrderStatus.PartiallyRegistered,
            PurchaseOrderStatus.CompletelyRegistered,
            PurchaseOrderStatus.PartiallyFinanceProcessed,
            PurchaseOrderStatus.FullyFinanceProcessed
        };

                if (!allowedStatuses.Contains(purchaseOrder.Status))
                {
                    _logger.LogWarning("Cannot reject purchase order in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail($"Cannot reject purchase order in {purchaseOrder.Status} status");
                }

                // If specific items are provided, reject only those items
                if (dto.ItemIds != null && dto.ItemIds.Any())
                {
                    foreach (var itemId in dto.ItemIds)
                    {
                        var item = purchaseOrder.Items.FirstOrDefault(i => i.Id == itemId);
                        if (item != null && !item.ApprovedAt.HasValue)
                        {
                            // Set QuantityAccepted to 0 to indicate rejection
                            item.QuantityAccepted = 0;
                            item.AcceptedAt = DateTime.UtcNow;
                            item.AcceptedBy = userId;
                            item.UpdatedAt = DateTime.UtcNow;

                            // Add rejection to history
                            await AddPurchaseHistoryAsync(
                                purchaseOrder.Id,
                                "ItemRejected",
                                userId,
                                $"Item {item.ProductId} rejected",
                                item.Id
                            );
                        }
                    }

                    // Recalculate status
                    purchaseOrder.Status = DeterminePurchaseOrderStatus(purchaseOrder);
                }
                else
                {
                    // Reject entire order
                    purchaseOrder.Status = PurchaseOrderStatus.Rejected;
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                // Add main rejection history
                await AddPurchaseHistoryAsync(purchaseOrder.Id, "Rejected", userId,
                    $"Purchase order rejected");

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Purchase order {PurchaseOrderId} rejected", purchaseOrder.Id);
                return ApiResponse<PurchaseOrderDto>.Success(result, "Purchase order rejected successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting purchase order: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error rejecting purchase order");
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

                // Can cancel only in early stages
                var allowedStatuses = new List<PurchaseOrderStatus>
        {
            PurchaseOrderStatus.PendingAdminAcceptance,
            PurchaseOrderStatus.AcceptedByAdmin,
            PurchaseOrderStatus.PartiallyRegistered
        };

                if (!allowedStatuses.Contains(purchaseOrder.Status))
                {
                    _logger.LogWarning("Cannot cancel purchase order in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<bool>.Fail($"Cannot cancel purchase order in {purchaseOrder.Status} status");
                }

                purchaseOrder.Status = PurchaseOrderStatus.Cancelled;
                purchaseOrder.Deactivate();
                foreach (var item in purchaseOrder.Items)
                {
                    item.Deactivate();
                }

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "Cancelled", userId,
                    $"Purchase order cancelled");

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

        public async Task<ApiResponse<PurchaseOrderDto>> DeleteItemFromPurchaseOrderBySalesAsync(Guid purchaseOrderId, Guid itemId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .Include(po => po.History) // Include history
                    .FirstOrDefaultAsync(po => po.Id == purchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", purchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                // Check if sales can edit (only PendingAdminAcceptance status)
                if (purchaseOrder.Status != PurchaseOrderStatus.PendingAdminAcceptance)
                {
                    _logger.LogWarning("Sales cannot delete items from purchase order in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Items can only be deleted when order is in PendingAdminAcceptance status");
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

                // FIRST: Delete any purchase history records that reference this item
                var itemHistory = await _context.PurchaseHistory
                    .Where(h => h.PurchaseOrderItemId == itemId)
                    .ToListAsync();

                if (itemHistory.Any())
                {
                    _context.PurchaseHistory.RemoveRange(itemHistory);
                }

                // THEN: Delete the item itself
                _context.PurchaseOrderItems.Remove(item);

                // Remove from the collection
                purchaseOrder.Items.Remove(item);

                // If no items left, delete the entire order and its history
                if (!purchaseOrder.Items.Any())
                {
                    // Delete all remaining history for this order
                    var orderHistory = await _context.PurchaseHistory
                        .Where(h => h.PurchaseOrderId == purchaseOrderId)
                        .ToListAsync();

                    if (orderHistory.Any())
                    {
                        _context.PurchaseHistory.RemoveRange(orderHistory);
                    }

                    // Delete the purchase order
                    _context.PurchaseOrders.Remove(purchaseOrder);
                }
                else
                {
                    purchaseOrder.UpdatedAt = DateTime.UtcNow;

                    // Add deletion history (this will be saved after item is deleted)
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

                // If order was deleted, return not found
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
        
        // ========== UPDATE PURCHASE ORDER (Legacy) ==========
        public async Task<ApiResponse<PurchaseOrderDto>> UpdatePurchaseOrderAsync(UpdatePurchaseOrderDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Items)
                    .FirstOrDefaultAsync(po => po.Id == dto.Id && po.IsActive && po.Status == PurchaseOrderStatus.PendingAdminAcceptance);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found, inactive, or not in PendingAdminAcceptance status: {PurchaseOrderId}", dto.Id);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found, inactive, or not in PendingAdminAcceptance status");
                }

                // Verify that the current user is the creator
                if (purchaseOrder.CreatedBy != userId)
                {
                    _logger.LogWarning("User {UserId} is not the creator of purchase order {PurchaseOrderId}", userId, dto.Id);
                    return ApiResponse<PurchaseOrderDto>.Fail("You can only update purchase orders that you created");
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                // Remove existing items
                _context.PurchaseOrderItems.RemoveRange(purchaseOrder.Items);
                purchaseOrder.Items.Clear();

                // Add new items
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
                        QuantityRequested = itemDto.QuantityRequested,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    purchaseOrder.Items.Add(poItem);
                }

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "Updated", userId,
                    $"Purchase order updated with {purchaseOrder.Items.Count} items");

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
                var query = _context.PurchaseOrders.AsQueryable();

                if (branchId.HasValue)
                {
                    query = query.Where(po => po.BranchId == branchId.Value);
                }

                var totalPurchaseOrders = await query.CountAsync();

                var totalBuyingCost = await query
                    .SelectMany(po => po.Items)
                    .Where(i => i.BuyingPrice.HasValue && i.QuantityRegistered.HasValue)
                    .SumAsync(i => i.BuyingPrice.Value * i.QuantityRegistered.Value);

                var totalSellingValue = await query
                    .SelectMany(po => po.Items)
                    .Where(i => i.UnitPrice.HasValue && i.QuantityRegistered.HasValue)
                    .SumAsync(i => i.UnitPrice.Value * i.QuantityRegistered.Value);

                var stats = new PurchaseOrderStatsDto
                {
                    TotalPurchaseOrders = totalPurchaseOrders,
                    PendingAdminAcceptance = await query.CountAsync(po => po.Status == PurchaseOrderStatus.PendingAdminAcceptance),
                    AcceptedByAdmin = await query.CountAsync(po => po.Status == PurchaseOrderStatus.AcceptedByAdmin),
                    PartiallyRegistered = await query.CountAsync(po => po.Status == PurchaseOrderStatus.PartiallyRegistered),
                    CompletelyRegistered = await query.CountAsync(po => po.Status == PurchaseOrderStatus.CompletelyRegistered),
                    PartiallyFinanceProcessed = await query.CountAsync(po => po.Status == PurchaseOrderStatus.PartiallyFinanceProcessed),
                    FullyFinanceProcessed = await query.CountAsync(po => po.Status == PurchaseOrderStatus.FullyFinanceProcessed),
                    PartiallyApproved = await query.CountAsync(po => po.Status == PurchaseOrderStatus.PartiallyApproved),
                    FullyApproved = await query.CountAsync(po => po.Status == PurchaseOrderStatus.FullyApproved),
                    Rejected = await query.CountAsync(po => po.Status == PurchaseOrderStatus.Rejected),
                    Cancelled = await query.CountAsync(po => po.Status == PurchaseOrderStatus.Cancelled),
                    TotalBuyingCost = totalBuyingCost,
                    TotalSellingValue = totalSellingValue
                };

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

                var now = DateTime.UtcNow;
                var sixMonthsAgo = now.AddMonths(-6);

                var dashboard = new PurchaseOrderDashboardDto
                {
                    TotalPending = await query.CountAsync(po =>
                        po.Status == PurchaseOrderStatus.PendingAdminAcceptance ||
                        po.Status == PurchaseOrderStatus.AcceptedByAdmin),

                    TotalInProgress = await query.CountAsync(po =>
                        po.Status == PurchaseOrderStatus.PartiallyRegistered ||
                        po.Status == PurchaseOrderStatus.CompletelyRegistered ||
                        po.Status == PurchaseOrderStatus.PartiallyFinanceProcessed ||
                        po.Status == PurchaseOrderStatus.FullyFinanceProcessed ||
                        po.Status == PurchaseOrderStatus.PartiallyApproved),

                    TotalCompleted = await query.CountAsync(po => po.Status == PurchaseOrderStatus.FullyApproved),
                    TotalRejected = await query.CountAsync(po => po.Status == PurchaseOrderStatus.Rejected),

                    OrdersByStatus = new Dictionary<string, int>(),
                    OrdersByBranch = new Dictionary<string, int>(),
                    ValueByBranch = new Dictionary<string, decimal>(),
                    RecentOrders = new List<RecentPurchaseOrderDto>(),
                    RecentActivity = new List<RecentPurchaseHistoryDto>()
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
                        .Select(g => new { BranchName = g.Key, Count = g.Count(), Value = g.Sum(po => po.Items.Sum(i => i.BuyingPrice.GetValueOrDefault() * i.QuantityRegistered.GetValueOrDefault())) })
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
                        TotalValue = po.Items.Sum(i => i.BuyingPrice.GetValueOrDefault() * i.QuantityRegistered.GetValueOrDefault())
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

                // Financial summary
                dashboard.TotalPurchaseValue = await query
                    .SelectMany(po => po.Items)
                    .Where(i => i.BuyingPrice.HasValue && i.QuantityRegistered.HasValue)
                    .SumAsync(i => i.BuyingPrice.Value * i.QuantityRegistered.Value);

                dashboard.TotalProfit = await query
                    .SelectMany(po => po.Items)
                    .Where(i => i.UnitPrice.HasValue && i.BuyingPrice.HasValue && i.QuantityRegistered.HasValue)
                    .SumAsync(i => (i.UnitPrice.Value - i.BuyingPrice.Value) * i.QuantityRegistered.Value);

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

        public async Task<ApiResponse<PurchaseOrderDto>> AcceptAllByAdminAsync(Guid purchaseOrderId)
        {
            var dto = new AcceptPurchaseQuantitiesDto
            {
                PurchaseOrderId = purchaseOrderId,
                Items = new List<AcceptQuantityItemDto>()
            };

            var purchaseOrder = await GetPurchaseOrderByIdAsync(purchaseOrderId);
            if (!purchaseOrder.IsCompletedSuccessfully)
            {
                return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found");
            }

            foreach (var item in purchaseOrder.Data.Items)
            {
                dto.Items.Add(new AcceptQuantityItemDto
                {
                    ItemId = item.Id,
                    QuantityAccepted = item.QuantityRequested
                });
            }

            return await AcceptQuantitiesByAdminAsync(dto);
        }

        public async Task<ApiResponse<PurchaseOrderDto>> RejectPurchaseOrderAsync(Guid purchaseOrderId, string reason)
        {
            var dto = new RejectPurchaseOrderDto
            {
                PurchaseOrderId = purchaseOrderId,
            };

            return await RejectPurchaseOrderAsync(dto);
        }

        public async Task<ApiResponse<bool>> CancelPurchaseOrderAsync(Guid purchaseOrderId, string? reason = null)
        {
            var dto = new CancelPurchaseOrderDto
            {
                PurchaseOrderId = purchaseOrderId,
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

                // Admin can edit (except fully approved)
                var isAdmin = true; // You need to check user role from claims
                if (isAdmin && purchaseOrder.Status != PurchaseOrderStatus.FullyApproved)
                {
                    return ApiResponse<bool>.Success(true, "Admin can edit");
                }

                // Sales can only edit their own orders in PendingAdminAcceptance
                if (purchaseOrder.CreatedBy == userId &&
                    purchaseOrder.Status == PurchaseOrderStatus.PendingAdminAcceptance)
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

        // ========== LEGACY/CONVENIENCE METHODS ==========

        public async Task<ApiResponse<PurchaseOrderDto>> ReceivePurchaseOrderAsync(ReceivePurchaseOrderDto dto)
        {
            var registerDto = new RegisterReceivedQuantitiesDto
            {
                PurchaseOrderId = dto.Id,
                Items = dto.Items.Select(i => new RegisterQuantityItemDto
                {
                    ItemId = i.Id,
                    QuantityRegistered = i.QuantityReceived
                }).ToList()
            };

            return await RegisterReceivedQuantitiesAsync(registerDto);
        }

        public async Task<ApiResponse<PurchaseOrderDto>> CheckoutByFinanceAsync(Guid purchaseOrderId)
        {
            var dto = new FinanceVerificationDto
            {
                PurchaseOrderId = purchaseOrderId,
                Items = new List<FinanceVerificationItemDto>()
            };

            var purchaseOrder = await GetPurchaseOrderByIdAsync(purchaseOrderId);
            if (!purchaseOrder.IsCompletedSuccessfully)
            {
                return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found");
            }

            foreach (var item in purchaseOrder.Data.Items.Where(i => i.QuantityRegistered.HasValue))
            {
                dto.Items.Add(new FinanceVerificationItemDto
                {
                    ItemId = item.Id,
                    BuyingPrice = item.BuyingPrice ?? 0,
                    SellingPrice = item.UnitPrice,
                    IsVerified = true
                });
            }

            return await FinanceVerificationAsync(dto);
        }

        public async Task<ApiResponse<PurchaseOrderDto>> ApprovePurchaseOrderAsync(ApprovePurchaseOrderDto dto)
        {
            var finalDto = new FinalApprovePurchaseOrderDto
            {
                PurchaseOrderId = dto.Id
            };

            return await FinalApprovalByAdminAsync(finalDto);
        }

        public async Task<ApiResponse<PurchaseOrderDto>> UpdatePurchaseOrderStatusAsync(Guid id, PurchaseOrderStatus status)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var purchaseOrder = await _context.PurchaseOrders
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

                await AddPurchaseHistoryAsync(purchaseOrder.Id, "StatusUpdated", await GetCurrentUserIdAsync(),
                    $"Status updated from {purchaseOrder.Status} to {status}");

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

        // ========== PRIVATE HELPER METHODS ==========

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
                    TransactionId = null,
                    PurchaseOrderId = purchaseOrderId,
                    MovementType = StockMovementType.Purchase,
                    Quantity = quantity,
                    PreviousQuantity = previousQuantity,
                    NewQuantity = previousQuantity + quantity,
                    Reason = $"Purchase Order #{purchaseOrderId} - Cost: ${buyingPrice}, Sell: ${sellingPrice}, Margin: {((sellingPrice - buyingPrice) / buyingPrice * 100):F1}%",
                    MovementDate = DateTime.UtcNow,
                    IsActive = true
                };

                _context.StockMovements.Add(stockMovement);

                var profitMargin = ((sellingPrice - buyingPrice) / buyingPrice * 100);

                _logger.LogInformation(
                    "Updated stock for Product {ProductId} in Branch {BranchId}: " +
                    "Added {Quantity} units (Buy: ${BuyingPrice}, Sell: ${SellingPrice}, Margin: {ProfitMargin:F1}%)",
                    productId, branchId, quantity, buyingPrice, sellingPrice, profitMargin);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating stock and creating movement for Product {ProductId}", productId);
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
                        PurchaseOrderStatus.AcceptedByAdmin,
                        PurchaseOrderStatus.Rejected,
                        PurchaseOrderStatus.Cancelled
                    }
                },
                {
                    PurchaseOrderStatus.AcceptedByAdmin,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.PartiallyRegistered,
                        PurchaseOrderStatus.CompletelyRegistered,
                        PurchaseOrderStatus.Rejected,
                        PurchaseOrderStatus.Cancelled
                    }
                },
                {
                    PurchaseOrderStatus.PartiallyRegistered,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.CompletelyRegistered,
                        PurchaseOrderStatus.PartiallyFinanceProcessed,
                        PurchaseOrderStatus.Rejected,
                        PurchaseOrderStatus.Cancelled
                    }
                },
                {
                    PurchaseOrderStatus.CompletelyRegistered,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.PartiallyFinanceProcessed,
                        PurchaseOrderStatus.FullyFinanceProcessed,
                        PurchaseOrderStatus.Rejected
                    }
                },
                {
                    PurchaseOrderStatus.PartiallyFinanceProcessed,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.FullyFinanceProcessed,
                        PurchaseOrderStatus.PartiallyApproved,
                        PurchaseOrderStatus.Rejected
                    }
                },
                {
                    PurchaseOrderStatus.FullyFinanceProcessed,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.PartiallyApproved,
                        PurchaseOrderStatus.FullyApproved,
                        PurchaseOrderStatus.Rejected
                    }
                },
                {
                    PurchaseOrderStatus.PartiallyApproved,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.FullyApproved,
                        PurchaseOrderStatus.Rejected
                    }
                }
            };

            return validTransitions.ContainsKey(currentStatus) &&
                   validTransitions[currentStatus].Contains(newStatus);
        }


        private PurchaseOrderStatus DeterminePurchaseOrderStatus(PurchaseOrder purchaseOrder)
        {
            if (purchaseOrder.Items.All(i => i.IsActive == false))
                return PurchaseOrderStatus.Cancelled;

            if (purchaseOrder.Items.All(i => i.QuantityAccepted == 0))
                return PurchaseOrderStatus.Rejected;

            if (purchaseOrder.Items.All(i => i.ApprovedAt.HasValue))
                return PurchaseOrderStatus.FullyApproved;

            if (purchaseOrder.Items.Any(i => i.ApprovedAt.HasValue))
                return PurchaseOrderStatus.PartiallyApproved;

            if (purchaseOrder.Items.All(i => i.FinanceVerified == true))
                return PurchaseOrderStatus.FullyFinanceProcessed;

            if (purchaseOrder.Items.Any(i => i.FinanceVerified == true))
                return PurchaseOrderStatus.PartiallyFinanceProcessed;

            if (purchaseOrder.Items.All(i => i.QuantityRegistered.HasValue && i.QuantityRegistered.Value > 0))
                return PurchaseOrderStatus.CompletelyRegistered;

            if (purchaseOrder.Items.Any(i => i.QuantityRegistered.HasValue && i.QuantityRegistered.Value > 0))
                return PurchaseOrderStatus.PartiallyRegistered;

            if (purchaseOrder.Items.All(i => i.QuantityAccepted.HasValue))
                return PurchaseOrderStatus.AcceptedByAdmin;

            return PurchaseOrderStatus.PendingAdminAcceptance;
        }


    }
}