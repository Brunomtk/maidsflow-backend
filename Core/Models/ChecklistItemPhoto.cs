namespace Core.Models{
 public class ChecklistItemPhoto: BaseModel{
  public int ChecklistItemId{get;set;} public ChecklistItem ChecklistItem{get;set;}=null!;
  public string Url{get;set;}=string.Empty; public string? Descricao{get;set;}
 }}