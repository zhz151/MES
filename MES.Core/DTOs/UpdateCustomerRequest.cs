using System.ComponentModel.DataAnnotations;
using MES.Core.Enums; 

namespace MES.Core.DTOs;

public class UpdateCustomerRequest
{
    public string? CustomerCode { get; set; }
    public string? Salesman { get; set; }

    public string? CustomerUnit { get; set; }

    public string? EndCustomer { get; set; }

    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }

    public string? Address { get; set; }

    public CustomerStatus? Status { get; set; } 

    public string? Remark { get; set; }
}