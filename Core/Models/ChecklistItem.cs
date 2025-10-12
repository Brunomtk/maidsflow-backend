using System.Collections.Generic;using Core.Enums;
namespace Core.Models{ public class ChecklistItem: BaseModel{
 public int ChecklistId{get;set;} public Checklist Checklist{get;set;}=null!;
 public int CustomerAreaId{get;set;} public CustomerArea CustomerArea{get;set;}=null!;
 public ChecklistItemStatus? Status{get;set;}
 public string? Observacoes{get;set;}
 public ICollection<ChecklistItemPhoto> Photos{get;set;}=new List<ChecklistItemPhoto>();
}}