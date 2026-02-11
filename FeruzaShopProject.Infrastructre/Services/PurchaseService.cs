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
                        QuantityRequested = itemDto.QuantityRequested,
                        QuantityAccepted = null,
                        QuantityRegistered = null,
                        FinanceVerified = null,
                        UnitPrice = null
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

        // ========== STEP 2: ADMIN ACCEPTS QUANTITIES ==========
        public async Task<ApiResponse<PurchaseOrderDto>> AcceptQuantitiesByAdminAsync(AcceptPurchaseQuantitiesDto dto)
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
                    if (itemDto.QuantityAccepted > 0)
                    {
                        allItemsRejected = false;
                    }
                }

                // Update status
                if (allItemsRejected)
                {
                    purchaseOrder.Status = PurchaseOrderStatus.Rejected;
                }
                else
                {
                    purchaseOrder.Status = PurchaseOrderStatus.AcceptedByAdmin;
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                // Add to purchase history
                _context.PurchaseHistory.Add(new PurchaseHistory
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderId = purchaseOrder.Id,
                    Action = "QuantitiesAcceptedByAdmin",
                    PerformedByUserId = userId,
                    Details = "Admin reviewed and accepted purchase quantities",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });

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

        // ========== STEP 3: SALES REGISTERS RECEIVED QUANTITIES ==========
        public async Task<ApiResponse<PurchaseOrderDto>> RegisterReceivedQuantitiesAsync(RegisterReceivedQuantitiesDto dto)
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
                    .FirstOrDefaultAsync(po => po.Id == dto.PurchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                if (purchaseOrder.Status != PurchaseOrderStatus.AcceptedByAdmin &&
                    purchaseOrder.Status != PurchaseOrderStatus.PartiallyRegistered)
                {
                    _logger.LogWarning("Purchase order not in correct status for registration: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order must be in AcceptedByAdmin or PartiallyRegistered status to register received quantities");
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
                }

                // Check registration status
                var allItemsRegistered = purchaseOrder.Items.All(i =>
                    i.QuantityAccepted.HasValue &&
                    i.QuantityRegistered.HasValue &&
                    i.QuantityRegistered.Value > 0);

                var someItemsRegistered = purchaseOrder.Items.Any(i =>
                    i.QuantityRegistered.HasValue &&
                    i.QuantityRegistered.Value > 0);

                if (allItemsRegistered)
                {
                    purchaseOrder.Status = PurchaseOrderStatus.CompletelyRegistered;
                }
                else if (someItemsRegistered)
                {
                    purchaseOrder.Status = PurchaseOrderStatus.PartiallyRegistered;
                }
                else
                {
                    purchaseOrder.Status = PurchaseOrderStatus.PendingRegistration;
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                // Add to purchase history
                _context.PurchaseHistory.Add(new PurchaseHistory
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderId = purchaseOrder.Id,
                    Action = "QuantitiesRegisteredBySales",
                    PerformedByUserId = userId,
                    Details = "Sales registered received quantities after purchase",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });

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

        // ========== STEP 4: FINANCE VERIFICATION ==========
        public async Task<ApiResponse<PurchaseOrderDto>> FinanceVerificationAsync(FinanceVerificationDto dto)
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
                    .FirstOrDefaultAsync(po => po.Id == dto.PurchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                if (purchaseOrder.Status != PurchaseOrderStatus.CompletelyRegistered &&
                    purchaseOrder.Status != PurchaseOrderStatus.PendingFinanceProcessing)
                {
                    _logger.LogWarning("Purchase order not in correct status for finance verification: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order must be in CompletelyRegistered or PendingFinanceProcessing status");
                }

                // Validate supplier if provided
                if (dto.SupplierId.HasValue)
                {
                    var supplier = await _context.Suppliers.FindAsync(dto.SupplierId.Value);
                    if (supplier == null || !supplier.IsActive)
                    {
                        _logger.LogWarning("Invalid or inactive supplier: {SupplierId}", dto.SupplierId.Value);
                        return ApiResponse<PurchaseOrderDto>.Fail("Invalid or inactive supplier");
                    }
                    purchaseOrder.SupplierId = dto.SupplierId.Value;
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

                    if (!item.QuantityRegistered.HasValue || item.QuantityRegistered.Value == 0)
                    {
                        _logger.LogWarning("Item quantity not registered: {ItemId}", itemDto.ItemId);
                        return ApiResponse<PurchaseOrderDto>.Fail($"Item quantity not registered: {itemDto.ItemId}");
                    }

                    // Validate buying price
                    if (itemDto.BuyingPrice.HasValue)
                    {
                        if (itemDto.BuyingPrice.Value <= 0)
                        {
                            _logger.LogWarning("Buying price must be greater than zero: {Price}", itemDto.BuyingPrice.Value);
                            return ApiResponse<PurchaseOrderDto>.Fail($"Buying price must be greater than zero for item: {itemDto.ItemId}");
                        }
                        item.BuyingPrice = itemDto.BuyingPrice.Value;
                    }

                    // Validate selling price (UnitPrice)
                    if (itemDto.SellingPrice.HasValue)
                    {
                        if (itemDto.SellingPrice.Value <= 0)
                        {
                            _logger.LogWarning("Selling price must be greater than zero: {Price}", itemDto.SellingPrice.Value);
                            return ApiResponse<PurchaseOrderDto>.Fail($"Selling price must be greater than zero for item: {itemDto.ItemId}");
                        }

                        // Validate selling price is higher than buying price
                        if (itemDto.BuyingPrice.HasValue && itemDto.SellingPrice.Value <= itemDto.BuyingPrice.Value)
                        {
                            _logger.LogWarning("Selling price must be higher than buying price: {Selling} <= {Buying}",
                                itemDto.SellingPrice.Value, itemDto.BuyingPrice.Value);
                            return ApiResponse<PurchaseOrderDto>.Fail($"Selling price must be higher than buying price for item: {itemDto.ItemId}");
                        }

                        // Selling price is stored as UnitPrice in Product entity
                        item.UnitPrice = itemDto.SellingPrice.Value;
                    }

                    // Auto-calculate selling price if not provided but buying price is
                    if (itemDto.BuyingPrice.HasValue && !itemDto.SellingPrice.HasValue)
                    {
                        // Apply default markup percentage (you can make this configurable)
                        var defaultMarkupPercentage = await GetDefaultMarkupPercentageAsync();
                        item.UnitPrice = itemDto.BuyingPrice.Value * (1 + defaultMarkupPercentage / 100);
                        _logger.LogInformation("Auto-calculated selling price for item {ItemId}: Buying ${Buying} -> Selling ${Selling} ({Markup}% markup)",
                            itemDto.ItemId, itemDto.BuyingPrice.Value, item.UnitPrice.Value, defaultMarkupPercentage);
                    }

                    // Finance verification is automatically true when finance provides prices
                    if (itemDto.BuyingPrice.HasValue)
                    {
                        item.FinanceVerified = true;
                        anyItemsVerified = true;

                        _logger.LogInformation("Finance verified item {ItemId}: Buying=${Buying}, Selling=${Selling}",
                            itemDto.ItemId, item.BuyingPrice, item.UnitPrice);
                    }
                    else
                    {
                        // If no buying price provided, item is not verified
                        item.FinanceVerified = false;
                        allItemsVerified = false;
                        item.BuyingPrice = null;
                        item.UnitPrice = null;

                        _logger.LogInformation("Finance did not verify item {ItemId}: No prices provided", itemDto.ItemId);
                    }
                }

                // Update status based on verification
                if (allItemsVerified && purchaseOrder.Items.All(i => i.FinanceVerified == true))
                {
                    purchaseOrder.Status = PurchaseOrderStatus.ProcessedByFinance;
                    _logger.LogInformation("All items verified for purchase order {PurchaseOrderId}. Status: ProcessedByFinance", dto.PurchaseOrderId);
                }
                else if (anyItemsVerified)
                {
                    purchaseOrder.Status = PurchaseOrderStatus.PendingFinanceProcessing;
                    _logger.LogInformation("Some items verified for purchase order {PurchaseOrderId}. Status: PendingFinanceProcessing", dto.PurchaseOrderId);
                }
                else
                {
                    purchaseOrder.Status = PurchaseOrderStatus.CompletelyRegistered;
                    _logger.LogInformation("No items verified for purchase order {PurchaseOrderId}. Status: CompletelyRegistered", dto.PurchaseOrderId);
                }

                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                // Add to purchase history
                _context.PurchaseHistory.Add(new PurchaseHistory
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderId = purchaseOrder.Id,
                    Action = "FinanceVerification",
                    PerformedByUserId = userId,
                    Details = $"Finance verification completed. Verified items: {purchaseOrder.Items.Count(i => i.FinanceVerified == true)}/{purchaseOrder.Items.Count}",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });

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

        private async Task<decimal> GetDefaultMarkupPercentageAsync()
        {
            // You can store this in a configuration table or app settings
            // For now, returning a fixed value
            return 30m; // 30% default markup
        }
        // ========== STEP 5: ADMIN FINAL APPROVAL ==========

        // ========== ADDITIONAL OPERATIONS ==========
        public async Task<ApiResponse<PurchaseOrderDto>> RejectPurchaseOrderAsync(RejectPurchaseOrderDto dto)
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
                    PurchaseOrderStatus.PendingRegistration,
                    PurchaseOrderStatus.PartiallyRegistered,
                    PurchaseOrderStatus.CompletelyRegistered,
                    PurchaseOrderStatus.PendingFinanceProcessing,
                    PurchaseOrderStatus.ProcessedByFinance
                };

                if (!allowedStatuses.Contains(purchaseOrder.Status))
                {
                    _logger.LogWarning("Cannot reject purchase order in status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail($"Cannot reject purchase order in {purchaseOrder.Status} status");
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
                    Details = $"Purchase order rejected. Reason: {dto.Reason}",
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
                    PurchaseOrderStatus.AcceptedByAdmin
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
                        QuantityRequested = itemDto.QuantityRequested,
                        QuantityAccepted = null,
                        QuantityRegistered = null,
                        FinanceVerified = null,
                        UnitPrice = null
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

        // ========== QUERY METHODS ==========
        public async Task<ApiResponse<PurchaseOrderDto>> GetPurchaseOrderByIdAsync(Guid id)
        {
            try
            {
                var purchaseOrder = await _context.PurchaseOrders
                    .Include(po => po.Branch)
                    .Include(po => po.Creator)
                    .Include(po => po.Supplier)
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
                    .Include(po => po.Supplier)
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
                    .Include(po => po.Supplier)
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
                    .Include(po => po.Supplier)
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
                    .Include(po => po.Supplier)
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
                var totalPurchaseValue = await query
                    .SelectMany(po => po.Items)
                    .Where(i => i.UnitPrice.HasValue && i.QuantityRegistered.HasValue)
                    .SumAsync(i => i.UnitPrice.Value * i.QuantityRegistered.Value);

                var stats = new PurchaseOrderStatsDto
                {
                    TotalPurchaseOrders = totalPurchaseOrders,
                    PendingAdminAcceptance = await query.CountAsync(po => po.Status == PurchaseOrderStatus.PendingAdminAcceptance),
                    AcceptedByAdmin = await query.CountAsync(po => po.Status == PurchaseOrderStatus.AcceptedByAdmin),
                    PendingRegistration = await query.CountAsync(po => po.Status == PurchaseOrderStatus.PendingRegistration),
                    CompletelyRegistered = await query.CountAsync(po => po.Status == PurchaseOrderStatus.CompletelyRegistered),
                    PendingFinanceProcessing = await query.CountAsync(po => po.Status == PurchaseOrderStatus.PendingFinanceProcessing),
                    ProcessedByFinance = await query.CountAsync(po => po.Status == PurchaseOrderStatus.ProcessedByFinance),
                    FullyApproved = await query.CountAsync(po => po.Status == PurchaseOrderStatus.FullyApproved),
                    Rejected = await query.CountAsync(po => po.Status == PurchaseOrderStatus.Rejected),
                    Cancelled = await query.CountAsync(po => po.Status == PurchaseOrderStatus.Cancelled),
                    TotalPurchaseValue = totalPurchaseValue
                };

                return ApiResponse<PurchaseOrderStatsDto>.Success(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving purchase order statistics");
                return ApiResponse<PurchaseOrderStatsDto>.Fail("Error retrieving purchase order statistics");
            }
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

                            // Create stock movement for purchase
                            var stockMovement = new StockMovement
                            {
                                Id = Guid.NewGuid(),
                                ProductId = productId,
                                BranchId = branchId,
                                TransactionId = null, // No transaction for purchases
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

                            // Calculate profit margin
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
                        PurchaseOrderStatus.PendingRegistration,
                        PurchaseOrderStatus.PartiallyRegistered,
                        PurchaseOrderStatus.Rejected,
                        PurchaseOrderStatus.Cancelled
                    }
                },
                {
                    PurchaseOrderStatus.PendingRegistration,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.PartiallyRegistered,
                        PurchaseOrderStatus.CompletelyRegistered,
                        PurchaseOrderStatus.Rejected
                    }
                },
                {
                    PurchaseOrderStatus.PartiallyRegistered,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.CompletelyRegistered,
                        PurchaseOrderStatus.Rejected
                    }
                },
                {
                    PurchaseOrderStatus.CompletelyRegistered,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.PendingFinanceProcessing,
                        PurchaseOrderStatus.ProcessedByFinance,
                        PurchaseOrderStatus.Rejected
                    }
                },
                {
                    PurchaseOrderStatus.PendingFinanceProcessing,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.ProcessedByFinance,
                        PurchaseOrderStatus.Rejected
                    }
                },
                {
                    PurchaseOrderStatus.ProcessedByFinance,
                    new List<PurchaseOrderStatus> {
                        PurchaseOrderStatus.FullyApproved,
                        PurchaseOrderStatus.Rejected
                    }
                }
            };

            return validTransitions.ContainsKey(currentStatus) &&
                   validTransitions[currentStatus].Contains(newStatus);
        }

        public async Task<ApiResponse<PurchaseOrderDto>> AcceptByAdminAsync(Guid purchaseOrderId)
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
                    SellingPrice = item.UnitPrice ?? 0
                });
            }

            return await FinanceVerificationAsync(dto);
        }

        public async Task<ApiResponse<PurchaseOrderDto>> FinalApprovalByAdminAsync(FinalApprovePurchaseOrderDto dto)
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
                    .FirstOrDefaultAsync(po => po.Id == dto.PurchaseOrderId && po.IsActive);

                if (purchaseOrder == null)
                {
                    _logger.LogWarning("Purchase order not found or inactive: {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order not found or inactive");
                }

                if (purchaseOrder.Status != PurchaseOrderStatus.ProcessedByFinance)
                {
                    _logger.LogWarning("Purchase order not in ProcessedByFinance status: {Status}", purchaseOrder.Status);
                    return ApiResponse<PurchaseOrderDto>.Fail("Purchase order must be in ProcessedByFinance status for final approval");
                }

                // Check if all items are finance verified
                if (!purchaseOrder.Items.All(i => i.FinanceVerified == true))
                {
                    _logger.LogWarning("Not all items are finance verified for purchase order {PurchaseOrderId}", dto.PurchaseOrderId);
                    return ApiResponse<PurchaseOrderDto>.Fail("All items must be finance verified before final approval");
                }

                // Update product prices and stock for verified items only
                foreach (var item in purchaseOrder.Items.Where(i =>
                    i.QuantityRegistered.HasValue &&
                    i.QuantityRegistered.Value > 0 &&
                    i.FinanceVerified == true &&
                    i.BuyingPrice.HasValue &&
                    i.UnitPrice.HasValue))
                {
                    // Update product buying price and unit price (selling price)
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        // Update product prices
                        product.BuyingPrice = item.BuyingPrice.Value;
                        product.UnitPrice = item.UnitPrice.Value; // This is the selling price
                        product.UpdatedAt = DateTime.UtcNow;

                        // You might want to track who updated the price
                        // product.UpdatedBy = userId;

                        _context.Products.Update(product);

                        _logger.LogInformation("Updated product {ProductId} prices: Buying=${BuyingPrice}, Selling=${UnitPrice}",
                            item.ProductId, item.BuyingPrice.Value, item.UnitPrice.Value);
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
                }

                purchaseOrder.Status = PurchaseOrderStatus.FullyApproved;
                purchaseOrder.UpdatedAt = DateTime.UtcNow;

                // Calculate total purchase value
                var totalPurchaseValue = purchaseOrder.Items
                    .Where(i => i.FinanceVerified == true && i.BuyingPrice.HasValue && i.QuantityRegistered.HasValue)
                    .Sum(i => i.BuyingPrice.Value * i.QuantityRegistered.Value);

                // Add to purchase history
                _context.PurchaseHistory.Add(new PurchaseHistory
                {
                    Id = Guid.NewGuid(),
                    PurchaseOrderId = purchaseOrder.Id,
                    Action = "FinalApprovedByAdmin",
                    PerformedByUserId = userId,
                    Details = $"Admin gave final approval. Updated stock and prices. Total value: ${totalPurchaseValue}",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<PurchaseOrderDto>(purchaseOrder);
                _logger.LogInformation("Admin gave final approval for purchase order {PurchaseOrderId}. Items updated: {ItemCount}",
                    purchaseOrder.Id, purchaseOrder.Items.Count(i => i.FinanceVerified == true));

                return ApiResponse<PurchaseOrderDto>.Success(result,
                    $"Purchase order fully approved. {purchaseOrder.Items.Count(i => i.FinanceVerified == true)} items added to inventory with updated prices.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in final approval by admin: {PurchaseOrderId}", dto.PurchaseOrderId);
                await transaction.RollbackAsync();
                return ApiResponse<PurchaseOrderDto>.Fail("Error in final approval by admin");
            }
        }
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

        public async Task<ApiResponse<PurchaseOrderDto>> RejectPurchaseOrderAsync(Guid purchaseOrderId, string reason)
        {
            var dto = new RejectPurchaseOrderDto
            {
                PurchaseOrderId = purchaseOrderId,
                Reason = reason
            };

            return await RejectPurchaseOrderAsync(dto);
        }

        public async Task<ApiResponse<bool>> CancelPurchaseOrderAsync(Guid id)
        {
            var dto = new CancelPurchaseOrderDto
            {
                PurchaseOrderId = id
            };

            return await CancelPurchaseOrderAsync(dto);
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
#pragma warning restore CS0618 // Type or member is obsolete
    }
}