using Microsoft.AspNetCore.Identity;
using Moq;
using Phisio.Application.Auth;
using Phisio.Application.Clinics;
using Phisio.Application.Common;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Authentication;
using Phisio.Infrastructure.Identity;
using Phisio.Infrastructure.Persistence;
using Phisio.Tests.MockFactory;

namespace Phisio.Tests.TestDataBuilder;

internal static class AuthServiceTestHelper
{
    public static Mock<IClinicService> CreateClinicServiceMock()
    {
        var clinicService = new Mock<IClinicService>();
        clinicService
            .Setup(service => service.LookupByPhonesAsync(
                It.IsAny<ClinicAccessContext>(),
                It.IsAny<LookupClinicsByPhonesDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(AuthResult<ClinicPhoneLookupResultDto>.Success(
                new ClinicPhoneLookupResultDto(ClinicPhoneLookupStatus.None, null, [])));

        clinicService
            .Setup(service => service.AssignDoctorAsync(
                It.IsAny<ClinicAccessContext>(),
                It.IsAny<AssignDoctorToClinicDto>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                ClinicAccessContext _,
                AssignDoctorToClinicDto request,
                CancellationToken _) =>
                Task.FromResult(AuthResult<AssignDoctorToClinicResultDto>.Success(
                    new AssignDoctorToClinicResultDto(
                        new ClinicDto(
                            Guid.NewGuid(),
                            request.Name ?? "Clinic",
                            request.Address ?? "Address",
                            request.DoctorId,
                            request.PhoneNumbers.ToList(),
                            DateTime.UtcNow),
                        new ClinicDoctorMemberDto(
                            request.DoctorId,
                            "Doctor",
                            "+15550000000",
                            UserRole.Doctor,
                            "Specialty",
                            "MD-1",
                            IsClinicManager: true),
                        ClinicCreated: true))));

        return clinicService;
    }

    public static AuthService Create(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IJwtTokenService jwtTokenService,
        Mock<IClinicService>? clinicService = null,
        AppDbContext? dbContext = null) =>
        new(
            dbContext ?? AppDbContextMockFactory.Create(),
            userManager,
            roleManager,
            jwtTokenService,
            (clinicService ?? CreateClinicServiceMock()).Object);
}

internal static class RegisterRequestBuilder
{
    public static RegisterRequest Valid() =>
        new()
        {
            Name = "علی رضایی",
            PhoneNumber = "09121234567",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

    public static RegisterRequest ValidDoctor() =>
        new()
        {
            Name = "دکتر مریم احمدی",
            PhoneNumber = "09121112233",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            Role = UserRole.Doctor,
            MedicalLicenseNumber = "123456",
            Specialty = "فیزیوتراپی",
            ClinicPhoneNumbers = ["02199999999"],
            NewClinicName = "New Clinic",
            NewClinicAddress = "New Clinic Address",
            ManagerIsThisDoctor = true,
        };
}

internal static class RegisterPatientRequestBuilder
{
    public static RegisterPatientRequest Valid() =>
        new()
        {
            Name = "Alice Patient",
            PhoneNumber = "+15559876543",
            Password = "SecurePass1!"
        };
}
