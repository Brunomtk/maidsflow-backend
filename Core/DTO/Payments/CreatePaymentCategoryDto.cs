namespace Core.DTO.Payments
{
    public class CreatePaymentCategoryDto
    {
        public string Name { get; set; } = null!;
        public bool Active { get; set; } = true;
    }
}
