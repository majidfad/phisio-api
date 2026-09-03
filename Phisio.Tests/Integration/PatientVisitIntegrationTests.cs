using System;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Phisio.Api.Controllers;
using Phisio.Api.Controllers.Patient;
using Phisio.Application.Common;
using Phisio.Application.PatientVisits;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;
using Phisio.Infrastructure.Services;
using Phisio.Tests.TestDataBuilder;

namespace Phisio.Tests.Integration;

public sealed class PatientVisitIntegrationTests
{
    [Fact]
    public async Task RegisterVisit_AsDoctor_WhenPatientConnected_ReturnsOkAndPersists()
    {
        await using var host = await RelationshipTestHost.CreateAsync(services =>
        {
            services.AddScoped<IPatientVisitService, PatientVisitService>();
        });

        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);

        host.DbContext.DoctorPatients.Add(
            DoctorPatientBuilder.Create(
                doctorId: scenario.Doctor.Id,
                patientId: scenario.Patient.Id,
                clinicId: scenario.ClinicAId));
        await host.DbContext.SaveChangesAsync();

        var controller = new VisitsController(host.GetRequiredService<IPatientVisitService>())
        {
            ControllerContext = RelationshipTestHost.CreateControllerContext(scenario.Doctor.Id, RoleNames.Doctor),
        };

        var visitAt = DateTime.UtcNow;
        var request = new RegisterPatientVisitRequest(
            PatientId: scenario.Patient.Id,
            DoctorId: scenario.Doctor.Id,
            ClinicId: scenario.ClinicAId,
            VisitAt: visitAt,
            VisitType: null,
            PatientCondition: null,
            DoctorNotes: "Patient felt better after the session.");

        var result = await controller.RegisterVisit(request, cancellationToken: default);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<PatientVisitDto>().Which.VisitAt.Should().Be(visitAt);

