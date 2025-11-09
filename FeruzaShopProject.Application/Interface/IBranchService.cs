using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Interface
{
    public interface IBranchService
    {
        Task<ApiResponse<BranchDto>> CreateBranchAsync(CreateBranchDto dto);
        Task<ApiResponse<BranchDto>> GetBranchByIdAsync(Guid id);
        Task<ApiResponse<List<BranchDto>>> GetAllBranchesAsync();
        Task<ApiResponse<BranchDto>> UpdateBranchAsync(UpdateBranchDto dto);
        Task<ApiResponse<bool>> DeleteBranchAsync(Guid id);
    }
}
