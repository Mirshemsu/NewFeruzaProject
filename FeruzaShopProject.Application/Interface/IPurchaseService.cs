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

        // Step 3: Sales registers received quantities
        Task<ApiResponse<PurchaseOrderDto>> RegisterReceivedQuantitiesAsync(RegisterReceivedQuantitiesDto dto);

        // Step 4: Finance verification
        Task<ApiResponse<PurchaseOrderDto>> FinanceVerificationAsync(FinanceVerificationDto dto);

        // Step 5: Admin final approval
        Task<ApiResponse<PurchaseOrderDto>> FinalApprovalByAdminAsync(FinalApprovePurchaseOrderDto dto);

        // ========== ADDITIONAL OPERATIONS ==========

        // Reject purchase order
        Task<ApiResponse<PurchaseOrderDto>> RejectPurchaseOrderAsync(RejectPurchaseOrderDto dto);

        // Cancel purchase order
        Task<ApiResponse<bool>> CancelPurchaseOrderAsync(CancelPurchaseOrderDto dto);

        // Update purchase order
        Task<ApiResponse<PurchaseOrderDto>> UpdatePurchaseOrderAsync(UpdatePurchaseOrderDto dto);

        // ========== QUERY METHODS ==========

        // Get purchase order by ID
        Task<ApiResponse<PurchaseOrderDto>> GetPurchaseOrderByIdAsync(Guid id);

        // Get all purchase orders
        Task<ApiResponse<List<PurchaseOrderDto>>> GetAllPurchaseOrdersAsync();

        // Get purchase orders by status
        Task<ApiResponse<List<PurchaseOrderDto>>> GetPurchaseOrdersByStatusAsync(PurchaseOrderStatus status);

        // Get purchase orders by branch
        Task<ApiResponse<List<PurchaseOrderDto>>> GetPurchaseOrdersByBranchAsync(Guid branchId);

        // Get purchase orders by creator
        Task<ApiResponse<List<PurchaseOrderDto>>> GetPurchaseOrdersByCreatorAsync(Guid createdBy);

 
        // Get purchase order statistics
        Task<ApiResponse<PurchaseOrderStatsDto>> GetPurchaseOrderStatsAsync(Guid? branchId = null);
    }
}