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
    public class PurchaseMapper : Profile
    {
        public PurchaseMapper() 
        {
            CreateMap<PurchaseOrder, PurchaseOrderDto>()
               .ForMember(dest => dest.BranchName, opt => opt.MapFrom(src => src.Branch.Name))
               .ForMember(dest => dest.SupplierName, opt => opt.MapFrom(src => src.Supplier.Name))
               .ForMember(dest => dest.CreatorName, opt => opt.MapFrom(src => src.Creator.Name))
               .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));

            CreateMap<PurchaseOrderItem, PurchaseOrderItemDto>()
                .ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.ProductName, opt => opt.MapFrom(src => src.Product.Name));

            CreateMap<CreatePurchaseOrderDto, PurchaseOrder>();
            CreateMap<CreatePurchaseOrderItemDto, PurchaseOrderItem>();
            CreateMap<UpdatePurchaseOrderDto, PurchaseOrder>();
            CreateMap<ReceivePurchaseOrderDto, PurchaseOrder>();
        }
    }
}
