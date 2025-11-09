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

                // Validate item code uniqueness (across all branches since product is not branch-specific)
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
                if (dto.BranchStocks != null && dto.BranchStocks.Any())
                {
                    foreach (var branchStock in dto.BranchStocks)
                    {
                        // Validate branch exists
                        if (!await _context.Branches.AnyAsync(b => b.Id == branchStock.BranchId && b.IsActive))
                        {
                            _logger.LogWarning("Branch not found: {BranchId}", branchStock.BranchId);
                            return ApiResponse<ProductResponseDto>.Fail($"Branch not found: {branchStock.BranchId}");
                        }

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
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = await GetProductResponse(product.Id);
                _logger.LogInformation("Created product {ProductId}", product.Id);
                return ApiResponse<ProductResponseDto>.Success(result, "Product created successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating product: {@Dto}", dto);
                return ApiResponse<ProductResponseDto>.Fail("Failed to create product");
            }
        }

        public async Task<ApiResponse<ProductResponseDto>> UpdateProductAsync(UpdateProductDto dto)
        {
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

                _context.Products.Update(product);
                await _context.SaveChangesAsync();

                var result = await GetProductResponse(product.Id);
                _logger.LogInformation("Updated product {ProductId}", product.Id);
                return ApiResponse<ProductResponseDto>.Success(result, "Product updated successfully");
            }
            catch (Exception ex)
            {
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

        public async Task<ApiResponse<List<ProductResponseDto>>> GetProductsByBranchAsync(Guid branchId)
        {
            try
            {
                if (!await _context.Branches.AnyAsync(b => b.Id == branchId && b.IsActive))
                {
                    _logger.LogWarning("Branch not found: {BranchId}", branchId);
                    return ApiResponse<List<ProductResponseDto>>.Fail("Branch not found");
                }

                // Get products that have stock in the specified branch
                var products = await _context.Stocks
                    .Include(s => s.Product)
                    .ThenInclude(p => p.Category)
                    .Include(s => s.Product.Stocks)
                    .ThenInclude(st => st.Branch)
                    .Where(s => s.BranchId == branchId && s.IsActive && s.Product.IsActive)
                    .Select(s => s.Product)
                    .Distinct()
                    .OrderBy(p => p.Name)
                    .ToListAsync();

                var result = _mapper.Map<List<ProductResponseDto>>(products);
                return ApiResponse<List<ProductResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving products for branch: {BranchId}", branchId);
                return ApiResponse<List<ProductResponseDto>>.Fail("Failed to retrieve products");
            }
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
                // Check if product exists and is active
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.IsActive);

                if (product == null)
                {
                    _logger.LogWarning("Product not found: {ProductId}", dto.ProductId);
                    return ApiResponse<ProductStockDto>.Fail("Product not found");
                }

                // Check if branch exists and is active
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.Id == dto.BranchId && b.IsActive);

                if (branch == null)
                {
                    _logger.LogWarning("Branch not found: {BranchId}", dto.BranchId);
                    return ApiResponse<ProductStockDto>.Fail("Branch not found");
                }

                // Check if product already exists in this branch
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

                // Create new stock entry for the branch
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

                // Get the complete stock information with product and branch details
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

                // Soft delete the product and its stocks
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