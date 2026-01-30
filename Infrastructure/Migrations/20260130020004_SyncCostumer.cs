using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    public partial class SyncCostumer : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) CustomerAddresses: cria tabela se não existir + adiciona colunas Guesty se faltar
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables
        WHERE table_schema = 'public' AND table_name = 'CustomerAddresses'
    ) THEN
        CREATE TABLE "CustomerAddresses" (
            "Id" serial PRIMARY KEY,
            "CustomerId" integer NOT NULL,
            "Label" character varying(100) NULL,
            "AddressLine1" text NOT NULL,
            "AddressLine2" text NULL,
            "City" text NOT NULL,
            "State" character varying(2) NOT NULL,
            "ZipCode" text NULL,
            "Observations" text NULL,
            "Ticket" numeric(18,2) NULL,
            "Frequency" character varying(50) NULL,
            "PaymentMethod" character varying(50) NULL,
            "GuestyListingId" character varying(80) NULL,
            "GuestyListingTitle" character varying(200) NULL,
            "GuestySyncedAtUtc" timestamp with time zone NULL,
            "IsPrimary" boolean NOT NULL DEFAULT FALSE,
            "CreatedDate" timestamp with time zone NOT NULL DEFAULT now(),
            "UpdatedDate" timestamp with time zone NOT NULL DEFAULT now(),
            CONSTRAINT "FK_CustomerAddresses_Customers_CustomerId"
                FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS "IX_CustomerAddresses_CustomerId"
            ON "CustomerAddresses" ("CustomerId");
        CREATE INDEX IF NOT EXISTS "IX_CustomerAddresses_CustomerId_GuestyListingId"
            ON "CustomerAddresses" ("CustomerId", "GuestyListingId");
        CREATE INDEX IF NOT EXISTS "IX_CustomerAddresses_CustomerId_IsPrimary"
            ON "CustomerAddresses" ("CustomerId", "IsPrimary");
    ELSE
        -- tabela já existe: garante colunas Guesty
        ALTER TABLE "CustomerAddresses" ADD COLUMN IF NOT EXISTS "GuestyListingId" character varying(80) NULL;
        ALTER TABLE "CustomerAddresses" ADD COLUMN IF NOT EXISTS "GuestyListingTitle" character varying(200) NULL;
        ALTER TABLE "CustomerAddresses" ADD COLUMN IF NOT EXISTS "GuestySyncedAtUtc" timestamp with time zone NULL;

        CREATE INDEX IF NOT EXISTS "IX_CustomerAddresses_CustomerId_GuestyListingId"
            ON "CustomerAddresses" ("CustomerId", "GuestyListingId");
    END IF;
END $$;
""");

            // 2) Companies: campos Guesty
            migrationBuilder.Sql("""
ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "GuestyAccessToken" text NULL;
ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "GuestyApiType" text NULL;
ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "GuestyAuthBaseUrl" text NULL;
ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "GuestyAuthScope" text NULL;
ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "GuestyClientId" text NULL;
ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "GuestyClientSecret" text NULL;
ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "GuestyTokenExpiresAtUtc" timestamp with time zone NULL;
ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "GuestyTokenType" text NULL;
ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "GuestyTokenUpdatedAtUtc" timestamp with time zone NULL;
""");

            // 3) Appointments: address + external guesty mapping
            migrationBuilder.Sql("""
ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "CustomerAddressId" integer NULL;
ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "ExternalListingId" text NULL;
ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "ExternalReservationId" text NULL;
ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "ExternalSource" text NULL;
ALTER TABLE "Appointments" ADD COLUMN IF NOT EXISTS "ExternalStatus" text NULL;

CREATE INDEX IF NOT EXISTS "IX_Appointments_CustomerAddressId"
    ON "Appointments" ("CustomerAddressId");

-- Idempotência: evita duplicar o mesmo appointment vindo do Guesty
CREATE UNIQUE INDEX IF NOT EXISTS "UX_Appointments_ExternalSource_ExternalReservationId"
    ON "Appointments" ("ExternalSource", "ExternalReservationId")
    WHERE "ExternalSource" IS NOT NULL AND "ExternalReservationId" IS NOT NULL;
""");

            // 4) Payments / CustomerAreas: CustomerAddressId
            migrationBuilder.Sql("""
ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "CustomerAddressId" integer NULL;
CREATE INDEX IF NOT EXISTS "IX_Payments_CustomerAddressId" ON "Payments" ("CustomerAddressId");

ALTER TABLE "CustomerAreas" ADD COLUMN IF NOT EXISTS "CustomerAddressId" integer NULL;
CREATE INDEX IF NOT EXISTS "IX_CustomerAreas_CustomerAddressId" ON "CustomerAreas" ("CustomerAddressId");

-- índice único novo (mantém o antigo se existir; não vamos dropar aqui)
CREATE UNIQUE INDEX IF NOT EXISTS "IX_CustomerAreas_CustomerId_CustomerAddressId_Name_Active"
    ON "CustomerAreas" ("CustomerId", "CustomerAddressId", "Name", "Active");
""");

            // 5) Recurrence exceptions / completions snapshots
            migrationBuilder.Sql("""
ALTER TABLE "AppointmentRecurrenceExceptions" ADD COLUMN IF NOT EXISTS "OverrideCustomerAddressId" integer NULL;

ALTER TABLE "AppointmentCompletions" ADD COLUMN IF NOT EXISTS "CustomerAddressIdSnapshot" integer NULL;
ALTER TABLE "AppointmentCompletions" ADD COLUMN IF NOT EXISTS "CustomerAddressSnapshot" text NULL;
ALTER TABLE "AppointmentCompletions" ADD COLUMN IF NOT EXISTS "FrequencySnapshot" text NULL;
ALTER TABLE "AppointmentCompletions" ADD COLUMN IF NOT EXISTS "PaymentMethodSnapshot" text NULL;
""");

            // 6) FKs (criadas somente se não existirem)
            migrationBuilder.Sql("""
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Appointments_CustomerAddresses_CustomerAddressId') THEN
        ALTER TABLE "Appointments"
            ADD CONSTRAINT "FK_Appointments_CustomerAddresses_CustomerAddressId"
            FOREIGN KEY ("CustomerAddressId") REFERENCES "CustomerAddresses" ("Id")
            ON DELETE SET NULL;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Checklists' AND column_name = 'CustomerAddressId'
    ) THEN
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Checklists_CustomerAddresses_CustomerAddressId') THEN
            ALTER TABLE "Checklists"
                ADD CONSTRAINT "FK_Checklists_CustomerAddresses_CustomerAddressId"
                FOREIGN KEY ("CustomerAddressId") REFERENCES "CustomerAddresses" ("Id")
                ON DELETE SET NULL;
        END IF;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_CustomerAreas_CustomerAddresses_CustomerAddressId') THEN
        ALTER TABLE "CustomerAreas"
            ADD CONSTRAINT "FK_CustomerAreas_CustomerAddresses_CustomerAddressId"
            FOREIGN KEY ("CustomerAddressId") REFERENCES "CustomerAddresses" ("Id")
            ON DELETE SET NULL;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Payments_CustomerAddresses_CustomerAddressId') THEN
        ALTER TABLE "Payments"
            ADD CONSTRAINT "FK_Payments_CustomerAddresses_CustomerAddressId"
            FOREIGN KEY ("CustomerAddressId") REFERENCES "CustomerAddresses" ("Id")
            ON DELETE SET NULL;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Reviews' AND column_name = 'CustomerAddressId'
    ) THEN
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Reviews_CustomerAddresses_CustomerAddressId') THEN
            ALTER TABLE "Reviews"
                ADD CONSTRAINT "FK_Reviews_CustomerAddresses_CustomerAddressId"
                FOREIGN KEY ("CustomerAddressId") REFERENCES "CustomerAddresses" ("Id")
                ON DELETE SET NULL;
        END IF;
    END IF;
