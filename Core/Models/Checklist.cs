using System.Collections.Generic;using Core.Enums;
namespace Core.Models{ public class Checklist: BaseModel{
        public int? AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }

        public int? ProfessionalId { get; set; }
        public Professional? Professional { get; set; }

 public int CustomerId{get;set;} public Customer Customer{get;set;}=null!;
 public ChecklistStatus Status{get;set;}=ChecklistStatus.EmAndamento;
 public string? ObservacoesGerais{get;set;}
 public ICollection<ChecklistItem> Items{get;set;}=new List<ChecklistItem>();
}}