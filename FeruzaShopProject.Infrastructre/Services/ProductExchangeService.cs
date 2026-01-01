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
    public class ProductExchangeService : IProductExchangeService
    {
        private readonly ShopDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMapper _mapper;
        private readonly ILogger<ProductExchangeService> _logger;

        public ProductExchangeService(
            ShopDbContext context,
            IMapper mapper,
            ILogger<ProductExchangeService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public async Task<ApiResponse<ProductExchangeResponseDto>> CreateExchangeAsync(CreateProductExchangeDto dto)  
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                //Authorization
                var (isAuthorized, userId, role) = await AuthorizeUserAsync();
                if (!isAuthorized || (role != Role.Manager && role != Role.Sales))
                {
                    return ApiResponse<ProductExchangeResponseDto>.Fail("Only Manager or Sales can create exchanges");
                }

                // Get original transaction
                var originalTransaction = await _context.Transactions
                    .Include(t => t.Product)
                    .Include(t => t.Customer)
                    .FirstOrDefaultAsync(t => t.Id == dto.OriginalTransactionId && t.IsActive);

                if (originalTransaction == null)
                    return ApiResponse<ProductExchangeResponseDto>.Fail("Transaction not found");

                // Get original product info FROM TRANSACTION
                var originalProduct = originalTransaction.Product;
                var originalQuantity = originalTransaction.Quantity;
                var originalPrice = originalTransaction.UnitPrice;

                // Get new product
                var newProduct = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == dto.NewProductId && p.IsActive);

                if (newProduct == null)
                    return ApiResponse<ProductExchangeResponseDto>.Fail("New product not found");

                // Validate quantities
                if (dto.NewQuantity <= 0)
                    return ApiResponse<ProductExchangeResponseDto>.Fail("New quantity must be greater than 0");

                // Check stock availability
                var branchId = originalTransaction.BranchId;
                var originalStock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == originalProduct.Id &&
                                            s.BranchId == branchId);

                var newStock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == dto.NewProductId &&
                                            s.BranchId == branchId);

                // For exchange: we need new product in stock
                if (newStock == null || newStock.Quantity < dto.NewQuantity)
                    return ApiResponse<ProductExchangeResponseDto>.Fail("Insufficient new product in stock");

                // Create exchange using AutoMapper for basic mapping
                var exchange = _mapper.Map<ProductExchange>(dto);

                // Set the properties that AutoMapper ignores
                exchange.OriginalProductId = originalProduct.Id;
                exchange.OriginalQuantity = originalQuantity;
                exchange.OriginalPrice = originalPrice;
                exchange.NewPrice = newProduct.UnitPrice;

                await _context.ProductExchanges.AddAsync(exchange);

                // Update stock
                // Add back original product to stock (customer returning it)
                if (originalStock != null)
                {
                    originalStock.Quantity += originalQuantity;
                    originalStock.UpdatedAt = DateTime.UtcNow;

                    // Create StockMovement for the returned product
                    await CreateStockMovementForReturnAsync(
                        originalProduct.Id,
                        branchId,
                        exchange.Id,
                        originalQuantity,
                        originalStock.Quantity - originalQuantity, // Previous quantity
                        originalStock.Quantity); // New quantity
                }

                // Remove new product from stock (customer taking it)
                newStock.Quantity -= dto.NewQuantity;
                newStock.UpdatedAt = DateTime.UtcNow;

                // Create StockMovement for the new product taken
                await CreateStockMovementForNewProductAsync(
                    newProduct.Id,
                    branchId,
                    exchange.Id,
                    dto.NewQuantity,
                    newStock.Quantity + dto.NewQuantity, // Previous quantity
                    newStock.Quantity); // New quantity

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                // Map to response using AutoMapper
                var result = _mapper.Map<ProductExchangeResponseDto>(exchange);
                result.Status = "Created";

                _logger.LogInformation("Exchange created: {ExchangeId}", exchange.Id);
                return ApiResponse<ProductExchangeResponseDto>.Success(result, "Exchange created successfully");
            }
            catch (Exception ex)
            {
                try { await dbTransaction.RollbackAsync(); } catch { }
                _logger.LogError(ex, "Error creating exchange");
                return ApiResponse<ProductExchangeResponseDto>.Fail($"Error: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ProductExchangeResponseDto>> GetExchangeByIdAsync(Guid id)
        {
            try
            {
                var exchange = await _context.ProductExchanges
                    .Include(e => e.OriginalTransaction)
                        .ThenInclude(t => t.Customer)
                    .Include(e => e.OriginalTransaction)
                        .ThenInclude(t => t.Branch)
                    .Include(e => e.OriginalProduct)
                    .Include(e => e.NewProduct)
                    .FirstOrDefaultAsync(e => e.Id == id && e.IsActive);

                if (exchange == null)
                    return ApiResponse<ProductExchangeResponseDto>.Fail("Exchange not found");

                // Map using AutoMapper - calculations are handled in the mapper
                var result = _mapper.Map<ProductExchangeResponseDto>(exchange);
                result.Status = "Retrieved";

                return ApiResponse<ProductExchangeResponseDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exchange by ID: {ExchangeId}", id);
                return ApiResponse<ProductExchangeResponseDto>.Fail($"Error retrieving exchange: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<ProductExchangeResponseDto>>> GetAllExchangesAsync(
            Guid? branchId = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var query = _context.ProductExchanges
                    .Include(e => e.OriginalTransaction)
                        .ThenInclude(t => t.Customer)
                    .Include(e => e.OriginalTransaction)
                        .ThenInclude(t => t.Branch)
                    .Include(e => e.OriginalProduct)
                    .Include(e => e.NewProduct)
                    .Where(e => e.IsActive)
                    .AsQueryable();

                // Apply filters
                if (branchId.HasValue)
                {
                    query = query.Where(e => e.OriginalTransaction.BranchId == branchId.Value);
                }

                if (startDate.HasValue)
                {
                    query = query.Where(e => e.CreatedAt.Date >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(e => e.CreatedAt.Date <= endDate.Value.Date);
                }

                var exchanges = await query
                    .OrderByDescending(e => e.CreatedAt)
                    .ToListAsync();

                // Map all exchanges using AutoMapper
                var result = exchanges.Select(exchange =>
                {
                    var dto = _mapper.Map<ProductExchangeResponseDto>(exchange);
                    dto.Status = "Listed";
                    return dto;
                }).ToList();

                return ApiResponse<List<ProductExchangeResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all exchanges");
                return ApiResponse<List<ProductExchangeResponseDto>>.Fail($"Error retrieving exchanges: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> DeleteExchangeAsync(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var exchange = await _context.ProductExchanges
                    .Include(e => e.OriginalTransaction)
                    .FirstOrDefaultAsync(e => e.Id == id && e.IsActive);

                if (exchange == null)
                    return ApiResponse<bool>.Fail("Exchange not found");

                // Reverse stock changes
                var branchId = exchange.OriginalTransaction.BranchId;
                var originalStock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == exchange.OriginalProductId &&
                                            s.BranchId == branchId);

                var newStock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == exchange.NewProductId &&
                                            s.BranchId == branchId);

                if (originalStock != null)
                {
                    originalStock.Quantity -= exchange.OriginalQuantity;
                    originalStock.UpdatedAt = DateTime.UtcNow;

                    // Create reverse StockMovement for the original product
                    await CreateReverseStockMovementForReturnAsync(
                        exchange.OriginalProductId,
                        branchId,
                        exchange.Id,
                        exchange.OriginalQuantity,
                        originalStock.Quantity + exchange.OriginalQuantity, // Previous quantity
                        originalStock.Quantity); // New quantity
                }

                if (newStock != null)
                {
                    newStock.Quantity += exchange.NewQuantity;
                    newStock.UpdatedAt = DateTime.UtcNow;

                    // Create reverse StockMovement for the new product
                    await CreateReverseStockMovementForNewProductAsync(
                        exchange.NewProductId,
                        branchId,
                        exchange.Id,
                        exchange.NewQuantity,
                        newStock.Quantity - exchange.NewQuantity, // Previous quantity
                        newStock.Quantity); // New quantity
                }

                // Soft delete
                exchange.IsActive = false;
                exchange.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully deleted exchange: {ExchangeId}", id);
                return ApiResponse<bool>.Success(true, "Exchange deleted successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting exchange: {ExchangeId}", id);
                return ApiResponse<bool>.Fail($"Error deleting exchange: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<ProductExchangeResponseDto>>> GetExchangesByTransactionAsync(Guid transactionId)
        {
            try
            {
                var exchanges = await _context.ProductExchanges
                    .Include(e => e.OriginalTransaction)
                        .ThenInclude(t => t.Customer)
                    .Include(e => e.OriginalTransaction)
                        .ThenInclude(t => t.Branch)
                    .Include(e => e.OriginalProduct)
                    .Include(e => e.NewProduct)
                    .Where(e => e.OriginalTransactionId == transactionId && e.IsActive)
                    .OrderByDescending(e => e.CreatedAt)
                    .ToListAsync();

                // Map using AutoMapper
                var result = exchanges.Select(exchange =>
                {
                    var dto = _mapper.Map<ProductExchangeResponseDto>(exchange);
                    dto.Status = "Transaction";
                    return dto;
                }).ToList();

                return ApiResponse<List<ProductExchangeResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exchanges for transaction: {TransactionId}", transactionId);
                return ApiResponse<List<ProductExchangeResponseDto>>.Fail($"Error retrieving exchanges: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<ProductExchangeResponseDto>>> GetExchangesByCustomerAsync(Guid customerId)
        {
            try
            {
                var exchanges = await _context.ProductExchanges
                    .Include(e => e.OriginalTransaction)
                        .ThenInclude(t => t.Customer)
                    .Include(e => e.OriginalTransaction)
                        .ThenInclude(t => t.Branch)
                    .Include(e => e.OriginalProduct)
                    .Include(e => e.NewProduct)
                    .Where(e => e.OriginalTransaction.CustomerId == customerId && e.IsActive)
                    .OrderByDescending(e => e.CreatedAt)
                    .ToListAsync();

                // Map using AutoMapper
                var result = exchanges.Select(exchange =>
                {
                    var dto = _mapper.Map<ProductExchangeResponseDto>(exchange);
                    dto.Status = "Customer";
                    return dto;
                }).ToList();

                return ApiResponse<List<ProductExchangeResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exchanges for customer: {CustomerId}", customerId);
                return ApiResponse<List<ProductExchangeResponseDto>>.Fail($"Error retrieving exchanges: {ex.Message}");
            }
        }

        public async Task<ApiResponse<ExchangeSummaryDto>> GetExchangeSummaryAsync(
            DateTime? startDate = null, DateTime? endDate = null, Guid? branchId = null)
        {
            try
            {
                var query = _context.ProductExchanges
                    .Include(e => e.OriginalTransaction)
                    .Where(e => e.IsActive)
                    .AsQueryable();

                // Apply filters
                if (branchId.HasValue)
                {
                    query = query.Where(e => e.OriginalTransaction.BranchId == branchId.Value);
                }

                if (startDate.HasValue)
                {
                    query = query.Where(e => e.CreatedAt.Date >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(e => e.CreatedAt.Date <= endDate.Value.Date);
                }

                var exchanges = await query.ToListAsync();

                var summary = new ExchangeSummaryDto
                {
                    TotalExchanges = exchanges.Count,
                    RefundExchanges = exchanges.Count(e => e.IsRefund),
                    AdditionalPaymentExchanges = exchanges.Count(e => e.IsAdditionalPayment),
                    EvenExchanges = exchanges.Count(e => e.IsEvenExchange),
                    TotalRefundAmount = exchanges.Where(e => e.IsRefund).Sum(e => e.Amount),
                    TotalAdditionalPayment = exchanges.Where(e => e.IsAdditionalPayment).Sum(e => e.Amount)
                };

                // Get recent exchanges
                var recentExchanges = exchanges
                    .OrderByDescending(e => e.CreatedAt)
                    .Take(10)
                    .ToList();

                // Map recent exchanges using AutoMapper
                summary.RecentExchanges = recentExchanges.Select(exchange =>
                {
                    var dto = _mapper.Map<ProductExchangeResponseDto>(exchange);
                    dto.Status = "Recent";
                    return dto;
                }).ToList();

                return ApiResponse<ExchangeSummaryDto>.Success(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exchange summary");
                return ApiResponse<ExchangeSummaryDto>.Fail($"Error retrieving exchange summary: {ex.Message}");
            }
        }

        #region Helper Methods - STOCK MOVEMENT METHODS ADDED 

        // Create StockMovement when original product is returned to stock
        private async Task CreateStockMovementForReturnAsync(
            Guid productId, Guid branchId, Guid exchangeId, decimal quantity,
            decimal previousQuantity, decimal newQuantity)
        {
            try
            {
                var stockMovement = new StockMovement
                {
                    ProductId = productId,
                    BranchId = branchId,
                    TransactionId = exchangeId,
                    MovementType = StockMovementType.Return,
                    Quantity = quantity, // Positive - adding to stock
                    PreviousQuantity = previousQuantity,
                    NewQuantity = newQuantity,
                    MovementDate = DateTime.UtcNow,
                    Reason = "Product exchange - original product returned"
                };

                await _context.StockMovements.AddAsync(stockMovement);
                _logger.LogDebug("StockMovement created for returned product in exchange {ExchangeId}", exchangeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating StockMovement for returned product in exchange {ExchangeId}", exchangeId);
            }
        }

        // Create StockMovement when new product is taken from stock
        private async Task CreateStockMovementForNewProductAsync(
            Guid productId, Guid branchId, Guid exchangeId, decimal quantity,
            decimal previousQuantity, decimal newQuantity)
        {
            try
            {
                var stockMovement = new StockMovement
                {
                    ProductId = productId,
                    BranchId = branchId,
                    TransactionId = exchangeId,
                    MovementType = StockMovementType.Sale,
                    Quantity = quantity, // Positive - removing from stock
                    PreviousQuantity = previousQuantity,
                    NewQuantity = newQuantity,
                    MovementDate = DateTime.UtcNow,
                    Reason = "Product exchange - new product taken"
                };

                await _context.StockMovements.AddAsync(stockMovement);
                _logger.LogDebug("StockMovement created for new product in exchange {ExchangeId}", exchangeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating StockMovement for new product in exchange {ExchangeId}", exchangeId);
            }
        }

        // Create reverse StockMovement when deleting an exchange (for original product)
        private async Task CreateReverseStockMovementForReturnAsync(
            Guid productId, Guid branchId, Guid exchangeId, decimal quantity,
            decimal previousQuantity, decimal newQuantity)
        {
            try
            {
                var stockMovement = new StockMovement
                {
                    ProductId = productId,
                    BranchId = branchId,
                    TransactionId = exchangeId,
                    MovementType = StockMovementType.Sale, // Reverse of return = sale
                    Quantity = quantity, // Positive - removing from stock
                    PreviousQuantity = previousQuantity,
                    NewQuantity = newQuantity,
                    MovementDate = DateTime.UtcNow,
                    Reason = "Exchange deletion - reverse return"
                };

                await _context.StockMovements.AddAsync(stockMovement);
                _logger.LogDebug("Reverse StockMovement created for exchange deletion {ExchangeId}", exchangeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating reverse StockMovement for exchange {ExchangeId}", exchangeId);
            }
        }

        // Create reverse StockMovement when deleting an exchange (for new product)
        private async Task CreateReverseStockMovementForNewProductAsync(
            Guid productId, Guid branchId, Guid exchangeId, decimal quantity,
            decimal previousQuantity, decimal newQuantity)
        {
            try
            {
                var stockMovement = new StockMovement
                {
                    ProductId = productId,
                    BranchId = branchId,
                    TransactionId = exchangeId,
                    MovementType = StockMovementType.Return, // Reverse of sale = return
                    Quantity = quantity, // Positive - adding to stock
                    PreviousQuantity = previousQuantity,
                    NewQuantity = newQuantity,
                    MovementDate = DateTime.UtcNow,
                    Reason = "Exchange deletion - reverse sale"
                };

                await _context.StockMovements.AddAsync(stockMovement);
                _logger.LogDebug("Reverse StockMovement created for exchange deletion {ExchangeId}", exchangeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating reverse StockMovement for exchange {ExchangeId}", exchangeId);
            }
        }

        private async Task<(bool IsAuthorized, Guid UserId, Role Role)> AuthorizeUserAsync()
        {
            try
            {
                var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value
                    ?? _httpContextAccessor.HttpContext?.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
                {
                    _logger.LogWarning("Invalid or missing user ID in JWT");
                    return (false, Guid.Empty, Role.Sales);
                }

                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return (false, Guid.Empty, Role.Sales);
                }

                return (true, userId, user.Role);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error authorizing user");
                return (false, Guid.Empty, Role.Sales);
            }
        }

        #endregion
    }
}