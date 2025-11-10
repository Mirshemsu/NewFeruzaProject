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
    public class TransactionMapper : Profile
    {
        public TransactionMapper()
        {
            // CreateTransactionDto -> Transaction
            CreateMap<CreateTransactionDto, Transaction>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.TransactionDate, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.CommissionPaid, opt => opt.MapFrom(src => false))
                .ForMember(dest => dest.CustomerId, opt => opt.Ignore()) // Handled in service
                .ForMember(dest => dest.PainterId, opt => opt.Ignore())  // Handled in service
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore())
                .ForMember(dest => dest.Customer, opt => opt.Ignore())
                .ForMember(dest => dest.Painter, opt => opt.Ignore());

            // UpdateTransactionDto -> Transaction
            CreateMap<UpdateTransactionDto, Transaction>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.TransactionDate, opt => opt.Ignore())
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.ItemCode, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore())
                .ForMember(dest => dest.Customer, opt => opt.Ignore())
                .ForMember(dest => dest.Painter, opt => opt.Ignore())
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));

            // Transaction -> TransactionResponseDto
            CreateMap<Transaction, TransactionResponseDto>()
                .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src => src.Branch.Name))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Product.Category.Name))
                .ForMember(dest => dest.UnitType, opt => opt.MapFrom(src => src.Product.Unit.ToString())) // Fixed: This should be UnitType
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice)) // Fixed: Map from Transaction.UnitPrice
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : null))
                .ForMember(dest => dest.CustomerPhoneNumber, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.PhoneNumber : null))
                .ForMember(dest => dest.PainterName, opt => opt.MapFrom(src => src.Painter != null ? src.Painter.Name : null))
                .ForMember(dest => dest.PainterPhoneNumber, opt => opt.MapFrom(src => src.Painter != null ? src.Painter.PhoneNumber : null))
                .ForMember(dest => dest.PaidAmount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.TotalAmount, opt => opt.Ignore()) // Calculated in DTO
                .ForMember(dest => dest.CommissionAmount, opt => opt.Ignore()) // Calculated in DTO
                .ForMember(dest => dest.RemainingAmount, opt => opt.Ignore()) // Calculated in DTO
                .ForMember(dest => dest.IsFullyPaid, opt => opt.Ignore()); // Calculated in DTO

            // Additional mappings for other DTOs
            CreateMap<PayCreditDto, CreditPayment>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Transaction, opt => opt.Ignore());

            // Transaction -> CreditTransactionHistoryDto
            CreateMap<Transaction, CreditTransactionHistoryDto>()
                .ForMember(dest => dest.TransactionId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : "Unknown"))
                .ForMember(dest => dest.CustomerPhoneNumber, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.PhoneNumber : "Unknown"))
                .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src => src.Branch.Name))
                .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.UnitPrice * src.Quantity))
                .ForMember(dest => dest.PaidAmount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.LastPaymentDate, opt => opt.Ignore()); // Calculated in service


        }
    }
}