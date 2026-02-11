using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TowerWars.Auth.Data;
using TowerWars.Auth.Models;
using TowerWars.Shared.DTOs;

namespace TowerWars.Auth.Services;

public interface ICharacterService
{
    Task<CharacterListResponse> GetCharactersAsync(Guid userId);
    Task<CharacterDto?> CreateCharacterAsync(Guid userId, CreateCharacterRequest request);
    Task<bool> DeleteCharacterAsync(Guid userId, Guid characterId);
    Task<SelectCharacterResponse> SelectCharacterAsync(Guid userId, Guid characterId);
}

public sealed class CharacterService : ICharacterService
{
    private readonly AuthDbContext _db;
    private readonly IConnectionTokenService _connectionTokenService;
    private const int MaxCharactersPerUser = 5;

    private static readonly HashSet<string> ValidClasses = ["Warrior", "Mage", "Ranger", "Support"];

    public CharacterService(AuthDbContext db, IConnectionTokenService connectionTokenService)
    {
        _db = db;
        _connectionTokenService = connectionTokenService;
    }

    public async Task<CharacterListResponse> GetCharactersAsync(Guid userId)
    {
        var characters = await _db.Characters
            .Where(c => c.UserId == userId && c.DeletedAt == null)
            .OrderByDescending(c => c.Level)
            .ThenByDescending(c => c.CreatedAt)
            .Select(c => new CharacterDto(
                c.Id,
                c.Name,
                c.Class,
                c.Level,
                c.Experience,
                c.CreatedAt
            ))
            .ToArrayAsync();

        return new CharacterListResponse(characters);
    }

    public async Task<CharacterDto?> CreateCharacterAsync(Guid userId, CreateCharacterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 2 || request.Name.Length > 32)
            return null;

        if (!ValidClasses.Contains(request.Class))
            return null;

        var existingCount = await _db.Characters
            .CountAsync(c => c.UserId == userId && c.DeletedAt == null);

        if (existingCount >= MaxCharactersPerUser)
            return null;

        var nameExists = await _db.Characters
            .AnyAsync(c => c.UserId == userId && c.Name == request.Name && c.DeletedAt == null);

        if (nameExists)
            return null;

        var character = new Character
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            Class = request.Class,
            Level = 1,
            Experience = 0,
            CreatedAt = DateTime.UtcNow
        };

        _db.Characters.Add(character);
        await _db.SaveChangesAsync();

        return new CharacterDto(
            character.Id,
            character.Name,
            character.Class,
            character.Level,
            character.Experience,
            character.CreatedAt
        );
    }

    public async Task<bool> DeleteCharacterAsync(Guid userId, Guid characterId)
    {
        var character = await _db.Characters
            .FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId && c.DeletedAt == null);

        if (character == null)
            return false;

        character.DeletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return true;
    }

    public async Task<SelectCharacterResponse> SelectCharacterAsync(Guid userId, Guid characterId)
    {
        var character = await _db.Characters
            .FirstOrDefaultAsync(c => c.Id == characterId && c.UserId == userId && c.DeletedAt == null);

        if (character == null)
            return new SelectCharacterResponse(false, null, "Character not found");

        var token = await _connectionTokenService.GenerateTokenAsync(userId, characterId);

        return new SelectCharacterResponse(true, token, null);
    }
}
