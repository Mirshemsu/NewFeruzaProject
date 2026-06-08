// Domain/DTOs/ProductTransferDtos.cs
using System;
using System.Collections.Generic;

namespace FeruzaShopProject.Domain.DTOs
{
    // Step 1: Sales Transfer
    public class InitiateTransferDto
    {
        public Guid ProductId { get; set; }
        public Guid FromBranchId { get; set; }
        public Guid ToBranchId { get; set; }
        public decimal Quantity { get; set; }
        public string Reason { get; set; }
    }

    // Step 2: Sales Receive
    public class ReceiveTransferDto
    {
        public Guid TransferId { get; set; }
        public string Reason { get; set; }
    }

    // Step 3: Finance Approve
    public class ApproveTransferDto
    {
        public Guid TransferId { get; set; }
        public bool IsApproved { get; set; }
        public string Reason { get; set; }
    }

    // Cancel Transfer
    public class CancelTransferDto
    {
        public Guid TransferId { get; set; }
        public string Reason { get; set; }
    }

    // Response
    public class TransferResponseDto
    {
        public Guid Id { get; set; }
        public string TransferNumber { get; set; }
        public string ProductName { get; set; }
        public string FromBranchName { get; set; }
        public string ToBranchName { get; set; }
        public decimal Quantity { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}