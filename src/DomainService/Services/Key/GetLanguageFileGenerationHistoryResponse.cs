using DomainService.Repositories;

namespace DomainService.Services
{
    public class GetLanguageFileGenerationHistoryResponse
    {
        public long TotalCount { get; set; }
        public List<LanguageFileGenerationHistory> Items { get; set; } = new();
    }
}
