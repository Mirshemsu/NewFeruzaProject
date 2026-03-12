using AutoMapper;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;

namespace FeruzaShopProject.Application.Mapper
{
    public class DailyClosingMapper : Profile
    {
        public DailyClosingMapper()
        {
            CreateMap<DailyClosing, DailyClosingDto>()
            .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src => src.Branch != null ? src.Branch.Name : null))
            .ForMember(dest => dest.ClosedBy, opt => opt.MapFrom(src => src.Closer != null ? src.Closer.Name : null))
            .ForMember(dest => dest.ApprovedBy, opt => opt.MapFrom(src => src.Approver != null ? src.Approver.Name : null))
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

            CreateMap<CloseDailySalesDto, DailyClosing>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.ClosedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ClosedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.TotalTransactions, opt => opt.Ignore())
                .ForMember(dest => dest.TotalSalesAmount, opt => opt.Ignore())
                .ForMember(dest => dest.TotalCashAmount, opt => opt.Ignore())
                .ForMember(dest => dest.TotalBankAmount, opt => opt.Ignore())
                .ForMember(dest => dest.TotalCreditAmount, opt => opt.Ignore())
                .ForMember(dest => dest.CashBankTransactionId, opt => opt.Ignore())
                .ForMember(dest => dest.BankTransferTransactionId, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Closer, opt => opt.Ignore())
                .ForMember(dest => dest.Approver, opt => opt.Ignore());

            CreateMap<ApproveDailyClosingDto, DailyClosing>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.MapFrom(_ => DateTime.UtcNow))
                .ForAllMembers(opts => opts.Condition((src, dest, srcMember) => srcMember != null));
        }
    }
}