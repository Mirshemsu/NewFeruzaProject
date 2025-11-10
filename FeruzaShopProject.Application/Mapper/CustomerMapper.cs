using AutoMapper;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Mapper
{
    public class CustomerMapper : Profile
    {
        public CustomerMapper()
        {
            // CreateTransactionDto -> Customer (for auto-creation)
            CreateMap<CreateTransactionDto, Customer>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.CustomerName))
                .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.CustomerPhoneNumber))
                .ForMember(dest => dest.Transactions, opt => opt.Ignore());

            // Customer -> CreditCustomerSummaryDto
            CreateMap<Customer, CreditCustomerSummaryDto>()
                .ForMember(dest => dest.CreditCount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.TotalCreditAmount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.TotalPaidAmount, opt => opt.Ignore()) // Calculated in service
                .ForMember(dest => dest.LastCreditDate, opt => opt.Ignore()); // Calculated in service
        }
    }
}
