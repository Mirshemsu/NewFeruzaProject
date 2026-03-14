using AutoMapper;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using System;
using System.Linq;

namespace FeruzaShopProject.Application.Mapper
{
    public class PurchaseOrderMapper : Profile
    {
        public PurchaseOrderMapper()
        {
            // ========== CREATE MAPPINGS ==========
            CreateMap<CreatePurchaseOrderDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => PurchaseOrderStatus.PendingFinanceVerification))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.InvoiceNumber, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedBy, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifier, opt => opt.Ignore())
                .ForMember(dest => dest.Approver, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore());

            CreateMap<CreatePurchaseItemDto, PurchaseOrderItem>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity))
                .ForMember(dest => dest.BuyingPrice, opt => opt.Ignore())
                .ForMember(dest => dest.UnitPrice, opt => opt.Ignore())
                .ForMember(dest => dest.SupplierName, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.PurchaseOrder, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore());

            // ========== FINANCE VERIFICATION MAPPINGS ==========
            CreateMap<FinanceVerificationDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.InvoiceNumber, opt => opt.MapFrom(src => src.InvoiceNumber))
                .ForMember(dest => dest.FinanceVerifiedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => PurchaseOrderStatus.PendingManagerApproval))
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifier, opt => opt.Ignore())
                .ForMember(dest => dest.Approver, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore());

            CreateMap<FinanceVerificationItemDto, PurchaseOrderItem>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ItemId))
                .ForMember(dest => dest.BuyingPrice, opt => opt.MapFrom(src => src.BuyingPrice))
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.SellingPrice))
                .ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src => src.SupplierName))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.Quantity, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.PurchaseOrder, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore());

            // ========== MANAGER APPROVAL MAPPING ==========
            CreateMap<ManagerApprovalDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.ApprovedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src =>
                    src.IsApproved ? PurchaseOrderStatus.Approved : PurchaseOrderStatus.Rejected))
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.InvoiceNumber, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedBy, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifier, opt => opt.Ignore())
                .ForMember(dest => dest.Approver, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore());

            // ========== SALES EDIT MAPPINGS ==========
            CreateMap<EditPurchaseOrderBySalesDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.BranchId, opt => opt.MapFrom(src => src.BranchId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.InvoiceNumber, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedBy, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifier, opt => opt.Ignore())
                .ForMember(dest => dest.Approver, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore());

            CreateMap<EditPurchaseItemDto, PurchaseOrderItem>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ItemId.HasValue ? src.ItemId.Value : Guid.NewGuid()))
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.ItemId.HasValue ? (DateTime?)null : DateTime.UtcNow))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(src => true))
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.BuyingPrice, opt => opt.Ignore())
                .ForMember(dest => dest.UnitPrice, opt => opt.Ignore())
                .ForMember(dest => dest.SupplierName, opt => opt.Ignore())
                .ForMember(dest => dest.PurchaseOrder, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore());

            // ========== FINANCE EDIT MAPPINGS (Comprehensive) ==========
            CreateMap<EditPurchaseOrderByFinanceDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.InvoiceNumber, opt => opt.MapFrom(src => src.InvoiceNumber))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedBy, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifier, opt => opt.Ignore())
                .ForMember(dest => dest.Approver, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore());

            CreateMap<EditFinanceItemDto, PurchaseOrderItem>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ItemId))
                .ForMember(dest => dest.BuyingPrice, opt => opt.MapFrom(src => src.BuyingPrice))
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.SellingPrice))
                .ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src => src.SupplierName))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.Quantity, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.PurchaseOrder, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore())
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) =>
                    srcMember != null)); // Only map non-null values

            // ========== REJECT/CANCEL MAPPINGS ==========
            CreateMap<RejectPurchaseOrderDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => PurchaseOrderStatus.Rejected))
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.InvoiceNumber, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedBy, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifier, opt => opt.Ignore())
                .ForMember(dest => dest.Approver, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore());

            CreateMap<CancelPurchaseOrderDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => PurchaseOrderStatus.Cancelled))
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.InvoiceNumber, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedBy, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifier, opt => opt.Ignore())
                .ForMember(dest => dest.Approver, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore());

            // ========== RESPONSE MAPPINGS ==========
            CreateMap<PurchaseOrder, PurchaseOrderDto>()
                .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src =>
                    src.Branch != null ? src.Branch.Name : null))
                .ForMember(dest => dest.CreatedByName, opt => opt.MapFrom(src =>
                    src.Creator != null ? src.Creator.Name : null))
                .ForMember(dest => dest.FinanceVerifiedBy, opt => opt.MapFrom(src =>
                    src.FinanceVerifier != null ? src.FinanceVerifier.Name : null))
                .ForMember(dest => dest.ApprovedBy, opt => opt.MapFrom(src =>
                    src.Approver != null ? src.Approver.Name : null))
                .ForMember(dest => dest.TotalValue, opt => opt.MapFrom(src => src.TotalBuyingCost))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items));

            CreateMap<PurchaseOrderItem, PurchaseOrderItemDto>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src =>
                    src.Product != null ? src.Product.Name : null))
                .ForMember(dest => dest.ProfitMargin, opt => opt.MapFrom(src => src.ProfitMargin));

            // ========== HISTORY MAPPING ==========
            CreateMap<PurchaseHistory, RecentPurchaseHistoryDto>()
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.OrderNumber, opt => opt.MapFrom(src =>
                    src.PurchaseOrder != null ? src.PurchaseOrder.Id.ToString().Substring(0, 8) : null))
                .ForMember(dest => dest.Action, opt => opt.MapFrom(src => src.Action))
                .ForMember(dest => dest.PerformedBy, opt => opt.MapFrom(src =>
                    src.PerformedByUser != null ? src.PerformedByUser.Name : null))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.Details, opt => opt.MapFrom(src => src.Details));
        }
    }
}