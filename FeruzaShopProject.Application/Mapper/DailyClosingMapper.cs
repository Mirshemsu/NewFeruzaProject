using AutoMapper;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using System;

namespace FeruzaShopProject.Application.Mapper
{
    public class DailyClosingMapper : Profile
    {
        public DailyClosingMapper()
        {
            // Map from Entity to DTO
            CreateMap<DailyClosing, DailyClosingDto>()
                .ForMember(dest => dest.BranchName,
                    opt => opt.MapFrom(src => src.Branch != null ? src.Branch.Name : null))
                .ForMember(dest => dest.ClosedBy,
                    opt => opt.MapFrom(src => src.Closer != null ? src.Closer.Name : null))
                .ForMember(dest => dest.ApprovedBy,
                    opt => opt.MapFrom(src => src.Approver != null ? src.Approver.Name : null))
                .ForMember(dest => dest.Status,
                    opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.ClosingDate,
                    opt => opt.MapFrom(src => src.ClosingDate.Date));

            // Map from Create DTO to Entity (for closing)
            CreateMap<CloseDailySalesDto, DailyClosing>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Closer, opt => opt.Ignore())
                .ForMember(dest => dest.Approver, opt => opt.Ignore())
                .ForMember(dest => dest.ClosedBy, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.ClosedAt, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.TotalSalesAmount, opt => opt.Ignore()) // Calculated
                .ForMember(dest => dest.TotalCashAmount, opt => opt.Ignore()) // Calculated
                .ForMember(dest => dest.TotalBankAmount, opt => opt.Ignore()) // Calculated
                .ForMember(dest => dest.TotalCreditAmount, opt => opt.Ignore()) // Calculated
                .ForMember(dest => dest.TotalTransactions, opt => opt.Ignore()) // Calculated
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.MapFrom(_ => true));

            // Map from Approve DTO to Entity - WITHOUT ForAllOtherMembers
            CreateMap<ApproveDailyClosingDto, DailyClosing>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ClosingId))
                .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.Status, opt => opt.Ignore()) // Set in service
                .ForMember(dest => dest.Remarks, opt => opt.MapFrom(src => src.Remarks))
                // Explicitly ignore all other properties
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.ClosingDate, opt => opt.Ignore())
                .ForMember(dest => dest.ClosedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ClosedBy, opt => opt.Ignore())
                .ForMember(dest => dest.TotalSalesAmount, opt => opt.Ignore())
                .ForMember(dest => dest.TotalCashAmount, opt => opt.Ignore())
                .ForMember(dest => dest.TotalBankAmount, opt => opt.Ignore())
                .ForMember(dest => dest.TotalCreditAmount, opt => opt.Ignore())
                .ForMember(dest => dest.TotalTransactions, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Closer, opt => opt.Ignore())
                .ForMember(dest => dest.Approver, opt => opt.Ignore());

            // Map from Reopen DTO to Entity - WITHOUT ForAllOtherMembers
            CreateMap<ReopenDailySalesDto, DailyClosing>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.ClosingId))
                .ForMember(dest => dest.Remarks, opt => opt.MapFrom(src => src.Reason))
                // Explicitly ignore all other properties
                .ForMember(dest => dest.BranchId, opt => opt.Ignore())
                .ForMember(dest => dest.ClosingDate, opt => opt.Ignore())
                .ForMember(dest => dest.ClosedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ClosedBy, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedAt, opt => opt.Ignore())
                .ForMember(dest => dest.ApprovedBy, opt => opt.Ignore())
                .ForMember(dest => dest.Status, opt => opt.Ignore())
                .ForMember(dest => dest.TotalSalesAmount, opt => opt.Ignore())
                .ForMember(dest => dest.TotalCashAmount, opt => opt.Ignore())
                .ForMember(dest => dest.TotalBankAmount, opt => opt.Ignore())
                .ForMember(dest => dest.TotalCreditAmount, opt => opt.Ignore())
                .ForMember(dest => dest.TotalTransactions, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.IsActive, opt => opt.Ignore())
                .ForMember(dest => dest.Branch, opt => opt.Ignore())
                .ForMember(dest => dest.Closer, opt => opt.Ignore())
                .ForMember(dest => dest.Approver, opt => opt.Ignore());
        }
    }
}