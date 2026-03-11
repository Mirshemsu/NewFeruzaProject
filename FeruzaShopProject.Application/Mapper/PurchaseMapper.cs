using System;
using AutoMapper;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using System.Linq;

namespace FeruzaShopProject.Application.Mapper
{
    public class PurchaseMapper : Profile
    {
        public PurchaseMapper()
        {
            // ========== STEP 1: CREATE PURCHASE ORDER (SALES) ==========
            CreateMap<CreatePurchaseOrderDto, PurchaseOrder>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(_ => PurchaseOrderStatus.PendingFinanceVerification))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore());

            CreateMap<CreatePurchaseItemDto, PurchaseOrderItem>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity))
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.BuyingPrice, opt => opt.Ignore())
                .ForMember(dest => dest.UnitPrice, opt => opt.Ignore())
                .ForMember(dest => dest.SupplierName, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerified, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedBy, opt => opt.Ignore())
                .ForMember(dest => dest.PriceSetAt, opt => opt.Ignore())
                .ForMember(dest => dest.PriceSetBy, opt => opt.Ignore())
                .ForMember(dest => dest.PriceEditCount, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.PurchaseOrder, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore());

            // ========== STEP 2: FINANCE VERIFICATION ==========
            CreateMap<FinanceVerificationDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore());

            CreateMap<FinanceVerificationItemDto, PurchaseOrderItem>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ItemId))
                .ForMember(dest => dest.BuyingPrice, opt => opt.MapFrom(src => src.BuyingPrice))
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.SellingPrice))
                .ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src => src.SupplierName))
                .ForMember(dest => dest.FinanceVerified, opt => opt.MapFrom(src => src.IsVerified))
                .ForMember(dest => dest.FinanceVerifiedAt, opt => opt.MapFrom(src => src.IsVerified ? DateTime.UtcNow : (DateTime?)null))
                .ForMember(dest => dest.PriceSetAt, opt => opt.MapFrom(src => src.IsVerified ? DateTime.UtcNow : (DateTime?)null))
                .ForMember(dest => dest.PriceEditCount, opt => opt.MapFrom(src => src.IsVerified ? 1 : 0))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.Quantity, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedBy, opt => opt.Ignore())
                .ForMember(dest => dest.PriceSetBy, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.PurchaseOrder, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore());

            // ========== STEP 3: MANAGER APPROVAL ==========
            CreateMap<ManagerApprovalDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore());

            // ========== SALES EDIT OPERATIONS ==========

            /// <summary>
            /// Sales can edit their purchase order before finance verification
            /// </summary>
            CreateMap<EditPurchaseOrderBySalesDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.BranchId, opt => opt.MapFrom(src => src.BranchId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore());

            CreateMap<EditPurchaseItemDto, PurchaseOrderItem>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ItemId.HasValue ? src.ItemId.Value : Guid.NewGuid()))
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.ItemId.HasValue ? (DateTime?)null : DateTime.UtcNow))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.BuyingPrice, opt => opt.Ignore())
                .ForMember(dest => dest.UnitPrice, opt => opt.Ignore())
                .ForMember(dest => dest.SupplierName, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerified, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedBy, opt => opt.Ignore())
                .ForMember(dest => dest.PriceSetAt, opt => opt.Ignore())
                .ForMember(dest => dest.PriceSetBy, opt => opt.Ignore())
                .ForMember(dest => dest.PriceEditCount, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
                .ForMember(dest => dest.PurchaseOrder, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore());


            // ========== FINANCE EDIT OPERATIONS ==========

            /// <summary>
            /// Finance can edit prices before manager approval
            /// </summary>
            CreateMap<EditPricesByFinanceDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore());

            CreateMap<EditPriceItemDto, PurchaseOrderItem>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ItemId))
                .ForMember(dest => dest.BuyingPrice, opt => opt.MapFrom(src => src.BuyingPrice))
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.SellingPrice))
                .ForMember(dest => dest.PriceSetAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.PriceEditCount, opt => opt.MapFrom(_ => 1)) // Fixed: Use constant value, not from DTO
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.Quantity, opt => opt.Ignore())
                .ForMember(dest => dest.SupplierName, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerified, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedAt, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerifiedBy, opt => opt.Ignore())
                .ForMember(dest => dest.PriceSetBy, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.PurchaseOrder, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore());

            // ========== REJECT/CANCEL OPERATIONS ==========
            CreateMap<RejectPurchaseOrderDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore());

            CreateMap<CancelPurchaseOrderDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Creator, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore())
                .ForMember(dest => dest.History, opt => opt.Ignore());

            // ========== RESPONSE MAPPINGS ==========
            CreateMap<PurchaseOrder, PurchaseOrderDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.BranchId, opt => opt.MapFrom(src => src.BranchId))
                .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src =>
                    src.Branch != null ? src.Branch.Name : string.Empty))
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
                .ForMember(dest => dest.CreatedByName, opt => opt.MapFrom(src =>
                    src.Creator != null ? src.Creator.Name : string.Empty))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreatedAt))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(src => src.UpdatedAt))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items))
                .ForMember(dest => dest.TotalItems, opt => opt.MapFrom(src => src.TotalItems))
                .ForMember(dest => dest.VerifiedItems, opt => opt.MapFrom(src =>
                    src.Items != null ? src.Items.Count(i => i.FinanceVerified == true) : 0))
                .ForMember(dest => dest.ApprovedItems, opt => opt.MapFrom(src =>
                    src.Items != null ? src.Items.Count(i => i.ApprovedAt.HasValue) : 0))
                .ForMember(dest => dest.TotalValue, opt => opt.MapFrom(src =>
                    src.Items != null && src.Items.Any()
                        ? src.Items.Where(i => i.BuyingPrice.HasValue)
                                  .Sum(i => i.BuyingPrice.Value * i.Quantity)
                        : 0m));

            CreateMap<PurchaseOrderItem, PurchaseOrderItemDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.ProductId, opt => opt.MapFrom(src => src.ProductId))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src =>
                    src.Product != null ? src.Product.Name : string.Empty))
                .ForMember(dest => dest.Quantity, opt => opt.MapFrom(src => src.Quantity))
                .ForMember(dest => dest.BuyingPrice, opt => opt.MapFrom(src => src.BuyingPrice))
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice))
                .ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src => src.SupplierName))
                .ForMember(dest => dest.FinanceVerified, opt => opt.MapFrom(src => src.FinanceVerified))
                .ForMember(dest => dest.FinanceVerifiedAt, opt => opt.MapFrom(src => src.FinanceVerifiedAt))
                .ForMember(dest => dest.ApprovedAt, opt => opt.MapFrom(src => src.ApprovedAt))
                .ForMember(dest => dest.IsFinanceVerified, opt => opt.MapFrom(src => src.IsFinanceVerified))
                .ForMember(dest => dest.IsApproved, opt => opt.MapFrom(src => src.IsApproved))
                .ForMember(dest => dest.ProfitMargin, opt => opt.MapFrom(src => src.ProfitMargin));

            // ========== REJECT RESPONSE MAPPING ==========
            CreateMap<PurchaseOrder, RejectResponseDto>()
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.NewStatus, opt => opt.MapFrom(src => src.Status))
                .ForMember(dest => dest.RejectedItems, opt => opt.Ignore()) // This will be set manually
                .ForMember(dest => dest.Message, opt => opt.Ignore());      // This will be set manually

            // ========== LEGACY MAPPINGS FOR BACKWARD COMPATIBILITY ==========
            // Map old property names to new ones for backward compatibility
            CreateMap<PurchaseOrder, object>().ReverseMap();
        }
    }
}