using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Phisio.Api.Controllers.Admin;
using Phisio.Application.Admin.Exercises;
using Phisio.Application.Common;
using Phisio.Application.Exercises;
using Phisio.Domain.Enums;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

public sealed class ExerciseManagementIntegrationTests
{
    // 1. Admin creates an exercise successfully.
    [Fact]
    public async Task CreateExercise_WhenAdminProvidesValidData_ReturnsCreatedAndPersists()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var admin = await ExerciseManagementTestHostSeeder.SeedAdminAsync(host);
        var controller = host.CreateAdminExercisesController(admin.Id);
        var request = ExerciseTestDataBuilder.CreateDto(
            title: "Hamstring Stretch",
            description: "Daily hamstring mobility work.");

        var result = await ExerciseManagementTestHelpers.CreateExerciseWithValidationAsync(
            controller,
            request,
            CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        var body = created.Value.Should().BeOfType<ExerciseDto>().Subject;
        body.Title.Should().Be("Hamstring Stretch");
        body.Description.Should().Be("Daily hamstring mobility work.");
        body.CreatedByDoctorId.Should().BeNull();

        host.DbContext.ChangeTracker.Clear();
        var persisted = await host.DbContext.Exercises.SingleAsync();
        persisted.Title.Should().Be("Hamstring Stretch");
        persisted.Description.Should().Be("Daily hamstring mobility work.");
        persisted.CreatedByDoctorId.Should().BeNull();
        persisted.IsEnabled.Should().BeTrue();
    }

    // 2. Invalid exercise data is rejected.
    [Fact]
    public async Task CreateExercise_WhenDataIsInvalid_ReturnsBadRequest()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var admin = await ExerciseManagementTestHostSeeder.SeedAdminAsync(host);
        var controller = host.CreateAdminExercisesController(admin.Id);
        var request = ExerciseTestDataBuilder.CreateDto(title: string.Empty, description: string.Empty);

        var result = await ExerciseManagementTestHelpers.CreateExerciseWithValidationAsync(
            controller,
            request,
            CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
        (await host.DbContext.Exercises.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateExercise_WhenCategoryIdsAreInvalid_ReturnsBadRequest()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var admin = await ExerciseManagementTestHostSeeder.SeedAdminAsync(host);
        var controller = host.CreateAdminExercisesController(admin.Id);
        var request = ExerciseTestDataBuilder.CreateDto();
        request.CategoryIds = [Guid.NewGuid()];

        var result = await controller.CreateExercise(request, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await host.DbContext.Exercises.CountAsync()).Should().Be(0);
    }

    // 3. Admin updates an exercise.
    [Fact]
    public async Task UpdateExercise_WhenAdminProvidesValidData_ReturnsOkAndPersists()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateAdminExercisesController(scenario.Admin.Id);
        var update = ExerciseTestDataBuilder.UpdateRequest(
            title: "Updated Admin Stretch",
            description: "Updated admin catalog description.");

        var result = await ExerciseManagementTestHelpers.UpdateExerciseWithValidationAsync(
            controller,
            scenario.AdminExercise.ExerciseId,
            update,
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<ExerciseDto>().Which.Title.Should().Be("Updated Admin Stretch");

        host.DbContext.ChangeTracker.Clear();
        var persisted = await host.DbContext.Exercises
            .SingleAsync(exercise => exercise.ExerciseId == scenario.AdminExercise.ExerciseId);
        persisted.Title.Should().Be("Updated Admin Stretch");
        persisted.Description.Should().Be("Updated admin catalog description.");
    }

    // 4. Admin deletes an exercise.
    [Fact]
    public async Task DeleteExercise_WhenExerciseExists_ReturnsNoContentAndSoftDeletes()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var scenario = await ExerciseManagementTestHostSeeder.SeedFullScenarioAsync(host);
        var controller = host.CreateAdminExercisesController(scenario.Admin.Id);

        var result = await controller.DeleteExercise(
            scenario.AdminExercise.ExerciseId,
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status204NoContent);

        host.DbContext.ChangeTracker.Clear();
        var persisted = await host.DbContext.Exercises
            .IgnoreQueryFilters()
            .SingleAsync(exercise => exercise.ExerciseId == scenario.AdminExercise.ExerciseId);
        persisted.IsEnabled.Should().BeFalse();
    }