END $$;
""");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort rollback (sem destruir tabela inteira, para evitar perda de dados)
            migrationBuilder.Sql("""
DROP INDEX IF EXISTS "UX_Appointments_ExternalSource_ExternalReservationId";

ALTER TABLE "Appointments" DROP COLUMN IF EXISTS "ExternalStatus";
ALTER TABLE "Appointments" DROP COLUMN IF EXISTS "ExternalSource";
ALTER TABLE "Appointments" DROP COLUMN IF EXISTS "ExternalReservationId";
ALTER TABLE "Appointments" DROP COLUMN IF EXISTS "ExternalListingId";
ALTER TABLE "Appointments" DROP COLUMN IF EXISTS "CustomerAddressId";

ALTER TABLE "Companies" DROP COLUMN IF EXISTS "GuestyTokenUpdatedAtUtc";
ALTER TABLE "Companies" DROP COLUMN IF EXISTS "GuestyTokenType";
ALTER TABLE "Companies" DROP COLUMN IF EXISTS "GuestyTokenExpiresAtUtc";
ALTER TABLE "Companies" DROP COLUMN IF EXISTS "GuestyClientSecret";
ALTER TABLE "Companies" DROP COLUMN IF EXISTS "GuestyClientId";
ALTER TABLE "Companies" DROP COLUMN IF EXISTS "GuestyAuthScope";
ALTER TABLE "Companies" DROP COLUMN IF EXISTS "GuestyAuthBaseUrl";
ALTER TABLE "Companies" DROP COLUMN IF EXISTS "GuestyApiType";
ALTER TABLE "Companies" DROP COLUMN IF EXISTS "GuestyAccessToken";

ALTER TABLE "Payments" DROP COLUMN IF EXISTS "CustomerAddressId";
ALTER TABLE "CustomerAreas" DROP COLUMN IF EXISTS "CustomerAddressId";

ALTER TABLE "AppointmentRecurrenceExceptions" DROP COLUMN IF EXISTS "OverrideCustomerAddressId";

ALTER TABLE "AppointmentCompletions" DROP COLUMN IF EXISTS "PaymentMethodSnapshot";
ALTER TABLE "AppointmentCompletions" DROP COLUMN IF EXISTS "FrequencySnapshot";
ALTER TABLE "AppointmentCompletions" DROP COLUMN IF EXISTS "CustomerAddressSnapshot";
ALTER TABLE "AppointmentCompletions" DROP COLUMN IF EXISTS "CustomerAddressIdSnapshot";

-- Não dropamos a tabela CustomerAddresses no Down por segurança (pode ter dados já em uso).
""");
        }
    }
}
