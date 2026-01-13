
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

                // Create daily sales for non-credit transactions
                if (dto.PaymentMethod != PaymentMethod.Credit)
                {
                    await CreateDailySalesAsync(salesTransaction);
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
                var query = _context.Transactions
                    .Include(t => t.Branch)
                    .Include(t => t.Product)
                        .ThenInclude(p => p.Category)
                    .Include(t => t.Customer)
                    .Include(t => t.Painter)
                    .Where(t => t.IsActive)
                    .AsQueryable();

                // Apply date filters if provided
                if (startDate.HasValue)
                {
                    query = query.Where(t => t.TransactionDate.Date >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(t => t.TransactionDate.Date <= endDate.Value.Date);
                }

                if (branchId.HasValue)
                {
                    query = query.Where(t => t.BranchId == branchId.Value);
                }

                var transactions = await query
                    .OrderByDescending(t => t.TransactionDate)
                    .ThenByDescending(t => t.CreatedAt)
                    .ToListAsync();

                var result = _mapper.Map<List<TransactionResponseDto>>(transactions);

                // Calculate paid amounts for credit transactions
                foreach (var transaction in result.Where(t => t.IsCredit))
                {
                    transaction.PaidAmount = await CalculatePaidAmountAsync(transaction.Id);
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
                    .FirstOrDefaultAsync(t => t.Id == dto.Id);

                if (existingTransaction == null)
                    return ApiResponse<TransactionResponseDto>.Fail("Transaction not found");

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

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Reload related data
                await LoadTransactionNavigationProperties(existingTransaction);

                var result = _mapper.Map<TransactionResponseDto>(existingTransaction);

                if (existingTransaction.PaymentMethod == PaymentMethod.Credit)
                {
                    result.PaidAmount = await CalculatePaidAmountAsync(existingTransaction.Id);
                }

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
                    .FirstOrDefaultAsync(t => t.Id == id);

                if (existingTransaction == null)
                    return ApiResponse<bool>.Fail("Transaction not found");

                // Check if transaction can be deleted (e.g., not already processed)
                if (existingTransaction.PaymentMethod == PaymentMethod.Credit && await CalculatePaidAmountAsync(id) > 0)
                {
                    return ApiResponse<bool>.Fail("Cannot delete credit transaction with payments");
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
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var creditTransaction = await _context.Transactions
                    .Include(t => t.Product)
                    .FirstOrDefaultAsync(t => t.Id == dto.TransactionId && t.PaymentMethod == PaymentMethod.Credit);

                if (creditTransaction == null)
                    return ApiResponse<TransactionResponseDto>.Fail("Credit transaction not found");

                var paidAmount = await CalculatePaidAmountAsync(creditTransaction.Id);
                var totalAmount = CalculateTotalAmount(creditTransaction);
                var remainingAmount = totalAmount - paidAmount;

                if (dto.Amount > remainingAmount)
                    return ApiResponse<TransactionResponseDto>.Fail($"Payment amount exceeds remaining balance. Remaining: {remainingAmount}");

                // NEW: Calculate what portion of the payment is being made
                var paymentPercentage = dto.Amount / remainingAmount;
                var paidQuantity = creditTransaction.Quantity * paymentPercentage;

                // Create credit payment record
                var creditPayment = new CreditPayment
                {
                    TransactionId = dto.TransactionId,
                    Amount = dto.Amount,
                    PaymentMethod = dto.PaymentMethod,
                    PaymentDate = dto.PaymentDate,
                };

                await _context.CreditPayments.AddAsync(creditPayment);

                // NEW: Update stock proportionally to the payment
                if (dto.PaymentMethod != PaymentMethod.Credit) // Only reduce stock for cash/bank payments
                {
                    await UpdateStockAsync(creditTransaction.ProductId, paidQuantity,
                        creditTransaction.BranchId, creditTransaction.Id);

                    // Create StockMovement for the payment
                    await CreateStockMovementForPaymentAsync(creditTransaction, paidQuantity);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Reload and return updated transaction
                await LoadTransactionNavigationProperties(creditTransaction);
                var result = _mapper.Map<TransactionResponseDto>(creditTransaction);
                result.PaidAmount = await CalculatePaidAmountAsync(creditTransaction.Id);

                _logger.LogInformation("Credit payment of {Amount} processed for transaction: {TransactionId}", dto.Amount, dto.TransactionId);
                return ApiResponse<TransactionResponseDto>.Success(result, "Credit payment processed successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
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
                    var totalAmount = CalculateTotalAmount(transaction); // Use helper method

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
                            TotalAmount = totalAmount, // Use calculated total
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
                    .Where(ds => ds.SaleDate == date.Date);

                if (branchId.HasValue)
                    query = query.Where(ds => ds.BranchId == branchId.Value);
                if (!string.IsNullOrEmpty(paymentMethod) && Enum.TryParse<PaymentMethod>(paymentMethod, out var method))
                    query = query.Where(ds => ds.PaymentMethod == method);

                var dailySales = await query.ToListAsync();

                var report = new DailySalesReportDto
                {
                    ReportDate = date.Date,
                    BranchId = branchId,
                    BranchName = branchId.HasValue ? dailySales.FirstOrDefault()?.Branch?.Name : "All Branches",
                    PaymentMethod = paymentMethod,
                    TotalTransactions = dailySales.Count,
                    TotalSalesAmount = dailySales.Sum(ds => ds.TotalAmount),
                    TotalCashAmount = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Cash).Sum(ds => ds.TotalAmount),
                    TotalBankAmount = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Bank).Sum(ds => ds.TotalAmount),
                    TotalCreditAmount = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Credit).Sum(ds => ds.TotalAmount),
                    TotalCommissionAmount = dailySales.Sum(ds => ds.CommissionAmount),
                    TotalPaidCommission = dailySales.Where(ds => ds.CommissionPaid).Sum(ds => ds.CommissionAmount),
                    TotalPendingCommission = dailySales.Where(ds => !ds.CommissionPaid).Sum(ds => ds.CommissionAmount)
                };

                // Use AutoMapper for sales items
                report.SalesItems = _mapper.Map<List<DailySalesItemDto>>(dailySales);

                // Add payment summaries
                var paymentGroups = dailySales.GroupBy(ds => ds.PaymentMethod);
                report.PaymentSummaries = paymentGroups.Select(g => new PaymentSummaryDto
                {
                    PaymentMethod = g.Key,
                    TransactionCount = g.Count(),
                    TotalAmount = g.Sum(ds => ds.TotalAmount),
                    Percentage = report.TotalSalesAmount > 0 ? (g.Sum(ds => ds.TotalAmount) / report.TotalSalesAmount) * 100 : 0
                }).ToList();

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
                    TotalCreditAmount = creditTransactions.Sum(t => CalculateTotalAmount(t)) // Use helper
                };

                // Calculate paid amounts
                foreach (var transaction in creditTransactions)
                {
                    summary.TotalPaidAmount += await CalculatePaidAmountAsync(transaction.Id);
                }

                // Fix: Count pending transactions without using await in lambda
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
                            TotalCreditAmount = group.Sum(t => CalculateTotalAmount(t)), // Use helper
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
                    var totalAmount = CalculateTotalAmount(transaction); // Use helper

                    // Use AutoMapper if available, otherwise manual mapping
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
                var query = _context.Transactions
                    .Include(t => t.Branch)
                    .Include(t => t.Product)
                        .ThenInclude(p => p.Category)
                    .Include(t => t.Customer)
                    .Include(t => t.Painter)
                    .Where(t => t.IsActive) // Assuming there's an IsActive field
                    .AsQueryable();

                // Apply date filters
                if (startDate.HasValue)
                {
                    query = query.Where(t => t.TransactionDate.Date >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(t => t.TransactionDate.Date <= endDate.Value.Date);
                }

                // Apply other filters
                if (branchId.HasValue)
                {
                    query = query.Where(t => t.BranchId == branchId.Value);
                }

                if (customerId.HasValue)
                {
                    query = query.Where(t => t.CustomerId == customerId.Value);
                }

                if (productId.HasValue)
                {
                    query = query.Where(t => t.ProductId == productId.Value);
                }

                if (!string.IsNullOrEmpty(paymentMethod) &&
                    Enum.TryParse<PaymentMethod>(paymentMethod, out var method))
                {
                    query = query.Where(t => t.PaymentMethod == method);
                }

                var transactions = await query
                    .OrderByDescending(t => t.TransactionDate)
                    .ThenByDescending(t => t.CreatedAt)
                    .ToListAsync();

                var result = _mapper.Map<List<TransactionResponseDto>>(transactions);

                // Calculate paid amounts for credit transactions
                foreach (var transaction in result.Where(t => t.IsCredit))
                {
                    transaction.PaidAmount = await CalculatePaidAmountAsync(transaction.Id);
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
                var query = _context.Transactions
                    .Include(t => t.Branch)
                    .Include(t => t.Product)
                        .ThenInclude(p => p.Category)
                    .Include(t => t.Customer)
                    .Include(t => t.Painter)
                    .Where(t => t.IsActive && t.TransactionDate.Date == date.Date)
                    .AsQueryable();

                // Apply other filters
                if (branchId.HasValue)
                {
                    query = query.Where(t => t.BranchId == branchId.Value);
                }

                if (customerId.HasValue)
                {
                    query = query.Where(t => t.CustomerId == customerId.Value);
                }

                if (productId.HasValue)
                {
                    query = query.Where(t => t.ProductId == productId.Value);
                }

                if (!string.IsNullOrEmpty(paymentMethod) &&
                    Enum.TryParse<PaymentMethod>(paymentMethod, out var method))
                {
                    query = query.Where(t => t.PaymentMethod == method);
                }

                var transactions = await query
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                var result = _mapper.Map<List<TransactionResponseDto>>(transactions);

                // Calculate paid amounts for credit transactions
                foreach (var transaction in result.Where(t => t.IsCredit))
                {
                    transaction.PaidAmount = await CalculatePaidAmountAsync(transaction.Id);
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
                var query = _context.Transactions
                    .Include(t => t.Branch)
                    .Include(t => t.Product)
                    .Include(t => t.Customer)
                    .Include(t => t.Painter)
                    .Where(t => t.IsActive)
                    .AsQueryable();

                // Apply filters
                if (startDate.HasValue)
                {
                    query = query.Where(t => t.TransactionDate.Date >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(t => t.TransactionDate.Date <= endDate.Value.Date);
                }

                if (branchId.HasValue)
                {
                    query = query.Where(t => t.BranchId == branchId.Value);
                }

                if (!string.IsNullOrEmpty(paymentMethod) &&
                    Enum.TryParse<PaymentMethod>(paymentMethod, out var method))
                {
                    query = query.Where(t => t.PaymentMethod == method);
                }

                var transactions = await query.ToListAsync();

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

                // Calculate counts
                summary.TotalTransactions = transactions.Count;
                summary.CashTransactions = transactions.Count(t => t.PaymentMethod == PaymentMethod.Cash);
                summary.BankTransactions = transactions.Count(t => t.PaymentMethod == PaymentMethod.Bank);
                summary.CreditTransactions = transactions.Count(t => t.PaymentMethod == PaymentMethod.Credit);

                // Calculate amounts
                summary.TotalSalesAmount = transactions.Sum(t => CalculateTotalAmount(t));
                summary.TotalCashAmount = transactions
                    .Where(t => t.PaymentMethod == PaymentMethod.Cash)
                    .Sum(t => CalculateTotalAmount(t));
                summary.TotalBankAmount = transactions
                    .Where(t => t.PaymentMethod == PaymentMethod.Bank)
                    .Sum(t => CalculateTotalAmount(t));
                summary.TotalCreditAmount = transactions
                    .Where(t => t.PaymentMethod == PaymentMethod.Credit)
                    .Sum(t => CalculateTotalAmount(t));

                // Calculate credit details
                foreach (var transaction in transactions.Where(t => t.PaymentMethod == PaymentMethod.Credit))
                {
                    var paidAmount = await CalculatePaidAmountAsync(transaction.Id);
                    var totalAmount = CalculateTotalAmount(transaction);

                    summary.TotalPaidCreditAmount += paidAmount;
                    summary.TotalPendingCreditAmount += (totalAmount - paidAmount);
                }

                // Calculate commission
                summary.TotalCommissionAmount = transactions.Sum(t => CalculateCommissionAmount(t));
                summary.TotalPaidCommission = transactions
                    .Where(t => t.CommissionPaid)
                    .Sum(t => CalculateCommissionAmount(t));
                summary.TotalPendingCommission = summary.TotalCommissionAmount - summary.TotalPaidCommission;

                // Calculate quantities
                summary.TotalQuantitySold = transactions.Sum(t => t.Quantity);

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

                // Calculate top products
                var productGroups = transactions
                    .GroupBy(t => new { t.ProductId, t.Product.Name, t.ItemCode })
                    .Select(g => new TransactionProductSummaryDto
                    {
                        ProductId = g.Key.ProductId,
                        ProductName = g.Key.Name,
                        ItemCode = g.Key.ItemCode,
                        TransactionCount = g.Count(),
                        TotalQuantity = g.Sum(t => t.Quantity),
                        TotalAmount = g.Sum(t => CalculateTotalAmount(t)),
                        PercentageOfTotal = summary.TotalSalesAmount > 0
                            ? (g.Sum(t => CalculateTotalAmount(t)) / summary.TotalSalesAmount) * 100
                            : 0
                    })
                    .OrderByDescending(p => p.TotalAmount)
                    .Take(10)
                    .ToList();

                summary.TopProducts = productGroups;

                // Calculate top customers
                var customerGroups = transactions
                    .Where(t => t.CustomerId.HasValue && t.Customer != null)
                    .GroupBy(t => new { t.CustomerId, t.Customer.Name, t.Customer.PhoneNumber })
                    .Select(g => new TransactionCustomerSummaryDto
                    {
                        CustomerId = g.Key.CustomerId.Value,
                        CustomerName = g.Key.Name,
                        CustomerPhoneNumber = g.Key.PhoneNumber,
                        TransactionCount = g.Count(),
                        TotalAmount = g.Sum(t => CalculateTotalAmount(t))
                    })
                    .OrderByDescending(c => c.TotalAmount)
                    .Take(10)
                    .ToList();

                // Calculate credit details for customers
                foreach (var customer in customerGroups)
                {
                    var customerCreditTransactions = transactions
                        .Where(t => t.CustomerId == customer.CustomerId && t.PaymentMethod == PaymentMethod.Credit);

                    foreach (var transaction in customerCreditTransactions)
                    {
                        var paidAmount = await CalculatePaidAmountAsync(transaction.Id);
                        var totalAmount = CalculateTotalAmount(transaction);

                        customer.CreditAmount += totalAmount;
                        customer.PaidCreditAmount += paidAmount;
                        customer.PendingCreditAmount += (totalAmount - paidAmount);
                    }
                }

                summary.TopCustomers = customerGroups;

                // Calculate top painters
                var painterGroups = transactions
                    .Where(t => t.PainterId.HasValue && t.Painter != null)
                    .GroupBy(t => new { t.PainterId, t.Painter.Name, t.Painter.PhoneNumber })
                    .Select(g => new TransactionPainterSummaryDto
                    {
                        PainterId = g.Key.PainterId.Value,
                        PainterName = g.Key.Name,
                        PainterPhoneNumber = g.Key.PhoneNumber,
                        TransactionCount = g.Count(),
                        TotalCommissionAmount = g.Sum(t => CalculateCommissionAmount(t)),
                        PaidCommissionAmount = g.Where(t => t.CommissionPaid).Sum(t => CalculateCommissionAmount(t)),
                        PendingCommissionAmount = g.Where(t => !t.CommissionPaid).Sum(t => CalculateCommissionAmount(t))
                    })
                    .OrderByDescending(p => p.TotalCommissionAmount)
                    .Take(10)
                    .ToList();

                summary.TopPainters = painterGroups;

                // Get recent transactions
                var recentTransactions = transactions
                    .OrderByDescending(t => t.TransactionDate)
                    .ThenByDescending(t => t.CreatedAt)
                    .Take(10)
                    .ToList();

                var recentDtos = _mapper.Map<List<TransactionResponseDto>>(recentTransactions);

                // Calculate paid amounts for credit transactions
                foreach (var transaction in recentDtos.Where(t => t.IsCredit))
                {
                    transaction.PaidAmount = await CalculatePaidAmountAsync(transaction.Id);
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
                    ProductId = productId,
                    BranchId = branchId,
                    Quantity = -quantity,
                    UpdatedAt = DateTime.UtcNow
                };
                await _context.Stocks.AddAsync(newStock);
                _logger.LogWarning("Created new stock record with negative quantity for ProductId: {ProductId}", productId);
            }
        }

        // NEW: Create StockMovement record
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
                    ProductId = transaction.ProductId,
                    BranchId = transaction.BranchId,
                    TransactionId = transaction.Id,
                    MovementType = StockMovementType.Sale,
                    Quantity = -transaction.Quantity, // Negative for sales
                    PreviousQuantity = previousQuantity,
                    NewQuantity = newQuantity,
                    MovementDate = transaction.TransactionDate,
                    Reason = $"Sale transaction - {transaction.ItemCode}"
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

        // NEW: Create StockMovement for credit payments
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
                    ProductId = transaction.ProductId,
                    BranchId = transaction.BranchId,
                    TransactionId = transaction.Id,
                    MovementType = StockMovementType.Sale,
                    Quantity = -paidQuantity, // Negative for sales
                    PreviousQuantity = previousQuantity,
                    NewQuantity = newQuantity,
                    MovementDate = DateTime.UtcNow,
                    Reason = $"Credit payment - {transaction.ItemCode}"
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

        private async Task CreateDailySalesAsync(Transaction transaction)
        {
            var dailySales = new DailySales
            {
                BranchId = transaction.BranchId,
                ProductId = transaction.ProductId,
                TransactionId = transaction.Id,
                SaleDate = transaction.TransactionDate.Date,
                Quantity = transaction.Quantity,
                UnitPrice = transaction.UnitPrice,
                TotalAmount = CalculateTotalAmount(transaction), // Use helper method
                PaymentMethod = transaction.PaymentMethod,
                CommissionRate = transaction.CommissionRate,
                CommissionAmount = CalculateCommissionAmount(transaction), // Use helper method
                CommissionPaid = transaction.CommissionPaid,
                CustomerId = transaction.CustomerId,
                PainterId = transaction.PainterId
            };
            await _context.DailySales.AddAsync(dailySales);
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