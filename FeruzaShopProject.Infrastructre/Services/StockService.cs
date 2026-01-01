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
            _context = context;
            _logger = logger;
        }

        public async Task<ApiResponse<decimal>> GetStockOnDateAsync(Guid productId, Guid branchId, DateTime date)
        {
            try
            {
                _logger.LogInformation("Getting stock for Product:{ProductId}, Branch:{BranchId}, Date:{Date}",
                    productId, branchId, date.Date);

                // Get the MOST RECENT StockMovement up to the requested date
                var lastMovement = await _context.StockMovements
                    .Where(m => m.ProductId == productId
                             && m.BranchId == branchId
                             && m.MovementDate.Date <= date.Date)
                    .OrderByDescending(m => m.MovementDate)
                    .FirstOrDefaultAsync();

                // If no movements, stock is 0
                if (lastMovement == null)
                {
                    return ApiResponse<decimal>.Success(0, $"Stock quantity on {date:yyyy-MM-dd}");
                }

                // The stock on that date is the NewQuantity from the last movement
                decimal stockQuantity = lastMovement.NewQuantity;

                _logger.LogInformation("Calculated stock for {Date}: {Quantity} items", date.Date, stockQuantity);
                return ApiResponse<decimal>.Success(stockQuantity, $"Stock quantity on {date:yyyy-MM-dd}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating stock for Product:{ProductId}, Branch:{BranchId}, Date:{Date}",
                    productId, branchId, date);
                return ApiResponse<decimal>.Fail($"Error calculating stock: {ex.Message}");
            }
        }
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
                    Items = new List<StockItemDto>()
                };

                // Get base query
                IQueryable<StockMovement> query = _context.StockMovements;

                // Apply filters
                if (branchId.HasValue)
                {
                    query = query.Where(m => m.BranchId == branchId.Value);
                }

                if (productId.HasValue)
                {
                    query = query.Where(m => m.ProductId == productId.Value);
                }

                // Get all movements up to today
                var allMovements = await query.ToListAsync();

                // Group by product and branch
                var stockGroups = allMovements
                    .GroupBy(m => new { m.ProductId, m.BranchId })
                    .ToList();

                // Get all product and branch details in one query for efficiency
                var productIds = stockGroups.Select(g => g.Key.ProductId).Distinct().ToList();
                var branchIds = stockGroups.Select(g => g.Key.BranchId).Distinct().ToList();

                var products = await _context.Products
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                var branches = await _context.Branches
                    .Where(b => branchIds.Contains(b.Id))
                    .ToDictionaryAsync(b => b.Id);

                // Calculate stock for each group
                foreach (var group in stockGroups)
                {
                    var currentProductId = group.Key.ProductId;
                    var currentBranchId = group.Key.BranchId;

                    // Calculate current stock
                    decimal stockQuantity = 0;
                    foreach (var movement in group)
                    {
                        stockQuantity = ApplyMovementToStock(stockQuantity, movement);
                    }

                    // Get product and branch details
                    if (products.TryGetValue(currentProductId, out var product) &&
                        branches.TryGetValue(currentBranchId, out var branch))
                    {
                        result.Items.Add(new StockItemDto
                        {
                            ProductId = product.Id,
                            ProductName = product.Name,
                            ItemCode = product.ItemCode,
                            BranchId = branch.Id,
                            BranchName = branch.Name,
                            Quantity = stockQuantity,
                            UnitPrice = product.UnitPrice,
                            TotalValue = stockQuantity * product.BuyingPrice
                        });
                    }
                }

                // Calculate totals
                result.TotalItems = result.Items.Count;
                result.TotalValue = result.Items.Sum(i => i.TotalValue);

                _logger.LogInformation("Current stock retrieved: {TotalItems} items, Total Value: {TotalValue}",
                    result.TotalItems, result.TotalValue);

                return ApiResponse<CurrentStockDto>.Success(result, "Current stock retrieved");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCurrentStockAsync");
                return ApiResponse<CurrentStockDto>.Fail($"Error retrieving current stock: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<StockHistoryDto>>> GetStockHistoryAsync(
            Guid productId, Guid branchId, DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogInformation("Getting stock history for Product:{ProductId}, Branch:{BranchId}, From:{StartDate} To:{EndDate}",
                    productId, branchId, startDate, endDate);

                // Validate date range
                if (startDate > endDate)
                {
                    return ApiResponse<List<StockHistoryDto>>.Fail("Start date cannot be after end date");
                }

                var history = new List<StockHistoryDto>();

                // Get ALL movements for this product+branch (ordered by date)
                var allMovements = await _context.StockMovements
                    .Where(m => m.ProductId == productId && m.BranchId == branchId)
                    .OrderBy(m => m.MovementDate)
                    .ToListAsync();

                // For each day in the range
                for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
                {
                    // Calculate stock for this date
                    decimal stockOnDate = CalculateStockUpToDate(allMovements, date);

                    // Calculate stock for previous day (for change calculation)
                    decimal stockPreviousDay = date > startDate.Date
                        ? CalculateStockUpToDate(allMovements, date.AddDays(-1))
                        : 0;

                    history.Add(new StockHistoryDto
                    {
                        Date = date,
                        Quantity = stockOnDate,
                        Change = stockOnDate - stockPreviousDay,
                        ChangeType = GetChangeType(stockOnDate, stockPreviousDay)
                    });
                }

                _logger.LogInformation("Stock history retrieved: {DaysCount} days", history.Count);
                return ApiResponse<List<StockHistoryDto>>.Success(history, "Stock history retrieved");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStockHistoryAsync for Product:{ProductId}, Branch:{BranchId}",
                    productId, branchId);
                return ApiResponse<List<StockHistoryDto>>.Fail($"Error retrieving stock history: {ex.Message}");
            }
        }

        #region Helper Methods

        private decimal ApplyMovementToStock(decimal currentStock, StockMovement movement)
        {
            return movement.MovementType switch
            {
                StockMovementType.Purchase => currentStock + movement.Quantity,
                StockMovementType.Sale => currentStock - movement.Quantity,
                StockMovementType.Return => currentStock + movement.Quantity,
                StockMovementType.Adjustment => currentStock + movement.Quantity, // Can be +/-
                StockMovementType.Damage => currentStock - movement.Quantity,
                StockMovementType.Transfer => currentStock + movement.Quantity, // Can be +/-
                _ => currentStock
            };
        }

        private decimal CalculateStockUpToDate(List<StockMovement> movements, DateTime date)
        {
            decimal stock = 0;

            // Only consider movements up to the given date
            var relevantMovements = movements
                .Where(m => m.MovementDate.Date <= date.Date)
                .ToList();

            foreach (var movement in relevantMovements)
            {
                stock = ApplyMovementToStock(stock, movement);
            }

            return stock;
        }

        private string GetChangeType(decimal current, decimal previous)
        {
            if (current > previous) return "Increase";
            if (current < previous) return "Decrease";
            return "No Change";
        }

        #endregion
    }
}