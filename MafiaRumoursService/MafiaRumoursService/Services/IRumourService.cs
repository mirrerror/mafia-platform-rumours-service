using MafiaRumoursService.Models;

namespace MafiaRumoursService.Services;

public interface IRumourService
{
    Task<Rumour> CreateRumourAsync(string lobbyId, long ownerId, long targetId, string type);
    Task<IEnumerable<Rumour>> GetRumoursByOwnerAsync(string lobbyId, long ownerId);
}