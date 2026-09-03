using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Phisio.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddClinicIdToCareEntities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_user_exercises_patient_doctor_exercise_scheduled_active",
            table: "user_exercises");

        migrationBuilder.DropIndex(
            name: "IX_exercise_programs_DoctorId_PatientId",
            table: "exercise_programs");

        migrationBuilder.DropIndex(
            name: "ix_daily_patient_feedbacks_patient_doctor_feedback_date",
            table: "daily_patient_feedbacks");

        migrationBuilder.AddColumn<Guid>(
            name: "ClinicId",
            table: "user_exercises",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ClinicId",
            table: "exercise_programs",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ClinicId",
            table: "daily_patient_feedbacks",
            type: "uuid",
            nullable: true);

        // Backfill ClinicId with deterministic fallbacks for historical rows.
        // PostgreSQL has no min(uuid); use array_agg / DISTINCT ON instead.
        // Priority:
        //   1) unique approved+enabled DoctorPatient clinic
        //   2) preferred DoctorPatient clinic (approved, then enabled, then oldest)
        //   3) doctor's sole clinic membership
        //   4) doctor's earliest clinic membership
        migrationBuilder.Sql(
            """
            -- 1) Unique approved+enabled clinic for the doctor/patient pair.
            UPDATE user_exercises ue
            SET "ClinicId" = mapping."ClinicId"
            FROM (
                SELECT dp."DoctorId",
                       dp."PatientId",
                       (array_agg(dp."ClinicId" ORDER BY dp."ClinicId"))[1] AS "ClinicId"
                FROM doctor_patients dp
                WHERE dp."Status" = 2 AND dp."IsEnabled" = true
                GROUP BY dp."DoctorId", dp."PatientId"
                HAVING COUNT(DISTINCT dp."ClinicId") = 1
            ) AS mapping
            WHERE ue."ClinicId" IS NULL
              AND ue."DoctorId" = mapping."DoctorId"
              AND ue."PatientId" = mapping."PatientId";

            UPDATE exercise_programs ep
            SET "ClinicId" = mapping."ClinicId"
            FROM (
                SELECT dp."DoctorId",
                       dp."PatientId",
                       (array_agg(dp."ClinicId" ORDER BY dp."ClinicId"))[1] AS "ClinicId"
                FROM doctor_patients dp
                WHERE dp."Status" = 2 AND dp."IsEnabled" = true
                GROUP BY dp."DoctorId", dp."PatientId"
                HAVING COUNT(DISTINCT dp."ClinicId") = 1
            ) AS mapping
            WHERE ep."ClinicId" IS NULL
              AND ep."DoctorId" = mapping."DoctorId"
              AND ep."PatientId" = mapping."PatientId";

            UPDATE daily_patient_feedbacks f
            SET "ClinicId" = mapping."ClinicId"
            FROM (
                SELECT dp."DoctorId",
                       dp."PatientId",
                       (array_agg(dp."ClinicId" ORDER BY dp."ClinicId"))[1] AS "ClinicId"
                FROM doctor_patients dp
                WHERE dp."Status" = 2 AND dp."IsEnabled" = true
                GROUP BY dp."DoctorId", dp."PatientId"
                HAVING COUNT(DISTINCT dp."ClinicId") = 1
            ) AS mapping
            WHERE f."ClinicId" IS NULL
              AND f."DoctorId" = mapping."DoctorId"
              AND f."PatientId" = mapping."PatientId";

            -- 2) Any DoctorPatient (incl. soft-deleted / non-approved): prefer approved, enabled, oldest.
            UPDATE user_exercises ue
            SET "ClinicId" = mapping."ClinicId"
            FROM (
                SELECT DISTINCT ON (dp."DoctorId", dp."PatientId")
                       dp."DoctorId",
                       dp."PatientId",
                       dp."ClinicId"
                FROM doctor_patients dp
                ORDER BY dp."DoctorId",
                         dp."PatientId",
                         CASE WHEN dp."Status" = 2 THEN 0 ELSE 1 END,
                         CASE WHEN dp."IsEnabled" THEN 0 ELSE 1 END,
                         dp."CreatedAt",
                         dp."ClinicId"
            ) AS mapping
            WHERE ue."ClinicId" IS NULL
              AND ue."DoctorId" = mapping."DoctorId"
              AND ue."PatientId" = mapping."PatientId";

            UPDATE exercise_programs ep
            SET "ClinicId" = mapping."ClinicId"
            FROM (
                SELECT DISTINCT ON (dp."DoctorId", dp."PatientId")
                       dp."DoctorId",
                       dp."PatientId",
                       dp."ClinicId"
                FROM doctor_patients dp
                ORDER BY dp."DoctorId",
                         dp."PatientId",
                         CASE WHEN dp."Status" = 2 THEN 0 ELSE 1 END,
                         CASE WHEN dp."IsEnabled" THEN 0 ELSE 1 END,
                         dp."CreatedAt",
                         dp."ClinicId"
            ) AS mapping
            WHERE ep."ClinicId" IS NULL
              AND ep."DoctorId" = mapping."DoctorId"
              AND ep."PatientId" = mapping."PatientId";

            UPDATE daily_patient_feedbacks f
            SET "ClinicId" = mapping."ClinicId"
            FROM (
                SELECT DISTINCT ON (dp."DoctorId", dp."PatientId")
                       dp."DoctorId",
                       dp."PatientId",
                       dp."ClinicId"
                FROM doctor_patients dp
                ORDER BY dp."DoctorId",
                         dp."PatientId",
                         CASE WHEN dp."Status" = 2 THEN 0 ELSE 1 END,
                         CASE WHEN dp."IsEnabled" THEN 0 ELSE 1 END,
                         dp."CreatedAt",
                         dp."ClinicId"
            ) AS mapping
            WHERE f."ClinicId" IS NULL
              AND f."DoctorId" = mapping."DoctorId"
              AND f."PatientId" = mapping."PatientId";

            -- 3) Doctor has exactly one clinic membership.
            UPDATE user_exercises ue
            SET "ClinicId" = mapping."ClinicId"
            FROM (
                SELECT cd."DoctorId",
                       (array_agg(cd."ClinicId" ORDER BY cd."ClinicId"))[1] AS "ClinicId"
                FROM clinic_doctors cd
                GROUP BY cd."DoctorId"
                HAVING COUNT(*) = 1
            ) AS mapping
            WHERE ue."ClinicId" IS NULL
              AND ue."DoctorId" = mapping."DoctorId";

            UPDATE exercise_programs ep
            SET "ClinicId" = mapping."ClinicId"
            FROM (
                SELECT cd."DoctorId",
                       (array_agg(cd."ClinicId" ORDER BY cd."ClinicId"))[1] AS "ClinicId"
                FROM clinic_doctors cd
                GROUP BY cd."DoctorId"
                HAVING COUNT(*) = 1
            ) AS mapping
            WHERE ep."ClinicId" IS NULL
              AND ep."DoctorId" = mapping."DoctorId";

            UPDATE daily_patient_feedbacks f
            SET "ClinicId" = mapping."ClinicId"
            FROM (
                SELECT cd."DoctorId",
                       (array_agg(cd."ClinicId" ORDER BY cd."ClinicId"))[1] AS "ClinicId"
                FROM clinic_doctors cd
                GROUP BY cd."DoctorId"
                HAVING COUNT(*) = 1
            ) AS mapping
            WHERE f."ClinicId" IS NULL
              AND f."DoctorId" = mapping."DoctorId";

            -- 4) Doctor has multiple clinics: pick earliest ClinicId as last resort.
            UPDATE user_exercises ue
            SET "ClinicId" = mapping."ClinicId"
            FROM (
                SELECT cd."DoctorId",
                       (array_agg(cd."ClinicId" ORDER BY cd."ClinicId"))[1] AS "ClinicId"
                FROM clinic_doctors cd
                GROUP BY cd."DoctorId"
            ) AS mapping
            WHERE ue."ClinicId" IS NULL
              AND ue."DoctorId" = mapping."DoctorId";

            UPDATE exercise_programs ep
            SET "ClinicId" = mapping."ClinicId"
            FROM (
                SELECT cd."DoctorId",
                       (array_agg(cd."ClinicId" ORDER BY cd."ClinicId"))[1] AS "ClinicId"
                FROM clinic_doctors cd
                GROUP BY cd."DoctorId"
            ) AS mapping
            WHERE ep."ClinicId" IS NULL
              AND ep."DoctorId" = mapping."DoctorId";

            UPDATE daily_patient_feedbacks f
            SET "ClinicId" = mapping."ClinicId"
            FROM (
                SELECT cd."DoctorId",
                       (array_agg(cd."ClinicId" ORDER BY cd."ClinicId"))[1] AS "ClinicId"
                FROM clinic_doctors cd
                GROUP BY cd."DoctorId"
            ) AS mapping
            WHERE f."ClinicId" IS NULL
              AND f."DoctorId" = mapping."DoctorId";
            """);

        migrationBuilder.Sql(
            """
            DO $$
            DECLARE
                ambiguous_assignments integer;
                ambiguous_programs integer;
                ambiguous_feedbacks integer;
            BEGIN
                SELECT COUNT(*) INTO ambiguous_assignments
                FROM user_exercises
                WHERE "ClinicId" IS NULL;

                SELECT COUNT(*) INTO ambiguous_programs
                FROM exercise_programs
                WHERE "ClinicId" IS NULL;

                SELECT COUNT(*) INTO ambiguous_feedbacks
                FROM daily_patient_feedbacks
                WHERE "ClinicId" IS NULL;

                IF ambiguous_assignments > 0 OR ambiguous_programs > 0 OR ambiguous_feedbacks > 0 THEN
                    RAISE EXCEPTION
                        'Cannot migrate care entities: % user_exercises, % exercise_programs, and % daily_patient_feedbacks have ambiguous or missing clinic mapping. Resolve manually before applying this migration.',
                        ambiguous_assignments,
                        ambiguous_programs,
                        ambiguous_feedbacks;
                END IF;
            END $$;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "ClinicId",
            table: "user_exercises",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "ClinicId",
            table: "exercise_programs",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "ClinicId",
            table: "daily_patient_feedbacks",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_user_exercises_ClinicId",
            table: "user_exercises",
            column: "ClinicId");

        migrationBuilder.CreateIndex(
            name: "ix_user_exercises_patient_doctor_clinic_exercise_scheduled_active",
            table: "user_exercises",
            columns: new[] { "PatientId", "DoctorId", "ClinicId", "ExerciseId", "ScheduledDate" },
            unique: true,
            filter: "\"IsActive\" = true AND \"IsEnabled\" = true");

        migrationBuilder.CreateIndex(
            name: "IX_exercise_programs_ClinicId",
            table: "exercise_programs",
            column: "ClinicId");

        migrationBuilder.CreateIndex(
            name: "IX_exercise_programs_DoctorId_PatientId_ClinicId",
            table: "exercise_programs",
            columns: new[] { "DoctorId", "PatientId", "ClinicId" });

        migrationBuilder.CreateIndex(
            name: "IX_daily_patient_feedbacks_ClinicId",
            table: "daily_patient_feedbacks",
            column: "ClinicId");

        migrationBuilder.CreateIndex(
            name: "ix_daily_patient_feedbacks_patient_doctor_clinic_feedback_date",
            table: "daily_patient_feedbacks",
            columns: new[] { "PatientId", "DoctorId", "ClinicId", "FeedbackDate" },
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_daily_patient_feedbacks_clinics_ClinicId",
            table: "daily_patient_feedbacks",
            column: "ClinicId",
            principalTable: "clinics",
            principalColumn: "ClinicId",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_exercise_programs_clinics_ClinicId",
            table: "exercise_programs",
            column: "ClinicId",
            principalTable: "clinics",
            principalColumn: "ClinicId",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_user_exercises_clinics_ClinicId",
            table: "user_exercises",
            column: "ClinicId",
            principalTable: "clinics",
            principalColumn: "ClinicId",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_daily_patient_feedbacks_clinics_ClinicId",
            table: "daily_patient_feedbacks");

        migrationBuilder.DropForeignKey(
            name: "FK_exercise_programs_clinics_ClinicId",
            table: "exercise_programs");

        migrationBuilder.DropForeignKey(
            name: "FK_user_exercises_clinics_ClinicId",
            table: "user_exercises");

        migrationBuilder.DropIndex(
            name: "IX_user_exercises_ClinicId",
            table: "user_exercises");

        migrationBuilder.DropIndex(
            name: "ix_user_exercises_patient_doctor_clinic_exercise_scheduled_active",
            table: "user_exercises");

        migrationBuilder.DropIndex(
            name: "IX_exercise_programs_ClinicId",
            table: "exercise_programs");

        migrationBuilder.DropIndex(
            name: "IX_exercise_programs_DoctorId_PatientId_ClinicId",
            table: "exercise_programs");

        migrationBuilder.DropIndex(
            name: "IX_daily_patient_feedbacks_ClinicId",
            table: "daily_patient_feedbacks");

        migrationBuilder.DropIndex(
            name: "ix_daily_patient_feedbacks_patient_doctor_clinic_feedback_date",
            table: "daily_patient_feedbacks");

        migrationBuilder.DropColumn(
            name: "ClinicId",
            table: "user_exercises");

        migrationBuilder.DropColumn(
            name: "ClinicId",
            table: "exercise_programs");

        migrationBuilder.DropColumn(
            name: "ClinicId",
            table: "daily_patient_feedbacks");

        migrationBuilder.CreateIndex(
            name: "ix_user_exercises_patient_doctor_exercise_scheduled_active",
            table: "user_exercises",
            columns: new[] { "PatientId", "DoctorId", "ExerciseId", "ScheduledDate" },
            unique: true,
            filter: "\"IsActive\" = true AND \"IsEnabled\" = true");

        migrationBuilder.CreateIndex(
            name: "IX_exercise_programs_DoctorId_PatientId",
            table: "exercise_programs",
            columns: new[] { "DoctorId", "PatientId" });

        migrationBuilder.CreateIndex(
            name: "ix_daily_patient_feedbacks_patient_doctor_feedback_date",
            table: "daily_patient_feedbacks",
            columns: new[] { "PatientId", "DoctorId", "FeedbackDate" },
            unique: true);
    }
}
