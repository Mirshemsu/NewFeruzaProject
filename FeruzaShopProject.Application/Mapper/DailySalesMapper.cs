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

            CreateMap<DailySales, DailySalesItemDto>()
    .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
    .ForMember(dest => dest.TransactionId, opt => opt.MapFrom(src => src.TransactionId))
    .ForMember(dest => dest.SaleDate, opt => opt.MapFrom(src => src.SaleDate))
    .ForMember(dest => dest.TransactionDate, opt => opt.MapFrom(src =>
        src.Transaction != null ? src.Transaction.TransactionDate : src.SaleDate))
    .ForMember(dest => dest.BranchId, opt => opt.MapFrom(src => src.BranchId))
    .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src =>
        src.Branch != null ? src.Branch.Name : null))
    .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
    .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src =>
        src.Product != null ? src.Product.Name : null))
    .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src =>
        src.Product != null && src.Product.Category != null ? src.Product.Category.Name : null))
    .ForMember(dest => dest.ItemCode, opt => opt.MapFrom(src =>
        src.Transaction != null ? src.Transaction.ItemCode :
        src.Product != null ? src.Product.ItemCode : null))
    .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity))
    .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice))
    .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.TotalAmount))

    // Profit-related fields - calculated in service
    .ForMember(dest => dest.BuyingPrice, opt => opt.Ignore())
    .ForMember(dest => dest.CostAmount, opt => opt.Ignore())
    .ForMember(dest => dest.Profit, opt => opt.Ignore())
    .ForMember(dest => dest.ProfitMargin, opt => opt.Ignore())

    .ForMember(dest => dest.PaymentMethod, opt => opt.MapFrom(src => src.PaymentMethod))
    .ForMember(dest => dest.CommissionRate, opt => opt.MapFrom(src => src.CommissionRate))
    .ForMember(dest => dest.CommissionAmount, opt => opt.MapFrom(src => src.CommissionAmount))
    .ForMember(dest => dest.CommissionPaid, opt => opt.MapFrom(src => src.CommissionPaid))
    .ForMember(dest => dest.CustomerId, opt => opt.MapFrom(src => src.CustomerId))
    .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src =>
        src.Customer != null ? src.Customer.Name : null))
    .ForMember(dest => dest.PainterId, opt => opt.MapFrom(src => src.PainterId))
    .ForMember(dest => dest.PainterName, opt => opt.MapFrom(src =>
        src.Painter != null ? src.Painter.Name : null))
    .ForMember(dest => dest.IsPartialPayment, opt => opt.MapFrom(src => src.IsPartialPayment))
    .ForMember(dest => dest.IsCreditPayment, opt => opt.MapFrom(src => src.IsCreditPayment));
        }
    }
}
