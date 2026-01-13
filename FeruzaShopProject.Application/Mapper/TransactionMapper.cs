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
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
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
                .ForMember(dest => dest.UnitType, opt => opt.MapFrom(src => src.Product.Unit.ToString()))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : null))
                .ForMember(dest => dest.CustomerPhoneNumber, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.PhoneNumber : null))
                .ForMember(dest => dest.PainterName, opt => opt.MapFrom(src => src.Painter != null ? src.Painter.Name : null))
                .ForMember(dest => dest.PainterPhoneNumber, opt => opt.MapFrom(src => src.Painter != null ? src.Painter.PhoneNumber : null))
                .ForMember(dest => dest.PaidAmount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.IsPartialPayment, opt => opt.Ignore()) // Set from DailySales
                .ForMember(dest => dest.TotalAmount, opt => opt.Ignore()) // Calculated in DTO
                .ForMember(dest => dest.CommissionAmount, opt => opt.Ignore()) // Calculated in DTO
                .ForMember(dest => dest.RemainingAmount, opt => opt.Ignore()) // Calculated in DTO
                .ForMember(dest => dest.IsFullyPaid, opt => opt.Ignore()); // Calculated in DTO

            // DailySales -> TransactionResponseDto (for listing partial payments)
            CreateMap<DailySales, TransactionResponseDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.TransactionId))
                .ForMember(dest => dest.BranchId, opt => opt.MapFrom(src => src.BranchId))
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
                .ForMember(dest => dest.CustomerId, opt => opt.MapFrom(src => src.CustomerId))
                .ForMember(dest => dest.PainterId, opt => opt.MapFrom(src => src.PainterId))
                .ForMember(dest => dest.TransactionDate, opt => opt.MapFrom(src => src.SaleDate))
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity))
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice))
                .ForMember(dest => dest.PaymentMethod, opt => opt.MapFrom(src => src.PaymentMethod))
                .ForMember(dest => dest.CommissionRate, opt => opt.MapFrom(src => src.CommissionRate))
                .ForMember(dest => dest.CommissionPaid, opt => opt.MapFrom(src => src.CommissionPaid))
                .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src => src.Branch.Name))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Product.Category.Name))
                .ForMember(dest => dest.UnitType, opt => opt.MapFrom(src => src.Product.Unit.ToString()))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : null))
                .ForMember(dest => dest.CustomerPhoneNumber, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.PhoneNumber : null))
                .ForMember(dest => dest.PainterName, opt => opt.MapFrom(src => src.Painter != null ? src.Painter.Name : null))
                .ForMember(dest => dest.PainterPhoneNumber, opt => opt.MapFrom(src => src.Painter != null ? src.Painter.PhoneNumber : null))
                .ForMember(dest => dest.ItemCode, opt => opt.MapFrom(src => src.Transaction != null ? src.Transaction.ItemCode : src.Product.ItemCode))
                .ForMember(dest => dest.IsPartialPayment, opt => opt.MapFrom(src => src.IsPartialPayment))
                .ForMember(dest => dest.TotalAmount, opt => opt.MapFrom(src => src.TotalAmount))
                .ForMember(dest => dest.CommissionAmount, opt => opt.MapFrom(src => src.CommissionAmount))
                .ForMember(dest => dest.PaidAmount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.RemainingAmount, opt => opt.Ignore()) // Calculated in DTO
                .ForMember(dest => dest.IsFullyPaid, opt => opt.Ignore()); // Calculated in DTO

            // PayCreditDto -> CreditPayment
            CreateMap<PayCreditDto, CreditPayment>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.PaymentDate, opt => opt.MapFrom(src => src.PaymentDate))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
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

            // DailySales -> DailySalesItemDto
            CreateMap<DailySales, DailySalesItemDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.TransactionId, opt => opt.MapFrom(src => src.TransactionId))
                .ForMember(dest => dest.SaleDate, opt => opt.MapFrom(src => src.SaleDate))
                .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src => src.Branch != null ? src.Branch.Name : null))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product != null ? src.Product.Name : null))
                .ForMember(dest => dest.ItemCode, opt => opt.MapFrom(src => src.Transaction != null ? src.Transaction.ItemCode : src.Product != null ? src.Product.ItemCode : null))
                .ForMember(dest => dest.CategoryName, opt => opt.MapFrom(src => src.Product != null && src.Product.Category != null ? src.Product.Category.Name : null))
                .ForMember(dest => dest.CustomerName, opt => opt.MapFrom(src => src.Customer != null ? src.Customer.Name : null))
                .ForMember(dest => dest.PainterName, opt => opt.MapFrom(src => src.Painter != null ? src.Painter.Name : null))
                .ForMember(dest => dest.TransactionDate, opt => opt.MapFrom(src => src.Transaction != null ? src.Transaction.TransactionDate : src.SaleDate))
                .ForMember(dest => dest.IsPartialPayment, opt => opt.MapFrom(src => src.IsPartialPayment))
                .ForMember(dest => dest.IsCreditPayment, opt => opt.MapFrom(src => src.IsCreditPayment));

            // CreditPayment -> CreditPaymentDto
            CreateMap<CreditPayment, CreditPaymentDto>();

            // Transaction -> TransactionProductSummaryDto (for summary)
            CreateMap<Transaction, TransactionProductSummaryDto>()
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name))
                .ForMember(dest => dest.ItemCode, opt => opt.MapFrom(src => src.ItemCode))
                .ForMember(dest => dest.TransactionCount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.TotalQuantity, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.TotalAmount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.PercentageOfTotal, opt => opt.Ignore()); // Calculated in service

            // Customer -> TransactionCustomerSummaryDto (for summary)
            CreateMap<Customer, TransactionCustomerSummaryDto>()
                .ForMember(dest => dest.CustomerId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.TransactionCount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.TotalAmount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.CreditAmount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.PaidCreditAmount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.PendingCreditAmount, opt => opt.Ignore()); // Calculated in service

            // Painter -> TransactionPainterSummaryDto (for summary)
            CreateMap<Painter, TransactionPainterSummaryDto>()
                .ForMember(dest => dest.PainterId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.TransactionCount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.TotalCommissionAmount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.PaidCommissionAmount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.PendingCommissionAmount, opt => opt.Ignore()); // Calculated in service
        }
    }
}