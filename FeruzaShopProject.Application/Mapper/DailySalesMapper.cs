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
    public class DailySalesMapper : Profile
    {
        public DailySalesMapper()
        {
            // SalesTransaction -> DailySales
            CreateMap<Transaction, DailySales>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.SaleDate, opt => opt.MapFrom(src => src.TransactionDate.Date))
                .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.UnitPrice * src.Quantity))
                .ForMember(dest => dest.CommissionAmount, opt => opt.MapFrom(src => src.Quantity * src.CommissionRate))
                .ForMember(dest => dest.CommissionPaid, opt => opt.MapFrom(src => src.CommissionPaid))
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore())
                .ForMember(dest => dest.Transaction, opt => opt.Ignore())
                .ForMember(dest => dest.Customer, opt => opt.Ignore())
                .ForMember(dest => dest.Painter, opt => opt.Ignore());

            // DailySales -> DailySalesItemDto
            CreateMap<DailySales, DailySalesItemDto>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Product.Category.Name))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : "Walk-in"))
                .ForMember(dest => dest.TransactionDate, opt => opt.MapFrom(src => src.Transaction.TransactionDate));
        }
    }
}
