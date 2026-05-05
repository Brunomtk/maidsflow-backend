using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent — uses DO $$ blocks with IF NOT EXISTS guards so it can be re-run
            // safely against databases that already have any of these objects applied manually.

            migrationBuilder.Sql(@"
DO $$
BEGIN

-- =============================================
-- CompanyMessagingProfiles
-- =============================================
CREATE TABLE IF NOT EXISTS ""CompanyMessagingProfiles"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""CompanyId"" INT NOT NULL,
    ""SmsEnabled"" BOOLEAN NOT NULL DEFAULT FALSE,
    ""Status"" VARCHAR(32) NOT NULL DEFAULT 'Trial',
    ""TrialStartedAtUtc"" TIMESTAMP WITH TIME ZONE NULL,
    ""TrialEndsAtUtc"" TIMESTAMP WITH TIME ZONE NULL,
    ""DefaultTrialFromPhoneE164"" VARCHAR(32) NULL,
    ""TwilioFromPhoneE164"" VARCHAR(32) NULL,
    ""TwilioPhoneNumberSid"" VARCHAR(64) NULL,
    ""TwilioMessagingServiceSid"" VARCHAR(64) NULL,
    ""TwilioBrandSid"" VARCHAR(64) NULL,
    ""TwilioCampaignSid"" VARCHAR(64) NULL,
    ""TwilioTrustProductSid"" VARCHAR(64) NULL,
    ""TwilioCustomerProfileSid"" VARCHAR(64) NULL,
    ""SubmittedToTwilioAtUtc"" TIMESTAMP WITH TIME ZONE NULL,
    ""ApprovedAtUtc"" TIMESTAMP WITH TIME ZONE NULL,
    ""RejectedAtUtc"" TIMESTAMP WITH TIME ZONE NULL,
    ""RejectionReason"" VARCHAR(2048) NULL,
    ""InternalAdminNotes"" TEXT NULL,
    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    ""UpdatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT ""FK_CompanyMessagingProfiles_Companies"" FOREIGN KEY (""CompanyId"")
        REFERENCES ""Companies""(""Id"") ON DELETE CASCADE
);

IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_CompanyMessagingProfiles_CompanyId_unique') THEN
    CREATE UNIQUE INDEX ""IX_CompanyMessagingProfiles_CompanyId_unique""
        ON ""CompanyMessagingProfiles""(""CompanyId"");
END IF;

IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_CompanyMessagingProfiles_Status') THEN
    CREATE INDEX ""IX_CompanyMessagingProfiles_Status""
        ON ""CompanyMessagingProfiles""(""Status"");
END IF;

-- =============================================
-- CompanyTwilioCampaignApplications
-- =============================================
CREATE TABLE IF NOT EXISTS ""CompanyTwilioCampaignApplications"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""CompanyId"" INT NOT NULL,
    ""LegalBusinessName"" VARCHAR(255) NOT NULL DEFAULT '',
    ""DbaName"" VARCHAR(255) NULL,
    ""Ein"" VARCHAR(64) NULL,
    ""BusinessType"" VARCHAR(64) NOT NULL DEFAULT '',
    ""BusinessWebsiteUrl"" VARCHAR(500) NOT NULL DEFAULT '',
    ""BusinessAddressLine1"" VARCHAR(255) NOT NULL DEFAULT '',
    ""BusinessAddressLine2"" VARCHAR(255) NULL,
    ""BusinessCity"" VARCHAR(120) NOT NULL DEFAULT '',
    ""BusinessState"" VARCHAR(120) NOT NULL DEFAULT '',
    ""BusinessPostalCode"" VARCHAR(20) NOT NULL DEFAULT '',
    ""BusinessCountry"" VARCHAR(2) NOT NULL DEFAULT 'US',
    ""ContactFirstName"" VARCHAR(120) NOT NULL DEFAULT '',
    ""ContactLastName"" VARCHAR(120) NOT NULL DEFAULT '',
    ""ContactEmail"" VARCHAR(255) NOT NULL DEFAULT '',
    ""ContactPhoneE164"" VARCHAR(32) NOT NULL DEFAULT '',
    ""UseCase"" VARCHAR(64) NOT NULL DEFAULT 'LOW_VOLUME',
    ""CampaignDescription"" VARCHAR(4096) NOT NULL DEFAULT '',
    ""MessageFlow"" VARCHAR(2049) NOT NULL DEFAULT '',
    ""MessageSamplesJson"" TEXT NOT NULL DEFAULT '[]',
    ""HasEmbeddedLinks"" BOOLEAN NOT NULL DEFAULT FALSE,
    ""HasEmbeddedPhone"" BOOLEAN NOT NULL DEFAULT FALSE,
    ""OptInKeywordsJson"" VARCHAR(1024) NOT NULL DEFAULT '[""START""]',
    ""OptOutKeywordsJson"" VARCHAR(1024) NOT NULL DEFAULT '[""STOP""]',
    ""HelpKeywordsJson"" VARCHAR(1024) NOT NULL DEFAULT '[""HELP""]',
    ""OptInMessage"" VARCHAR(2048) NULL,
    ""OptOutMessage"" VARCHAR(2048) NOT NULL DEFAULT 'You have successfully unsubscribed. You will no longer receive SMS messages.',
    ""HelpMessage"" VARCHAR(2048) NOT NULL DEFAULT 'Reply STOP to unsubscribe. Contact the business directly for support.',
    ""PublicConsentPageSlug"" VARCHAR(120) NOT NULL DEFAULT '',
    ""TermsUrl"" VARCHAR(500) NOT NULL DEFAULT '',
    ""PrivacyPolicyUrl"" VARCHAR(500) NOT NULL DEFAULT '',
    ""EstimatedMonthlyVolume"" VARCHAR(64) NOT NULL DEFAULT '1-1000',
    ""Status"" VARCHAR(32) NOT NULL DEFAULT 'Draft',
    ""AdminReviewNotes"" TEXT NULL,
    ""SubmittedAtUtc"" TIMESTAMP WITH TIME ZONE NULL,
    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    ""UpdatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT ""FK_CompanyTwilioCampaignApplications_Companies"" FOREIGN KEY (""CompanyId"")
        REFERENCES ""Companies""(""Id"") ON DELETE CASCADE
);

IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_CompanyTwilioCampaignApplications_CompanyId') THEN
    CREATE INDEX ""IX_CompanyTwilioCampaignApplications_CompanyId""
        ON ""CompanyTwilioCampaignApplications""(""CompanyId"");
END IF;

IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_CompanyTwilioCampaignApplications_PublicConsentPageSlug_unique') THEN
    CREATE UNIQUE INDEX ""IX_CompanyTwilioCampaignApplications_PublicConsentPageSlug_unique""
        ON ""CompanyTwilioCampaignApplications""(""PublicConsentPageSlug"")
        WHERE ""PublicConsentPageSlug"" <> '';
END IF;

IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_CompanyTwilioCampaignApplications_Status') THEN
    CREATE INDEX ""IX_CompanyTwilioCampaignApplications_Status""
        ON ""CompanyTwilioCampaignApplications""(""Status"");
END IF;

-- =============================================
-- CompanyTwilioDocuments
-- =============================================
CREATE TABLE IF NOT EXISTS ""CompanyTwilioDocuments"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""CompanyId"" INT NOT NULL,
    ""CampaignApplicationId"" INT NOT NULL,
    ""DocumentType"" VARCHAR(64) NOT NULL DEFAULT '',
    ""FileUrl"" VARCHAR(1024) NOT NULL DEFAULT '',
    ""OriginalFileName"" VARCHAR(255) NOT NULL DEFAULT '',
    ""ContentType"" VARCHAR(120) NOT NULL DEFAULT '',
    ""Status"" VARCHAR(32) NOT NULL DEFAULT 'Pending',
    ""RejectionReason"" VARCHAR(2048) NULL,
    ""ReviewedByUserId"" INT NULL,
    ""ReviewedAtUtc"" TIMESTAMP WITH TIME ZONE NULL,
    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    ""UpdatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT ""FK_CompanyTwilioDocuments_Companies"" FOREIGN KEY (""CompanyId"")
        REFERENCES ""Companies""(""Id"") ON DELETE CASCADE
);

IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_CompanyTwilioDocuments_CompanyId_CampaignApplicationId') THEN
    CREATE INDEX ""IX_CompanyTwilioDocuments_CompanyId_CampaignApplicationId""
        ON ""CompanyTwilioDocuments""(""CompanyId"", ""CampaignApplicationId"");
END IF;

IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_CompanyTwilioDocuments_Status') THEN
    CREATE INDEX ""IX_CompanyTwilioDocuments_Status""
        ON ""CompanyTwilioDocuments""(""Status"");
END IF;

-- =============================================
-- CompanySmsConsentRecords
-- =============================================
CREATE TABLE IF NOT EXISTS ""CompanySmsConsentRecords"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""CompanyId"" INT NOT NULL,
    ""LandingSlug"" VARCHAR(120) NOT NULL DEFAULT '',
    ""Name"" VARCHAR(255) NULL,
    ""Email"" VARCHAR(255) NULL,
    ""PhoneE164"" VARCHAR(32) NOT NULL DEFAULT '',
    ""ConsentTextSnapshot"" TEXT NOT NULL DEFAULT '',
    ""TermsUrl"" VARCHAR(500) NOT NULL DEFAULT '',
    ""PrivacyPolicyUrl"" VARCHAR(500) NOT NULL DEFAULT '',
    ""TermsVersion"" VARCHAR(32) NOT NULL DEFAULT 'v1',
    ""PrivacyVersion"" VARCHAR(32) NOT NULL DEFAULT 'v1',
    ""IpAddress"" VARCHAR(64) NULL,
    ""UserAgent"" VARCHAR(500) NULL,
    ""AcceptedAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    ""UpdatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT ""FK_CompanySmsConsentRecords_Companies"" FOREIGN KEY (""CompanyId"")
        REFERENCES ""Companies""(""Id"") ON DELETE CASCADE
);

IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_CompanySmsConsentRecords_CompanyId_PhoneE164') THEN
    CREATE INDEX ""IX_CompanySmsConsentRecords_CompanyId_PhoneE164""
        ON ""CompanySmsConsentRecords""(""CompanyId"", ""PhoneE164"");
END IF;

IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_CompanySmsConsentRecords_LandingSlug') THEN
    CREATE INDEX ""IX_CompanySmsConsentRecords_LandingSlug""
        ON ""CompanySmsConsentRecords""(""LandingSlug"");
END IF;

IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_CompanySmsConsentRecords_AcceptedAtUtc') THEN
    CREATE INDEX ""IX_CompanySmsConsentRecords_AcceptedAtUtc""
        ON ""CompanySmsConsentRecords""(""AcceptedAtUtc"");
END IF;

-- =============================================
-- CompanyMessagingAuditLogs
-- =============================================
CREATE TABLE IF NOT EXISTS ""CompanyMessagingAuditLogs"" (
    ""Id"" SERIAL PRIMARY KEY,
    ""CompanyId"" INT NOT NULL,
    ""UserId"" INT NULL,
    ""Action"" VARCHAR(64) NOT NULL DEFAULT '',
    ""BeforeJson"" TEXT NULL,
    ""AfterJson"" TEXT NULL,
    ""Notes"" TEXT NULL,
    ""CreatedAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    ""CreatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    ""UpdatedDate"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    CONSTRAINT ""FK_CompanyMessagingAuditLogs_Companies"" FOREIGN KEY (""CompanyId"")
        REFERENCES ""Companies""(""Id"") ON DELETE CASCADE
);

IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_CompanyMessagingAuditLogs_CompanyId_CreatedAtUtc') THEN
    CREATE INDEX ""IX_CompanyMessagingAuditLogs_CompanyId_CreatedAtUtc""
        ON ""CompanyMessagingAuditLogs""(""CompanyId"", ""CreatedAtUtc"");
END IF;

IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'IX_CompanyMessagingAuditLogs_Action') THEN
    CREATE INDEX ""IX_CompanyMessagingAuditLogs_Action""
        ON ""CompanyMessagingAuditLogs""(""Action"");
END IF;

-- =============================================
-- AppointmentMessageLogs — extra audit columns for sender source
-- =============================================
IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AppointmentMessageLogs' AND column_name = 'SenderPhoneE164') THEN
    ALTER TABLE ""AppointmentMessageLogs"" ADD COLUMN ""SenderPhoneE164"" VARCHAR(32) NULL;
END IF;

IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AppointmentMessageLogs' AND column_name = 'SenderSource') THEN
    ALTER TABLE ""AppointmentMessageLogs"" ADD COLUMN ""SenderSource"" VARCHAR(64) NULL;
END IF;

IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AppointmentMessageLogs' AND column_name = 'MessagingProfileStatus') THEN
    ALTER TABLE ""AppointmentMessageLogs"" ADD COLUMN ""MessagingProfileStatus"" VARCHAR(32) NULL;
END IF;

IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AppointmentMessageLogs' AND column_name = 'WasBlockedByMessagingPolicy') THEN
    ALTER TABLE ""AppointmentMessageLogs"" ADD COLUMN ""WasBlockedByMessagingPolicy"" BOOLEAN NOT NULL DEFAULT FALSE;
END IF;

IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AppointmentMessageLogs' AND column_name = 'MessagingBlockReason') THEN
    ALTER TABLE ""AppointmentMessageLogs"" ADD COLUMN ""MessagingBlockReason"" VARCHAR(255) NULL;
END IF;

END$$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse, also idempotent
            migrationBuilder.Sql(@"
DO $$
BEGIN
    DROP TABLE IF EXISTS ""CompanyMessagingAuditLogs"";
    DROP TABLE IF EXISTS ""CompanySmsConsentRecords"";
    DROP TABLE IF EXISTS ""CompanyTwilioDocuments"";
    DROP TABLE IF EXISTS ""CompanyTwilioCampaignApplications"";
    DROP TABLE IF EXISTS ""CompanyMessagingProfiles"";

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AppointmentMessageLogs' AND column_name = 'SenderPhoneE164') THEN
        ALTER TABLE ""AppointmentMessageLogs"" DROP COLUMN ""SenderPhoneE164"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AppointmentMessageLogs' AND column_name = 'SenderSource') THEN
        ALTER TABLE ""AppointmentMessageLogs"" DROP COLUMN ""SenderSource"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AppointmentMessageLogs' AND column_name = 'MessagingProfileStatus') THEN
        ALTER TABLE ""AppointmentMessageLogs"" DROP COLUMN ""MessagingProfileStatus"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AppointmentMessageLogs' AND column_name = 'WasBlockedByMessagingPolicy') THEN
        ALTER TABLE ""AppointmentMessageLogs"" DROP COLUMN ""WasBlockedByMessagingPolicy"";
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'AppointmentMessageLogs' AND column_name = 'MessagingBlockReason') THEN
        ALTER TABLE ""AppointmentMessageLogs"" DROP COLUMN ""MessagingBlockReason"";
    END IF;
END$$;
");
        }
    }
}
