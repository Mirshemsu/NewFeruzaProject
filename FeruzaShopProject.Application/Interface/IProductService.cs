using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Interface
{
    public interface IProductService
    {
        Task<ApiResponse<ProductResponseDto>> CreateProductAsync(CreateProductDto dto);
        Task<ApiResponse<ProductResponseDto>> UpdateProductAsync(UpdateProductDto dto);
        Task<ApiResponse<ProductResponseDto>> GetProductByIdAsync(Guid id);
        Task<ApiResponse<List<ProductResponseDto>>> GetAllProductsAsync();
        Task<ApiResponse<List<ProductResponseDto>>> GetProductsByCategoryAsync(Guid categoryId);
        Task<ApiResponse<List<ProductResponseDto>>> GetProductsByBranchAsync(Guid branchId);
        Task<ApiResponse<List<ProductLowStockDto>>> GetLowStockProductsAsync(int threshold = 10);
        Task<ApiResponse<bool>> DeleteProductAsync(Guid id);
        Task<ApiResponse<ProductStockDto>> AddProductToBranchAsync(AddProductToBranchDto dto);
        Task<ApiResponse<ProductStockDto>> AdjustStockAsync(AdjustStockDto dto); // Added this method
        Task<ApiResponse<List<ProductStockDto>>> GetProductStockByBranchAsync(Guid branchId); // Added this method
    }
}
