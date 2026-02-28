using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;
using FeruzaShopProject.Infrastructre.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;

namespace FeruzaShopProject.Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly ShopDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<ProductService> _logger;

        public ProductService(ShopDbContext context, IMapper mapper, ILogger<ProductService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // ========== SINGLE CREATE ==========
        public async Task<ApiResponse<ProductResponseDto>> CreateProductAsync(CreateProductDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate category exists
                if (!await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId && c.IsActive))
                {
                    _logger.LogWarning("Category not found: {CategoryId}", dto.CategoryId);
                    return ApiResponse<ProductResponseDto>.Fail("Category not found");
                }

                // Check if product with this ItemCode already exists
                var existingProduct = await _context.Products
                    .Include(p => p.Stocks)
                    .FirstOrDefaultAsync(p => p.ItemCode == dto.ItemCode && p.IsActive);

                // Validate branches exist
                var branchIds = dto.BranchStocks.Select(b => b.BranchId).Distinct().ToList();
                var existingBranches = await _context.Branches
                    .Where(b => branchIds.Contains(b.Id) && b.IsActive)
                    .Select(b => b.Id)
                    .ToListAsync();

                var invalidBranches = branchIds.Except(existingBranches).ToList();
                if (invalidBranches.Any())
                {
                    _logger.LogWarning("Invalid branches: {BranchIds}", string.Join(", ", invalidBranches));
                    return ApiResponse<ProductResponseDto>.Fail($"Invalid branches: {string.Join(", ", invalidBranches)}");
                }

                if (existingProduct != null)
                {
                    // PRODUCT EXISTS - Add to new branches only

                    // Get branches where product already exists
                    var existingBranchIds = existingProduct.Stocks?.Select(s => s.BranchId).ToHashSet() ?? new HashSet<Guid>();

                    // Find branches that already have this product
                    var conflictingBranches = branchIds.Intersect(existingBranchIds).ToList();

                    if (conflictingBranches.Any())
                    {
                        _logger.LogWarning("Product already exists in branches: {BranchIds}", string.Join(", ", conflictingBranches));
                        return ApiResponse<ProductResponseDto>.Fail($"Product already exists in branches: {string.Join(", ", conflictingBranches)}");
                    }

                    // Add stocks for NEW branches only
                    foreach (var branchStock in dto.BranchStocks)
                    {
                        var stock = new Stock
                        {
                            Id = Guid.NewGuid(),
                            ProductId = existingProduct.Id,
                            BranchId = branchStock.BranchId,
                            Quantity = branchStock.Quantity,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        await _context.Stocks.AddAsync(stock);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var result = await GetProductResponse(existingProduct.Id);
                    _logger.LogInformation("Added existing product {ProductId} to new branches", existingProduct.Id);
                    return ApiResponse<ProductResponseDto>.Success(result, "Product added to new branches successfully");
                }
                else
                {
                    // NEW PRODUCT - Create product and all stocks

                    // Validate item code uniqueness (double-check)
                    if (await _context.Products.AnyAsync(p => p.ItemCode == dto.ItemCode && p.IsActive))
                    {
                        _logger.LogWarning("Item code already exists: {ItemCode}", dto.ItemCode);
                        return ApiResponse<ProductResponseDto>.Fail("Item code already exists");
                    }

                    // Create product
                    var product = _mapper.Map<Product>(dto);
                    product.Id = Guid.NewGuid();
                    product.CreatedAt = DateTime.UtcNow;
                    product.IsActive = true;

                    // Validate product amount based on unit type
                    product.ValidateAmount();

                    await _context.Products.AddAsync(product);

                    // Create stock entries for each branch
                    foreach (var branchStock in dto.BranchStocks)
                    {
                        var stock = new Stock
                        {
                            Id = Guid.NewGuid(),
                            ProductId = product.Id,
                            BranchId = branchStock.BranchId,
                            Quantity = branchStock.Quantity,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        await _context.Stocks.AddAsync(stock);
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var result = await GetProductResponse(product.Id);
                    _logger.LogInformation("Created product {ProductId} with ItemCode: {ItemCode}",
                        product.Id, product.ItemCode);
                    return ApiResponse<ProductResponseDto>.Success(result, "Product created successfully");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating product: {@Dto}", dto);
                return ApiResponse<ProductResponseDto>.Fail("Failed to create product");
            }
        }
        // ========== BULK CREATE ==========
        public async Task<ApiResponse<BulkProductResultDto>> BulkCreateProductsAsync(BulkCreateProductDto dto)
        {
            var result = new BulkProductResultDto
            {
                TotalProcessed = dto.Products.Count,
                SuccessfulProducts = new List<ProductResponseDto>(),
                FailedProducts = new List<BulkProductErrorDto>()
            };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _logger.LogInformation("Starting bulk product creation for {Count} products", dto.Products.Count);

                // Get all categories for validation
                var categoryIds = dto.Products.Select(p => p.CategoryId).Distinct().ToList();
                var validCategories = (await _context.Categories
                         .Where(c => categoryIds.Contains(c.Id) && c.IsActive)
                         .Select(c => c.Id)
                         .ToListAsync())
                         .ToHashSet();

                // Get all branch IDs for validation
                var allBranchIds = dto.Products
                    .SelectMany(p => p.BranchStocks.Select(b => b.BranchId))
                    .Distinct()
                    .ToList();
                var validBranches = (await _context.Branches
                        .Where(b => allBranchIds.Contains(b.Id) && b.IsActive)
                        .Select(b => b.Id)
                        .ToListAsync())
                        .ToHashSet();

                // Get existing products by ItemCode
                var allItemCodes = dto.Products.Select(p => p.ItemCode).Distinct().ToList();
                var existingProducts = await _context.Products
                    .Include(p => p.Stocks)
                    .Where(p => allItemCodes.Contains(p.ItemCode) && p.IsActive)
                    .ToDictionaryAsync(p => p.ItemCode);

                var productsToAdd = new List<Product>();
                var stocksToAdd = new List<Stock>();

                // Process each product
                for (int i = 0; i < dto.Products.Count; i++)
                {
                    var productDto = dto.Products[i];
                    var errors = new List<string>();

                    // Validate category
                    if (!validCategories.Contains(productDto.CategoryId))
                    {
                        errors.Add($"Category '{productDto.CategoryId}' not found or inactive");
                    }

                    // Validate branches
                    var branchIds = productDto.BranchStocks.Select(b => b.BranchId).Distinct().ToList();
                    var invalidBranches = branchIds.Where(b => !validBranches.Contains(b)).ToList();
                    if (invalidBranches.Any())
                    {
                        errors.Add($"Invalid branches: {string.Join(", ", invalidBranches)}");
                    }

                    // Validate at least one branch stock
                    if (!productDto.BranchStocks.Any())
                    {
                        errors.Add("At least one branch stock entry is required");
                    }

                    // Validate quantity non-negative
                    if (productDto.BranchStocks.Any(b => b.Quantity < 0))
                    {
                        errors.Add("Stock quantity cannot be negative");
                    }

                    // Check if product already exists
                    var existingProduct = existingProducts.GetValueOrDefault(productDto.ItemCode);

                    if (existingProduct != null)
                    {
                        // PRODUCT EXISTS - Check for branch conflicts
                        var existingBranchIds = existingProduct.Stocks?.Select(s => s.BranchId).ToHashSet() ?? new HashSet<Guid>();
                        var newBranchIds = productDto.BranchStocks.Select(b => b.BranchId).ToHashSet();

                        // Find branches that already have this product
                        var conflictingBranches = existingBranchIds.Intersect(newBranchIds).ToList();

                        if (conflictingBranches.Any())
                        {
                            errors.Add($"Product already exists in branches: {string.Join(", ", conflictingBranches)}. Use update endpoint to modify existing stock.");
                        }

                        if (errors.Any())
                        {
                            result.FailedCount++;
                            result.FailedProducts.Add(new BulkProductErrorDto
                            {
                                RowIndex = i + 1,
                                ItemCode = productDto.ItemCode,
                                ProductName = productDto.Name,
                                ErrorMessage = string.Join("; ", errors)
                            });
                            continue;
                        }

                        // Add stocks for NEW branches only
                        try
                        {
                            foreach (var branchStock in productDto.BranchStocks)
                            {
                                // Only add if product doesn't exist in this branch
                                if (!existingBranchIds.Contains(branchStock.BranchId))
                                {
                                    var stock = new Stock
                                    {
                                        Id = Guid.NewGuid(),
                                        ProductId = existingProduct.Id,
                                        BranchId = branchStock.BranchId,
                                        Quantity = branchStock.Quantity,
                                        CreatedAt = DateTime.UtcNow,
                                        IsActive = true
                                    };
                                    stocksToAdd.Add(stock);

                                    _logger.LogDebug("Adding existing product {ItemCode} to new branch {BranchId} with quantity {Quantity}",
                                        productDto.ItemCode, branchStock.BranchId, branchStock.Quantity);
                                }
                            }

                            // Add to successful results (will fetch complete product later)
                            result.SuccessCount++;
                        }
                        catch (Exception ex)
                        {
                            result.FailedCount++;
                            result.FailedProducts.Add(new BulkProductErrorDto
                            {
                                RowIndex = i + 1,
                                ItemCode = productDto.ItemCode,
                                ProductName = productDto.Name,
                                ErrorMessage = $"Error adding to new branch: {ex.Message}"
                            });
                        }
                    }
                    else
                    {
                        // NEW PRODUCT - Create product and all stocks
                        if (errors.Any())
                        {
                            result.FailedCount++;
                            result.FailedProducts.Add(new BulkProductErrorDto
                            {
                                RowIndex = i + 1,
                                ItemCode = productDto.ItemCode,
                                ProductName = productDto.Name,
                                ErrorMessage = string.Join("; ", errors)
                            });
                            continue;
                        }

                        try
                        {
                            // Create new product
                            var product = _mapper.Map<Product>(productDto);
                            product.Id = Guid.NewGuid();
                            product.CreatedAt = DateTime.UtcNow;
                            product.IsActive = true;

                            // Validate amount
                            product.ValidateAmount();

                            productsToAdd.Add(product);

                            // Add to existing products dictionary to prevent duplicates within same batch
                            existingProducts[product.ItemCode] = product;

                            // Create stock entries for all branches
                            foreach (var branchStock in productDto.BranchStocks)
                            {
                                var stock = new Stock
                                {
                                    Id = Guid.NewGuid(),
                                    ProductId = product.Id,
                                    BranchId = branchStock.BranchId,
                                    Quantity = branchStock.Quantity,
                                    CreatedAt = DateTime.UtcNow,
                                    IsActive = true
                                };
                                stocksToAdd.Add(stock);
                            }

                            result.SuccessCount++;
                            _logger.LogDebug("Prepared new product for bulk insert: {ItemCode}", product.ItemCode);
                        }
                        catch (Exception ex)
                        {
                            result.FailedCount++;
                            result.FailedProducts.Add(new BulkProductErrorDto
                            {
                                RowIndex = i + 1,
                                ItemCode = productDto.ItemCode,
                                ProductName = productDto.Name,
                                ErrorMessage = $"Validation error: {ex.Message}"
                            });
                        }
                    }
                }

                // Bulk insert all new products and stocks
                if (productsToAdd.Any())
                {
                    await _context.Products.AddRangeAsync(productsToAdd);
                    _logger.LogInformation("Adding {Count} new products", productsToAdd.Count);
                }

                if (stocksToAdd.Any())
                {
                    await _context.Stocks.AddRangeAsync(stocksToAdd);
                    _logger.LogInformation("Adding {Count} stock entries", stocksToAdd.Count);
                }

                if (productsToAdd.Any() || stocksToAdd.Any())
                {
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                // Get complete product responses for successful items
                var allAffectedProducts = new HashSet<Guid>();

                // Add newly created products
                foreach (var product in productsToAdd)
                {
                    allAffectedProducts.Add(product.Id);
                }

                // Add existing products that got new stocks
                foreach (var stock in stocksToAdd.Where(s => productsToAdd.All(p => p.Id != s.ProductId)))
                {
                    allAffectedProducts.Add(stock.ProductId);
                }

                foreach (var productId in allAffectedProducts)
                {
                    var productResponse = await GetProductResponse(productId);
                    if (productResponse != null && !result.SuccessfulProducts.Any(p => p.Id == productId))
                    {
                        result.SuccessfulProducts.Add(productResponse);
                    }
                }

                _logger.LogInformation("Bulk product creation completed. Success: {Success}, Failed: {Failed}",
                    result.SuccessCount, result.FailedCount);

                var message = $"Bulk product creation completed. {result.SuccessCount} products processed successfully, {result.FailedCount} failed.";
                return ApiResponse<BulkProductResultDto>.Success(result, message);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error in bulk product creation");
                return ApiResponse<BulkProductResultDto>.Fail("Failed to bulk create products");
            }
        }
        // ========== EXISTING METHODS ==========
        public async Task<ApiResponse<ProductResponseDto>> UpdateProductAsync(UpdateProductDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var product = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Stocks)
                    .ThenInclude(s => s.Branch)
                    .FirstOrDefaultAsync(p => p.Id == dto.Id && p.IsActive);

                if (product == null)
                {
                    _logger.LogWarning("Product not found: {ProductId}", dto.Id);
                    return ApiResponse<ProductResponseDto>.Fail("Product not found");
                }

                // Validate category if being updated
                if (dto.CategoryId.HasValue && !await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId && c.IsActive))
                {
                    _logger.LogWarning("New category not found: {CategoryId}", dto.CategoryId);
                    return ApiResponse<ProductResponseDto>.Fail("New category not found");
                }

                // Validate item code uniqueness if being updated
                if (!string.IsNullOrEmpty(dto.ItemCode) && dto.ItemCode != product.ItemCode &&
                    await _context.Products.AnyAsync(p => p.ItemCode == dto.ItemCode && p.Id != dto.Id && p.IsActive))
                {
                    _logger.LogWarning("Item code already exists: {ItemCode}", dto.ItemCode);
                    return ApiResponse<ProductResponseDto>.Fail("Item code already exists");
                }

                // Update product properties
                _mapper.Map(dto, product);
                product.UpdatedAt = DateTime.UtcNow;

                // Validate amount if being updated
                if (dto.Amount.HasValue || dto.Unit.HasValue)
                {
                    product.ValidateAmount();
                }

                // ========== HANDLE BRANCH STOCKS UPDATE ==========
                if (dto.BranchStocks != null && dto.BranchStocks.Any())
                {
                    // Validate branches exist
                    var branchIds = dto.BranchStocks.Select(b => b.BranchId).Distinct().ToList();
                    var existingBranches = await _context.Branches
                        .Where(b => branchIds.Contains(b.Id) && b.IsActive)
                        .Select(b => b.Id)
                        .ToListAsync();

                    var invalidBranches = branchIds.Except(existingBranches).ToList();
                    if (invalidBranches.Any())
                    {
                        _logger.LogWarning("Invalid branches: {BranchIds}", string.Join(", ", invalidBranches));
                        return ApiResponse<ProductResponseDto>.Fail($"Invalid branches: {string.Join(", ", invalidBranches)}");
                    }

                    // Get existing stock records
                    var existingStocks = product.Stocks.ToDictionary(s => s.BranchId);

                    // Track which branches we've processed
                    var processedBranchIds = new HashSet<Guid>();

                    foreach (var branchStock in dto.BranchStocks)
                    {
                        processedBranchIds.Add(branchStock.BranchId);

                        if (existingStocks.TryGetValue(branchStock.BranchId, out var existingStock))
                        {
                            // UPDATE existing stock
                            var oldQuantity = existingStock.Quantity;
                            existingStock.Quantity = branchStock.Quantity;
                            existingStock.UpdatedAt = DateTime.UtcNow;

                            _logger.LogDebug("Updated stock for Product {ProductId} in Branch {BranchId}: {OldQuantity} -> {NewQuantity}",
                                product.Id, branchStock.BranchId, oldQuantity, branchStock.Quantity);
                        }
                        else
                        {
                            // CREATE new stock for new branch
                            var newStock = new Stock
                            {
                                Id = Guid.NewGuid(),
                                ProductId = product.Id,
                                BranchId = branchStock.BranchId,
                                Quantity = branchStock.Quantity,
                                CreatedAt = DateTime.UtcNow,
                                IsActive = true
                            };
                            await _context.Stocks.AddAsync(newStock);

                            _logger.LogDebug("Added new stock for Product {ProductId} in Branch {BranchId} with quantity {Quantity}",
                                product.Id, branchStock.BranchId, branchStock.Quantity);
                        }
                    }

                    // REMOVE stocks for branches that are no longer in the list
                    var stocksToRemove = product.Stocks
                        .Where(s => !processedBranchIds.Contains(s.BranchId))
                        .ToList();

                    foreach (var stock in stocksToRemove)
                    {
                        stock.IsActive = false;
                        stock.UpdatedAt = DateTime.UtcNow;
                        _logger.LogDebug("Removed stock for Product {ProductId} from Branch {BranchId}",
                            product.Id, stock.BranchId);
                    }
                }

                _context.Products.Update(product);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = await GetProductResponse(product.Id);
                _logger.LogInformation("Updated product {ProductId} with branch stocks", product.Id);
                return ApiResponse<ProductResponseDto>.Success(result, "Product updated successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating product: {@Dto}", dto);
                return ApiResponse<ProductResponseDto>.Fail("Failed to update product");
            }
        }

        public async Task<ApiResponse<ProductResponseDto>> GetProductByIdAsync(Guid id)
        {
            try
            {
                var result = await GetProductResponse(id);
                if (result == null)
                {
                    _logger.LogWarning("Product not found: {ProductId}", id);
                    return ApiResponse<ProductResponseDto>.Fail("Product not found");
                }
                return ApiResponse<ProductResponseDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product: {ProductId}", id);
                return ApiResponse<ProductResponseDto>.Fail("Failed to retrieve product");
            }
        }

        public async Task<ApiResponse<List<ProductResponseDto>>> GetAllProductsAsync()
        {
            try
            {
                var products = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Stocks)
                    .ThenInclude(s => s.Branch)
                    .Where(p => p.IsActive)
                    .OrderBy(p => p.Name)
                    .ToListAsync();

                var result = _mapper.Map<List<ProductResponseDto>>(products);
                return ApiResponse<List<ProductResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all products");
                return ApiResponse<List<ProductResponseDto>>.Fail("Failed to retrieve products");
            }
        }

        public async Task<ApiResponse<List<ProductResponseDto>>> GetProductsByCategoryAsync(Guid categoryId)
        {
            try
            {
                if (!await _context.Categories.AnyAsync(c => c.Id == categoryId && c.IsActive))
                {
                    _logger.LogWarning("Category not found: {CategoryId}", categoryId);
                    return ApiResponse<List<ProductResponseDto>>.Fail("Category not found");
                }

                var products = await _context.Products
                    .Include(p => p.Category)
                    .Include(p => p.Stocks)
                    .ThenInclude(s => s.Branch)
                    .Where(p => p.CategoryId == categoryId && p.IsActive)
                    .OrderBy(p => p.Name)
                    .ToListAsync();

                var result = _mapper.Map<List<ProductResponseDto>>(products);
                return ApiResponse<List<ProductResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products for category: {CategoryId}", categoryId);
                return ApiResponse<List<ProductResponseDto>>.Fail("Failed to retrieve products");
            }
        }

        public async Task<ApiResponse<List<ProductBranchDto>>> GetProductsByBranchAsync(Guid branchId)
        {
            try
            {
                if (!await _context.Branches.AnyAsync(b => b.Id == branchId && b.IsActive))
                {
                    _logger.LogWarning("Branch not found: {BranchId}", branchId);
                    return ApiResponse<List<ProductBranchDto>>.Fail("Branch not found");
                }

                var stocks = await _context.Stocks
                    .Include(s => s.Product)
                        .ThenInclude(p => p.Category)
                    .Include(s => s.Branch)
                    .Where(s => s.BranchId == branchId && s.IsActive && s.Product.IsActive)
                    .OrderBy(s => s.Product.Name)
                    .ToListAsync();

                var result = stocks.Select(s => new ProductBranchDto
                {
                    ProductId = s.ProductId,
                    ProductName = s.Product.Name,
                    ItemCode = s.Product.ItemCode,
                    ItemDescription = s.Product.ItemDescription,
                    BuyingPrice = s.Product.BuyingPrice,
                    SellingPrice = s.Product.UnitPrice,
                    CommissionPerProduct = s.Product.CommissionPerProduct,
                    ReorderLevel = s.Product.ReorderLevel,
                    CategoryId = s.Product.CategoryId,
                    CategoryName = s.Product.Category?.Name,
                    BranchId = s.BranchId,
                    BranchName = s.Branch.Name,
                    Quantity = s.Quantity, // Branch-specific quantity
                    Amount = s.Product.Amount,
                    Unit = s.Product.Unit,
                    QuantityDisplay = $"{s.Product.Amount} {s.Product.Unit}",
                    Status = GetStockStatus(s.Quantity, s.Product.ReorderLevel),
                    CreatedAt = s.Product.CreatedAt,
                    UpdatedAt = s.Product.UpdatedAt
                }).ToList();

                _logger.LogInformation("Retrieved {Count} products for branch: {BranchId}", result.Count, branchId);
                return ApiResponse<List<ProductBranchDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products for branch: {BranchId}", branchId);
                return ApiResponse<List<ProductBranchDto>>.Fail("Failed to retrieve products");
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
        public async Task<ApiResponse<List<ProductLowStockDto>>> GetLowStockProductsAsync(int threshold = 10)
        {
            try
            {
                var lowStockItems = await _context.Stocks
                    .Include(s => s.Product)
                    .Include(s => s.Branch)
                    .Where(s => s.Quantity <= threshold && s.IsActive && s.Product.IsActive)
                    .Select(s => new ProductLowStockDto
                    {
                        ProductId = s.ProductId,
                        ProductName = s.Product.Name,
                        ItemCode = s.Product.ItemCode,
                        ItemDescription = s.Product.ItemDescription,
                        Quantity = $"{s.Product.Amount} {s.Product.Unit}",
                        BranchId = s.BranchId,
                        BranchName = s.Branch.Name,
                        QuantityRemaining = s.Quantity,
                        ReorderLevel = s.Product.ReorderLevel
                    })
                    .OrderBy(p => p.QuantityRemaining)
                    .ToListAsync();

                return ApiResponse<List<ProductLowStockDto>>.Success(lowStockItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving low stock products");
                return ApiResponse<List<ProductLowStockDto>>.Fail("Failed to retrieve low stock products");
            }
        }

        public async Task<ApiResponse<ProductStockDto>> AddProductToBranchAsync(AddProductToBranchDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.IsActive);

                if (product == null)
                {
                    _logger.LogWarning("Product not found: {ProductId}", dto.ProductId);
                    return ApiResponse<ProductStockDto>.Fail("Product not found");
                }

                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.Id == dto.BranchId && b.IsActive);

                if (branch == null)
                {
                    _logger.LogWarning("Branch not found: {BranchId}", dto.BranchId);
                    return ApiResponse<ProductStockDto>.Fail("Branch not found");
                }

                var existingStock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == dto.ProductId &&
                                            s.BranchId == dto.BranchId &&
                                            s.IsActive);

                if (existingStock != null)
                {
                    _logger.LogWarning("Product already exists in branch. ProductId: {ProductId}, BranchId: {BranchId}",
                        dto.ProductId, dto.BranchId);
                    return ApiResponse<ProductStockDto>.Fail("Product already exists in this branch");
                }

                var stock = new Stock
                {
                    Id = Guid.NewGuid(),
                    ProductId = dto.ProductId,
                    BranchId = dto.BranchId,
                    Quantity = dto.InitialStock,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _context.Stocks.AddAsync(stock);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var createdStock = await _context.Stocks
                    .Include(s => s.Product)
                    .Include(s => s.Branch)
                    .FirstOrDefaultAsync(s => s.Id == stock.Id);

                var result = _mapper.Map<ProductStockDto>(createdStock);

                _logger.LogInformation("Added product {ProductId} to branch {BranchId} with initial stock {InitialStock}",
                    dto.ProductId, dto.BranchId, dto.InitialStock);

                return ApiResponse<ProductStockDto>.Success(result, "Product added to branch successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error adding product to branch: {@Dto}", dto);
                return ApiResponse<ProductStockDto>.Fail("Failed to add product to branch");
            }
        }

        public async Task<ApiResponse<bool>> DeleteProductAsync(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var product = await _context.Products
                    .Include(p => p.Stocks)
                    .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

                if (product == null)
                {
                    _logger.LogWarning("Product not found: {ProductId}", id);
                    return ApiResponse<bool>.Fail("Product not found");
                }

                product.IsActive = false;
                product.UpdatedAt = DateTime.UtcNow;

                foreach (var stock in product.Stocks.Where(s => s.IsActive))
                {
                    stock.IsActive = false;
                    stock.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Deleted product {ProductId}", id);
                return ApiResponse<bool>.Success(true, "Product deleted successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting product: {ProductId}", id);
                return ApiResponse<bool>.Fail("Failed to delete product");
            }
        }

        public async Task<ApiResponse<ProductStockDto>> AdjustStockAsync(AdjustStockDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var stock = await _context.Stocks
                    .Include(s => s.Product)
                    .Include(s => s.Branch)
                    .FirstOrDefaultAsync(s => s.ProductId == dto.ProductId &&
                                            s.BranchId == dto.BranchId &&
                                            s.IsActive);

                if (stock == null)
                {
                    _logger.LogWarning("Stock record not found for ProductId: {ProductId}, BranchId: {BranchId}",
                        dto.ProductId, dto.BranchId);
                    return ApiResponse<ProductStockDto>.Fail("Stock record not found");
                }

                var newQuantity = stock.Quantity + dto.QuantityChange;
                if (newQuantity < 0)
                {
                    _logger.LogWarning("Insufficient stock for ProductId: {ProductId}, BranchId: {BranchId}. Current: {Current}, Requested change: {Change}",
                        dto.ProductId, dto.BranchId, stock.Quantity, dto.QuantityChange);
                    return ApiResponse<ProductStockDto>.Fail("Insufficient stock");
                }

                stock.Quantity = newQuantity;
                stock.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<ProductStockDto>(stock);
                _logger.LogInformation("Adjusted stock for ProductId: {ProductId}, BranchId: {BranchId}, New quantity: {Quantity}",
                    dto.ProductId, dto.BranchId, newQuantity);

                return ApiResponse<ProductStockDto>.Success(result, "Stock adjusted successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error adjusting stock: {@Dto}", dto);
                return ApiResponse<ProductStockDto>.Fail("Failed to adjust stock");
            }
        }

        public async Task<ApiResponse<List<ProductStockDto>>> GetProductStockByBranchAsync(Guid branchId)
        {
            try
            {
                if (!await _context.Branches.AnyAsync(b => b.Id == branchId && b.IsActive))
                {
                    _logger.LogWarning("Branch not found: {BranchId}", branchId);
                    return ApiResponse<List<ProductStockDto>>.Fail("Branch not found");
                }

                var stocks = await _context.Stocks
                    .Include(s => s.Product)
                    .Include(s => s.Branch)
                    .Where(s => s.BranchId == branchId && s.IsActive && s.Product.IsActive)
                    .Select(s => new ProductStockDto
                    {
                        ProductId = s.ProductId,
                        ProductName = s.Product.Name,
                        ItemCode = s.Product.ItemCode,
                        ItemDescription = s.Product.ItemDescription,
                        Quantity = $"{s.Product.Amount} {s.Product.Unit}",
                        BranchId = s.BranchId,
                        BranchName = s.Branch.Name,
                        QuantityRemaining = s.Quantity
                    })
                    .OrderBy(s => s.ProductName)
                    .ToListAsync();

                return ApiResponse<List<ProductStockDto>>.Success(stocks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving product stock for branch: {BranchId}", branchId);
                return ApiResponse<List<ProductStockDto>>.Fail("Failed to retrieve product stock");
            }
        }

        private async Task<ProductResponseDto> GetProductResponse(Guid productId)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.Stocks)
                .ThenInclude(s => s.Branch)
                .Where(p => p.Id == productId && p.IsActive)
                .FirstOrDefaultAsync();

            return product == null ? null : _mapper.Map<ProductResponseDto>(product);
        }
    }
}