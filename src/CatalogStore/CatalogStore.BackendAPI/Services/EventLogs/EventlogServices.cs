using CatalogStore.BackendAPI.DTO.EventLogs;
using CatalogStore.BackendAPI.Models.EventLogs;
using CatalogStore.BackendAPI.Repository.EventLogs;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace CatalogStore.BackendAPI.Services.EventLogs
{
    public class EventlogServices : IEventlogServices
    {
        private readonly IEventlogRepository _eventlogRepository;
        private readonly IHttpContextAccessor _contextAccessor;

        public EventlogServices(IEventlogRepository eventlogRepository, IHttpContextAccessor contextAccessor)
        {
            _eventlogRepository = eventlogRepository;
            _contextAccessor = contextAccessor;
        }

        public async Task<List<Eventlog>> GetAllEventsAsync() => await _eventlogRepository.GetAllEventsAsync();

        public async Task AddAsync(AddEventlogDTO eventlog)
        {
            Eventlog log = new Eventlog
            {
                ModuleName = eventlog.ModuleName,
                TableName = eventlog.TableName,
                TypeLog = eventlog.TypeLog,
                RecordID = eventlog.RecordID,
                EventDesc = eventlog.EventDesc,
                StackTrace = eventlog.StackTrace,
                PreData = eventlog.PreData,
                PostData = eventlog.PostData,
                CreatedBy = eventlog.CreatedBy,
                CreatedOn = eventlog.CreatedOn
            };
            await _eventlogRepository.AddAsync(log);
        }

        public async Task LogAsync(
            typeEvent type,
            string moduleName,
            string tableName,
            string recordId,
            string description,
            object? preData = null,
            object? postData = null,
            string? stackTrace = null,
            string? actorOverride = null)
        {
            var createdBy = actorOverride
                ?? _contextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.UniqueName)?.Value
                ?? "Sistema";

            AddEventlogDTO dto = new AddEventlogDTO
            {
                ModuleName = moduleName,
                TableName = tableName,
                RecordID = recordId,
                TypeLog = type,
                EventDesc = description,
                PreData = preData is null ? string.Empty : JsonSerializer.Serialize(preData),
                PostData = postData is null ? string.Empty : JsonSerializer.Serialize(postData),
                StackTrace = stackTrace ?? string.Empty,
                CreatedBy = createdBy,
                CreatedOn = DateTime.UtcNow
            };

            await AddAsync(dto);
        }
    }
}
