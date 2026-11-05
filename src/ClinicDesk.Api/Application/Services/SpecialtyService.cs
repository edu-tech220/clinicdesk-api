using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Application.Exceptions;
using ClinicDesk.Api.Domain.Entities;
using ClinicDesk.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicDesk.Api.Application.Services;

public class SpecialtyService
{
    private readonly ClinicDeskDbContext _db;

    public SpecialtyService(ClinicDeskDbContext db)
    {
        _db = db;
    }

    public async Task<List<SpecialtyDto>> ListAsync(CancellationToken ct)
    {
        return await _db.Specialties
            .OrderBy(s => s.Name)
            .Select(s => new SpecialtyDto(s.Id, s.Name))
            .ToListAsync(ct);
    }

    public async Task<SpecialtyDto> CreateAsync(CreateSpecialtyRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (await _db.Specialties.AnyAsync(s => s.Name == name, ct))
        {
            throw new ConflictException("Ya existe una especialidad con ese nombre.");
        }

        var specialty = new Specialty { Id = Guid.NewGuid(), Name = name };
        _db.Specialties.Add(specialty);
        await _db.SaveChangesAsync(ct);
        return new SpecialtyDto(specialty.Id, specialty.Name);
    }
}