    // 5. Non-admin users cannot create/update/delete exercises.
    [Fact]
    public async Task Authorization_AdminOnlyPolicy_RejectsNonAdminUsersForExerciseManagement()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.AdminOnly, policy =>
                policy.RequireRole(RoleNames.Admin));
        });

        await using var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var patient = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Patient)],
            authenticationType: "Test"));
        var doctor = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Doctor)],
            authenticationType: "Test"));
        var admin = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, RoleNames.Admin)],
            authenticationType: "Test"));

        (await authorizationService.AuthorizeAsync(
            anonymous, resource: null, AuthorizationPolicies.AdminOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            patient, resource: null, AuthorizationPolicies.AdminOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            doctor, resource: null, AuthorizationPolicies.AdminOnly))
            .Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(
            admin, resource: null, AuthorizationPolicies.AdminOnly))
            .Succeeded.Should().BeTrue();

        typeof(AdminExercisesController)
            .GetCustomAttribute<AuthorizeAttribute>()!
            .Policy.Should().Be(AuthorizationPolicies.AdminOnly);
    }

    // 6. Non-existing exercise returns the correct error.
    [Fact]
    public async Task GetExercise_WhenExerciseDoesNotExist_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var admin = await ExerciseManagementTestHostSeeder.SeedAdminAsync(host);
        var controller = host.CreateAdminExercisesController(admin.Id);

        var result = await controller.GetExercise(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateExercise_WhenExerciseDoesNotExist_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var admin = await ExerciseManagementTestHostSeeder.SeedAdminAsync(host);
        var controller = host.CreateAdminExercisesController(admin.Id);

        var result = await controller.UpdateExercise(
            Guid.NewGuid(),
            ExerciseTestDataBuilder.UpdateRequest(),
            CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteExercise_WhenExerciseDoesNotExist_ReturnsNotFound()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var admin = await ExerciseManagementTestHostSeeder.SeedAdminAsync(host);
        var controller = host.CreateAdminExercisesController(admin.Id);

        var result = await controller.DeleteExercise(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    // 7. Exercise data is persisted correctly.
    [Fact]
    public async Task CreateExercise_PersistsAllCatalogFieldsCorrectly()
    {
        await using var host = await ExerciseManagementTestHost.CreateAsync();
        var admin = await ExerciseManagementTestHostSeeder.SeedAdminAsync(host);
        var controller = host.CreateAdminExercisesController(admin.Id);
        var request = new CreateExerciseDto
        {
            Title = "Posterior Chain",
            Description = "Glute and hamstring activation.",
            Instructions = "Hold each rep for 5 seconds.",
            VideoUrl = "https://example.com/posterior.mp4",
            MediaType = ExerciseMediaType.ExternalVideo,
            Equipment = ExerciseEquipment.Band,
            Difficulty = ExerciseDifficulty.Hard,
            CategoryIds = [],
        };

        await ExerciseManagementTestHelpers.CreateExerciseWithValidationAsync(
            controller,
            request,
            CancellationToken.None);

        host.DbContext.ChangeTracker.Clear();
        var persisted = await host.DbContext.Exercises.SingleAsync();
        persisted.Title.Should().Be("Posterior Chain");
        persisted.Description.Should().Be("Glute and hamstring activation.");
        persisted.Instructions.Should().Be("Hold each rep for 5 seconds.");
        persisted.VideoUrl.Should().Be("https://example.com/posterior.mp4");
        persisted.MediaType.Should().Be(ExerciseMediaType.ExternalVideo);
        persisted.Equipment.Should().Be(ExerciseEquipment.Band);
        persisted.Difficulty.Should().Be(ExerciseDifficulty.Hard);
        persisted.CreatedByDoctorId.Should().BeNull();
        persisted.IsEnabled.Should().BeTrue();
    }
}
