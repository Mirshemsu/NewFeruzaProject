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
        // Sales creates → Admin accepts
        PendingAdminAcceptance,  // Step 1: Sales created, waiting admin approval
        PendingReceiving,        // Step 2: Admin accepted, ready to receive

        // Receiving flow
        PartiallyReceived,       // Step 3: Some items received
        CompletelyReceived,      // Step 3: All items received

        // Finance & Final approval
        PendingFinanceCheckout,  // Step 4: Goods received, waiting payment
        FullyApproved,           // Step 5: Payment done, final approval

        // Terminal states
        Rejected,
        Cancelled
    }
}
