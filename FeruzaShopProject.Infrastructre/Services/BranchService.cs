using AutoMapper;
using AutoMapper.QueryableExtensions;
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
    public class BranchService : IBranchService
    {
        private readonly ShopDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<BranchService> _logger;

        public BranchService(ShopDbContext context, IMapper mapper, ILogger<BranchService> logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ApiResponse<BranchDto>> CreateBranchAsync(CreateBranchDto dto)
        {
            try
            {
                var branch = _mapper.Map<Branch>(dto);
                await _context.Branches.AddAsync(branch);
                await _context.SaveChangesAsync();

                var result = _mapper.Map<BranchDto>(branch);
                return ApiResponse<BranchDto>.Success(result, "Branch created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating branch");
                return ApiResponse<BranchDto>.Fail("Error creating branch");
            }
        }

        public async Task<ApiResponse<List<BranchDto>>> GetAllBranchesAsync()
        {
            var branches = await _context.Branches
                .Include(b => b.Users)
                .ProjectTo<BranchDto>(_mapper.ConfigurationProvider)
                .ToListAsync();

            return ApiResponse<List<BranchDto>>.Success(branches);
        }

        public async Task<ApiResponse<BranchDto>> GetBranchByIdAsync(Guid id)
        {
            var branch = await _context.Branches
                .Include(b => b.Users)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (branch == null)
                return ApiResponse<BranchDto>.Fail("Branch not found");

            return ApiResponse<BranchDto>.Success(_mapper.Map<BranchDto>(branch));
        }

        

        public async Task<ApiResponse<BranchDto>> UpdateBranchAsync(UpdateBranchDto dto)
        {
            try
            {
                var branch = await _context.Branches.FindAsync(dto.Id);
                if (branch == null)
                    return ApiResponse<BranchDto>.Fail("Branch not found");

                _mapper.Map(dto, branch);
                _context.Branches.Update(branch);
                await _context.SaveChangesAsync();

                return ApiResponse<BranchDto>.Success(_mapper.Map<BranchDto>(branch), "Branch updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating branch");
                return ApiResponse<BranchDto>.Fail("Error updating branch");
            }
        }

        public async Task<ApiResponse<bool>> DeleteBranchAsync(Guid id)
        {
            try
            {
                var branch = await _context.Branches.FindAsync(id);
                if (branch == null)
                    return ApiResponse<bool>.Fail("Branch not found");

                _context.Branches.Remove(branch);
                await _context.SaveChangesAsync();

                return ApiResponse<bool>.Success(true, "Branch deleted");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting branch");
                return ApiResponse<bool>.Fail("Error deleting branch");
            }
        }
    }
}
