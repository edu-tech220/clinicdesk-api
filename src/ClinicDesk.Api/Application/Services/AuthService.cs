using ClinicDesk.Api.Application.Abstractions;
using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Application.Exceptions;
using ClinicDesk.Api.Domain.Entities;
using ClinicDesk.Api.Domain.Enums;
using ClinicDesk.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClinicDesk.Api.Application.Services;

public class AuthService
{
    private readonly ClinicDeskDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthService(ClinicDeskDbContext db, IPasswordHasher passwordHasher, IJwtTokenService jwtTokenService)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
    }

    public async Task<AuthResponse> RegisterPatientAsync(RegisterPatientRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException("Ya existe una cuenta con ese correo.");
        }

        var (hash, salt) = _passwordHasher.Hash(request.Password);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRole.Patient,
            FullName = request.FullName.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        var patient = new Patient
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FullName = request.FullName.Trim(),
            Phone = request.Phone.Trim(),
            DateOfBirth = request.DateOfBirth,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Users.Add(user);
        _db.Patients.Add(patient);
        await _db.SaveChangesAsync(ct);

        var token = _jwtTokenService.GenerateToken(user, patientId: patient.Id);
        return new AuthResponse(token, user.Role.ToString(), user.FullName, user.Id);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw new UnauthorizedDomainException("Correo o contrasena invalidos.");
        }

        Guid? patientId = null;
        Guid? doctorId = null;
        if (user.Role == UserRole.Patient)
        {
            patientId = await _db.Patients.Where(p => p.UserId == user.Id).Select(p => p.Id).FirstOrDefaultAsync(ct);
        }
        else if (user.Role == UserRole.Doctor)
        {
            doctorId = await _db.Doctors.Where(d => d.UserId == user.Id).Select(d => d.Id).FirstOrDefaultAsync(ct);
        }

        var token = _jwtTokenService.GenerateToken(user, patientId, doctorId);
        return new AuthResponse(token, user.Role.ToString(), user.FullName, user.Id);
    }

    public async Task<DoctorAccountCreatedResponse> CreateDoctorAsync(CreateDoctorRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException("Ya existe una cuenta con ese correo.");
        }

        var specialtyExists = await _db.Specialties.AnyAsync(s => s.Id == request.SpecialtyId, ct);
        if (!specialtyExists)
        {
            throw new NotFoundException("La especialidad indicada no existe.");
        }

        var (hash, salt) = _passwordHasher.Hash(request.Password);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRole.Doctor,
            FullName = request.FullName.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        var doctor = new Doctor
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            SpecialtyId = request.SpecialtyId,
            LicenseNumber = request.LicenseNumber.Trim(),
            Bio = request.Bio.Trim(),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Users.Add(user);
        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync(ct);

        return new DoctorAccountCreatedResponse(doctor.Id, user.Id, user.Email, user.FullName);
    }
}
