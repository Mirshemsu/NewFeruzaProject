using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;
using FeruzaShopProject.Infrastructre.Data;
using Microsoft.EntityFrameworkCore;

namespace FeruzaShopProject.Infrastructre.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ShopDbContext _context;

        public CustomerService(ShopDbContext context)
        {
            _context = context;
        }

        public static string NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return string.Empty;

            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.StartsWith("251") && digits.Length > 9)
                digits = digits[3..];
            digits = digits.TrimStart('0');
            return digits;
        }

        public async Task<ApiResponse<List<CustomerListItemDto>>> GetAllAsync(Guid? branchId, string? search)
        {
            var query = _context.Customers
                .AsNoTracking()
                .Include(c => c.Branch)
                .Where(c => c.IsDirectory);

            if (branchId.HasValue)
                query = query.Where(c => c.BranchId == branchId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                var phone = NormalizePhone(search);
                query = query.Where(c =>
                    c.Name.ToLower().Contains(term) ||
                    (phone != "" && c.PhoneNumber.Contains(phone)));
            }

            var customers = await query.OrderBy(c => c.Name).ToListAsync();
            var credits = await LoadCreditTotalsAsync(customers.Select(c => c.Id).ToList());

            var result = customers.Select(c =>
            {
                credits.TryGetValue(c.Id, out var totals);
                return ToListItem(c, totals);
            }).ToList();

            return ApiResponse<List<CustomerListItemDto>>.Success(result);
        }

        public async Task<ApiResponse<List<CreditTransactionHistoryDto>>> GetCreditsAsync(Guid customerId, Guid? branchScope)
        {
            var customer = await _context.Customers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == customerId && c.IsDirectory);
            if (customer == null || (branchScope.HasValue && customer.BranchId != branchScope))
                return ApiResponse<List<CreditTransactionHistoryDto>>.Fail("Customer not found.");

            var rows = await CreditQuery()
                .Where(t => t.CustomerId == customerId && t.BranchId == customer.BranchId)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            return ApiResponse<List<CreditTransactionHistoryDto>>.Success(await MapHistoryAsync(rows));
        }

        public async Task<ApiResponse<List<CreditTransactionHistoryDto>>> GetUnlinkedCreditsAsync(Guid? branchId)
        {
            var directoryIds = _context.Customers.Where(c => c.IsDirectory).Select(c => c.Id);
            var query = CreditQuery()
                .Where(t => t.CustomerId == null || !directoryIds.Contains(t.CustomerId.Value));

            if (branchId.HasValue)
                query = query.Where(t => t.BranchId == branchId.Value);

            var rows = await query.OrderByDescending(t => t.TransactionDate).ToListAsync();
            return ApiResponse<List<CreditTransactionHistoryDto>>.Success(await MapHistoryAsync(rows));
        }

        public async Task<ApiResponse<CustomerListItemDto>> CreateAsync(CreateCustomerDto dto)
        {
            var branch = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == dto.BranchId);
            if (branch == null)
                return ApiResponse<CustomerListItemDto>.Fail("Branch not found.");

            var name = dto.Name.Trim();
            var phone = NormalizePhone(dto.PhoneNumber);
            var phoneError = ValidatePhone(phone);
            if (phoneError != null)
                return ApiResponse<CustomerListItemDto>.Fail(phoneError);

            if (await PhoneTakenAsync(dto.BranchId, phone, null))
                return ApiResponse<CustomerListItemDto>.Fail("A customer with this phone number already exists in this branch.");

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = name,
                PhoneNumber = phone,
                BranchId = dto.BranchId,
                IsDirectory = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            await _context.Customers.AddAsync(customer);
            await _context.SaveChangesAsync();
            customer.Branch = branch;
            return ApiResponse<CustomerListItemDto>.Success(ToListItem(customer, null));
        }

        public async Task<ApiResponse<CustomerListItemDto>> UpdateAsync(UpdateCustomerDto dto, Guid? branchScope)
        {
            var customer = await _context.Customers.Include(c => c.Branch)
                .FirstOrDefaultAsync(c => c.Id == dto.Id && c.IsDirectory);
            if (customer == null || customer.BranchId == null || (branchScope.HasValue && customer.BranchId != branchScope))
                return ApiResponse<CustomerListItemDto>.Fail("Customer not found.");

            var phone = NormalizePhone(dto.PhoneNumber);
            var phoneError = ValidatePhone(phone);
            if (phoneError != null)
                return ApiResponse<CustomerListItemDto>.Fail(phoneError);

            if (await PhoneTakenAsync(customer.BranchId.Value, phone, customer.Id))
                return ApiResponse<CustomerListItemDto>.Fail("A customer with this phone number already exists in this branch.");

            customer.Name = dto.Name.Trim();
            customer.PhoneNumber = phone;
            customer.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            var credits = await LoadCreditTotalsAsync(new List<Guid> { customer.Id });
            credits.TryGetValue(customer.Id, out var totals);
            return ApiResponse<CustomerListItemDto>.Success(ToListItem(customer, totals));
        }

        public async Task<ApiResponse<ImportCustomersResultDto>> ImportAsync(ImportCustomersDto dto)
        {
            if (dto.Rows == null || dto.Rows.Count == 0)
                return ApiResponse<ImportCustomersResultDto>.Fail("The file has no customers.");
            if (dto.Rows.Count > 500)
                return ApiResponse<ImportCustomersResultDto>.Fail("Import at most 500 customers at a time.");

            var branchExists = await _context.Branches.AnyAsync(b => b.Id == dto.BranchId);
            if (!branchExists)
                return ApiResponse<ImportCustomersResultDto>.Fail("Branch not found.");

            var taken = (await _context.Customers
                .Where(c => c.IsDirectory && c.BranchId == dto.BranchId)
                .Select(c => c.PhoneNumber)
                .ToListAsync()).ToHashSet();

            var result = new ImportCustomersResultDto();
            var pending = new List<Customer>();

            foreach (var row in dto.Rows)
            {
                var name = row.Name?.Trim() ?? "";
                var phone = NormalizePhone(row.PhoneNumber);
                string? reason = null;
                if (string.IsNullOrWhiteSpace(name))
                    reason = "Customer name is required.";
                else if (name.Length > 200)
                    reason = "Customer name is too long.";
                else
                    reason = ValidatePhone(phone);

                if (reason == null && !taken.Add(phone))
                    reason = "Duplicate phone number in this branch or in the file.";

                if (reason != null)
                {
                    result.Skipped.Add(new ImportCustomerSkipDto
                    {
                        RowNumber = row.RowNumber,
                        Name = row.Name,
                        PhoneNumber = row.PhoneNumber,
                        Reason = reason
                    });
                    continue;
                }

                pending.Add(new Customer
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    PhoneNumber = phone,
                    BranchId = dto.BranchId,
                    IsDirectory = true,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                });
            }

            if (pending.Count > 0)
            {
                await _context.Customers.AddRangeAsync(pending);
                await _context.SaveChangesAsync();
            }

            result.AddedCount = pending.Count;
            return ApiResponse<ImportCustomersResultDto>.Success(result);
        }

        public async Task<ApiResponse<bool>> LinkSaleAsync(LinkCustomerSaleDto dto, Guid? branchScope)
        {
            var sale = await _context.Transactions
                .FirstOrDefaultAsync(t => t.Id == dto.TransactionId && t.PaymentMethod == PaymentMethod.Credit);
            if (sale == null || (branchScope.HasValue && sale.BranchId != branchScope))
                return ApiResponse<bool>.Fail("Credit sale not found.");

            if (sale.CustomerId.HasValue)
            {
                var current = await _context.Customers.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == sale.CustomerId.Value);
                if (current != null && current.IsDirectory)
                    return ApiResponse<bool>.Fail("This sale is already linked to a customer.");
            }

            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Id == dto.CustomerId && c.IsDirectory);
            if (customer == null || customer.BranchId != sale.BranchId)
                return ApiResponse<bool>.Fail("Select a customer from the same branch as the sale.");

            sale.CustomerId = customer.Id;
            sale.UpdatedAt = DateTime.UtcNow;

            var dailySales = await _context.DailySales
                .Where(ds => ds.TransactionId == sale.Id)
                .ToListAsync();
            foreach (var daily in dailySales)
                daily.CustomerId = customer.Id;

            await _context.SaveChangesAsync();
            return ApiResponse<bool>.Success(true);
        }

        public async Task<ApiResponse<bool>> DeactivateAsync(Guid id, Guid? branchScope)
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.Id == id && c.IsDirectory);
            if (customer == null || (branchScope.HasValue && customer.BranchId != branchScope))
                return ApiResponse<bool>.Fail("Customer not found.");

            var used = await _context.Transactions.AnyAsync(t => t.CustomerId == id)
                || await _context.DailySales.AnyAsync(ds => ds.CustomerId == id);
            if (used)
                return ApiResponse<bool>.Fail("This customer has sales and cannot be removed.");

            customer.Deactivate();
            await _context.SaveChangesAsync();
            return ApiResponse<bool>.Success(true);
        }

        private IQueryable<Transaction> CreditQuery()
        {
            return _context.Transactions
                .AsNoTracking()
                .Include(t => t.Product)
                .Include(t => t.Branch)
                .Include(t => t.Customer)
                .Where(t => t.PaymentMethod == PaymentMethod.Credit);
        }

        private async Task<Dictionary<Guid, (int Count, decimal Total, decimal Paid)>> LoadCreditTotalsAsync(List<Guid> customerIds)
        {
            var totals = new Dictionary<Guid, (int Count, decimal Total, decimal Paid)>();
            if (customerIds.Count == 0)
                return totals;

            var sales = await _context.Transactions.AsNoTracking()
                .Where(t => t.PaymentMethod == PaymentMethod.Credit
                    && t.CustomerId != null
                    && customerIds.Contains(t.CustomerId.Value))
                .Select(t => new
                {
                    t.Id,
                    CustomerId = t.CustomerId!.Value,
                    t.BranchId,
                    Total = t.UnitPrice * t.Quantity
                })
                .ToListAsync();

            var branchByCustomer = await _context.Customers.AsNoTracking()
                .Where(c => customerIds.Contains(c.Id))
                .Select(c => new { c.Id, c.BranchId })
                .ToDictionaryAsync(c => c.Id, c => c.BranchId);

            sales = sales.Where(s =>
                branchByCustomer.TryGetValue(s.CustomerId, out var branchId) && branchId == s.BranchId).ToList();

            var saleIds = sales.Select(s => s.Id).ToList();
            var paidBySale = saleIds.Count == 0
                ? new Dictionary<Guid, decimal>()
                : await _context.CreditPayments.AsNoTracking()
                    .Where(p => saleIds.Contains(p.TransactionId))
                    .GroupBy(p => p.TransactionId)
                    .Select(g => new { g.Key, Paid = g.Sum(x => x.Amount) })
                    .ToDictionaryAsync(x => x.Key, x => x.Paid);

            foreach (var group in sales.GroupBy(s => s.CustomerId))
            {
                var paid = group.Sum(s => paidBySale.TryGetValue(s.Id, out var amount) ? amount : 0m);
                totals[group.Key] = (group.Count(), group.Sum(s => s.Total), paid);
            }

            return totals;
        }

        private async Task<List<CreditTransactionHistoryDto>> MapHistoryAsync(List<Transaction> rows)
        {
            var ids = rows.Select(r => r.Id).ToList();
            var paidBySale = ids.Count == 0
                ? new Dictionary<Guid, decimal>()
                : await _context.CreditPayments.AsNoTracking()
                    .Where(p => ids.Contains(p.TransactionId))
                    .GroupBy(p => p.TransactionId)
                    .Select(g => new { g.Key, Paid = g.Sum(x => x.Amount) })
                    .ToDictionaryAsync(x => x.Key, x => x.Paid);

            return rows.Select(t =>
            {
                var total = t.UnitPrice * t.Quantity;
                paidBySale.TryGetValue(t.Id, out var paid);
                return new CreditTransactionHistoryDto
                {
                    TransactionId = t.Id,
                    TransactionDate = t.TransactionDate,
                    ItemCode = t.ItemCode,
                    ProductName = t.Product?.Name ?? "",
                    Quantity = t.Quantity,
                    UnitPrice = t.UnitPrice,
                    TotalAmount = total,
                    PaidAmount = paid,
                    CustomerId = t.CustomerId ?? Guid.Empty,
                    CustomerName = t.Customer?.Name ?? "",
                    CustomerPhoneNumber = t.Customer?.PhoneNumber ?? "",
                    BranchId = t.BranchId,
                    BranchName = t.Branch?.Name ?? "",
                    Remark = t.Remark
                };
            }).ToList();
        }

        private async Task<bool> PhoneTakenAsync(Guid branchId, string phone, Guid? exceptId)
        {
            return await _context.Customers.AnyAsync(c =>
                c.IsDirectory &&
                c.BranchId == branchId &&
                c.PhoneNumber == phone &&
                (exceptId == null || c.Id != exceptId));
        }

        private static string? ValidatePhone(string phone)
        {
            if (string.IsNullOrEmpty(phone))
                return "Phone number is required.";
            if (phone.Length < 6 || phone.Length > 20)
                return "Enter a valid phone number.";
            return null;
        }

        private static CustomerListItemDto ToListItem(
            Customer customer,
            (int Count, decimal Total, decimal Paid)? totals)
        {
            var count = totals?.Count ?? 0;
            var total = totals?.Total ?? 0m;
            var paid = totals?.Paid ?? 0m;
            return new CustomerListItemDto
            {
                Id = customer.Id,
                Name = customer.Name,
                PhoneNumber = customer.PhoneNumber,
                BranchId = customer.BranchId ?? Guid.Empty,
                BranchName = customer.Branch?.Name ?? "",
                CreditCount = count,
                TotalCreditAmount = total,
                TotalPaidAmount = paid,
                RemainingAmount = total - paid
            };
        }
    }
}
