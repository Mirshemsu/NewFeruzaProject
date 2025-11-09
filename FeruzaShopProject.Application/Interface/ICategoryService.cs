using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Interface
{
    public interface ICategoryService
    {
        Task<ApiResponse<CategoryDto>> CreateCategoryAsync(CreateCategoryDto dto);
        Task<ApiResponse<CategoryDto>> GetCategoryByIdAsync(Guid id);
        Task<ApiResponse<List<CategoryDto>>> GetAllCategoriesAsync();
        Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(UpdateCategoryDto dto);
        Task<ApiResponse<bool>> DeleteCategoryAsync(Guid id);
    }
}
