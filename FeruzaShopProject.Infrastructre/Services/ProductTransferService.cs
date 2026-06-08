// Infrastructure/Services/ProductTransferService.cs
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

namespace FeruzaShopProject.Infrastructure.Services
{
    public class ProductTransferService : IProductTransferService
    {
        private readonly ShopDbContext _context;
        private readonly ILogger<ProductTransferService> _logger;

        public ProductTransferService(ShopDbContext context, ILogger<ProductTransferService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // STEP 1: Initiate Transfer - JUST CREATE REQUEST (NO stock change)
        public async Task<ApiResponse<TransferResponseDto>> InitiateTransferAsync(InitiateTransferDto dto, Guid userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate product exists
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.IsActive);
                if (product == null)
                    return ApiResponse<TransferResponseDto>.Fail("Product not found");

                // Validate branches
                var fromBranch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.Id == dto.FromBranchId && b.IsActive);
                var toBranch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.Id == dto.ToBranchId && b.IsActive);

                if (fromBranch == null || toBranch == null)
                    return ApiResponse<TransferResponseDto>.Fail("Branch not found");

                // Check if enough stock exists (validation only, NOT deduction)
                var sourceStock = await _context.Stocks
                    .FirstOrDefaultAsync(s => s.ProductId == dto.ProductId &&
                                             s.BranchId == dto.FromBranchId &&
                                             s.IsActive);

                if (sourceStock == null || sourceStock.Quantity < dto.Quantity)
                {
                    return ApiResponse<TransferResponseDto>.Fail(
                        $"Insufficient stock. Available: {sourceStock?.Quantity ?? 0}, Requested: {dto.Quantity}");
                }

                // Generate transfer number
                var transferNumber = $"TRF-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

