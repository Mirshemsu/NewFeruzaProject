// Application/Interface/IProductTransferService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;

namespace FeruzaShopProject.Application.Interface
{
    public interface IProductTransferService
    {
        // Step 1: Sales transfers product (decrease stock from source)
        Task<ApiResponse<TransferResponseDto>> InitiateTransferAsync(InitiateTransferDto dto, Guid userId);

        // Step 2: Sales receives product (increase stock at destination)
        Task<ApiResponse<TransferResponseDto>> ReceiveTransferAsync(ReceiveTransferDto dto, Guid userId);

        // Step 3: Finance approves
        Task<ApiResponse<TransferResponseDto>> ApproveTransferAsync(ApproveTransferDto dto, Guid userId);

        // Cancel transfer
        Task<ApiResponse<TransferResponseDto>> CancelTransferAsync(CancelTransferDto dto, Guid userId);

        // Query
        Task<ApiResponse<List<TransferResponseDto>>> GetTransfersByBranchAsync(Guid branchId);
        Task<ApiResponse<TransferResponseDto>> GetTransferByIdAsync(Guid id);
    }
}