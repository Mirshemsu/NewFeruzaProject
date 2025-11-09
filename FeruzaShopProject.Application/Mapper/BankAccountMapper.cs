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
    public class BankAccountMapper : Profile
    {
        public BankAccountMapper() 
        {
            CreateMap<CreateBankAccountDto, BankAccount>();
            CreateMap<UpdateBankAccountDto, BankAccount>()
                .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));
            CreateMap<BankAccount, BankAccountResponseDto>()
                .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src => src.BranchId.HasValue ? src.Branch.Name : null));

        }
    }
}
