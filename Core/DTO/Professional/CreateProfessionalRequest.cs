namespace Core.DTO.Professional
{
    public class CreateProfessionalRequest
    {
        public required string Name { get; set; }
        public required string Cpf { get; set; }
        public required string Email { get; set; }
        public required string Phone { get; set; }

        public int? TeamId { get; set; }
        public required int CompanyId { get; set; }
    }
}