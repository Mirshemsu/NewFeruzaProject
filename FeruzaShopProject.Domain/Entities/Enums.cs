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
}
