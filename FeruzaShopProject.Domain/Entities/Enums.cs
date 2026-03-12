using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public enum Role
    {
        Manager,
        Sales,
        Finance
    }
    public enum UnitType
    {
        Kg = 0,
        Lit = 1,
        MG = 2,
        Pcs = 3
    }
    public enum PaymentMethod
    {
        Cash,
        Bank,
        Credit,
       
    }
    public enum StockMovementType
    {
        Purchase,
        Sale,
        Adjustment,
        Return,
        Damage,
        Transfer
    }

    public enum PurchaseOrderStatus
    {
        PendingFinanceVerification,  // Sales registered, waiting for finance
        PendingManagerApproval,      // Finance verified, waiting for manager
        Approved,                    // Fully approved by manager
        Rejected,                    // Rejected at any stage
        Cancelled                    // Cancelled before completion
    }
    public enum DailyClosingStatus
    {
        Pending,    // Sales are still working - transactions and transfers allowed
        Closed,     // Sales has closed the day - no more transactions, transfers still allowed
        Approved,   // Finance approved - DATE IS LOCKED, no more changes
        Rejected,   // Finance rejected (needs reopening)
        Adjusted    // Finance made adjustments
    }
}
