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
                .ForMember(dest => dest.Status, opt => opt.MapFrom(_ => PurchaseOrderStatus.PendingAdminAcceptance))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.Items, opt => opt.Ignore()) // Handled separately
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            CreateMap<CreatePurchaseOrderItemDto, PurchaseOrderItem>()
                .ForMember(dest => dest.QuantityRequested, opt => opt.MapFrom(src => src.QuantityRequested))
                .ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true))
                .ForMember(dest => dest.QuantityAccepted, opt => opt.Ignore())
                .ForMember(dest => dest.QuantityRegistered, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerified, opt => opt.Ignore())
                .ForMember(dest => dest.UnitPrice, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            // ========== STEP 2: ADMIN ACCEPTS QUANTITIES ==========
            CreateMap<AcceptPurchaseQuantitiesDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                // Ignore all other properties
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.SupplierId, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore());

            CreateMap<AcceptQuantityItemDto, PurchaseOrderItem>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ItemId))
                .ForMember(dest => dest.QuantityAccepted, opt => opt.MapFrom(src => src.QuantityAccepted))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                // Ignore all other properties
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.QuantityRequested, opt => opt.Ignore())
                .ForMember(dest => dest.QuantityRegistered, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerified, opt => opt.Ignore())
                .ForMember(dest => dest.UnitPrice, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore());

            // ========== STEP 3: SALES REGISTERS RECEIVED QUANTITIES ==========
            CreateMap<RegisterReceivedQuantitiesDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                // Ignore all other properties
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.SupplierId, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore());

            CreateMap<RegisterQuantityItemDto, PurchaseOrderItem>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ItemId))
                .ForMember(dest => dest.QuantityRegistered, opt => opt.MapFrom(src => src.QuantityRegistered))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                // Ignore all other properties
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.QuantityRequested, opt => opt.Ignore())
                .ForMember(dest => dest.QuantityAccepted, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerified, opt => opt.Ignore())
                .ForMember(dest => dest.UnitPrice, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore());

            // ========== STEP 4: FINANCE VERIFICATION ==========
            CreateMap<FinanceVerificationDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.SupplierId, opt => opt.MapFrom(src => src.SupplierId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                // Ignore all other properties
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore());

            CreateMap<FinanceVerificationItemDto, PurchaseOrderItem>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ItemId))
                .ForMember(dest => dest.FinanceVerified, opt => opt.MapFrom(src => src.FinanceVerified))
                .ForMember(dest => dest.UnitPrice, opt => opt.MapFrom(src => src.UnitPrice))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                // Ignore all other properties
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.QuantityRequested, opt => opt.Ignore())
                .ForMember(dest => dest.QuantityAccepted, opt => opt.Ignore())
                .ForMember(dest => dest.QuantityRegistered, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore());

            // ========== STEP 5: ADMIN FINAL APPROVAL ==========
            CreateMap<FinalApprovePurchaseOrderDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                // Ignore all other properties
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.SupplierId, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore());

            // ========== RESPONSE MAPPINGS ==========
            CreateMap<PurchaseOrder, PurchaseOrderDto>()
                .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src =>
                    src.Branch != null ? src.Branch.Name : string.Empty))
                .ForMember(dest => dest.CreatorName, opt => opt.MapFrom(src =>
                    src.Creator != null ? src.Creator.Name : string.Empty))
                .ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src =>
                    src.Supplier != null ? src.Supplier.Name : string.Empty))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src =>
                    src.Items != null && src.Items.Any()
                        ? src.Items.Where(i => i.UnitPrice.HasValue && i.QuantityRegistered.HasValue)
                                  .Sum(i => i.UnitPrice.Value * i.QuantityRegistered.Value)
                        : 0))
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items));

            CreateMap<PurchaseOrderItem, PurchaseOrderItemDto>()
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src =>
                    src.Product != null ? src.Product.Name : string.Empty))
                .ForMember(dest => dest.TotalPrice, opt => opt.MapFrom(src =>
                    src.UnitPrice.HasValue && src.QuantityRegistered.HasValue
                        ? src.UnitPrice.Value * src.QuantityRegistered.Value
                        : (decimal?)null));

            // ========== UPDATE PURCHASE ORDER ==========
            CreateMap<UpdatePurchaseOrderDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                // Ignore all other properties
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.SupplierId, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore());

            // ========== REJECT PURCHASE ORDER ==========
            CreateMap<RejectPurchaseOrderDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                // Ignore all other properties
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.SupplierId, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore());

            // ========== CANCEL PURCHASE ORDER ==========
            CreateMap<CancelPurchaseOrderDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.PurchaseOrderId))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                // Ignore all other properties
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.SupplierId, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore());

            // ========== BACKWARD COMPATIBILITY MAPPINGS ==========
            CreateMap<ReceivePurchaseOrderDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                // Ignore all other properties
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.SupplierId, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore());

            CreateMap<ReceivePurchaseOrderItemDto, PurchaseOrderItem>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.QuantityRegistered, opt => opt.MapFrom(src => src.QuantityReceived))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                // Ignore all other properties
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.QuantityRequested, opt => opt.Ignore())
                .ForMember(dest => dest.QuantityAccepted, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerified, opt => opt.Ignore())
                .ForMember(dest => dest.UnitPrice, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore());

            CreateMap<ApprovePurchaseOrderDto, PurchaseOrder>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                // Ignore all other properties
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore())
                .ForMember(dest => dest.SupplierId, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Items, opt => opt.Ignore());

            CreateMap<ApprovePurchaseOrderItemDto, PurchaseOrderItem>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.QuantityRegistered, opt => opt.MapFrom(src => src.QuantityApproved))
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                // Ignore all other properties
                .ForMember(dest => dest.PurchaseOrderId, opt => opt.Ignore())
                .ForMember(dest => dest.ProductId, opt => opt.Ignore())
                .ForMember(dest => dest.QuantityRequested, opt => opt.Ignore())
                .ForMember(dest => dest.QuantityAccepted, opt => opt.Ignore())
                .ForMember(dest => dest.FinanceVerified, opt => opt.Ignore())
                .ForMember(dest => dest.UnitPrice, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore());
        }
    }
}