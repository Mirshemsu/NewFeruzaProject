using AutoMapper;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Mapper
{
    public class ProductMapper : Profile
    {
        public ProductMapper()
        {
            CreateMap<Product, ProductResponseDto>()
           .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
           .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => $"{src.Amount} {src.Unit}"))
           .ForMember(dest => dest.SellingPrice, opt => opt.MapFrom(src => src.UnitPrice))
           .ForMember(dest => dest.TotalStock, opt => opt.MapFrom(src => src.Stocks.Sum(s => s.Quantity)))
           .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Category.Name))
           .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt))
           .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => src.UpdatedAt));

            // CreateProductDto to Product
            CreateMap<CreateProductDto, Product>()
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice)) // Map UnitPrice to UnitPrice
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow));

            // UpdateProductDto to Product
            CreateMap<UpdateProductDto, Product>()
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.SellingPrice))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            // Stock to ProductLowStockDto
            CreateMap<Stock, ProductLowStockDto>()
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.ItemCode, opt => opt.MapFrom(src => src.Product.ItemCode))
                .ForMember(dest => dest.ItemDescription, opt => opt.MapFrom(src => src.Product.ItemDescription))
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => $"{src.Product.Amount} {src.Product.Unit}"))
                .ForMember(dest => dest.BranchId, opt => opt.MapFrom(src => src.BranchId))
                .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src => src.Branch.Name))
                .ForMember(dest => dest.QuantityRemaining, opt => opt.MapFrom(src => src.Quantity))
                .ForMember(dest => dest.ReorderLevel, opt => opt.MapFrom(src => src.Product.ReorderLevel));

            // Stock to ProductStockDto
            CreateMap<Stock, ProductStockDto>()
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.ItemCode, opt => opt.MapFrom(src => src.Product.ItemCode))
                .ForMember(dest => dest.ItemDescription, opt => opt.MapFrom(src => src.Product.ItemDescription))
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => $"{src.Product.Amount} {src.Product.Unit}"))
                .ForMember(dest => dest.BranchId, opt => opt.MapFrom(src => src.BranchId))
                .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src => src.Branch.Name))
                .ForMember(dest => dest.QuantityRemaining, opt => opt.MapFrom(src => src.Quantity));
        }
    }
}