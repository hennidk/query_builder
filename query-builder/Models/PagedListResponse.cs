using System.Collections.Generic;

namespace query_builder
{
    public class PagedListResponse<T>
    {
        public int StartAt { get; set; }
        
        public int MaxResults { get; set; }

        public IEnumerable<T> Results { get; set; }
    }
}