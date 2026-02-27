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
    public class TransactionService : ITransactionService
    {
        private readonly ShopDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMapper _mapper;
        private readonly ILogger<TransactionService> _logger;

        public TransactionService(
            ShopDbContext context,
            IMapper mapper,
            ILogger<TransactionService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public async Task<ApiResponse<TransactionResponseDto>> CreateTransactionAsync(CreateTransactionDto dto)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate user authorization
                var (isAuthorized, userId, role) = await AuthorizeUserAsync();
                if (!isAuthorized || (role != Role.Manager && role != Role.Sales))
                {
                    return ApiResponse<TransactionResponseDto>.Fail("Only Manager or Sales can create transactions");
                }

                // Validate branch
                var branch = await _context.Branches
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.Id == dto.BranchId && b.IsActive);
                if (branch == null)
                    return ApiResponse<TransactionResponseDto>.Fail("Invalid or inactive branch");

                // Validate product
                var product = await _context.Products
                    .Include(p => p.Category)
                    .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.IsActive);
                if (product == null)
                    return ApiResponse<TransactionResponseDto>.Fail("Invalid or inactive product");

                // Handle Customer (create if not exists)
                Guid? customerId = dto.CustomerId;
                Customer newCustomer = null;
                if (!customerId.HasValue && !string.IsNullOrWhiteSpace(dto.CustomerName) && !string.IsNullOrWhiteSpace(dto.CustomerPhoneNumber))
                {
                    var existingCustomer = await _context.Customers
                        .FirstOrDefaultAsync(c => c.PhoneNumber == dto.CustomerPhoneNumber.Trim());

                    if (existingCustomer != null)
                    {
                        customerId = existingCustomer.Id;
                    }
                    else
                    {
                        newCustomer = new Customer
                        {
                            Name = dto.CustomerName.Trim(),
                            PhoneNumber = dto.CustomerPhoneNumber.Trim()
                        };
                        await _context.Customers.AddAsync(newCustomer);
                    }
                }

                // Handle Painter (create if not exists)
                Guid? painterId = dto.PainterId;
                Painter newPainter = null;
                if (!painterId.HasValue && !string.IsNullOrWhiteSpace(dto.PainterName) && !string.IsNullOrWhiteSpace(dto.PainterPhoneNumber))
                {
                    var existingPainter = await _context.Painters
                        .FirstOrDefaultAsync(p => p.PhoneNumber == dto.PainterPhoneNumber.Trim());

                    if (existingPainter != null)
                    {
                        painterId = existingPainter.Id;
                    }
                    else
                    {
                        newPainter = new Painter
                        {
                            Name = dto.PainterName.Trim(),
                            PhoneNumber = dto.PainterPhoneNumber.Trim()
                        };
                        await _context.Painters.AddAsync(newPainter);
                    }
                }

                // Validate stock for non-credit transactions
                if (dto.PaymentMethod != PaymentMethod.Credit &&
                    !await ValidateStockAsync(dto.ProductId, dto.Quantity, dto.BranchId))
                {
                    return ApiResponse<TransactionResponseDto>.Fail("Insufficient stock");
                }

                // Create sales transaction using AutoMapper
                var salesTransaction = _mapper.Map<Transaction>(dto);
                salesTransaction.CustomerId = customerId;
                salesTransaction.PainterId = painterId;

                // If we created new customer/painter, we need to get their IDs after saving
                if (newCustomer != null || newPainter != null)
                {
                    await _context.SaveChangesAsync();

                    if (newCustomer != null)
                        salesTransaction.CustomerId = newCustomer.Id;
                    if (newPainter != null)
                        salesTransaction.PainterId = newPainter.Id;
                }

                salesTransaction.Validate();

                await _context.Transactions.AddAsync(salesTransaction);

                // Update stock for non-credit transactions
                if (dto.PaymentMethod != PaymentMethod.Credit)
                {
                    await UpdateStockAsync(dto.ProductId, dto.Quantity, dto.BranchId, salesTransaction.Id);
                }

                // Create daily sales ONLY for non-credit transactions
                // Credit transactions will create daily sales when payment is made
                if (dto.PaymentMethod != PaymentMethod.Credit)
                {
                    await CreateDailySalesAsync(salesTransaction, false);
                }

                // Create StockMovement record for non-credit transactions
                if (dto.PaymentMethod != PaymentMethod.Credit)
                {
                    await CreateStockMovementAsync(salesTransaction);
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                // Load related data for response
                await LoadTransactionNavigationProperties(salesTransaction);

                var result = _mapper.Map<TransactionResponseDto>(salesTransaction);

                // Calculate paid amount for credit transactions
                if (salesTransaction.PaymentMethod == PaymentMethod.Credit)
                {
                    result.PaidAmount = await CalculatePaidAmountAsync(salesTransaction.Id);
                }

                _logger.LogInformation("Successfully created sales transaction {TransactionId}", salesTransaction.Id);
                return ApiResponse<TransactionResponseDto>.Success(result, "Transaction created successfully");
            }
            catch (Exception ex)
            {
                try
                {
                    await dbTransaction.RollbackAsync();
                    _logger.LogInformation("Transaction rolled back due to error: {ErrorMessage}", ex.Message);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogWarning(rollbackEx, "Error rolling back transaction (may already be rolled back)");
                }

                _logger.LogError(ex, "Error creating sales transaction");
                return ApiResponse<TransactionResponseDto>.Fail($"Error creating transaction: {ex.Message}");
            }
        }

        public async Task<ApiResponse<TransactionResponseDto>> GetTransactionByIdAsync(Guid id)
        {
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.Branch)
                    .Include(t => t.Product)
                        .ThenInclude(p => p.Category)
                    .Include(t => t.Customer)
                    .Include(t => t.Painter)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (transaction == null)
                    return ApiResponse<TransactionResponseDto>.Fail("Transaction not found");

                var result = _mapper.Map<TransactionResponseDto>(transaction);

                if (transaction.PaymentMethod == PaymentMethod.Credit)
                {
                    result.PaidAmount = await CalculatePaidAmountAsync(transaction.Id);
                }

                return ApiResponse<TransactionResponseDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction by ID: {TransactionId}", id);
                return ApiResponse<TransactionResponseDto>.Fail($"Error retrieving transaction: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<TransactionResponseDto>>> GetAllTransactionsAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            Guid? branchId = null)
        {
            try
            {
                // Get all DailySales entries for the date range (these represent ACTUAL payments/sales)
                var dailySalesQuery = _context.DailySales
                    .Include(ds => ds.Transaction)
                        .ThenInclude(t => t.Branch)
                    .Include(ds => ds.Transaction)
                        .ThenInclude(t => t.Product)
                            .ThenInclude(p => p.Category)
                    .Include(ds => ds.Transaction)
                        .ThenInclude(t => t.Customer)
                    .Include(ds => ds.Transaction)
                        .ThenInclude(t => t.Painter)
                    .Where(ds => ds.IsActive)
                    .AsQueryable();

                // Apply date filters if provided
                if (startDate.HasValue)
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.SaleDate.Date >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.SaleDate.Date <= endDate.Value.Date);
                }

                if (branchId.HasValue)
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.BranchId == branchId.Value);
                }

                var dailySales = await dailySalesQuery
                    .OrderByDescending(ds => ds.SaleDate)
                    .ThenByDescending(ds => ds.CreatedAt)
                    .ToListAsync();

                // Transform DailySales into TransactionResponseDto format
                var result = new List<TransactionResponseDto>();

                foreach (var dailySale in dailySales)
                {
                    if (dailySale.Transaction != null)
                    {
                        var transactionDto = _mapper.Map<TransactionResponseDto>(dailySale.Transaction);

                        // Override with DailySales data (actual sold/payment data)
                        transactionDto.Quantity = dailySale.Quantity;
                        transactionDto.UnitPrice = dailySale.UnitPrice;
                        transactionDto.TotalAmount = dailySale.TotalAmount;
                        transactionDto.CommissionAmount = dailySale.CommissionAmount;
                        transactionDto.CommissionPaid = dailySale.CommissionPaid;
                        transactionDto.TransactionDate = dailySale.SaleDate;

                        // For credit payments, mark as partial if applicable
                        if (dailySale.IsCreditPayment)
                        {
                            transactionDto.IsPartialPayment = dailySale.IsPartialPayment;
                            transactionDto.PaidAmount = await CalculatePaidAmountAsync(dailySale.TransactionId);
                        }
                        else
                        {
                            transactionDto.IsPartialPayment = false;
                            if (dailySale.Transaction.PaymentMethod == PaymentMethod.Credit)
                            {
                                transactionDto.PaidAmount = await CalculatePaidAmountAsync(dailySale.TransactionId);
                            }
                        }

                        result.Add(transactionDto);
                    }
                }

                return ApiResponse<List<TransactionResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all transactions");
                return ApiResponse<List<TransactionResponseDto>>.Fail($"Error retrieving transactions: {ex.Message}");
            }
        }

        public async Task<ApiResponse<TransactionResponseDto>> UpdateTransactionAsync(UpdateTransactionDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingTransaction = await _context.Transactions
                    .Include(t => t.Product)
                    .FirstOrDefaultAsync(t => t.Id == dto.Id);

                if (existingTransaction == null)
                    return ApiResponse<TransactionResponseDto>.Fail("Transaction not found");

                // Check if transaction can be updated (e.g., not fully paid credit transaction)
                if (existingTransaction.PaymentMethod == PaymentMethod.Credit)
                {
                    var paidAmount = await CalculatePaidAmountAsync(existingTransaction.Id);
                    if (paidAmount > 0)
                    {
                        return ApiResponse<TransactionResponseDto>.Fail("Cannot update credit transaction with existing payments");
                    }
                }

                // Store old values for comparison
                var oldQuantity = existingTransaction.Quantity;
                var oldUnitPrice = existingTransaction.UnitPrice;
                var oldPaymentMethod = existingTransaction.PaymentMethod;
                var oldCustomerId = existingTransaction.CustomerId;
                var oldPainterId = existingTransaction.PainterId;
                var oldCommissionRate = existingTransaction.CommissionRate;
                var oldCommissionPaid = existingTransaction.CommissionPaid;

                // Update properties
                if (dto.CustomerId.HasValue) existingTransaction.CustomerId = dto.CustomerId;
                if (dto.PainterId.HasValue) existingTransaction.PainterId = dto.PainterId;
                if (dto.UnitPrice.HasValue) existingTransaction.UnitPrice = dto.UnitPrice.Value;
                if (dto.Quantity.HasValue) existingTransaction.Quantity = dto.Quantity.Value;
                if (dto.PaymentMethod.HasValue) existingTransaction.PaymentMethod = dto.PaymentMethod.Value;
                if (dto.CommissionRate.HasValue) existingTransaction.CommissionRate = dto.CommissionRate.Value;
                if (dto.CommissionPaid.HasValue) existingTransaction.CommissionPaid = dto.CommissionPaid.Value;

                existingTransaction.UpdatedAt = DateTime.UtcNow;
                existingTransaction.Validate();

                // Handle stock updates if quantity changed
                if (dto.Quantity.HasValue && dto.Quantity.Value != oldQuantity)
                {
                    var quantityDifference = dto.Quantity.Value - oldQuantity;

                    // For non-credit transactions, update stock
                    if (existingTransaction.PaymentMethod != PaymentMethod.Credit)
                    {
                        var stock = await _context.Stocks
                            .FirstOrDefaultAsync(s => s.ProductId == existingTransaction.ProductId &&
                                                     s.BranchId == existingTransaction.BranchId);

                        if (stock != null)
                        {
                            // Check if we have enough stock for increase
                            if (quantityDifference > 0 && stock.Quantity < quantityDifference)
                            {
                                return ApiResponse<TransactionResponseDto>.Fail($"Insufficient stock. Available: {stock.Quantity}, Needed: {quantityDifference}");
                            }

                            stock.Quantity -= quantityDifference; // Subtract difference (negative = add back to stock)
                            stock.UpdatedAt = DateTime.UtcNow;

                            // Create stock movement for the adjustment
                            var stockMovement = new StockMovement
                            {
                                Id = Guid.NewGuid(),
                                ProductId = existingTransaction.ProductId,
                                BranchId = existingTransaction.BranchId,
                                TransactionId = existingTransaction.Id,
                                MovementType = StockMovementType.Adjustment,
                                Quantity = -quantityDifference, // Positive = adding back to stock, Negative = removing more
                                PreviousQuantity = stock.Quantity + quantityDifference,
                                NewQuantity = stock.Quantity,
                                MovementDate = DateTime.UtcNow,
                                Reason = $"Transaction update - Quantity changed from {oldQuantity} to {dto.Quantity.Value}",
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow,
                                IsActive = true
                            };
                            await _context.StockMovements.AddAsync(stockMovement);
                        }
                    }
                }

                // Update DailySales entries
                var existingDailySales = await _context.DailySales
                    .Where(ds => ds.TransactionId == existingTransaction.Id)
                    .ToListAsync();

                if (existingDailySales.Any())
                {
                    foreach (var dailySale in existingDailySales)
                    {
                        // Update basic fields
                        dailySale.ProductId = existingTransaction.ProductId;
                        dailySale.CustomerId = existingTransaction.CustomerId;
                        dailySale.PainterId = existingTransaction.PainterId;
                        dailySale.Quantity = existingTransaction.Quantity;
                        dailySale.UnitPrice = existingTransaction.UnitPrice;
                        dailySale.TotalAmount = existingTransaction.UnitPrice * existingTransaction.Quantity;
                        dailySale.CommissionRate = existingTransaction.CommissionRate;
                        dailySale.CommissionAmount = existingTransaction.Quantity * existingTransaction.CommissionRate;
                        dailySale.CommissionPaid = existingTransaction.CommissionPaid;
                        dailySale.PaymentMethod = existingTransaction.PaymentMethod;
                        dailySale.UpdatedAt = DateTime.UtcNow;

                        _context.DailySales.Update(dailySale);
                    }
                }
                else
                {
                    // If no DailySales exist, create one (for non-credit transactions)
                    if (existingTransaction.PaymentMethod != PaymentMethod.Credit)
                    {
                        await CreateDailySalesAsync(existingTransaction, false);
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Reload related data
                await LoadTransactionNavigationProperties(existingTransaction);

                var result = _mapper.Map<TransactionResponseDto>(existingTransaction);

                if (existingTransaction.PaymentMethod == PaymentMethod.Credit)
                {
                    result.PaidAmount = await CalculatePaidAmountAsync(existingTransaction.Id);
                }

                _logger.LogInformation("Successfully updated transaction {TransactionId} and related DailySales", existingTransaction.Id);
                return ApiResponse<TransactionResponseDto>.Success(result, "Transaction updated successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating transaction: {TransactionId}", dto.Id);
                return ApiResponse<TransactionResponseDto>.Fail($"Error updating transaction: {ex.Message}");
            }
        }
        public async Task<ApiResponse<bool>> DeleteTransactionAsync(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingTransaction = await _context.Transactions
                    .Include(t => t.Product)
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (existingTransaction == null)
                    return ApiResponse<bool>.Fail("Transaction not found");

                // Check if transaction can be deleted (e.g., not already processed)
                if (existingTransaction.PaymentMethod == PaymentMethod.Credit && await CalculatePaidAmountAsync(id) > 0)
                {
                    return ApiResponse<bool>.Fail("Cannot delete credit transaction with payments");
                }

                // Restore stock for non-credit transactions
                if (existingTransaction.PaymentMethod != PaymentMethod.Credit)
                {
                    var stock = await _context.Stocks
                        .FirstOrDefaultAsync(s => s.ProductId == existingTransaction.ProductId &&
                                                 s.BranchId == existingTransaction.BranchId);

                    if (stock != null)
                    {
                        var previousQuantity = stock.Quantity;
                        stock.Quantity += existingTransaction.Quantity;
                        stock.UpdatedAt = DateTime.UtcNow;

                        // Create stock movement for deletion
                        var stockMovement = new StockMovement
                        {
                            Id = Guid.NewGuid(),
                            ProductId = existingTransaction.ProductId,
                            BranchId = existingTransaction.BranchId,
                            TransactionId = existingTransaction.Id,
                            MovementType = StockMovementType.Adjustment,
                            Quantity = existingTransaction.Quantity, // Positive = adding back to stock
                            PreviousQuantity = previousQuantity,
                            NewQuantity = stock.Quantity,
                            MovementDate = DateTime.UtcNow,
                            Reason = $"Transaction deleted - {existingTransaction.ItemCode}",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        await _context.StockMovements.AddAsync(stockMovement);
                    }
                }

                // Delete associated DailySales entries
                var dailySales = await _context.DailySales
                    .Where(ds => ds.TransactionId == id)
                    .ToListAsync();

                if (dailySales.Any())
                {
                    _context.DailySales.RemoveRange(dailySales);
                    _logger.LogInformation("Deleted {Count} DailySales entries for transaction {TransactionId}",
                        dailySales.Count, id);
                }

                // Delete associated CreditPayments
                var creditPayments = await _context.CreditPayments
                    .Where(cp => cp.TransactionId == id)
                    .ToListAsync();

                if (creditPayments.Any())
                {
                    _context.CreditPayments.RemoveRange(creditPayments);
                }

                _context.Transactions.Remove(existingTransaction);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Successfully deleted transaction: {TransactionId}", id);
                return ApiResponse<bool>.Success(true, "Transaction deleted successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting transaction: {TransactionId}", id);
                return ApiResponse<bool>.Fail($"Error deleting transaction: {ex.Message}");
            }
        }
        public async Task<ApiResponse<TransactionResponseDto>> PayCreditAsync(PayCreditDto dto)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var creditTransaction = await _context.Transactions
                    .Include(t => t.Product)
                    .FirstOrDefaultAsync(t => t.Id == dto.TransactionId && t.PaymentMethod == PaymentMethod.Credit);

                if (creditTransaction == null)
                    return ApiResponse<TransactionResponseDto>.Fail("Credit transaction not found");

                var previousPaidAmount = await CalculatePaidAmountAsync(creditTransaction.Id);
                var totalAmount = CalculateTotalAmount(creditTransaction);
                var remainingAmount = totalAmount - previousPaidAmount;

                if (dto.Amount > remainingAmount)
                    return ApiResponse<TransactionResponseDto>.Fail($"Payment amount exceeds remaining balance. Remaining: {remainingAmount}");

                // Calculate what portion of the payment is being made
                var paymentPercentage = dto.Amount / totalAmount;
                var paidQuantity = creditTransaction.Quantity * paymentPercentage;

                // Create credit payment record
                var creditPayment = new CreditPayment
                {
                    Id = Guid.NewGuid(),
                    TransactionId = dto.TransactionId,
                    Amount = dto.Amount,
                    PaymentMethod = dto.PaymentMethod,
                    PaymentDate = dto.PaymentDate,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _context.CreditPayments.AddAsync(creditPayment);

                // IMPORTANT: Create DailySales entry for ONLY the newly paid amount
                // This makes only the paid portion appear in daily reports
                var dailySales = new DailySales
                {
                    Id = Guid.NewGuid(),
                    BranchId = creditTransaction.BranchId,
                    ProductId = creditTransaction.ProductId,
                    TransactionId = creditTransaction.Id,
                    SaleDate = DateTime.UtcNow.Date, // Date of payment (today)
                    Quantity = paidQuantity, // Only the paid quantity
                    UnitPrice = creditTransaction.UnitPrice,
                    TotalAmount = dto.Amount, // Only the paid amount
                    PaymentMethod = dto.PaymentMethod, // Payment method used for this payment
                    CommissionRate = creditTransaction.CommissionRate,
                    CommissionAmount = paidQuantity * creditTransaction.CommissionRate, // Commission for paid portion
                    CommissionPaid = creditTransaction.CommissionPaid,
                    CustomerId = creditTransaction.CustomerId,
                    PainterId = creditTransaction.PainterId,
                    IsPartialPayment = (dto.Amount < totalAmount), // Mark as partial if not fully paid
                    IsCreditPayment = true, // Mark as credit payment
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                await _context.DailySales.AddAsync(dailySales);

                // Update stock proportionally to the payment
                if (dto.PaymentMethod != PaymentMethod.Credit) // Only reduce stock for cash/bank payments
                {
                    await UpdateStockAsync(creditTransaction.ProductId, paidQuantity,
                        creditTransaction.BranchId, creditTransaction.Id);

                    // Create StockMovement for the payment
                    await CreateStockMovementForPaymentAsync(creditTransaction, paidQuantity);
                }

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                // Reload and return updated transaction
                await LoadTransactionNavigationProperties(creditTransaction);
                var result = _mapper.Map<TransactionResponseDto>(creditTransaction);
                result.PaidAmount = await CalculatePaidAmountAsync(creditTransaction.Id);

                _logger.LogInformation("Credit payment of {Amount} processed for transaction: {TransactionId}", dto.Amount, dto.TransactionId);
                return ApiResponse<TransactionResponseDto>.Success(result, "Credit payment processed successfully");
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                _logger.LogError(ex, "Error processing credit payment for transaction: {TransactionId}", dto.TransactionId);
                return ApiResponse<TransactionResponseDto>.Fail($"Error processing credit payment: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<CreditTransactionHistoryDto>>> GetCreditTransactionHistoryAsync(Guid? customerId)
        {
            try
            {
                var query = _context.Transactions
                    .Include(t => t.Branch)
                    .Include(t => t.Product)
                    .Include(t => t.Customer)
                    .Where(t => t.PaymentMethod == PaymentMethod.Credit);

                if (customerId.HasValue)
                {
                    query = query.Where(t => t.CustomerId == customerId.Value);
                }

                var transactions = await query
                    .OrderByDescending(t => t.TransactionDate)
                    .ToListAsync();

                var result = new List<CreditTransactionHistoryDto>();

                foreach (var transaction in transactions)
                {
                    var paidAmount = await CalculatePaidAmountAsync(transaction.Id);
                    var history = new CreditTransactionHistoryDto
                    {
                        TransactionId = transaction.Id,
                        TransactionDate = transaction.TransactionDate,
                        ItemCode = transaction.ItemCode,
                        ProductName = transaction.Product.Name,
                        Quantity = transaction.Quantity,
                        UnitPrice = transaction.UnitPrice,
                        TotalAmount = transaction.Quantity * transaction.UnitPrice,
                        PaidAmount = paidAmount,
                        CustomerId = transaction.CustomerId ?? Guid.Empty,
                        CustomerName = transaction.Customer?.Name ?? "Unknown",
                        CustomerPhoneNumber = transaction.Customer?.PhoneNumber ?? "Unknown",
                        BranchId = transaction.BranchId,
                        BranchName = transaction.Branch.Name,
                        LastPaymentDate = await GetLastPaymentDateAsync(transaction.Id)
                    };
                    result.Add(history);
                }

                return ApiResponse<List<CreditTransactionHistoryDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting credit transaction history for customer: {CustomerId}", customerId);
                return ApiResponse<List<CreditTransactionHistoryDto>>.Fail($"Error retrieving credit history: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<CreditTransactionHistoryDto>>> GetPendingCreditTransactionsAsync(Guid? customerId = null, Guid? branchId = null)
        {
            try
            {
                var query = _context.Transactions
                    .Include(t => t.Branch)
                    .Include(t => t.Product)
                    .Include(t => t.Customer)
                    .Where(t => t.PaymentMethod == PaymentMethod.Credit);

                if (customerId.HasValue)
                    query = query.Where(t => t.CustomerId == customerId.Value);
                if (branchId.HasValue)
                    query = query.Where(t => t.BranchId == branchId.Value);

                var transactions = await query
                    .OrderByDescending(t => t.TransactionDate)
                    .ToListAsync();

                var result = new List<CreditTransactionHistoryDto>();

                foreach (var transaction in transactions)
                {
                    var paidAmount = await CalculatePaidAmountAsync(transaction.Id);
                    var totalAmount = CalculateTotalAmount(transaction);

                    if (paidAmount < totalAmount) // Only include pending transactions
                    {
                        var history = new CreditTransactionHistoryDto
                        {
                            TransactionId = transaction.Id,
                            TransactionDate = transaction.TransactionDate,
                            ItemCode = transaction.ItemCode,
                            ProductName = transaction.Product.Name,
                            Quantity = transaction.Quantity,
                            UnitPrice = transaction.UnitPrice,
                            TotalAmount = totalAmount,
                            PaidAmount = paidAmount,
                            CustomerId = transaction.CustomerId ?? Guid.Empty,
                            CustomerName = transaction.Customer?.Name ?? "Unknown",
                            CustomerPhoneNumber = transaction.Customer?.PhoneNumber ?? "Unknown",
                            BranchId = transaction.BranchId,
                            BranchName = transaction.Branch.Name,
                            LastPaymentDate = await GetLastPaymentDateAsync(transaction.Id)
                        };
                        result.Add(history);
                    }
                }

                return ApiResponse<List<CreditTransactionHistoryDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending credit transactions");
                return ApiResponse<List<CreditTransactionHistoryDto>>.Fail($"Error retrieving pending credit transactions: {ex.Message}");
            }
        }

        public async Task<ApiResponse<DailySalesReportDto>> GenerateDailySalesReportAsync(DateTime date, Guid? branchId = null, string? paymentMethod = null, Guid? bankAccountId = null)
        {
            try
            {
                var query = _context.DailySales
                    .Include(ds => ds.Branch)
                    .Include(ds => ds.Product)
                        .ThenInclude(p => p.Category)
                    .Include(ds => ds.Transaction)
                    .Include(ds => ds.Customer)
                    .Include(ds => ds.Painter)
                    .Where(ds => ds.SaleDate == date.Date)
                    .AsQueryable();

                if (branchId.HasValue)
                    query = query.Where(ds => ds.BranchId == branchId.Value);
                if (!string.IsNullOrEmpty(paymentMethod) && Enum.TryParse<PaymentMethod>(paymentMethod, out var method))
                    query = query.Where(ds => ds.PaymentMethod == method);

                var dailySales = await query
                    .Select(ds => new
                    {
                        Sale = ds,
                        BuyingPrice = ds.Product != null ? ds.Product.BuyingPrice : 0,
                        BranchName = ds.Branch != null ? ds.Branch.Name : null,
                        ProductName = ds.Product != null ? ds.Product.Name : null,
                        CategoryName = ds.Product != null && ds.Product.Category != null
                            ? ds.Product.Category.Name : null,
                        ItemCode = ds.Transaction != null ? ds.Transaction.ItemCode :
                                  ds.Product != null ? ds.Product.ItemCode : null,
                        CustomerName = ds.Customer != null ? ds.Customer.Name : null,
                        PainterName = ds.Painter != null ? ds.Painter.Name : null,
                        TransactionDate = ds.Transaction != null ? ds.Transaction.TransactionDate : ds.SaleDate
                    })
                    .ToListAsync();

                // Calculate totals in a single pass
                var totals = dailySales.Aggregate(
                    new
                    {
                        TotalSales = 0m,
                        TotalCost = 0m,
                        TotalCommission = 0m,
                        CashAmount = 0m,
                        BankAmount = 0m,
                        CreditAmount = 0m,
                        PaidCommission = 0m,
                        PendingCommission = 0m
                    },
                    (acc, x) => new
                    {
                        TotalSales = acc.TotalSales + x.Sale.TotalAmount,
                        TotalCost = acc.TotalCost + (x.BuyingPrice * x.Sale.Quantity),
                        TotalCommission = acc.TotalCommission + x.Sale.CommissionAmount,
                        CashAmount = acc.CashAmount + (x.Sale.PaymentMethod == PaymentMethod.Cash ? x.Sale.TotalAmount : 0),
                        BankAmount = acc.BankAmount + (x.Sale.PaymentMethod == PaymentMethod.Bank ? x.Sale.TotalAmount : 0),
                        CreditAmount = acc.CreditAmount + (x.Sale.PaymentMethod == PaymentMethod.Credit ? x.Sale.TotalAmount : 0),
                        PaidCommission = acc.PaidCommission + (x.Sale.CommissionPaid ? x.Sale.CommissionAmount : 0),
                        PendingCommission = acc.PendingCommission + (!x.Sale.CommissionPaid ? x.Sale.CommissionAmount : 0)
                    });

                var totalNetProfit = totals.TotalSales - totals.TotalCost - totals.TotalCommission;

                var report = new DailySalesReportDto
                {
                    ReportDate = date.Date,
                    BranchId = branchId,
                    BranchName = branchId.HasValue ? dailySales.FirstOrDefault()?.BranchName : "All Branches",
                    PaymentMethod = paymentMethod,
                    TotalTransactions = dailySales.Count,
                    TotalSalesAmount = totals.TotalSales,
                    TotalCostAmount = totals.TotalCost,
                    TotalNetProfit = totalNetProfit,
                    ProfitMarginPercentage = totals.TotalSales > 0 ? (totalNetProfit / totals.TotalSales) * 100 : 0,
                    TotalCashAmount = totals.CashAmount,
                    TotalBankAmount = totals.BankAmount,
                    TotalCreditAmount = totals.CreditAmount,
                    TotalCommissionAmount = totals.TotalCommission,
                    TotalPaidCommission = totals.PaidCommission,
                    TotalPendingCommission = totals.PendingCommission,
                    SalesItems = dailySales.Select(x => new DailySalesItemDto
                    {
                        Id = x.Sale.Id,
                        TransactionId = x.Sale.TransactionId,
                        SaleDate = x.Sale.SaleDate,
                        TransactionDate = x.TransactionDate,
                        BranchId = x.Sale.BranchId,
                        BranchName = x.BranchName,
                        ProductId = x.Sale.ProductId,
                        ProductName = x.ProductName,
                        CategoryName = x.CategoryName,
                        ItemCode = x.ItemCode,
                        Quantity = x.Sale.Quantity,
                        UnitPrice = x.Sale.UnitPrice,
                        TotalAmount = x.Sale.TotalAmount,
                        BuyingPrice = x.BuyingPrice,
                        CostAmount = x.BuyingPrice * x.Sale.Quantity,
                        Profit = x.Sale.TotalAmount - (x.BuyingPrice * x.Sale.Quantity) - x.Sale.CommissionAmount,
                        ProfitMargin = x.Sale.TotalAmount > 0
                            ? ((x.Sale.TotalAmount - (x.BuyingPrice * x.Sale.Quantity) - x.Sale.CommissionAmount) / x.Sale.TotalAmount) * 100
                            : 0,
                        PaymentMethod = x.Sale.PaymentMethod,
                        CommissionRate = x.Sale.CommissionRate,
                        CommissionAmount = x.Sale.CommissionAmount,
                        CommissionPaid = x.Sale.CommissionPaid,
                        CustomerId = x.Sale.CustomerId,
                        CustomerName = x.CustomerName,
                        PainterId = x.Sale.PainterId,
                        PainterName = x.PainterName,
                        IsPartialPayment = x.Sale.IsPartialPayment,
                        IsCreditPayment = x.Sale.IsCreditPayment
                    }).ToList(),
                    PaymentSummaries = dailySales
                        .GroupBy(x => x.Sale.PaymentMethod)
                        .Select(g => new PaymentSummaryDto
                        {
                            PaymentMethod = g.Key,
                            TransactionCount = g.Count(),
                            TotalAmount = g.Sum(x => x.Sale.TotalAmount),
                            Percentage = totals.TotalSales > 0
                                ? (g.Sum(x => x.Sale.TotalAmount) / totals.TotalSales) * 100
                                : 0
                        }).ToList()
                };

                _logger.LogInformation("Generated daily sales report for {Date}. " +
                    "Sales: {TotalSales:C2}, Cost: {TotalCost:C2}, Profit: {NetProfit:C2}, Margin: {Margin:F1}%",
                    date.Date, report.TotalSalesAmount, report.TotalCostAmount,
                    report.TotalNetProfit, report.ProfitMarginPercentage);

                return ApiResponse<DailySalesReportDto>.Success(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating daily sales report for date: {Date}", date);
                return ApiResponse<DailySalesReportDto>.Fail($"Error generating report: {ex.Message}");
            }
        }
        public async Task<ApiResponse<CreditSummaryDto>> GetCreditSummaryAsync(Guid? customerId = null)
        {
            try
            {
                var query = _context.Transactions
                    .Include(t => t.Customer)
                    .Include(t => t.Branch)
                    .Include(t => t.Product)
                    .Where(t => t.PaymentMethod == PaymentMethod.Credit);

                if (customerId.HasValue)
                    query = query.Where(t => t.CustomerId == customerId.Value);

                var creditTransactions = await query.ToListAsync();

                var summary = new CreditSummaryDto
                {
                    CustomerId = customerId,
                    CustomerName = customerId.HasValue ? creditTransactions.FirstOrDefault()?.Customer?.Name : null,
                    CustomerPhoneNumber = customerId.HasValue ? creditTransactions.FirstOrDefault()?.Customer?.PhoneNumber : null,
                    TotalCreditTransactions = creditTransactions.Count,
                    TotalCreditAmount = creditTransactions.Sum(t => CalculateTotalAmount(t))
                };

                // Calculate paid amounts
                foreach (var transaction in creditTransactions)
                {
                    summary.TotalPaidAmount += await CalculatePaidAmountAsync(transaction.Id);
                }

                // Count pending transactions
                summary.PendingCreditTransactions = 0;
                foreach (var transaction in creditTransactions)
                {
                    var paidAmount = await CalculatePaidAmountAsync(transaction.Id);
                    if (paidAmount < CalculateTotalAmount(transaction))
                    {
                        summary.PendingCreditTransactions++;
                    }
                }

                summary.CompletedCreditTransactions = summary.TotalCreditTransactions - summary.PendingCreditTransactions;

                // Customer summaries
                if (!customerId.HasValue)
                {
                    var customerGroups = creditTransactions
                        .Where(t => t.CustomerId.HasValue)
                        .GroupBy(t => new { t.CustomerId, t.Customer.Name, t.Customer.PhoneNumber });

                    summary.CustomerSummaries = new List<CreditCustomerSummaryDto>();
                    foreach (var group in customerGroups)
                    {
                        var customerPaidAmount = 0m;
                        foreach (var transaction in group)
                        {
                            customerPaidAmount += await CalculatePaidAmountAsync(transaction.Id);
                        }

                        summary.CustomerSummaries.Add(new CreditCustomerSummaryDto
                        {
                            CustomerId = group.Key.CustomerId.Value,
                            CustomerName = group.Key.Name,
                            CustomerPhoneNumber = group.Key.PhoneNumber,
                            CreditCount = group.Count(),
                            TotalCreditAmount = group.Sum(t => CalculateTotalAmount(t)),
                            TotalPaidAmount = customerPaidAmount,
                            LastCreditDate = group.Max(t => t.TransactionDate)
                        });
                    }
                }

                // Recent transactions
                var recentTransactions = creditTransactions
                    .OrderByDescending(t => t.TransactionDate)
                    .Take(10)
                    .ToList();

                summary.RecentTransactions = new List<CreditTransactionHistoryDto>();
                foreach (var transaction in recentTransactions)
                {
                    var paidAmount = await CalculatePaidAmountAsync(transaction.Id);
                    var totalAmount = CalculateTotalAmount(transaction);

                    var history = new CreditTransactionHistoryDto
                    {
                        TransactionId = transaction.Id,
                        TransactionDate = transaction.TransactionDate,
                        ItemCode = transaction.ItemCode,
                        ProductName = transaction.Product.Name,
                        Quantity = transaction.Quantity,
                        UnitPrice = transaction.UnitPrice,
                        TotalAmount = totalAmount,
                        PaidAmount = paidAmount,
                        CustomerId = transaction.CustomerId ?? Guid.Empty,
                        CustomerName = transaction.Customer?.Name ?? "Unknown",
                        CustomerPhoneNumber = transaction.Customer?.PhoneNumber ?? "Unknown",
                        BranchId = transaction.BranchId,
                        BranchName = transaction.Branch.Name
                    };
                    summary.RecentTransactions.Add(history);
                }

                return ApiResponse<CreditSummaryDto>.Success(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting credit summary for customer: {CustomerId}", customerId);
                return ApiResponse<CreditSummaryDto>.Fail($"Error retrieving credit summary: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> MarkCommissionAsPaidAsync(Guid transactionId)
        {
            try
            {
                var transaction = await _context.Transactions
                    .FirstOrDefaultAsync(t => t.Id == transactionId);

                if (transaction == null)
                    return ApiResponse<bool>.Fail("Transaction not found");

                if (transaction.CommissionRate <= 0)
                    return ApiResponse<bool>.Fail("No commission for this transaction");

                transaction.CommissionPaid = true;
                transaction.UpdatedAt = DateTime.UtcNow;

                // Also update DailySales entries for this transaction
                var dailySalesEntries = await _context.DailySales
                    .Where(ds => ds.TransactionId == transactionId)
                    .ToListAsync();

                foreach (var dailySale in dailySalesEntries)
                {
                    dailySale.CommissionPaid = true;
                    dailySale.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Commission marked as paid for transaction: {TransactionId}", transactionId);
                return ApiResponse<bool>.Success(true, "Commission marked as paid successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking commission as paid for transaction: {TransactionId}", transactionId);
                return ApiResponse<bool>.Fail($"Error marking commission as paid: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<TransactionResponseDto>>> GetTransactionsByDateRangeAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            Guid? branchId = null,
            Guid? customerId = null,
            Guid? productId = null,
            string? paymentMethod = null)
        {
            try
            {
                // Get all DailySales entries for the date range
                var dailySalesQuery = _context.DailySales
                    .Include(ds => ds.Transaction)
                        .ThenInclude(t => t.Branch)
                    .Include(ds => ds.Transaction)
                        .ThenInclude(t => t.Product)
                            .ThenInclude(p => p.Category)
                    .Include(ds => ds.Transaction)
                        .ThenInclude(t => t.Customer)
                    .Include(ds => ds.Transaction)
                        .ThenInclude(t => t.Painter)
                    .Where(ds => ds.IsActive)
                    .AsQueryable();

                // Apply date filters
                if (startDate.HasValue)
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.SaleDate.Date >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.SaleDate.Date <= endDate.Value.Date);
                }

                // Apply other filters
                if (branchId.HasValue)
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.BranchId == branchId.Value);
                }

                if (customerId.HasValue)
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.Transaction.CustomerId == customerId.Value);
                }

                if (productId.HasValue)
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.ProductId == productId.Value);
                }

                if (!string.IsNullOrEmpty(paymentMethod) &&
                    Enum.TryParse<PaymentMethod>(paymentMethod, out var method))
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.PaymentMethod == method);
                }

                var dailySales = await dailySalesQuery
                    .OrderByDescending(ds => ds.SaleDate)
                    .ThenByDescending(ds => ds.CreatedAt)
                    .ToListAsync();

                // Transform DailySales into TransactionResponseDto format
                var result = new List<TransactionResponseDto>();

                foreach (var dailySale in dailySales)
                {
                    if (dailySale.Transaction != null)
                    {
                        var transactionDto = _mapper.Map<TransactionResponseDto>(dailySale.Transaction);

                        // Override with DailySales data (actual sold/payment data)
                        transactionDto.Quantity = dailySale.Quantity;
                        transactionDto.UnitPrice = dailySale.UnitPrice;
                        transactionDto.TotalAmount = dailySale.TotalAmount;
                        transactionDto.CommissionAmount = dailySale.CommissionAmount;
                        transactionDto.CommissionPaid = dailySale.CommissionPaid;
                        transactionDto.TransactionDate = dailySale.SaleDate;

                        // For credit payments, mark as partial if applicable
                        if (dailySale.IsCreditPayment)
                        {
                            transactionDto.IsPartialPayment = dailySale.IsPartialPayment;
                            transactionDto.PaidAmount = await CalculatePaidAmountAsync(dailySale.TransactionId);
                        }
                        else
                        {
                            transactionDto.IsPartialPayment = false;
                            if (dailySale.Transaction.PaymentMethod == PaymentMethod.Credit)
                            {
                                transactionDto.PaidAmount = await CalculatePaidAmountAsync(dailySale.TransactionId);
                            }
                        }

                        result.Add(transactionDto);
                    }
                }

                return ApiResponse<List<TransactionResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions by date range: Start={StartDate}, End={EndDate}",
                    startDate, endDate);
                return ApiResponse<List<TransactionResponseDto>>.Fail($"Error retrieving transactions: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<TransactionResponseDto>>> GetTransactionsByDateAsync(
            DateTime date,
            Guid? branchId = null,
            Guid? customerId = null,
            Guid? productId = null,
            string? paymentMethod = null)
        {
            try
            {
                // Get DailySales for the specific date
                var dailySalesQuery = _context.DailySales
                    .Include(ds => ds.Transaction)
                        .ThenInclude(t => t.Branch)
                    .Include(ds => ds.Transaction)
                        .ThenInclude(t => t.Product)
                            .ThenInclude(p => p.Category)
                    .Include(ds => ds.Transaction)
                        .ThenInclude(t => t.Customer)
                    .Include(ds => ds.Transaction)
                        .ThenInclude(t => t.Painter)
                    .Where(ds => ds.IsActive && ds.SaleDate.Date == date.Date)
                    .AsQueryable();

                // Apply filters
                if (branchId.HasValue)
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.BranchId == branchId.Value);
                }

                if (customerId.HasValue)
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.Transaction.CustomerId == customerId.Value);
                }

                if (productId.HasValue)
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.ProductId == productId.Value);
                }

                if (!string.IsNullOrEmpty(paymentMethod) &&
                    Enum.TryParse<PaymentMethod>(paymentMethod, out var method))
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.PaymentMethod == method);
                }

                var dailySales = await dailySalesQuery
                    .OrderByDescending(ds => ds.CreatedAt)
                    .ToListAsync();

                // Transform DailySales into TransactionResponseDto format
                var result = new List<TransactionResponseDto>();

                foreach (var dailySale in dailySales)
                {
                    if (dailySale.Transaction != null)
                    {
                        var transactionDto = _mapper.Map<TransactionResponseDto>(dailySale.Transaction);

                        // Override with DailySales data (actual sold/payment data)
                        transactionDto.Quantity = dailySale.Quantity;
                        transactionDto.UnitPrice = dailySale.UnitPrice;
                        transactionDto.TotalAmount = dailySale.TotalAmount;
                        transactionDto.CommissionAmount = dailySale.CommissionAmount;
                        transactionDto.CommissionPaid = dailySale.CommissionPaid;
                        transactionDto.TransactionDate = dailySale.SaleDate;

                        // For credit payments, mark as partial if applicable
                        if (dailySale.IsCreditPayment)
                        {
                            transactionDto.IsPartialPayment = dailySale.IsPartialPayment;
                            transactionDto.PaidAmount = await CalculatePaidAmountAsync(dailySale.TransactionId);
                        }
                        else
                        {
                            transactionDto.IsPartialPayment = false;
                            if (dailySale.Transaction.PaymentMethod == PaymentMethod.Credit)
                            {
                                transactionDto.PaidAmount = await CalculatePaidAmountAsync(dailySale.TransactionId);
                            }
                        }

                        result.Add(transactionDto);
                    }
                }

                return ApiResponse<List<TransactionResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions for date: {Date}", date);
                return ApiResponse<List<TransactionResponseDto>>.Fail($"Error retrieving transactions: {ex.Message}");
            }
        }

        public async Task<ApiResponse<TransactionSummaryDto>> GetTransactionSummaryAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            Guid? branchId = null,
            string? paymentMethod = null)
        {
            try
            {
                // Get DailySales entries for the date range
                var dailySalesQuery = _context.DailySales
                    .Include(ds => ds.Transaction)
                    .Include(ds => ds.Branch)
                    .Include(ds => ds.Product)
                    .Include(ds => ds.Customer)
                    .Include(ds => ds.Painter)
                    .Where(ds => ds.IsActive)
                    .AsQueryable();

                // Apply filters
                if (startDate.HasValue)
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.SaleDate.Date >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.SaleDate.Date <= endDate.Value.Date);
                }

                if (branchId.HasValue)
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.BranchId == branchId.Value);
                }

                if (!string.IsNullOrEmpty(paymentMethod) &&
                    Enum.TryParse<PaymentMethod>(paymentMethod, out var method))
                {
                    dailySalesQuery = dailySalesQuery.Where(ds => ds.PaymentMethod == method);
                }

                var dailySales = await dailySalesQuery.ToListAsync();

                var summary = new TransactionSummaryDto
                {
                    StartDate = startDate,
                    EndDate = endDate,
                    BranchId = branchId,
                    PaymentMethod = paymentMethod
                };

                if (branchId.HasValue)
                {
                    var branch = await _context.Branches
                        .FirstOrDefaultAsync(b => b.Id == branchId.Value);
                    summary.BranchName = branch?.Name;
                }

                // Calculate counts and amounts from DailySales
                summary.TotalTransactions = dailySales.Count;
                summary.CashTransactions = dailySales.Count(ds => ds.PaymentMethod == PaymentMethod.Cash);
                summary.BankTransactions = dailySales.Count(ds => ds.PaymentMethod == PaymentMethod.Bank);
                summary.CreditTransactions = dailySales.Count(ds => ds.PaymentMethod == PaymentMethod.Credit);

                // Calculate amounts from DailySales (actual payments/sales)
                summary.TotalSalesAmount = dailySales.Sum(ds => ds.TotalAmount);
                summary.TotalCashAmount = dailySales
                    .Where(ds => ds.PaymentMethod == PaymentMethod.Cash)
                    .Sum(ds => ds.TotalAmount);
                summary.TotalBankAmount = dailySales
                    .Where(ds => ds.PaymentMethod == PaymentMethod.Bank)
                    .Sum(ds => ds.TotalAmount);
                summary.TotalCreditAmount = dailySales
                    .Where(ds => ds.PaymentMethod == PaymentMethod.Credit)
                    .Sum(ds => ds.TotalAmount);

                // For credit transactions, calculate paid vs pending
                var creditTransactions = dailySales
                    .Where(ds => ds.PaymentMethod == PaymentMethod.Credit)
                    .Select(ds => ds.Transaction)
                    .Distinct()
                    .ToList();

                foreach (var transaction in creditTransactions)
                {
                    if (transaction != null)
                    {
                        var paidAmount = await CalculatePaidAmountAsync(transaction.Id);
                        var totalAmount = CalculateTotalAmount(transaction);

                        summary.TotalPaidCreditAmount += paidAmount;
                        summary.TotalPendingCreditAmount += (totalAmount - paidAmount);
                    }
                }

                // Calculate commission
                summary.TotalCommissionAmount = dailySales.Sum(ds => ds.CommissionAmount);
                summary.TotalPaidCommission = dailySales
                    .Where(ds => ds.CommissionPaid)
                    .Sum(ds => ds.CommissionAmount);
                summary.TotalPendingCommission = summary.TotalCommissionAmount - summary.TotalPaidCommission;

                // Calculate quantities
                summary.TotalQuantitySold = dailySales.Sum(ds => ds.Quantity);

                // Calculate averages
                summary.AverageTransactionAmount = summary.TotalTransactions > 0
                    ? summary.TotalSalesAmount / summary.TotalTransactions
                    : 0;

                // Calculate days in period
                if (startDate.HasValue && endDate.HasValue)
                {
                    summary.DaysInPeriod = (int)(endDate.Value.Date - startDate.Value.Date).TotalDays + 1;
                    summary.AverageDailySales = summary.DaysInPeriod > 0
                        ? summary.TotalSalesAmount / summary.DaysInPeriod
                        : summary.TotalSalesAmount;
                }
                else if (startDate.HasValue)
                {
                    var days = (int)(DateTime.UtcNow.Date - startDate.Value.Date).TotalDays + 1;
                    summary.DaysInPeriod = days;
                    summary.AverageDailySales = days > 0 ? summary.TotalSalesAmount / days : summary.TotalSalesAmount;
                }
                else
                {
                    summary.DaysInPeriod = 1;
                    summary.AverageDailySales = summary.TotalSalesAmount;
                }

                // Calculate top products from DailySales
                var productGroups = dailySales
                    .GroupBy(ds => new { ds.ProductId, ProductName = ds.Product?.Name, ItemCode = ds.Transaction?.ItemCode ?? ds.Product?.ItemCode })
                    .Where(g => g.Key.ProductId != Guid.Empty)
                    .Select(g => new TransactionProductSummaryDto
                    {
                        ProductId = g.Key.ProductId,
                        ProductName = g.Key.ProductName ?? "Unknown",
                        ItemCode = g.Key.ItemCode ?? "N/A",
                        TransactionCount = g.Count(),
                        TotalQuantity = g.Sum(ds => ds.Quantity),
                        TotalAmount = g.Sum(ds => ds.TotalAmount),
                        PercentageOfTotal = summary.TotalSalesAmount > 0
                            ? (g.Sum(ds => ds.TotalAmount) / summary.TotalSalesAmount) * 100
                            : 0
                    })
                    .OrderByDescending(p => p.TotalAmount)
                    .Take(10)
                    .ToList();

                summary.TopProducts = productGroups;

                // Calculate top customers from DailySales
                var customerGroups = dailySales
                    .Where(ds => ds.CustomerId.HasValue && ds.Customer != null)
                    .GroupBy(ds => new { ds.CustomerId, ds.Customer.Name, ds.Customer.PhoneNumber })
                    .Select(g => new TransactionCustomerSummaryDto
                    {
                        CustomerId = g.Key.CustomerId.Value,
                        CustomerName = g.Key.Name,
                        CustomerPhoneNumber = g.Key.PhoneNumber,
                        TransactionCount = g.Count(),
                        TotalAmount = g.Sum(ds => ds.TotalAmount)
                    })
                    .OrderByDescending(c => c.TotalAmount)
                    .Take(10)
                    .ToList();

                // Calculate credit details for customers
                foreach (var customer in customerGroups)
                {
                    var customerCreditTransactions = dailySales
                        .Where(ds => ds.CustomerId == customer.CustomerId && ds.PaymentMethod == PaymentMethod.Credit)
                        .Select(ds => ds.Transaction)
                        .Distinct();

                    foreach (var transaction in customerCreditTransactions)
                    {
                        if (transaction != null)
                        {
                            var paidAmount = await CalculatePaidAmountAsync(transaction.Id);
                            var totalAmount = CalculateTotalAmount(transaction);

                            customer.CreditAmount += totalAmount;
                            customer.PaidCreditAmount += paidAmount;
                            customer.PendingCreditAmount += (totalAmount - paidAmount);
                        }
                    }
                }

                summary.TopCustomers = customerGroups;

                // Calculate top painters from DailySales
                var painterGroups = dailySales
                    .Where(ds => ds.PainterId.HasValue && ds.Painter != null)
                    .GroupBy(ds => new { ds.PainterId, ds.Painter.Name, ds.Painter.PhoneNumber })
                    .Select(g => new TransactionPainterSummaryDto
                    {
                        PainterId = g.Key.PainterId.Value,
                        PainterName = g.Key.Name,
                        PainterPhoneNumber = g.Key.PhoneNumber,
                        TransactionCount = g.Count(),
                        TotalCommissionAmount = g.Sum(ds => ds.CommissionAmount),
                        PaidCommissionAmount = g.Where(ds => ds.CommissionPaid).Sum(ds => ds.CommissionAmount),
                        PendingCommissionAmount = g.Where(ds => !ds.CommissionPaid).Sum(ds => ds.CommissionAmount)
                    })
                    .OrderByDescending(p => p.TotalCommissionAmount)
                    .Take(10)
                    .ToList();

                summary.TopPainters = painterGroups;

                // Get recent transactions from DailySales
                var recentDailySales = dailySales
                    .OrderByDescending(ds => ds.SaleDate)
                    .ThenByDescending(ds => ds.CreatedAt)
                    .Take(10)
                    .ToList();

                var recentDtos = new List<TransactionResponseDto>();
                foreach (var dailySale in recentDailySales)
                {
                    if (dailySale.Transaction != null)
                    {
                        var transactionDto = _mapper.Map<TransactionResponseDto>(dailySale.Transaction);

                        // Override with DailySales data
                        transactionDto.Quantity = dailySale.Quantity;
                        transactionDto.UnitPrice = dailySale.UnitPrice;
                        transactionDto.TotalAmount = dailySale.TotalAmount;
                        transactionDto.CommissionAmount = dailySale.CommissionAmount;
                        transactionDto.CommissionPaid = dailySale.CommissionPaid;
                        transactionDto.TransactionDate = dailySale.SaleDate;

                        if (dailySale.IsCreditPayment)
                        {
                            transactionDto.IsPartialPayment = dailySale.IsPartialPayment;
                            transactionDto.PaidAmount = await CalculatePaidAmountAsync(dailySale.TransactionId);
                        }

                        recentDtos.Add(transactionDto);
                    }
                }

                summary.RecentTransactions = recentDtos;

                return ApiResponse<TransactionSummaryDto>.Success(summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction summary: Start={StartDate}, End={EndDate}",
                    startDate, endDate);
                return ApiResponse<TransactionSummaryDto>.Fail($"Error retrieving transaction summary: {ex.Message}");
            }
        }

        // NEW METHOD: Get Credit Transaction with Payment Details
        public async Task<ApiResponse<CreditTransactionWithPaymentsDto>> GetCreditTransactionWithPaymentsAsync(Guid transactionId)
        {
            try
            {
                var transaction = await _context.Transactions
                    .Include(t => t.Branch)
                    .Include(t => t.Product)
                        .ThenInclude(p => p.Category)
                    .Include(t => t.Customer)
                    .Include(t => t.Painter)
                    .FirstOrDefaultAsync(t => t.Id == transactionId && t.PaymentMethod == PaymentMethod.Credit);

                if (transaction == null)
                    return ApiResponse<CreditTransactionWithPaymentsDto>.Fail("Credit transaction not found");

                // Get all payments for this transaction
                var payments = await _context.CreditPayments
                    .Where(cp => cp.TransactionId == transactionId)
                    .OrderBy(cp => cp.PaymentDate)
                    .ToListAsync();

                // Get all DailySales entries for this transaction (payment records)
                var dailySalesPayments = await _context.DailySales
                    .Where(ds => ds.TransactionId == transactionId && ds.IsCreditPayment)
                    .OrderBy(ds => ds.SaleDate)
                    .ToListAsync();

                var result = new CreditTransactionWithPaymentsDto
                {
                    Transaction = _mapper.Map<TransactionResponseDto>(transaction),
                    Payments = payments.Select(p => new CreditPaymentDto
                    {
                        Id = p.Id,
                        TransactionId = p.TransactionId,
                        Amount = p.Amount,
                        PaymentMethod = p.PaymentMethod,
                        PaymentDate = p.PaymentDate,
                        CreatedAt = p.CreatedAt
                    }).ToList(),
                    DailySalesPayments = dailySalesPayments.Select(ds => new DailySalesItemDto
                    {
                        Id = ds.Id,
                        TransactionId = ds.TransactionId,
                        SaleDate = ds.SaleDate,
                        BranchId = ds.BranchId,
                        BranchName = ds.Branch?.Name,
                        ProductId = ds.ProductId,
                        ProductName = ds.Product?.Name,
                        ItemCode = transaction.ItemCode,
                        Quantity = ds.Quantity,
                        UnitPrice = ds.UnitPrice,
                        TotalAmount = ds.TotalAmount,
                        PaymentMethod = ds.PaymentMethod,
                        CommissionRate = ds.CommissionRate,
                        CommissionAmount = ds.CommissionAmount,
                        CommissionPaid = ds.CommissionPaid,
                        CustomerId = ds.CustomerId,
                        CustomerName = ds.Customer?.Name,
                        PainterId = ds.PainterId,
                        PainterName = ds.Painter?.Name,
                        IsPartialPayment = ds.IsPartialPayment,
                        IsCreditPayment = ds.IsCreditPayment
                    }).ToList(),
                    TotalPaidAmount = payments.Sum(p => p.Amount),
                    RemainingAmount = CalculateTotalAmount(transaction) - payments.Sum(p => p.Amount),
                    IsFullyPaid = payments.Sum(p => p.Amount) >= CalculateTotalAmount(transaction)
                };

                return ApiResponse<CreditTransactionWithPaymentsDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting credit transaction with payments: {TransactionId}", transactionId);
                return ApiResponse<CreditTransactionWithPaymentsDto>.Fail($"Error retrieving credit transaction: {ex.Message}");
            }
        }

        #region Helper Methods

        private async Task LoadTransactionNavigationProperties(Transaction transaction)
        {
            await _context.Entry(transaction)
                .Reference(t => t.Branch).LoadAsync();
            await _context.Entry(transaction)
                .Reference(t => t.Product).LoadAsync();
            await _context.Entry(transaction.Product)
                .Reference(p => p.Category).LoadAsync();
            if (transaction.CustomerId.HasValue)
                await _context.Entry(transaction).Reference(t => t.Customer).LoadAsync();
            if (transaction.PainterId.HasValue)
                await _context.Entry(transaction).Reference(t => t.Painter).LoadAsync();
        }

        private async Task<bool> ValidateStockAsync(Guid productId, decimal quantity, Guid branchId)
        {
            var stock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.ProductId == productId && s.BranchId == branchId);
            return stock != null && stock.Quantity >= quantity;
        }

        private async Task UpdateStockAsync(Guid productId, decimal quantity, Guid branchId, Guid transactionId)
        {
            var stock = await _context.Stocks
                .FirstOrDefaultAsync(s => s.ProductId == productId && s.BranchId == branchId);

            if (stock != null)
            {
                stock.Quantity -= quantity;
                stock.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Create new stock record with negative quantity
                var newStock = new Stock
                {
                    Id = Guid.NewGuid(),
                    ProductId = productId,
                    BranchId = branchId,
                    Quantity = -quantity,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                await _context.Stocks.AddAsync(newStock);
                _logger.LogWarning("Created new stock record with negative quantity for ProductId: {ProductId}", productId);
            }
        }

        // Create StockMovement record
        private async Task CreateStockMovementAsync(Transaction transaction)
        {
            try
            {
                // Get current stock from Stock table
                var stock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == transaction.ProductId && s.BranchId == transaction.BranchId);

                var previousQuantity = stock?.Quantity ?? 0;
                var newQuantity = previousQuantity - transaction.Quantity;

                var stockMovement = new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = transaction.ProductId,
                    BranchId = transaction.BranchId,
                    TransactionId = transaction.Id,
                    MovementType = StockMovementType.Sale,
                    Quantity = -transaction.Quantity, // Negative for sales
                    PreviousQuantity = previousQuantity,
                    NewQuantity = newQuantity,
                    MovementDate = transaction.TransactionDate,
                    Reason = $"Sale transaction - {transaction.ItemCode}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _context.StockMovements.AddAsync(stockMovement);
                _logger.LogDebug("StockMovement created for transaction {TransactionId}", transaction.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating StockMovement for transaction {TransactionId}", transaction.Id);
                // Don't fail the transaction if StockMovement creation fails
            }
        }

        // Create StockMovement for credit payments
        private async Task CreateStockMovementForPaymentAsync(Transaction transaction, decimal paidQuantity)
        {
            try
            {
                // Get current stock from Stock table
                var stock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == transaction.ProductId && s.BranchId == transaction.BranchId);

                var previousQuantity = stock?.Quantity ?? 0;
                var newQuantity = previousQuantity - paidQuantity;

                var stockMovement = new StockMovement
                {
                    Id = Guid.NewGuid(),
                    ProductId = transaction.ProductId,
                    BranchId = transaction.BranchId,
                    TransactionId = transaction.Id,
                    MovementType = StockMovementType.Sale,
                    Quantity = -paidQuantity, // Negative for sales
                    PreviousQuantity = previousQuantity,
                    NewQuantity = newQuantity,
                    MovementDate = DateTime.UtcNow,
                    Reason = $"Credit payment - {transaction.ItemCode}",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _context.StockMovements.AddAsync(stockMovement);
                _logger.LogDebug("StockMovement created for credit payment of transaction {TransactionId}", transaction.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating StockMovement for credit payment of transaction {TransactionId}", transaction.Id);
                // Don't fail the payment if StockMovement creation fails
            }
        }

        private async Task CreateDailySalesAsync(Transaction transaction, bool isCreditPayment = false)
        {
            // For non-credit transactions, create DailySales normally
            // For credit transactions, only create DailySales when payment is made
            if (transaction.PaymentMethod != PaymentMethod.Credit || isCreditPayment)
            {
                var dailySales = new DailySales
                {
                    Id = Guid.NewGuid(),
                    BranchId = transaction.BranchId,
                    ProductId = transaction.ProductId,
                    TransactionId = transaction.Id,
                    SaleDate = transaction.TransactionDate.Date,
                    Quantity = transaction.Quantity,
                    UnitPrice = transaction.UnitPrice,
                    TotalAmount = CalculateTotalAmount(transaction),
                    PaymentMethod = transaction.PaymentMethod,
                    CommissionRate = transaction.CommissionRate,
                    CommissionAmount = CalculateCommissionAmount(transaction),
                    CommissionPaid = transaction.CommissionPaid,
                    CustomerId = transaction.CustomerId,
                    PainterId = transaction.PainterId,
                    IsPartialPayment = false, // Not a partial payment for non-credit
                    IsCreditPayment = isCreditPayment,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                await _context.DailySales.AddAsync(dailySales);
            }
        }

        private async Task<decimal> CalculatePaidAmountAsync(Guid transactionId)
        {
            var payments = await _context.CreditPayments
                .Where(cp => cp.TransactionId == transactionId)
                .SumAsync(cp => cp.Amount);
            return payments;
        }

        private decimal CalculateTotalAmount(Transaction transaction)
        {
            return transaction.UnitPrice * transaction.Quantity;
        }

        private decimal CalculateCommissionAmount(Transaction transaction)
        {
            return transaction.Quantity * transaction.CommissionRate;
        }

        private decimal CalculateTotalAmount(decimal unitPrice, decimal quantity)
        {
            return unitPrice * quantity;
        }

        private decimal CalculateCommissionAmount(decimal quantity, decimal commissionRate)
        {
            return quantity * commissionRate;
        }

        private async Task<DateTime?> GetLastPaymentDateAsync(Guid transactionId)
        {
            var lastPayment = await _context.CreditPayments
                .Where(cp => cp.TransactionId == transactionId)
                .OrderByDescending(cp => cp.PaymentDate)
                .FirstOrDefaultAsync();
            return lastPayment?.PaymentDate;
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