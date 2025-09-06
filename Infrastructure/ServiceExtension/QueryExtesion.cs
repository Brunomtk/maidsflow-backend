using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.ServiceExtension
{
    public static class QueryExtension
    {
        /// <summary>
        /// Paginação genérica para qualquer entidade
        /// </summary>
        public static async Task<PagedResult<T>> GetPagedAsync<T>(
            this IQueryable<T> query,
            int page,
            int pageSize) where T : class
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;

            var totalItems = await query.CountAsync();

            var results = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResult<T>
            {
                CurrentPage = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                PageCount = (int)Math.Ceiling(totalItems / (double)pageSize),
                Results = results
            };
        }
    }

    public abstract class PagedResultBase
    {
        public int CurrentPage { get; set; }
        public int PageCount { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }

        public int FirstRowOnPage => (CurrentPage - 1) * PageSize + 1;
        public int LastRowOnPage => Math.Min(CurrentPage * PageSize, TotalItems);
    }

    public class PagedResult<T> : PagedResultBase where T : class
    {
        public List<T> Results { get; set; } = new();
    }
}
