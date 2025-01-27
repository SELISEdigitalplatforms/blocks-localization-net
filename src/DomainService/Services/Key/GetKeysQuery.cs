using Blocks.Genesis;

namespace DomainService.Services
{
    public class GetKeysQuery : IProjectKey
    {
        public int PageSize { get; set; }
        public int PageNumber { get; set; }
        public string? KeySearchText { get; set; }
        public string[] ModuleIds { get; set; }
        public bool IsPartiallyTranslated { get; set; }
        public DateRange? CreateDateRange { get; set; }
        public string? SortProperty { get; set; }
        public bool IsDescending { get; set; }
        public string? ProjectKey { get; set; }
    }

    public class DateRange
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
