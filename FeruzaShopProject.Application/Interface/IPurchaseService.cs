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
        // ========== 3-STEP PURCHASE WORKFLOW ==========

        // Step 1: Sales creates purchase order (quantities only)
        Task<ApiResponse<PurchaseOrderDto>> CreatePurchaseOrderAsync(CreatePurchaseOrderDto dto);

        // Step 2: Finance verifies and sets prices
        Task<ApiResponse<PurchaseOrderDto>> FinanceVerificationAsync(FinanceVerificationDto dto);

        // Step 3: Manager final approval
        Task<ApiResponse<PurchaseOrderDto>> ManagerApprovalAsync(ManagerApprovalDto dto);


        // ========== SALES EDIT/DELETE OPERATIONS ==========

        /// <summary>
        /// Sales can edit their purchase order only when status is PendingFinanceVerification
        /// </summary>
        Task<ApiResponse<PurchaseOrderDto>> EditPurchaseOrderBySalesAsync(EditPurchaseOrderBySalesDto dto);

        /// <summary>
        /// Sales can delete (cancel) their own purchase order only when status is PendingFinanceVerification
        /// </summary>
        Task<ApiResponse<bool>> DeletePurchaseOrderBySalesAsync(Guid purchaseOrderId, string? reason = null);

        /// <summary>
        /// Sales can delete a specific item from their purchase order (only in PendingFinanceVerification status)
        /// </summary>
        Task<ApiResponse<PurchaseOrderDto>> DeleteItemFromPurchaseOrderBySalesAsync(Guid purchaseOrderId, Guid itemId);


        // ========== FINANCE EDIT OPERATIONS ==========

        /// <summary>
        /// Finance can edit prices before manager approval
        /// </summary>
        Task<ApiResponse<PurchaseOrderDto>> EditPricesByFinanceAsync(EditPricesByFinanceDto dto);


        // ========== REJECT/CANCEL OPERATIONS ==========

        /// <summary>
        /// Reject purchase order (can reject specific items if needed)
        /// </summary>
        Task<ApiResponse<RejectResponseDto>> RejectPurchaseOrderAsync(RejectPurchaseOrderDto dto);

        /// <summary>
        /// Cancel purchase order
        /// </summary>
        Task<ApiResponse<bool>> CancelPurchaseOrderAsync(CancelPurchaseOrderDto dto);


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
        /// Get purchase order statistics
        /// </summary>
        Task<ApiResponse<PurchaseOrderStatsDto>> GetPurchaseOrderStatsAsync(Guid? branchId = null);

        /// <summary>
        /// Get purchase order summary for dashboard
        /// </summary>
        Task<ApiResponse<PurchaseOrderDashboardDto>> GetPurchaseOrderDashboardAsync(Guid? branchId = null);


        // ========== HELPER/CONVENIENCE METHODS ==========

        /// <summary>
        /// Convenience method for quick reject
        /// </summary>
        Task<ApiResponse<RejectResponseDto>> RejectPurchaseOrderAsync(Guid purchaseOrderId, string reason);

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