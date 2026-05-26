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
                // Get original transaction
                var originalTransaction = await _context.Transactions
                    .Include(t => t.Product)
                    .Include(t => t.Customer)
                    .Include(t => t.Branch)
                    .FirstOrDefaultAsync(t => t.Id == dto.OriginalTransactionId && t.IsActive);

                if (originalTransaction == null)
                    return ApiResponse<ProductExchangeResponseDto>.Fail("Transaction not found");

                // Get original product info
                var originalProduct = originalTransaction.Product;
                var originalQuantity = originalTransaction.Quantity;
                var originalPrice = originalTransaction.UnitPrice;
                var branchId = originalTransaction.BranchId;

                // ========== CHECK IF THIS IS A CREDIT TRANSACTION ==========
                bool isCreditTransaction = originalTransaction.PaymentMethod == PaymentMethod.Credit;

                // For credit transactions, check if any payments have been made
                if (isCreditTransaction)
                {
                    var paidAmount = await CalculatePaidAmountAsync(originalTransaction.Id);
                    if (paidAmount > 0)
                    {
                        return ApiResponse<ProductExchangeResponseDto>.Fail(
                            "Cannot return/exchange credit transaction with existing payments. Please process refund through payment.");
                    }
                }

                // Determine if this is a return-only or exchange
                bool isReturnOnly = !dto.NewProductId.HasValue || dto.NewQuantity.GetValueOrDefault(0) <= 0;

                // Validate return quantity
                decimal returnQuantity = dto.ReturnQuantity.GetValueOrDefault(originalQuantity);

                if (returnQuantity <= 0)
                    return ApiResponse<ProductExchangeResponseDto>.Fail("Return quantity must be greater than 0");

                if (returnQuantity > originalQuantity)
                    return ApiResponse<ProductExchangeResponseDto>.Fail($"Cannot return more than purchased. Purchased: {originalQuantity}");

                Product newProduct = null;
                decimal? newQuantity = null;
                decimal? newPrice = null;

                if (!isReturnOnly)
                {
                    // Validate new product for exchange
                    newProduct = await _context.Products
                        .FirstOrDefaultAsync(p => p.Id == dto.NewProductId.Value && p.IsActive);

                    if (newProduct == null)
                        return ApiResponse<ProductExchangeResponseDto>.Fail("New product not found");

                    // Validate new quantity is provided for exchange
                    if (!dto.NewQuantity.HasValue)
                        return ApiResponse<ProductExchangeResponseDto>.Fail("New quantity is required for exchange");

                    newQuantity = dto.NewQuantity.Value;

                    if (newQuantity <= 0)
                        return ApiResponse<ProductExchangeResponseDto>.Fail("New quantity must be greater than 0");

                    newPrice = newProduct.UnitPrice;

                    // Check stock for new product (always check, regardless of credit status)
                    var newStock = await _context.Stocks
                        .FirstOrDefaultAsync(s => s.ProductId == newProduct.Id && s.BranchId == branchId);

                    if (newStock == null || newStock.Quantity < newQuantity)
                        return ApiResponse<ProductExchangeResponseDto>.Fail($"Insufficient new product in stock. Available: {newStock?.Quantity ?? 0}");
                }

                // Calculate amounts
                decimal originalTotal = originalPrice * returnQuantity;
                decimal? newTotal = newPrice * newQuantity;
                decimal? moneyDifference = null;
                bool isRefund = false;
                bool isAdditionalPayment = false;
                bool isEvenExchange = false;
                decimal? amount = null;

                if (!isReturnOnly && newTotal.HasValue)
                {
                    moneyDifference = newTotal.Value - originalTotal;

                    if (moneyDifference < 0)
                    {
                        isRefund = true;
                        amount = Math.Abs(moneyDifference.Value);
                    }
                    else if (moneyDifference > 0)
                    {
                        isAdditionalPayment = true;
                        amount = moneyDifference.Value;
                    }
                    else
                    {
                        isEvenExchange = true;
                        amount = 0;
                    }
                }
                else if (isReturnOnly)
                {
                    amount = originalTotal;
                    isRefund = true;
                    moneyDifference = -originalTotal;
                }

                // Create exchange entity
                var exchange = new ProductExchange
                {
                    Id = Guid.NewGuid(),
                    OriginalTransactionId = dto.OriginalTransactionId,
                    OriginalProductId = originalProduct.Id,
                    OriginalQuantity = originalQuantity,
                    OriginalPrice = originalPrice,
                    ReturnQuantity = returnQuantity,
                    NewProductId = newProduct?.Id,
                    NewQuantity = newQuantity,
                    NewPrice = newPrice,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await _context.ProductExchanges.AddAsync(exchange);
                await _context.SaveChangesAsync();

                // ========== STOCK UPDATES - ONLY FOR NON-CREDIT TRANSACTIONS ==========
                if (!isCreditTransaction)
                {
                    // Update stock for original product (add back returned items)
                    var originalStock = await _context.Stocks
                        .FirstOrDefaultAsync(s => s.ProductId == originalProduct.Id && s.BranchId == branchId);

                    if (originalStock != null)
                    {
                        var previousOriginalQuantity = originalStock.Quantity;
                        originalStock.Quantity += returnQuantity;
                        originalStock.UpdatedAt = DateTime.UtcNow;

                        // Create StockMovement for the returned product
                        await CreateStockMovementAsync(
                            originalProduct.Id,
                            branchId,
                            originalTransaction.Id,
                            StockMovementType.Return,
                            returnQuantity, // Positive = adding back to stock
                            previousOriginalQuantity,
                            originalStock.Quantity,
                            $"Product {(isReturnOnly ? "return" : "exchange")} #{exchange.Id} - {returnQuantity} items returned"
                        );
                    }
                    else
                    {
                        // Create new stock record for the product
                        var newStock = new Stock
                        {
                            Id = Guid.NewGuid(),
                            ProductId = originalProduct.Id,
                            BranchId = branchId,
                            Quantity = returnQuantity,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        await _context.Stocks.AddAsync(newStock);

                        await CreateStockMovementAsync(
                            originalProduct.Id,
                            branchId,
                            originalTransaction.Id,
                            StockMovementType.Return,
                            returnQuantity,
                            0,
                            returnQuantity,
                            $"Product {(isReturnOnly ? "return" : "exchange")} #{exchange.Id} - New stock record created with {returnQuantity} items"
                        );
                    }

                    // For exchange: remove new product from stock
                    if (!isReturnOnly && newProduct != null && newQuantity.HasValue)
                    {
                        var newStock = await _context.Stocks
                            .FirstOrDefaultAsync(s => s.ProductId == newProduct.Id && s.BranchId == branchId);

                        if (newStock != null)
                        {
                            var previousNewQuantity = newStock.Quantity;
                            newStock.Quantity -= newQuantity.Value;
                            newStock.UpdatedAt = DateTime.UtcNow;

                            await CreateStockMovementAsync(
                                newProduct.Id,
                                branchId,
                                originalTransaction.Id,
                                StockMovementType.Sale,
                                -newQuantity.Value, // Negative for removal
                                previousNewQuantity,
                                newStock.Quantity,
                                $"Product exchange #{exchange.Id} - {newQuantity} items taken"
                            );
                        }
                    }
                }
                else
                {
                    // For credit transactions, just log that no stock changes were made
                    _logger.LogInformation("Credit transaction exchange/return - no stock changes. TransactionId: {TransactionId}, ExchangeId: {ExchangeId}",
                        originalTransaction.Id, exchange.Id);
                }

                // Update original transaction quantity
                decimal newTransactionQuantity = originalTransaction.Quantity - returnQuantity;
                originalTransaction.Quantity = newTransactionQuantity;
                originalTransaction.UpdatedAt = DateTime.UtcNow;

                if (newTransactionQuantity <= 0)
                {
                    originalTransaction.IsActive = false;
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                // Load related data for response
                await LoadNavigationPropertiesAsync(exchange);

                // Map to response
                var result = _mapper.Map<ProductExchangeResponseDto>(exchange);
                result.IsReturnOnly = isReturnOnly;
                result.OriginalTotal = originalTotal;
                result.NewTotal = newTotal;
                result.MoneyDifference = moneyDifference;
                result.Status = "Created";
                result.IsCreditTransaction = isCreditTransaction;

                _logger.LogInformation("{Type} created: {ExchangeId} for {TransactionType} transaction. " +
                    "ReturnQty: {ReturnQty}, NewQty: {NewQty}, StockChanges: {StockChanges}",
                    isReturnOnly ? "Return" : "Exchange",
                    exchange.Id,
                    isCreditTransaction ? "Credit" : "Cash/Bank",
                    returnQuantity,
                    newQuantity,
                    !isCreditTransaction ? "Yes" : "No (Credit)");

                return ApiResponse<ProductExchangeResponseDto>.Success(
                    result,
                    isReturnOnly ? "Return processed successfully" : "Exchange created successfully");
            }
            catch (Exception ex)
            {
                try { await dbTransaction.RollbackAsync(); } catch { }
                _logger.LogError(ex, "Error creating exchange/return");
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

                var originalTotal = exchange.OriginalPrice * exchange.ReturnQuantity;
                decimal? newTotal = null;
                decimal? moneyDifference = null;
                bool isReturnOnly = !exchange.NewProductId.HasValue;
                bool isCreditTransaction = exchange.OriginalTransaction?.PaymentMethod == PaymentMethod.Credit;

                if (!isReturnOnly && exchange.NewPrice.HasValue && exchange.NewQuantity.HasValue)
                {
                    newTotal = exchange.NewPrice.Value * exchange.NewQuantity.Value;
                    moneyDifference = newTotal - originalTotal;
                }
                else if (isReturnOnly)
                {
                    moneyDifference = -originalTotal;
                }

                var result = _mapper.Map<ProductExchangeResponseDto>(exchange);
                result.OriginalTotal = originalTotal;
                result.NewTotal = newTotal;
                result.MoneyDifference = moneyDifference;
                result.IsReturnOnly = isReturnOnly;
                result.IsCreditTransaction = isCreditTransaction;
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

                var result = exchanges.Select(exchange =>
                {
                    var originalTotal = exchange.OriginalPrice * exchange.ReturnQuantity;
                    decimal? newTotal = null;
                    decimal? moneyDifference = null;
                    bool isReturnOnly = !exchange.NewProductId.HasValue;
                    bool isCreditTransaction = exchange.OriginalTransaction?.PaymentMethod == PaymentMethod.Credit;

                    if (!isReturnOnly && exchange.NewPrice.HasValue && exchange.NewQuantity.HasValue)
                    {
                        newTotal = exchange.NewPrice.Value * exchange.NewQuantity.Value;
                        moneyDifference = newTotal - originalTotal;
                    }
                    else if (isReturnOnly)
                    {
                        moneyDifference = -originalTotal;
                    }

                    var dto = _mapper.Map<ProductExchangeResponseDto>(exchange);
                    dto.OriginalTotal = originalTotal;
                    dto.NewTotal = newTotal;
                    dto.MoneyDifference = moneyDifference;
                    dto.IsReturnOnly = isReturnOnly;
                    dto.IsCreditTransaction = isCreditTransaction;
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
                    .Include(e => e.OriginalProduct)
                    .Include(e => e.NewProduct)
                    .FirstOrDefaultAsync(e => e.Id == id && e.IsActive);

                if (exchange == null)
                    return ApiResponse<bool>.Fail("Exchange not found");

                bool isCreditTransaction = exchange.OriginalTransaction?.PaymentMethod == PaymentMethod.Credit;
                var branchId = exchange.OriginalTransaction.BranchId;
                bool isReturnOnly = !exchange.NewProductId.HasValue;

                // ========== REVERSE STOCK CHANGES - ONLY FOR NON-CREDIT TRANSACTIONS ==========
                if (!isCreditTransaction)
                {
                    // Reverse original product stock (remove the returned items)
                    var originalStock = await _context.Stocks
                        .FirstOrDefaultAsync(s => s.ProductId == exchange.OriginalProductId &&
                                                s.BranchId == branchId);

                    if (originalStock != null)
                    {
                        var currentOriginalQuantity = originalStock.Quantity;
                        originalStock.Quantity -= exchange.ReturnQuantity;
                        originalStock.UpdatedAt = DateTime.UtcNow;

                        await CreateStockMovementAsync(
                            exchange.OriginalProductId,
                            branchId,
                            exchange.OriginalTransactionId,
                            StockMovementType.Adjustment,
                            -exchange.ReturnQuantity, // Negative for removal
                            currentOriginalQuantity,
                            originalStock.Quantity,
                            $"Exchange #{exchange.Id} deletion - reverse return"
                        );
                    }

                    // Reverse new product stock (add back taken items) - only for exchanges
                    if (!isReturnOnly && exchange.NewProductId.HasValue && exchange.NewQuantity.HasValue)
                    {
                        var newStock = await _context.Stocks
                            .FirstOrDefaultAsync(s => s.ProductId == exchange.NewProductId.Value &&
                                                    s.BranchId == branchId);

                        if (newStock != null)
                        {
                            var currentNewQuantity = newStock.Quantity;
                            newStock.Quantity += exchange.NewQuantity.Value;
                            newStock.UpdatedAt = DateTime.UtcNow;

                            await CreateStockMovementAsync(
                                exchange.NewProductId.Value,
                                branchId,
                                exchange.OriginalTransactionId,
                                StockMovementType.Adjustment,
                                exchange.NewQuantity.Value, // Positive for adding back
                                currentNewQuantity,
                                newStock.Quantity,
                                $"Exchange #{exchange.Id} deletion - reverse sale"
                            );
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("Deleting exchange for credit transaction - no stock changes to reverse. ExchangeId: {ExchangeId}", id);
                }

                // Restore original transaction quantity
                if (exchange.OriginalTransaction != null)
                {
                    exchange.OriginalTransaction.Quantity += exchange.ReturnQuantity;
                    exchange.OriginalTransaction.UpdatedAt = DateTime.UtcNow;
                    exchange.OriginalTransaction.IsActive = true;
                }

                // Soft delete
                exchange.IsActive = false;
                exchange.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully deleted exchange: {ExchangeId} (Credit: {IsCredit})", id, isCreditTransaction);
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

                var result = exchanges.Select(exchange =>
                {
                    var originalTotal = exchange.OriginalPrice * exchange.ReturnQuantity;
                    decimal? newTotal = null;
                    decimal? moneyDifference = null;
                    bool isReturnOnly = !exchange.NewProductId.HasValue;
                    bool isCreditTransaction = exchange.OriginalTransaction?.PaymentMethod == PaymentMethod.Credit;

                    if (!isReturnOnly && exchange.NewPrice.HasValue && exchange.NewQuantity.HasValue)
                    {
                        newTotal = exchange.NewPrice.Value * exchange.NewQuantity.Value;
                        moneyDifference = newTotal - originalTotal;
                    }
                    else if (isReturnOnly)
                    {
                        moneyDifference = -originalTotal;
                    }

                    var dto = _mapper.Map<ProductExchangeResponseDto>(exchange);
                    dto.OriginalTotal = originalTotal;
                    dto.NewTotal = newTotal;
                    dto.MoneyDifference = moneyDifference;
                    dto.IsReturnOnly = isReturnOnly;
                    dto.IsCreditTransaction = isCreditTransaction;
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

                var result = exchanges.Select(exchange =>
                {
                    var originalTotal = exchange.OriginalPrice * exchange.ReturnQuantity;
                    decimal? newTotal = null;
                    decimal? moneyDifference = null;
                    bool isReturnOnly = !exchange.NewProductId.HasValue;
                    bool isCreditTransaction = exchange.OriginalTransaction?.PaymentMethod == PaymentMethod.Credit;

                    if (!isReturnOnly && exchange.NewPrice.HasValue && exchange.NewQuantity.HasValue)
                    {
                        newTotal = exchange.NewPrice.Value * exchange.NewQuantity.Value;
                        moneyDifference = newTotal - originalTotal;
                    }
                    else if (isReturnOnly)
                    {
                        moneyDifference = -originalTotal;
                    }

                    var dto = _mapper.Map<ProductExchangeResponseDto>(exchange);
                    dto.OriginalTotal = originalTotal;
                    dto.NewTotal = newTotal;
                    dto.MoneyDifference = moneyDifference;
                    dto.IsReturnOnly = isReturnOnly;
                    dto.IsCreditTransaction = isCreditTransaction;
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
                    TotalRefundAmount = exchanges.Where(e => e.IsRefund).Sum(e => e.Amount ?? 0),
                    TotalAdditionalPayment = exchanges.Where(e => e.IsAdditionalPayment).Sum(e => e.Amount ?? 0),
                };

                var recentExchanges = exchanges
                    .OrderByDescending(e => e.CreatedAt)
                    .Take(10)
                    .ToList();

                summary.RecentExchanges = recentExchanges.Select(exchange =>
                {
                    var originalTotal = exchange.OriginalPrice * exchange.ReturnQuantity;
                    decimal? newTotal = null;
                    decimal? moneyDifference = null;
                    bool isReturnOnly = !exchange.NewProductId.HasValue;
                    bool isCreditTransaction = exchange.OriginalTransaction?.PaymentMethod == PaymentMethod.Credit;

                    if (!isReturnOnly && exchange.NewPrice.HasValue && exchange.NewQuantity.HasValue)
                    {
                        newTotal = exchange.NewPrice.Value * exchange.NewQuantity.Value;
                        moneyDifference = newTotal - originalTotal;
                    }
                    else if (isReturnOnly)
                    {
                        moneyDifference = -originalTotal;
                    }

                    var dto = _mapper.Map<ProductExchangeResponseDto>(exchange);
                    dto.OriginalTotal = originalTotal;
                    dto.NewTotal = newTotal;
                    dto.MoneyDifference = moneyDifference;
                    dto.IsReturnOnly = isReturnOnly;
                    dto.IsCreditTransaction = isCreditTransaction;
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

        public async Task<ApiResponse<List<ProductExchangeResponseDto>>> GetReturnsOnlyAsync(
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
                    .Where(e => e.IsActive && !e.NewProductId.HasValue)
                    .AsQueryable();

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

                var returns = await query
                    .OrderByDescending(e => e.CreatedAt)
                    .ToListAsync();

                var result = returns.Select(exchange =>
                {
                    var originalTotal = exchange.OriginalPrice * exchange.ReturnQuantity;
                    bool isCreditTransaction = exchange.OriginalTransaction?.PaymentMethod == PaymentMethod.Credit;

                    var dto = _mapper.Map<ProductExchangeResponseDto>(exchange);
                    dto.OriginalTotal = originalTotal;
                    dto.MoneyDifference = -originalTotal;
                    dto.IsReturnOnly = true;
                    dto.IsCreditTransaction = isCreditTransaction;
                    dto.Status = "Return";
                    return dto;
                }).ToList();

                return ApiResponse<List<ProductExchangeResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting returns only");
                return ApiResponse<List<ProductExchangeResponseDto>>.Fail($"Error retrieving returns: {ex.Message}");
            }
        }

        #region Helper Methods

        private async Task<decimal> CalculatePaidAmountAsync(Guid transactionId)
        {
            var payments = await _context.CreditPayments
                .Where(cp => cp.TransactionId == transactionId)
                .SumAsync(cp => cp.Amount);
            return payments;
        }

        private async Task CreateStockMovementAsync(
            Guid productId,
            Guid branchId,
            Guid transactionId,
            StockMovementType movementType,
            decimal quantity,
            decimal previousQuantity,
            decimal newQuantity,
            string reason)
        {
            try
            {
                var stockMovement = new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    BranchId = branchId,
                    TransactionId = transactionId,
                    MovementType = movementType,
                    Quantity = quantity,
                    PreviousQuantity = previousQuantity,
                    NewQuantity = newQuantity,
                    MovementDate = DateTime.UtcNow,
                    Reason = reason,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _context.StockMovements.AddAsync(stockMovement);
                _logger.LogDebug("StockMovement created: Product={ProductId}, Branch={BranchId}, " +
                    "PreviousQty={PreviousQty}, NewQty={NewQty}, Change={Change}, Type={Type}, Reason={Reason}",
                    productId, branchId, previousQuantity, newQuantity, quantity, movementType, reason);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating StockMovement for ProductId: {ProductId}", productId);
                throw;
            }
        }

        private async Task LoadNavigationPropertiesAsync(ProductExchange exchange)
        {
            await _context.Entry(exchange)
                .Reference(e => e.OriginalTransaction).LoadAsync();

            if (exchange.OriginalTransaction != null)
            {
                await _context.Entry(exchange.OriginalTransaction)
                    .Reference(t => t.Customer).LoadAsync();
                await _context.Entry(exchange.OriginalTransaction)
                    .Reference(t => t.Branch).LoadAsync();
            }

            await _context.Entry(exchange)
                .Reference(e => e.OriginalProduct).LoadAsync();

            if (exchange.NewProductId.HasValue)
            {
                await _context.Entry(exchange)
                    .Reference(e => e.NewProduct).LoadAsync();
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