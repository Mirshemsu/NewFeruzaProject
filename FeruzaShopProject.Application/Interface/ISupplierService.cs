using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Interface
{
    public interface ISupplierService
    {
        Task<ApiResponse<SupplierDto>> CreateSupplierAsync(CreateSupplierDto dto);
        Task<ApiResponse<SupplierDto>> UpdateSupplierAsync(UpdateSupplierDto dto);
        Task<ApiResponse<SupplierDto>> GetSupplierByIdAsync(Guid id);
        Task<ApiResponse<List<SupplierDto>>> GetAllSuppliersAsync();
        Task<ApiResponse<bool>> DeactivateSupplierAsync(Guid id);
    }
}
