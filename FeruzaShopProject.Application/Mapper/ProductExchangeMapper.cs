using AutoMapper;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using System;

namespace FeruzaShopProject.Application.Mapper
{
    public class ProductExchangeMapper : Profile
    {
        public ProductExchangeMapper()
        {
            // CreateProductExchangeDto -> ProductExchange
            CreateMap<CreateProductExchangeDto, ProductExchange>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.OriginalTransaction, opt => opt.Ignore())
                .ForMember(dest => dest.OriginalProduct, opt => opt.Ignore())
                .ForMember(dest => dest.NewProduct, opt => opt.Ignore())
                .ForMember(dest => dest.OriginalProductId, opt => opt.Ignore()) // Will be set from transaction
                .ForMember(dest => dest.OriginalQuantity, opt => opt.Ignore()) // Will be set from transaction
                .ForMember(dest => dest.OriginalPrice, opt => opt.Ignore()) // Will be set from transaction
                .ForMember(dest => dest.NewPrice, opt => opt.Ignore()); // Will be set from product

            // ProductExchange -> ProductExchangeResponseDto
            CreateMap<ProductExchange, ProductExchangeResponseDto>()
                .ForMember(dest => dest.OriginalTotal,
                    opt => opt.MapFrom(src => src.TotalOriginal))
                .ForMember(dest => dest.NewTotal,
                    opt => opt.MapFrom(src => src.TotalNew))
                .ForMember(dest => dest.MoneyDifference,
                    opt => opt.MapFrom(src => src.MoneyDifference))
                .ForMember(dest => dest.IsRefund,
                    opt => opt.MapFrom(src => src.IsRefund))
                .ForMember(dest => dest.IsAdditionalPayment,
                    opt => opt.MapFrom(src => src.IsAdditionalPayment))
                .ForMember(dest => dest.Amount,
                    opt => opt.MapFrom(src => src.Amount))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => "Exchange"))
                .ForMember(dest => dest.OriginalProduct,
                    opt => opt.MapFrom(src => src.OriginalProduct))
                .ForMember(dest => dest.NewProduct,
                    opt => opt.MapFrom(src => src.NewProduct))
                .ForMember(dest => dest.OriginalQuantity,
                    opt => opt.MapFrom(src => src.OriginalQuantity))
                .ForMember(dest => dest.NewQuantity,
                    opt => opt.MapFrom(src => src.NewQuantity))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt));

            // Product -> ProductDto
            CreateMap<Product, ProductDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.Name))
                .ForMember(dest => dest.ItemCode, opt => opt.MapFrom(src => src.ItemCode))
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice))
                .ForMember(dest => dest.BuyingPrice, opt => opt.MapFrom(src => src.BuyingPrice));

            // ExchangeSummaryDto (for self-mapping if needed)
            CreateMap<ExchangeSummaryDto, ExchangeSummaryDto>();
        }
    }
}