        host.DbContext.PatientVisits.Count().Should().Be(1);
    }

    [Fact]
    public async Task RegisterVisit_AsDoctor_WhenPatientNotConnected_ReturnsBadRequest()
    {
        await using var host = await RelationshipTestHost.CreateAsync(services =>
        {
            services.AddScoped<IPatientVisitService, PatientVisitService>();
        });

        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);

        // A different patient without an active doctor-patient relationship.
        var otherPatient = ApplicationUserBuilder.Patient(name: "Other Patient", phoneNumber: "+15559990001");
        host.DbContext.Users.Add(otherPatient);
        await host.DbContext.SaveChangesAsync();

        var controller = new VisitsController(host.GetRequiredService<IPatientVisitService>())
        {
            ControllerContext = RelationshipTestHost.CreateControllerContext(scenario.Doctor.Id, RoleNames.Doctor),
        };

        var request = new RegisterPatientVisitRequest(
            PatientId: otherPatient.Id,
            DoctorId: scenario.Doctor.Id,
            ClinicId: scenario.ClinicAId,
            VisitAt: DateTime.UtcNow,
            VisitType: null,
            PatientCondition: null,
            DoctorNotes: null);

        var result = await controller.RegisterVisit(request, cancellationToken: default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RegisterVisit_AsClinicManager_ForAnotherDoctor_InSameClinic_SucceedsAndIsVisible()
    {
        await using var host = await RelationshipTestHost.CreateAsync(services =>
        {
            services.AddScoped<IPatientVisitService, PatientVisitService>();
        });

        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);

        // Make scenario.Doctor act as clinic manager for the clinic.
        var clinicManager = scenario.Doctor;

        // Approve care relationships for both doctors.
        host.DbContext.DoctorPatients.Add(
            DoctorPatientBuilder.Create(clinicManager.Id, scenario.Patient.Id, scenario.ClinicAId));

        var otherDoctor = await RelationshipTestHostSeeder.SeedExtraDoctorAsync(host, scenario.ClinicA);
        host.DbContext.DoctorPatients.Add(
            DoctorPatientBuilder.Create(otherDoctor.Id, scenario.Patient.Id, scenario.ClinicAId));

        await host.DbContext.SaveChangesAsync();

        var managerController = new VisitsController(host.GetRequiredService<IPatientVisitService>())
        {
            ControllerContext = RelationshipTestHost.CreateControllerContext(
                clinicManager.Id,
                RoleNames.ClinicManager),
        };

        // Register two visits: one by clinicManager doctor, one by otherDoctor.
        await managerController.RegisterVisit(
            new RegisterPatientVisitRequest(
                PatientId: scenario.Patient.Id,
                DoctorId: clinicManager.Id,
                ClinicId: scenario.ClinicAId,
                VisitAt: DateTime.UtcNow.AddHours(-2),
                VisitType: null,
                PatientCondition: null,
                DoctorNotes: "Notes A"),
            default);

        await managerController.RegisterVisit(
            new RegisterPatientVisitRequest(
                PatientId: scenario.Patient.Id,
                DoctorId: otherDoctor.Id,
                ClinicId: scenario.ClinicAId,
                VisitAt: DateTime.UtcNow.AddHours(-1),
                VisitType: null,
                PatientCondition: null,
                DoctorNotes: "Notes B"),
            default);

        // Clinic manager can see the full patient history within the clinic.
        var listResult = await managerController.GetPatientVisits(
            scenario.Patient.Id,
            clinicId: scenario.ClinicAId,
            doctorId: null,
            page: 1,
            pageSize: 10,
            search: null,
            cancellationToken: default);

        var ok = listResult.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<PatientVisitHistoryResponse>().Which;
        payload.TotalVisits.Should().Be(2);
    }

    [Fact]
    public async Task Patient_CanViewOwnVisitHistory_AndRecentVisitIsCorrect()
    {
        await using var host = await RelationshipTestHost.CreateAsync(services =>
        {
            services.AddScoped<IPatientVisitService, PatientVisitService>();
        });

        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);

        host.DbContext.DoctorPatients.Add(
            DoctorPatientBuilder.Create(
                scenario.Doctor.Id,
                scenario.Patient.Id,
                scenario.ClinicAId));
        await host.DbContext.SaveChangesAsync();

        // Create two visits.
        var doctorController = new VisitsController(host.GetRequiredService<IPatientVisitService>())
        {
            ControllerContext = RelationshipTestHost.CreateControllerContext(scenario.Doctor.Id, RoleNames.Doctor),
        };

        var visitOlder = DateTime.UtcNow.AddDays(-2);
        var visitNewer = DateTime.UtcNow.AddDays(-1);

        await doctorController.RegisterVisit(
            new RegisterPatientVisitRequest(
                PatientId: scenario.Patient.Id,
                DoctorId: scenario.Doctor.Id,
                ClinicId: scenario.ClinicAId,
                VisitAt: visitOlder,
                VisitType: null,
                PatientCondition: null,
                DoctorNotes: "Older"),
            default);

        await doctorController.RegisterVisit(
            new RegisterPatientVisitRequest(
                PatientId: scenario.Patient.Id,
                DoctorId: scenario.Doctor.Id,
                ClinicId: scenario.ClinicAId,
                VisitAt: visitNewer,
                VisitType: null,
                PatientCondition: null,
                DoctorNotes: "Newer"),
            default);

        var patientController = new PatientVisitsController(host.GetRequiredService<IPatientVisitService>())
        {
            ControllerContext = RelationshipTestHost.CreateControllerContext(scenario.Patient.Id, RoleNames.Patient),
        };

        var recentResult = await patientController.GetMostRecentVisit(cancellationToken: default);
        var recentOk = recentResult.Should().BeOfType<OkObjectResult>().Subject;
        var recent = recentOk.Value.Should().BeOfType<PatientVisitDto>().Which;
        recent.VisitAt.Should().Be(visitNewer);

        var historyResult = await patientController.GetMyVisits(cancellationToken: default);
        var historyOk = historyResult.Should().BeOfType<OkObjectResult>().Subject;
        var payload = historyOk.Value.Should().BeOfType<PatientVisitHistoryResponse>().Which;
        payload.TotalVisits.Should().Be(2);
    }

    [Fact]
    public async Task SubmitVisitFeedback_AsPatient_SucceedsOnce_AndAppearsOnVisit()
    {
        await using var host = await RelationshipTestHost.CreateAsync(services =>
        {
            services.AddScoped<IPatientVisitService, PatientVisitService>();
        });

        var scenario = await RelationshipTestHostSeeder.SeedPatientDoctorClinicAsync(host);

        host.DbContext.DoctorPatients.Add(
            DoctorPatientBuilder.Create(
                scenario.Doctor.Id,
                scenario.Patient.Id,
                scenario.ClinicAId));
        await host.DbContext.SaveChangesAsync();

        var doctorController = new VisitsController(host.GetRequiredService<IPatientVisitService>())
        {
            ControllerContext = RelationshipTestHost.CreateControllerContext(scenario.Doctor.Id, RoleNames.Doctor),
        };

        var registerResult = await doctorController.RegisterVisit(
            new RegisterPatientVisitRequest(
                PatientId: scenario.Patient.Id,
                DoctorId: scenario.Doctor.Id,
                ClinicId: scenario.ClinicAId,
                VisitAt: DateTime.UtcNow,
                VisitType: null,
                PatientCondition: null,
                DoctorNotes: "Session notes"),
            default);

        var visit = registerResult.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<PatientVisitDto>().Which;

        var patientController = new PatientVisitsController(host.GetRequiredService<IPatientVisitService>())
        {
            ControllerContext = RelationshipTestHost.CreateControllerContext(scenario.Patient.Id, RoleNames.Patient),
        };

        var feedbackResult = await patientController.SubmitVisitFeedback(
            visit.VisitId,
            new SubmitVisitFeedbackRequest(4, 5, "Clear explanations"),
            default);

        var feedbackOk = feedbackResult.Should().BeOfType<OkObjectResult>().Subject;
        var feedback = feedbackOk.Value.Should().BeOfType<VisitFeedbackDto>().Which;
        feedback.SatisfactionScore.Should().Be(4);
        feedback.DoctorCommunicationScore.Should().Be(5);

        var duplicate = await patientController.SubmitVisitFeedback(
            visit.VisitId,
            new SubmitVisitFeedbackRequest(3, 3, null),
            default);
        duplicate.Should().BeOfType<BadRequestObjectResult>();

        var historyResult = await patientController.GetMyVisits(cancellationToken: default);
        var history = historyResult.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<PatientVisitHistoryResponse>().Which;
        history.Visits.Should().ContainSingle();
        history.Visits[0].Feedback.Should().NotBeNull();
        history.Visits[0].Feedback!.SatisfactionScore.Should().Be(4);
    }
}

