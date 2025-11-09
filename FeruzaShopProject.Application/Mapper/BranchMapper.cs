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
    public class BranchMapper: Profile
    {
        public BranchMapper() 
        {
            CreateMap<CreateBranchDto, Branch>().ReverseMap();
            CreateMap<UpdateBranchDto, Branch>().ReverseMap();
            CreateMap<Branch, BranchDto>().ReverseMap();
        }
    }
}
