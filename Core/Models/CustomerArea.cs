using System.ComponentModel.DataAnnotations;
namespace Core.Models{
 public class CustomerArea: BaseModel{
  [Required] public int CustomerId{get;set;}
  public Customer Customer{get;set;}=null!;
  public int? CustomerAddressId{get;set;}
  public CustomerAddress? CustomerAddress{get;set;}
  [Required,MaxLength(120)] public string Name{get;set;}=string.Empty;
  public bool Active{get;set;}=true;
 }}