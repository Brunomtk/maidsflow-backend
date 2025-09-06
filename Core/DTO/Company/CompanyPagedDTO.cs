using System.Collections.Generic;

namespace Core.DTO.Company
{
    public class CompanyPagedDTO
    {
        public int PageCount { get; set; }
        public IList<CompanyDTO> Result { get; set; } = new List<CompanyDTO>();
    }
}
