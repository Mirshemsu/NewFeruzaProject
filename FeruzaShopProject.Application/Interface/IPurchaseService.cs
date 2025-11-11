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
        Task<ApiResponse<PurchaseOrderDto>> CreatePurchaseOrderAsync(CreatePurchaseOrderDto dto);
        Task<ApiResponse<PurchaseOrderDto>> UpdatePurchaseOrderAsync(UpdatePurchaseOrderDto dto);
        Task<ApiResponse<PurchaseOrderDto>> ReceivePurchaseOrderAsync(ReceivePurchaseOrderDto dto);
        Task<ApiResponse<PurchaseOrderDto>> ApprovePurchaseOrderAsync(ApprovePurchaseOrderDto dto);
        Task<ApiResponse<PurchaseOrderDto>> UpdatePurchaseOrderStatusAsync(Guid id, PurchaseOrderStatus status);
        Task<ApiResponse<PurchaseOrderDto>> GetPurchaseOrderByIdAsync(Guid id);
        Task<ApiResponse<List<PurchaseOrderDto>>> GetAllPurchaseOrdersAsync();
        Task<ApiResponse<bool>> CancelPurchaseOrderAsync(Guid id);

        // New methods for revised workflow
        Task<ApiResponse<PurchaseOrderDto>> AcceptByAdminAsync(Guid purchaseOrderId);
        Task<ApiResponse<PurchaseOrderDto>> CheckoutByFinanceAsync(Guid purchaseOrderId);
        Task<ApiResponse<PurchaseOrderDto>> FinalApproveByAdminAsync(Guid purchaseOrderId);
        Task<ApiResponse<PurchaseOrderDto>> RejectPurchaseOrderAsync(Guid purchaseOrderId, string reason);
    }
}
