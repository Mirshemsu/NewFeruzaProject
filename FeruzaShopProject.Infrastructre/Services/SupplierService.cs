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
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Infrastructre.Services
{
    public class SupplierService : ISupplierService
    {
        private readonly ShopDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<SupplierService> _logger;

        public SupplierService(ShopDbContext context, IMapper mapper, ILogger<SupplierService> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ApiResponse<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var supplier = new Supplier
                {
                    Id = Guid.NewGuid(),
                    Name = dto.Name,
                    ContactInfo = dto.ContactInfo,
                    Address = dto.Address,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<SupplierDto>(supplier);
                _logger.LogInformation("Created supplier {SupplierId}: {SupplierName}", supplier.Id, supplier.Name);
                return ApiResponse<SupplierDto>.Success(result, "Supplier created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating supplier: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<SupplierDto>.Fail("Error creating supplier");
            }
        }

        public async Task<ApiResponse<SupplierDto>> UpdateSupplierAsync(UpdateSupplierDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var supplier = await _context.Suppliers
                    .FirstOrDefaultAsync(s => s.Id == dto.Id && s.IsActive);
                if (supplier == null)
                {
                    _logger.LogWarning("Supplier not found or inactive: {SupplierId}", dto.Id);
                    return ApiResponse<SupplierDto>.Fail("Supplier not found or inactive");
                }

                supplier.Update(dto.Name, dto.ContactInfo, dto.Address);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<SupplierDto>(supplier);
                _logger.LogInformation("Updated supplier {SupplierId}: {SupplierName}", supplier.Id, supplier.Name);
                return ApiResponse<SupplierDto>.Success(result, "Supplier updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating supplier: {@Dto}", dto);
                await transaction.RollbackAsync();
                return ApiResponse<SupplierDto>.Fail("Error updating supplier");
            }
        }

        public async Task<ApiResponse<SupplierDto>> GetSupplierByIdAsync(Guid id)
        {
            try
            {
                var supplier = await _context.Suppliers
                    .FirstOrDefaultAsync(s => s.Id == id);
                if (supplier == null)
                {
                    _logger.LogWarning("Supplier not found: {SupplierId}", id);
                    return ApiResponse<SupplierDto>.Fail("Supplier not found");
                }

                var result = _mapper.Map<SupplierDto>(supplier);
                return ApiResponse<SupplierDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving supplier {SupplierId}", id);
                return ApiResponse<SupplierDto>.Fail("Error retrieving supplier");
            }
        }

        public async Task<ApiResponse<List<SupplierDto>>> GetAllSuppliersAsync()
        {
            try
            {
                var suppliers = await _context.Suppliers
                    .ToListAsync();

                var result = _mapper.Map<List<SupplierDto>>(suppliers);
                return ApiResponse<List<SupplierDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all suppliers");
                return ApiResponse<List<SupplierDto>>.Fail("Error retrieving suppliers");
            }
        }

        public async Task<ApiResponse<bool>> DeactivateSupplierAsync(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var supplier = await _context.Suppliers
                    .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
                if (supplier == null)
                {
                    _logger.LogWarning("Supplier not found or already inactive: {SupplierId}", id);
                    return ApiResponse<bool>.Fail("Supplier not found or already inactive");
                }

                var hasActivePurchaseOrders = await _context.PurchaseOrders
                    .AnyAsync(po => po.IsActive &&
                                    po.Status != PurchaseOrderStatus.PartiallyReceived && po.Status != PurchaseOrderStatus.Cancelled);
                if (hasActivePurchaseOrders)
                {
                    _logger.LogWarning("Cannot deactivate supplier {SupplierId} with active purchase orders", id);
                    return ApiResponse<bool>.Fail("Cannot deactivate supplier with active purchase orders");
                }

                supplier.Deactivate();
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Deactivated supplier {SupplierId}", id);
                return ApiResponse<bool>.Success(true, "Supplier deactivated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating supplier {SupplierId}", id);
                await transaction.RollbackAsync();
                return ApiResponse<bool>.Fail("Error deactivating supplier");
            }
        }
    }
}