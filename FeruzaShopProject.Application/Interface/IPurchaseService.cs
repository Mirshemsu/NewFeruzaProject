using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Interface
{
    public interface IPurchaseService
    {
        // ========== 5-STEP PURCHASE WORKFLOW ==========

        // Step 1: Sales creates purchase order
        Task<ApiResponse<PurchaseOrderDto>> CreatePurchaseOrderAsync(CreatePurchaseOrderDto dto);

        // Step 2: Admin accepts quantities
        Task<ApiResponse<PurchaseOrderDto>> AcceptQuantitiesByAdminAsync(AcceptPurchaseQuantitiesDto dto);

        // Step 3: Sales registers received quantities (can be done multiple times)
        Task<ApiResponse<PurchaseOrderDto>> RegisterReceivedQuantitiesAsync(RegisterReceivedQuantitiesDto dto);

        // Step 4: Finance verification (partial processing supported)
        Task<ApiResponse<PurchaseOrderDto>> FinanceVerificationAsync(FinanceVerificationDto dto);

        // Step 5: Admin final approval (partial approval supported)
        Task<ApiResponse<PurchaseOrderDto>> FinalApprovalByAdminAsync(FinalApprovePurchaseOrderDto dto);


        // ========== SALES EDIT/DELETE OPERATIONS ==========

        /// <summary>
        /// Sales can edit their purchase order only when status is PendingAdminAcceptance
        /// </summary>
        Task<ApiResponse<PurchaseOrderDto>> EditPurchaseOrderBySalesAsync(EditPurchaseOrderBySalesDto dto);

        /// <summary>
        /// Sales can delete (cancel) their own purchase order only when status is PendingAdminAcceptance
        /// </summary>
        Task<ApiResponse<bool>> DeletePurchaseOrderBySalesAsync(Guid purchaseOrderId, string? reason = null);

        /// <summary>
        /// Sales can edit registered quantities before finance verification
        /// </summary>
        Task<ApiResponse<PurchaseOrderDto>> EditRegisteredQuantitiesBySalesAsync(EditRegisteredQuantitiesBySalesDto dto);


        // ========== ADMIN EDIT/DELETE OPERATIONS ==========

        /// <summary>
        /// Admin can edit any purchase order at any stage (except FullyApproved)
        /// </summary>
        Task<ApiResponse<PurchaseOrderDto>> EditPurchaseOrderByAdminAsync(EditPurchaseOrderByAdminDto dto);

        /// <summary>
        /// Admin can edit only accepted quantities
        /// </summary>
        Task<ApiResponse<PurchaseOrderDto>> EditAcceptedQuantitiesByAdminAsync(EditAcceptedQuantitiesByAdminDto dto);

        /// <summary>
        /// Admin can edit only registered quantities
        /// </summary>
        Task<ApiResponse<PurchaseOrderDto>> EditRegisteredQuantitiesByAdminAsync(EditRegisteredQuantitiesByAdminDto dto);

        /// <summary>
        /// Admin can edit prices at any time before final approval
        /// </summary>
        Task<ApiResponse<PurchaseOrderDto>> EditPricesByAdminAsync(EditPricesByAdminDto dto);

        /// <summary>
        /// Admin can delete any purchase order (except FullyApproved)
        /// </summary>
        Task<ApiResponse<bool>> DeletePurchaseOrderByAdminAsync(Guid purchaseOrderId, string reason);


        // ========== REJECT/CANCEL OPERATIONS ==========

        /// <summary>
        /// Reject purchase order (can reject specific items if needed)
        /// </summary>
        Task<ApiResponse<PurchaseOrderDto>> RejectPurchaseOrderAsync(RejectPurchaseOrderDto dto);

        /// <summary>
        /// Cancel purchase order
        /// </summary>
        Task<ApiResponse<bool>> CancelPurchaseOrderAsync(CancelPurchaseOrderDto dto);


        // ========== UPDATE PURCHASE ORDER (Legacy) ==========
        Task<ApiResponse<PurchaseOrderDto>> UpdatePurchaseOrderAsync(UpdatePurchaseOrderDto dto);


        // ========== QUERY METHODS ==========

        /// <summary>
        /// Get purchase order by ID with all details
        /// </summary>
        Task<ApiResponse<PurchaseOrderDto>> GetPurchaseOrderByIdAsync(Guid id);

        /// <summary>
        /// Get all purchase orders
        /// </summary>
        Task<ApiResponse<List<PurchaseOrderDto>>> GetAllPurchaseOrdersAsync();

        /// <summary>
        /// Get purchase orders by status
        /// </summary>
        Task<ApiResponse<List<PurchaseOrderDto>>> GetPurchaseOrdersByStatusAsync(PurchaseOrderStatus status);

        /// <summary>
        /// Get purchase orders by branch
        /// </summary>
        Task<ApiResponse<List<PurchaseOrderDto>>> GetPurchaseOrdersByBranchAsync(Guid branchId);

        /// <summary>
        /// Get purchase orders by creator
        /// </summary>
        Task<ApiResponse<List<PurchaseOrderDto>>> GetPurchaseOrdersByCreatorAsync(Guid createdBy);

        /// <summary>
        /// Get purchase orders by date range
        /// </summary>
        Task<ApiResponse<List<PurchaseOrderDto>>> GetPurchaseOrdersByDateRangeAsync(DateTime fromDate, DateTime toDate, Guid? branchId = null);

        /// <summary>
        /// Get purchase orders by supplier
        /// </summary>
        Task<ApiResponse<List<PurchaseOrderDto>>> GetPurchaseOrdersBySupplierAsync(Guid supplierId);

        /// <summary>
        /// Get purchase order statistics
        /// </summary>
        Task<ApiResponse<PurchaseOrderStatsDto>> GetPurchaseOrderStatsAsync(Guid? branchId = null);

        /// <summary>
        /// Get purchase order summary for dashboard
        /// </summary>
        Task<ApiResponse<PurchaseOrderDashboardDto>> GetPurchaseOrderDashboardAsync(Guid? branchId = null);


        // ========== HELPER/CONVENIENCE METHODS ==========

        /// <summary>
        /// Convenience method to accept all quantities (accept requested quantities)
        /// </summary>
        Task<ApiResponse<PurchaseOrderDto>> AcceptAllByAdminAsync(Guid purchaseOrderId);

        /// <summary>
        /// Convenience method for quick reject
        /// </summary>
        Task<ApiResponse<PurchaseOrderDto>> RejectPurchaseOrderAsync(Guid purchaseOrderId, string reason);

        /// <summary>
        /// Convenience method for quick cancel
        /// </summary>
        Task<ApiResponse<bool>> CancelPurchaseOrderAsync(Guid purchaseOrderId, string? reason = null);

        /// <summary>
        /// Check if user has permission to edit a purchase order
        /// </summary>
        Task<ApiResponse<bool>> CanUserEditAsync(Guid purchaseOrderId, Guid userId);
    }
}