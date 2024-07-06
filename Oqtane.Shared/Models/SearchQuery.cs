using System;
using System.Collections.Generic;
using Oqtane.Shared;

namespace Oqtane.Models
{
    public class SearchQuery
    {
        public int SiteId { get; set; }

        public Alias Alias { get; set; }

        public User User { get; set; }

        public string Keywords { get; set; }

        public List<string> EntityNames { get; set; } = new List<string>();

        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public IDictionary<string, string> Properties { get; set; } = new Dictionary<string, string>();

        public int PageIndex { get; set; }

        public int PageSize { get; set; }

        public SearchSortFields SortField { get; set; }

        public SearchSortDirections SortDirection { get; set; }

        public int BodySnippetLength { get; set;} = 255;
    }
}
