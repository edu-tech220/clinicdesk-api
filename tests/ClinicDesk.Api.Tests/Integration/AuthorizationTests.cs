using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClinicDesk.Api.Application.Dtos;
using ClinicDesk.Api.Domain.Entities;
using ClinicDesk.Api.Domain.Enums;
using ClinicDesk.Api.Infrastructure;
using ClinicDesk.Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace ClinicDesk.Api.Tests.Integration;

public class AuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthorizationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> SeedAdminAndLoginAsync(HttpClient client)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ClinicDeskDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<Application.Abstractions.IPasswordHasher>();

        var email = $"admin-{Guid.NewGuid()}@clinicdesk.test";
        const string password = "AdminSecreta1!";
        var (hash, salt) = hasher.Hash(password);
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = UserRole.Admin,
            FullName = "Admin de Prueba",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        loginResponse.EnsureSuccessStatusCode();
        var auth = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.Token;
    }

    private async Task<(string Token, Guid PatientUserId)> RegisterPatientAndLoginAsync(HttpClient client)
    {
        var email = $"patient-{Guid.NewGuid()}@clinicdesk.test";
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterPatientRequest(
            email, "Sup3rSecreta!", "Paciente Autorizacion", "5555555555", new DateOnly(1990, 1, 1)));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return (auth!.Token, auth.UserId);
    }

    [Fact]
    public async Task Patient_CannotCreateSpecialty()
    {
        var client = _factory.CreateClient();
        var (token, _) = await RegisterPatientAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest("Pediatria"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanCreateSpecialtyAndDoctor()
    {
        var client = _factory.CreateClient();
        var adminToken = await SeedAdminAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var specialtyResponse = await client.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest($"Especialidad-{Guid.NewGuid()}"));
        specialtyResponse.EnsureSuccessStatusCode();
        var specialty = await specialtyResponse.Content.ReadFromJsonAsync<SpecialtyDto>();

        var doctorResponse = await client.PostAsJsonAsync("/api/auth/doctors", new CreateDoctorRequest(
            $"doc-{Guid.NewGuid()}@clinicdesk.test", "Sup3rSecreta!", "Doctor Nuevo", specialty!.Id, "LIC-999"));

        Assert.Equal(HttpStatusCode.OK, doctorResponse.StatusCode);
        var created = await doctorResponse.Content.ReadFromJsonAsync<DoctorAccountCreatedResponse>();
        Assert.NotEqual(Guid.Empty, created!.DoctorId);
    }

    [Fact]
    public async Task Patient_CannotSetDoctorSchedule()
    {
        var client = _factory.CreateClient();
        var adminToken = await SeedAdminAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var specialtyResponse = await client.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest($"Especialidad-{Guid.NewGuid()}"));
        var specialty = await specialtyResponse.Content.ReadFromJsonAsync<SpecialtyDto>();
        var doctorResponse = await client.PostAsJsonAsync("/api/auth/doctors", new CreateDoctorRequest(
            $"doc-{Guid.NewGuid()}@clinicdesk.test", "Sup3rSecreta!", "Doctor Agenda", specialty!.Id, "LIC-111"));
        var doctor = await doctorResponse.Content.ReadFromJsonAsync<DoctorAccountCreatedResponse>();

        var patientClient = _factory.CreateClient();
        var (patientToken, _) = await RegisterPatientAndLoginAsync(patientClient);
        patientClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", patientToken);

        var scheduleRequest = new SetScheduleRequest(DateOnly.FromDateTime(DateTime.UtcNow), new List<ScheduleRuleRequest>
        {
            new((int)DayOfWeek.Monday, TimeSpan.FromHours(9), TimeSpan.FromHours(13), 30)
        });

        var response = await patientClient.PutAsJsonAsync($"/api/doctors/{doctor!.DoctorId}/schedule", scheduleRequest);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
