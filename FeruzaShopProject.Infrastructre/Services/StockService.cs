using AutoMapper;
using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;
using FeruzaShopProject.Infrastructre.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FeruzaShopProject.Infrastructre.Services
{
    public class StockService : IStockService
    {
        private readonly ShopDbContext _context;
        private readonly ILogger<StockService> _logger;

        public StockService(ShopDbContext context, ILogger<StockService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Get stock on a specific date with credit information
        /// </summary>
        /// <summary>
        /// Get stock for all products on a specific date with credit information
        /// </summary>
        public async Task<ApiResponse<List<StockOnDateDto>>> GetStockOnDateAsync(DateTime date, Guid? branchId = null, Guid? productId = null)
        {
            try
            {
                _logger.LogInformation("Getting stock for all products on Date:{Date}, Branch:{BranchId}, Product:{ProductId}",
                    date.Date, branchId, productId);

                var result = new List<StockOnDateDto>();

                // Get all active products
                var productsQuery = _context.Products
                    .Where(p => p.IsActive)
                    .AsQueryable();

                if (productId.HasValue)
                    productsQuery = productsQuery.Where(p => p.Id == productId.Value);

                var products = await productsQuery.ToListAsync();

                // Get all active branches
                var branchesQuery = _context.Branches
                    .Where(b => b.IsActive)
                    .AsQueryable();

                if (branchId.HasValue)
                    branchesQuery = branchesQuery.Where(b => b.Id == branchId.Value);

                var branches = await branchesQuery.ToListAsync();

                if (!products.Any() || !branches.Any())
                {
                    return ApiResponse<List<StockOnDateDto>>.Success(result, "No products or branches found");
                }

                // Get all stock movements up to the requested date
                var movementsQuery = _context.StockMovements
                    .Where(m => m.MovementDate.Date <= date.Date && m.IsActive)
                    .AsQueryable();

                if (branchId.HasValue)
                    movementsQuery = movementsQuery.Where(m => m.BranchId == branchId.Value);

                if (productId.HasValue)
                    movementsQuery = movementsQuery.Where(m => m.ProductId == productId.Value);

                var allMovements = await movementsQuery
                    .OrderBy(m => m.ProductId)
                    .ThenBy(m => m.BranchId)
                    .ThenBy(m => m.MovementDate)
                    .ToListAsync();

                // Process each product-branch combination
                foreach (var product in products)
                {
                    foreach (var branch in branches)
                    {
                        // Get the most recent movement for this product and branch up to the date
                        var lastMovement = allMovements
                            .Where(m => m.ProductId == product.Id && m.BranchId == branch.Id)
                            .OrderByDescending(m => m.MovementDate)
                            .FirstOrDefault();

                        decimal quantity = 0;

                        if (lastMovement != null)
                        {
                            quantity = lastMovement.NewQuantity;
                        }
                        else
                        {
                            // If no movements, check if there's a stock record with initial quantity
                            var stockRecord = await _context.Stocks
                                .FirstOrDefaultAsync(s => s.ProductId == product.Id &&
                                                         s.BranchId == branch.Id &&
                                                         s.IsActive);

                            if (stockRecord != null && stockRecord.CreatedAt.Date <= date.Date)
                            {
                                // This product existed on that date with this quantity
                                quantity = stockRecord.Quantity;
                            }
                            else
                            {
                                // No stock on this date
                                continue;
                            }
                        }

                        // Calculate credit stock on that date
                        decimal creditStock = await GetCreditStockOnDateAsync(product.Id, branch.Id, date);

                        result.Add(new StockOnDateDto
                        {
                            ProductId = product.Id,
                            ProductName = product.Name,
                            ItemCode = product.ItemCode,
                            BranchId = branch.Id,
                            BranchName = branch.Name,
                            Date = date,
                            Quantity = quantity,
                            CreditQuantity = creditStock,
                            NetQuantity = quantity - creditStock,
                            UnitPrice = product.UnitPrice,
                            BuyingPrice = product.BuyingPrice,
                            TotalValue = quantity * product.BuyingPrice,
                            CreditValue = creditStock * product.BuyingPrice,
                            NetValue = (quantity - creditStock) * product.BuyingPrice
                        });
                    }
                }

                _logger.LogInformation("Retrieved stock for {Count} product-branch combinations on {Date}",
                    result.Count, date.Date);

                return ApiResponse<List<StockOnDateDto>>.Success(result, $"Stock on {date:yyyy-MM-dd} retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating stock for date: {Date}", date);
                return ApiResponse<List<StockOnDateDto>>.Fail($"Error calculating stock: {ex.Message}");
            }
        }
        /// <summary>
        /// Get current stock with credit information for all products/branches
        /// </summary>
        public async Task<ApiResponse<CurrentStockDto>> GetCurrentStockAsync(Guid? branchId = null, Guid? productId = null)
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                _logger.LogInformation("Getting current stock for Branch:{BranchId}, Product:{ProductId}",
                    branchId, productId);

                var result = new CurrentStockDto
                {
                    Date = today,
                    Items = new List<StockItemDetailDto>(),
                    TotalActualStock = 0,
                    TotalCreditStock = 0,
                    TotalNetStock = 0,
                    TotalActualValue = 0,
                    TotalCreditValue = 0,
                    TotalNetValue = 0,
                    InStockItems = 0,
                    LowStockItems = 0,
                    OutOfStockItems = 0
                };

                // Get stocks directly from Stock table (this is the source of truth)
                var stocksQuery = _context.Stocks
                    .Include(s => s.Product)
                        .ThenInclude(p => p.Category)
                    .Include(s => s.Branch)
                    .Where(s => s.IsActive && s.Product.IsActive)
                    .AsQueryable();

                if (branchId.HasValue)
                    stocksQuery = stocksQuery.Where(s => s.BranchId == branchId.Value);

                if (productId.HasValue)
                    stocksQuery = stocksQuery.Where(s => s.ProductId == productId.Value);

                var stocks = await stocksQuery.ToListAsync();

                // Get unpaid credit quantities
                var unpaidCreditQuantities = await GetUnpaidCreditQuantitiesAsync(branchId, productId);

                foreach (var stock in stocks)
                {
                    var key = $"{stock.ProductId}_{stock.BranchId}";
                    decimal creditStock = unpaidCreditQuantities.ContainsKey(key) ? unpaidCreditQuantities[key] : 0;
                    decimal netStock = stock.Quantity - creditStock;
                    if (netStock < 0) netStock = 0;

                    var stockItem = new StockItemDetailDto
                    {
                        // Product Info
                        ProductId = stock.ProductId,
                        ProductName = stock.Product.Name,
                        ItemCode = stock.Product.ItemCode,
                        CategoryName = stock.Product.Category?.Name,

                        // Branch Info
                        BranchId = stock.BranchId,
                        BranchName = stock.Branch.Name,

                        // Stock Quantities
                        ActualQuantity = stock.Quantity,  // Direct from Stock table
                        CreditQuantity = creditStock,
                        NetQuantity = netStock,

                        // Financial Values
                        BuyingPrice = stock.Product.BuyingPrice,
                        SellingPrice = stock.Product.UnitPrice,
                        ActualValue = stock.Quantity * stock.Product.BuyingPrice,
                        CreditValue = creditStock * stock.Product.BuyingPrice,
                        NetValue = netStock * stock.Product.BuyingPrice,

                        // Status
                        ReorderLevel = stock.Product.ReorderLevel,
                        StockStatus = GetStockStatus(netStock, stock.Product.ReorderLevel),

                        // Unit Info
                        UnitAmount = stock.Product.Amount,
                        UnitType = stock.Product.Unit.ToString()
                    };

                    result.Items.Add(stockItem);

                    // Update totals
                    result.TotalActualStock += stock.Quantity;
                    result.TotalCreditStock += creditStock;
                    result.TotalNetStock += netStock;
                    result.TotalActualValue += stock.Quantity * stock.Product.BuyingPrice;
                    result.TotalCreditValue += creditStock * stock.Product.BuyingPrice;
                    result.TotalNetValue += netStock * stock.Product.BuyingPrice;

                    // Update status counts
                    switch (stockItem.StockStatus)
                    {
                        case "In Stock": result.InStockItems++; break;
                        case "Low Stock": result.LowStockItems++; break;
                        case "Out of Stock": result.OutOfStockItems++; break;
                    }
                }

                result.TotalItems = result.Items.Count;

                _logger.LogInformation("Current stock retrieved: {TotalItems} items, Net Value: {TotalNetValue:C2}",
                    result.TotalItems, result.TotalNetValue);

                return ApiResponse<CurrentStockDto>.Success(result, "Current stock retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCurrentStockAsync");
                return ApiResponse<CurrentStockDto>.Fail($"Error retrieving current stock: {ex.Message}");
            }
        }
        /// <summary>
        /// Get detailed stock history with movement breakdown
        /// </summary>
        public async Task<ApiResponse<List<StockHistoryDto>>> GetStockHistoryAsync(
            Guid productId, Guid branchId, DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogInformation("Getting stock history for Product:{ProductId}, Branch:{BranchId}, From:{StartDate:yyyy-MM-dd} To:{EndDate:yyyy-MM-dd}",
                    productId, branchId, startDate, endDate);

                // Validate date range
                if (startDate > endDate)
                {
                    return ApiResponse<List<StockHistoryDto>>.Fail("Start date cannot be after end date");
                }

                var history = new List<StockHistoryDto>();

                // Get ALL movements for this product+branch up to end date
                var allMovements = await _context.StockMovements
                    .Where(m => m.ProductId == productId
                             && m.BranchId == branchId
                             && m.MovementDate.Date <= endDate.Date)
                    .OrderBy(m => m.MovementDate)
                    .ToListAsync();

                // Get opening balance before start date
                var openingMovements = allMovements
                    .Where(m => m.MovementDate.Date < startDate.Date)
                    .ToList();

                decimal currentStock = 0;
                foreach (var movement in openingMovements)
                {
                    currentStock = ApplyMovementToStock(currentStock, movement);
                }

                // Group movements by date
                var movementsByDate = allMovements
                    .Where(m => m.MovementDate.Date >= startDate.Date && m.MovementDate.Date <= endDate.Date)
                    .GroupBy(m => m.MovementDate.Date)
                    .OrderBy(g => g.Key)
                    .ToDictionary(g => g.Key, g => g.ToList());

                // For each day in the range
                for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
                {
                    decimal openingBalance = currentStock;
                    decimal purchases = 0, sales = 0, returns = 0, adjustments = 0, damages = 0;

                    if (movementsByDate.TryGetValue(date, out var dayMovements))
                    {
                        foreach (var movement in dayMovements)
                        {
                            switch (movement.MovementType)
                            {
                                case StockMovementType.Purchase:
                                    purchases += movement.Quantity;
                                    break;
                                case StockMovementType.Sale:
                                    sales += Math.Abs(movement.Quantity);
                                    break;
                                case StockMovementType.Return:
                                    returns += movement.Quantity;
                                    break;
                                case StockMovementType.Adjustment:
                                    adjustments += movement.Quantity;
                                    break;
                                case StockMovementType.Damage:
                                    damages += Math.Abs(movement.Quantity);
                                    break;
                            }
                            currentStock = ApplyMovementToStock(currentStock, movement);
                        }
                    }

                    // Get credit stock for this date
                    decimal creditStock = await GetCreditStockOnDateAsync(productId, branchId, date);

                    history.Add(new StockHistoryDto
                    {
                        Date = date,
                        OpeningBalance = openingBalance,
                        ClosingBalance = currentStock,
                        NetChange = currentStock - openingBalance,
                        ChangeType = GetChangeType(currentStock, openingBalance),
                        Purchases = purchases,
                        Sales = sales,
                        Returns = returns,
                        Adjustments = adjustments,
                        Damages = damages,
                        CreditStock = creditStock,
                        NetAvailable = currentStock - creditStock,
                        Movements = dayMovements?.Select(m => new StockMovementSummaryDto
                        {
                            MovementId = m.Id,
                            MovementType = m.MovementType.ToString(),
                            Quantity = m.Quantity,
                            Reason = m.Reason,
                            MovementDate = m.MovementDate,
                            PreviousQuantity = m.PreviousQuantity,
                            NewQuantity = m.NewQuantity
                        }).ToList() ?? new List<StockMovementSummaryDto>()
                    });
                }

                _logger.LogInformation("Stock history retrieved: {DaysCount} days", history.Count);
                return ApiResponse<List<StockHistoryDto>>.Success(history, "Stock history retrieved successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStockHistoryAsync for Product:{ProductId}, Branch:{BranchId}",
                    productId, branchId);
                return ApiResponse<List<StockHistoryDto>>.Fail($"Error retrieving stock history: {ex.Message}");
            }
        }

        /// <summary>
        /// Get credit stock summary (unpaid credit items)
        /// </summary>
        public async Task<ApiResponse<StockCreditSummaryDto>> GetCreditStockSummaryAsync(Guid? branchId = null, Guid? customerId = null)
        {
            try
            {
                var result = new StockCreditSummaryDto
                {
                    GeneratedAt = DateTime.UtcNow,
                    Items = new List<CreditStockItemDto>()
                };

                // Get all credit transactions that are not fully paid
                var query = _context.Transactions
                    .Include(t => t.Customer)
                    .Include(t => t.Product)
                    .Include(t => t.Branch)
                    .Include(t => t.CreditPayments)
                    .Where(t => t.PaymentMethod == PaymentMethod.Credit && t.IsActive)
                    .AsQueryable();

                if (branchId.HasValue)
                    query = query.Where(t => t.BranchId == branchId.Value);

                if (customerId.HasValue)
                    query = query.Where(t => t.CustomerId == customerId.Value);

                var transactions = await query.ToListAsync();
                var uniqueCustomers = new HashSet<Guid?>();

                foreach (var transaction in transactions)
                {
                    var totalAmount = transaction.UnitPrice * transaction.Quantity;
                    var paidAmount = transaction.CreditPayments?.Sum(p => p.Amount) ?? 0;

                    if (paidAmount < totalAmount)
                    {
                        // Calculate unpaid quantity
                        var paidPercentage = totalAmount > 0 ? paidAmount / totalAmount : 0;
                        var paidQuantity = transaction.Quantity * paidPercentage;
                        var unpaidQuantity = transaction.Quantity - paidQuantity;

                        var daysOverdue = (DateTime.UtcNow - transaction.TransactionDate).Days;

                        result.Items.Add(new CreditStockItemDto
                        {
                            TransactionId = transaction.Id,
                            TransactionDate = transaction.TransactionDate,
                            CustomerName = transaction.Customer?.Name ?? "Unknown",
                            ProductName = transaction.Product?.Name ?? "Unknown",
                            ItemCode = transaction.ItemCode,
                            Quantity = transaction.Quantity,
                            UnitPrice = transaction.UnitPrice,
                            TotalAmount = totalAmount,
                            PaidAmount = paidAmount,
                            PendingAmount = totalAmount - paidAmount,
                            PendingQuantity = unpaidQuantity,
                            BranchName = transaction.Branch?.Name ?? "Unknown",
                            LastPaymentDate = transaction.CreditPayments?.OrderByDescending(p => p.PaymentDate).FirstOrDefault()?.PaymentDate,
                            DaysOverdue = daysOverdue > 30 ? daysOverdue : 0 // Only show if overdue
                        });

                        if (transaction.CustomerId.HasValue)
                            uniqueCustomers.Add(transaction.CustomerId);
                    }
                }

                result.TotalCustomers = uniqueCustomers.Count;
                result.TotalCreditAmount = result.Items.Sum(i => i.TotalAmount);
                result.TotalPaidAmount = result.Items.Sum(i => i.PaidAmount);
                result.TotalPendingAmount = result.Items.Sum(i => i.PendingAmount);

                _logger.LogInformation("Credit stock summary retrieved: {Count} items", result.Items.Count);
                return ApiResponse<StockCreditSummaryDto>.Success(result, "Credit stock summary retrieved");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting credit stock summary");
                return ApiResponse<StockCreditSummaryDto>.Fail($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get stock alerts (low stock, out of stock, overdue credit)
        /// </summary>
        public async Task<ApiResponse<StockAlertDto>> GetStockAlertsAsync(Guid? branchId = null)
        {
            try
            {
                var currentStock = await GetCurrentStockAsync(branchId);

                var alertDto = new StockAlertDto
                {
                    LowStockItems = new List<StockItemDetailDto>(),
                    OutOfStockItems = new List<StockItemDetailDto>(),
                    OverdueCreditItems = new List<CreditStockItemDto>()
                };

                if (currentStock.IsCompletedSuccessfully && currentStock.Data != null)
                {
                    alertDto.LowStockItems = currentStock.Data.Items
                        .Where(i => i.StockStatus == "Low Stock")
                        .ToList();

                    alertDto.OutOfStockItems = currentStock.Data.Items
                        .Where(i => i.StockStatus == "Out of Stock")
                        .ToList();
                }

                // Get overdue credit items (more than 30 days)
                var creditSummary = await GetCreditStockSummaryAsync(branchId);
                if (creditSummary.IsCompletedSuccessfully && creditSummary.Data != null)
                {
                    alertDto.OverdueCreditItems = creditSummary.Data.Items
                        .Where(i => i.DaysOverdue > 30)
                        .OrderByDescending(i => i.DaysOverdue)
                        .ToList();
                }

                alertDto.AlertCount = alertDto.LowStockItems.Count +
                                      alertDto.OutOfStockItems.Count +
                                      alertDto.OverdueCreditItems.Count;

                return ApiResponse<StockAlertDto>.Success(alertDto, "Stock alerts retrieved");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting stock alerts");
                return ApiResponse<StockAlertDto>.Fail($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get detailed stock for a specific product in a branch
        /// </summary>
        public async Task<ApiResponse<StockItemDetailDto>> GetProductStockDetailAsync(Guid productId, Guid branchId)
        {
            try
            {
                var currentStock = await GetCurrentStockAsync(branchId, productId);

                if (currentStock.IsCompletedSuccessfully && currentStock.Data != null && currentStock.Data.Items.Any())
                {
                    return ApiResponse<StockItemDetailDto>.Success(currentStock.Data.Items.First(), "Product stock detail retrieved");
                }

                return ApiResponse<StockItemDetailDto>.Fail("Product stock not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product stock detail for {ProductId}", productId);
                return ApiResponse<StockItemDetailDto>.Fail($"Error: {ex.Message}");
            }
        }

        #region Helper Methods

        private decimal ApplyMovementToStock(decimal currentStock, StockMovement movement)
        {
            return movement.MovementType switch
            {
                StockMovementType.Purchase => currentStock + movement.Quantity,
                StockMovementType.Sale => currentStock - Math.Abs(movement.Quantity),
                StockMovementType.Return => currentStock + movement.Quantity,
                StockMovementType.Adjustment => currentStock + movement.Quantity,
                StockMovementType.Damage => currentStock - Math.Abs(movement.Quantity),
                StockMovementType.Transfer => currentStock + movement.Quantity,
                _ => currentStock
            };
        }

        private async Task<Dictionary<string, decimal>> GetUnpaidCreditQuantitiesAsync(Guid? branchId = null, Guid? productId = null)
        {
            var result = new Dictionary<string, decimal>();

            try
            {
                var creditTransactionsQuery = _context.Transactions
                    .Include(t => t.CreditPayments)
                    .Where(t => t.PaymentMethod == PaymentMethod.Credit && t.IsActive)
                    .AsQueryable();

                if (branchId.HasValue)
                    creditTransactionsQuery = creditTransactionsQuery.Where(t => t.BranchId == branchId.Value);

                if (productId.HasValue)
                    creditTransactionsQuery = creditTransactionsQuery.Where(t => t.ProductId == productId.Value);

                var creditTransactions = await creditTransactionsQuery.ToListAsync();

                foreach (var transaction in creditTransactions)
                {
                    var totalAmount = transaction.UnitPrice * transaction.Quantity;
                    var paidAmount = transaction.CreditPayments?.Sum(p => p.Amount) ?? 0;

                    if (paidAmount < totalAmount)
                    {
                        var paidPercentage = totalAmount > 0 ? paidAmount / totalAmount : 0;
                        var paidQuantity = transaction.Quantity * paidPercentage;
                        var unpaidQuantity = transaction.Quantity - paidQuantity;

                        var key = $"{transaction.ProductId}_{transaction.BranchId}";

                        if (result.ContainsKey(key))
                            result[key] += unpaidQuantity;
                        else
                            result[key] = unpaidQuantity;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating unpaid credit quantities");
            }

            return result;
        }

        private async Task<decimal> GetCreditStockOnDateAsync(Guid productId, Guid branchId, DateTime date)
        {
            try
            {
                // Get all credit transactions up to this date
                var creditTransactions = await _context.Transactions
                    .Include(t => t.CreditPayments)
                    .Where(t => t.ProductId == productId
                             && t.BranchId == branchId
                             && t.PaymentMethod == PaymentMethod.Credit
                             && t.IsActive
                             && t.TransactionDate.Date <= date.Date)
                    .ToListAsync();

                decimal totalCreditStock = 0;

                foreach (var transaction in creditTransactions)
                {
                    var totalAmount = transaction.UnitPrice * transaction.Quantity;

                    // Get payments up to this date
                    var paymentsUpToDate = transaction.CreditPayments?
                        .Where(p => p.PaymentDate.Date <= date.Date)
                        .Sum(p => p.Amount) ?? 0;

                    if (paymentsUpToDate < totalAmount)
                    {
                        var paidPercentage = totalAmount > 0 ? paymentsUpToDate / totalAmount : 0;
                        var paidQuantity = transaction.Quantity * paidPercentage;
                        var unpaidQuantity = transaction.Quantity - paidQuantity;
                        totalCreditStock += unpaidQuantity;
                    }
                }

                return totalCreditStock;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating credit stock on date");
                return 0;
            }
        }

        private string GetStockStatus(decimal quantity, int reorderLevel)
        {
            if (quantity <= 0)
                return "Out of Stock";
            if (quantity <= reorderLevel)
                return "Low Stock";
            return "In Stock";
        }

        private string GetChangeType(decimal current, decimal previous)
        {
            if (current > previous) return "Increased";
            if (current < previous) return "Decreased";
            return "No Change";
        }

        #endregion
    }
}