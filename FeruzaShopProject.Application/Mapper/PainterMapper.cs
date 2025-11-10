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
    public class PainterMapper : Profile
    {
        public PainterMapper()
        {
            // CreateTransactionDto -> Painter (for auto-creation)
            CreateMap<CreateTransactionDto, Painter>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.PainterName))
                .ForMember(dest => dest.PhoneNumber, opt => opt.MapFrom(src => src.PainterPhoneNumber))
                .ForMember(dest => dest.Transactions, opt => opt.Ignore());
        }
    }
}