                // Create transfer request (NO stock change yet)
                var transfer = new ProductTransfer
                {
                    Id = Guid.NewGuid(),
                    TransferNumber = transferNumber,
                    ProductId = dto.ProductId,
                    FromBranchId = dto.FromBranchId,
                    ToBranchId = dto.ToBranchId,
                    Quantity = dto.Quantity,
                    Status = TransferStatus.PendingTransfer, // Waiting for finance approval
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _context.ProductTransfers.AddAsync(transfer);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Transfer request initiated: {TransferNumber}, waiting for finance approval", transferNumber);

                var result = await MapToResponse(transfer);
                return ApiResponse<TransferResponseDto>.Success(result, "Transfer request created. Awaiting finance approval.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error initiating transfer");
                return ApiResponse<TransferResponseDto>.Fail("Failed to initiate transfer");
            }
        }

        // STEP 2: Receive Transfer - JUST UPDATE RECEIPT CONFIRMATION (NO stock change)
        public async Task<ApiResponse<TransferResponseDto>> ReceiveTransferAsync(ReceiveTransferDto dto, Guid userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var transfer = await _context.ProductTransfers
                    .FirstOrDefaultAsync(t => t.Id == dto.TransferId && t.IsActive);

                if (transfer == null)
                    return ApiResponse<TransferResponseDto>.Fail("Transfer not found");

                if (transfer.Status != TransferStatus.PendingTransfer)
                    return ApiResponse<TransferResponseDto>.Fail($"Cannot receive. Current status: {transfer.Status}");

                // Just mark as received, NO stock change yet
                transfer.Status = TransferStatus.Received; // Waiting for finance approval
                transfer.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Transfer received confirmation: {TransferNumber}, waiting for finance approval", transfer.TransferNumber);

                var result = await MapToResponse(transfer);
                return ApiResponse<TransferResponseDto>.Success(result, "Transfer receipt confirmed. Awaiting finance approval.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error receiving transfer");
                return ApiResponse<TransferResponseDto>.Fail("Failed to receive transfer");
            }
        }

        // STEP 3: Finance Approve - FINALLY UPDATE STOCK (decrease source, increase destination)
        public async Task<ApiResponse<TransferResponseDto>> ApproveTransferAsync(ApproveTransferDto dto, Guid userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var transfer = await _context.ProductTransfers
                    .Include(t => t.Product)
                    .Include(t => t.FromBranch)
                    .Include(t => t.ToBranch)
                    .FirstOrDefaultAsync(t => t.Id == dto.TransferId && t.IsActive);

                if (transfer == null)
                    return ApiResponse<TransferResponseDto>.Fail("Transfer not found");

                if (transfer.Status != TransferStatus.Received)
                    return ApiResponse<TransferResponseDto>.Fail($"Cannot approve. Current status: {transfer.Status}");

                if (dto.IsApproved)
                {
                    // Re-check stock availability before deduction (might have changed since initiation)
                    var sourceStock = await _context.Stocks
                        .FirstOrDefaultAsync(s => s.ProductId == transfer.ProductId &&
                                                 s.BranchId == transfer.FromBranchId &&
                                                 s.IsActive);

                    if (sourceStock == null || sourceStock.Quantity < transfer.Quantity)
                    {
                        return ApiResponse<TransferResponseDto>.Fail(
                            $"Insufficient stock at source branch. Available: {sourceStock?.Quantity ?? 0}, Required: {transfer.Quantity}");
                    }

                    // 1. DECREASE stock from source branch
                    decimal previousSourceQty = sourceStock.Quantity;
                    sourceStock.Quantity -= transfer.Quantity;
                    sourceStock.UpdatedAt = DateTime.UtcNow;

                    // Record stock movement for source branch (DECREASE)
                    var sourceMovement = new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        ProductId = transfer.ProductId,
                        BranchId = transfer.FromBranchId,
                        MovementType = StockMovementType.Transfer,
                        Quantity = transfer.Quantity,
                        PreviousQuantity = previousSourceQty,
                        NewQuantity = sourceStock.Quantity,
                        Reason = $"Transfer approved: {transfer.TransferNumber} to {transfer.ToBranch?.Name}",
                        MovementDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _context.StockMovements.AddAsync(sourceMovement);

                    // 2. INCREASE stock at destination branch
                    var destStock = await _context.Stocks
                        .FirstOrDefaultAsync(s => s.ProductId == transfer.ProductId &&
                                                 s.BranchId == transfer.ToBranchId &&
                                                 s.IsActive);

                    decimal previousDestQty = 0;

                    if (destStock == null)
                    {
                        // Create new stock record at destination
                        destStock = new Stock
                        {
                            Id = Guid.NewGuid(),
                            ProductId = transfer.ProductId,
                            BranchId = transfer.ToBranchId,
                            Quantity = transfer.Quantity,
                            CreatedAt = DateTime.UtcNow,
                            IsActive = true
                        };
                        await _context.Stocks.AddAsync(destStock);
                        previousDestQty = 0;
                    }
                    else
                    {
                        previousDestQty = destStock.Quantity;
                        destStock.Quantity += transfer.Quantity;
                        destStock.UpdatedAt = DateTime.UtcNow;
                    }

                    // Record stock movement for destination branch (INCREASE)
                    var destMovement = new StockMovement
                    {
                        Id = Guid.NewGuid(),
                        ProductId = transfer.ProductId,
                        BranchId = transfer.ToBranchId,
                        MovementType = StockMovementType.Transfer,
                        Quantity = transfer.Quantity,
                        PreviousQuantity = previousDestQty,
                        NewQuantity = destStock.Quantity,
                        Reason = $"Transfer approved: {transfer.TransferNumber} from {transfer.FromBranch?.Name}",
                        MovementDate = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _context.StockMovements.AddAsync(destMovement);

                    transfer.Status = TransferStatus.Approved;
                    transfer.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Transfer approved and stock updated: {TransferNumber}", transfer.TransferNumber);

                    var result = await MapToResponse(transfer);
                    return ApiResponse<TransferResponseDto>.Success(result, "Transfer approved and stock updated successfully");
                }
                else
                {
                    // REJECTED - No stock change needed since stock was never touched
                    transfer.Status = TransferStatus.Rejected;
                    transfer.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Transfer rejected: {TransferNumber}", transfer.TransferNumber);

                    var result = await MapToResponse(transfer);
                    return ApiResponse<TransferResponseDto>.Success(result, "Transfer rejected");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error approving transfer");
                return ApiResponse<TransferResponseDto>.Fail("Failed to process approval");
            }
        }

        // Cancel Transfer - Only possible if not yet approved
        public async Task<ApiResponse<TransferResponseDto>> CancelTransferAsync(CancelTransferDto dto, Guid userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var transfer = await _context.ProductTransfers
                    .FirstOrDefaultAsync(t => t.Id == dto.TransferId && t.IsActive);

                if (transfer == null)
                    return ApiResponse<TransferResponseDto>.Fail("Transfer not found");

                // Can cancel if status is PendingTransfer or Received (before approval)
                if (transfer.Status != TransferStatus.PendingTransfer && transfer.Status != TransferStatus.Received)
                    return ApiResponse<TransferResponseDto>.Fail($"Cannot cancel. Current status: {transfer.Status}");

                // NO stock change needed since stock was never touched
                transfer.Status = TransferStatus.Cancelled;
                transfer.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Transfer cancelled: {TransferNumber}", transfer.TransferNumber);

                var result = await MapToResponse(transfer);
                return ApiResponse<TransferResponseDto>.Success(result, "Transfer cancelled");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error cancelling transfer");
                return ApiResponse<TransferResponseDto>.Fail("Failed to cancel transfer");
            }
        }

        // Get transfers by branch
        public async Task<ApiResponse<List<TransferResponseDto>>> GetTransfersByBranchAsync(Guid branchId)
        {
            try
            {
                var transfers = await _context.ProductTransfers
                    .Include(t => t.Product)
                    .Include(t => t.FromBranch)
                    .Include(t => t.ToBranch)
                    .Where(t => t.IsActive && (t.FromBranchId == branchId || t.ToBranchId == branchId))
                    .OrderByDescending(t => t.CreatedAt)
                    .ToListAsync();

                var result = new List<TransferResponseDto>();
                foreach (var transfer in transfers)
                {
                    result.Add(await MapToResponse(transfer));
                }

                return ApiResponse<List<TransferResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transfers for branch {BranchId}", branchId);
                return ApiResponse<List<TransferResponseDto>>.Fail("Failed to get transfers");
            }
        }

        // Get single transfer
        public async Task<ApiResponse<TransferResponseDto>> GetTransferByIdAsync(Guid id)
        {
            try
            {
                var transfer = await _context.ProductTransfers
                    .Include(t => t.Product)
                    .Include(t => t.FromBranch)
                    .Include(t => t.ToBranch)
                    .FirstOrDefaultAsync(t => t.Id == id && t.IsActive);

                if (transfer == null)
                    return ApiResponse<TransferResponseDto>.Fail("Transfer not found");

                var result = await MapToResponse(transfer);
                return ApiResponse<TransferResponseDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transfer {TransferId}", id);
                return ApiResponse<TransferResponseDto>.Fail("Failed to get transfer");
            }
        }

        // Helper method
        private async Task<TransferResponseDto> MapToResponse(ProductTransfer transfer)
        {
            if (transfer.Product == null)
                await _context.Entry(transfer).Reference(t => t.Product).LoadAsync();
            if (transfer.FromBranch == null)
                await _context.Entry(transfer).Reference(t => t.FromBranch).LoadAsync();
            if (transfer.ToBranch == null)
                await _context.Entry(transfer).Reference(t => t.ToBranch).LoadAsync();

            return new TransferResponseDto
            {
                Id = transfer.Id,
                TransferNumber = transfer.TransferNumber,
                ProductName = transfer.Product?.Name ?? "Unknown",
                FromBranchName = transfer.FromBranch?.Name ?? "Unknown",
                ToBranchName = transfer.ToBranch?.Name ?? "Unknown",
                Quantity = transfer.Quantity,
                Status = transfer.Status.ToString(),
                CreatedAt = transfer.CreatedAt,
                UpdatedAt = transfer.UpdatedAt
            };
        }
    }
}