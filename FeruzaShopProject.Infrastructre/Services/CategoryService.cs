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
    public class CategoryService : ICategoryService
    {
        private readonly ShopDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<CategoryService> _logger;

        public CategoryService(
            ShopDbContext context,
            IMapper mapper,
            ILogger<CategoryService> logger)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
        }

        public async Task<ApiResponse<CategoryDto>> CreateCategoryAsync(CreateCategoryDto dto)
        {
            try
            {
                if (await _context.Categories.AnyAsync(c => c.Name == dto.Name))
                    return ApiResponse<CategoryDto>.Fail("Category name already exists");

                var category = _mapper.Map<Category>(dto);
                await _context.Categories.AddAsync(category);
                await _context.SaveChangesAsync();

                var result = _mapper.Map<CategoryDto>(category);
                return ApiResponse<CategoryDto>.Success(result, "Category created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating category");
                return ApiResponse<CategoryDto>.Fail("An error occurred while creating category");
            }
        }

        public async Task<ApiResponse<CategoryDto>> GetCategoryByIdAsync(Guid id)
        {
            var category = await _context.Categories
                .Include(c => c.Products)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (category == null)
                return ApiResponse<CategoryDto>.Fail("Category not found");

            var result = _mapper.Map<CategoryDto>(category);
            result.ProductCount = category.Products.Count;
            return ApiResponse<CategoryDto>.Success(result);
        }

        public async Task<ApiResponse<List<CategoryDto>>> GetAllCategoriesAsync()
        {
            var categories = await _context.Categories
                .Include(c => c.Products)
                .ToListAsync();

            var result = _mapper.Map<List<CategoryDto>>(categories);
            return ApiResponse<List<CategoryDto>>.Success(result);
        }

        public async Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(UpdateCategoryDto dto)
        {
            try
            {
                var category = await _context.Categories.FindAsync(dto.Id);
                if (category == null)
                    return ApiResponse<CategoryDto>.Fail("Category not found");

                if (!string.IsNullOrEmpty(dto.Name))
                    category.Name = dto.Name;

                _context.Categories.Update(category);
                await _context.SaveChangesAsync();

                var result = _mapper.Map<CategoryDto>(category);
                return ApiResponse<CategoryDto>.Success(result, "Category updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating category");
                return ApiResponse<CategoryDto>.Fail("An error occurred while updating category");
            }
        }

        public async Task<ApiResponse<bool>> DeleteCategoryAsync(Guid id)
        {
            try
            {
                var category = await _context.Categories.FindAsync(id);
                if (category == null)
                    return ApiResponse<bool>.Fail("Category not found");

                _context.Categories.Remove(category);
                await _context.SaveChangesAsync();
                return ApiResponse<bool>.Success(true, "Category deleted successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting category");
                return ApiResponse<bool>.Fail("An error occurred while deleting category");
            }
        }
    }
}
