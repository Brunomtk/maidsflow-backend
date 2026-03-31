using Core.Enums;
﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure
{
    public class DbContextClass : DbContext
    {
        public DbSet<ChecklistItemPhoto> ChecklistItemPhotos { get; set; } = null!;
        public DbSet<ChecklistItem> ChecklistItems { get; set; } = null!;
        public DbSet<Checklist> Checklists { get; set; } = null!;
        public DbSet<ChecklistTemplate> ChecklistTemplates { get; set; } = null!;
        public DbSet<ChecklistTemplateItem> ChecklistTemplateItems { get; set; } = null!;

        public DbSet<CustomerArea> CustomerAreas { get; set; } = null!;

        public DbContextClass(DbContextOptions<DbContextClass> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserPermission> UserPermissions { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<Plan> Plans { get; set; }
        public DbSet<PlanSubscription> PlanSubscriptions { get; set; }
        public DbSet<Professional> Professionals { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<TeamMember> TeamMembers { get; set; }
        public DbSet<Leader> Leaders { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<ServiceType> ServiceTypes { get; set; }
        public DbSet<PayrollRule> PayrollRules { get; set; }
        public DbSet<PayrollRun> PayrollRuns { get; set; }
        public DbSet<PayrollItem> PayrollItems { get; set; }
        public DbSet<AppointmentRecurrenceException> AppointmentRecurrenceExceptions { get; set; }
        public DbSet<AppointmentCompletion> AppointmentCompletions { get; set; }
        public DbSet<Customer> Customers { get; set; }
        public DbSet<CustomerAddress> CustomerAddresses { get; set; }
        public DbSet<CheckRecord> CheckRecords { get; set; }
        public DbSet<Recurrence> Recurrences { get; set; }
        public DbSet<GpsTracking> GpsTrackings { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<InternalFeedback> InternalFeedbacks { get; set; }
        public DbSet<Cancellation> Cancellations { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<PaymentCategory> PaymentCategories { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<PushSubscription> PushSubscriptions { get; set; }
        public DbSet<AppointmentReminderDispatch> AppointmentReminderDispatches { get; set; }
        public DbSet<AppointmentReviewRequestDispatch> AppointmentReviewRequestDispatches { get; set; }
        public DbSet<AppointmentMessageLog> AppointmentMessageLogs { get; set; }
        public DbSet<BackgroundJobStatus> BackgroundJobStatuses { get; set; }
        public DbSet<BackgroundJobExecution> BackgroundJobExecutions { get; set; }
        public DbSet<AutomationFailureLog> AutomationFailureLogs { get; set; }
        public DbSet<ServiceIssue> ServiceIssues { get; set; }
        
        

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            

            modelBuilder.Entity<BackgroundJobStatus>(entity =>
            {
                entity.ToTable("BackgroundJobStatuses");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.JobKey).IsUnique();
                entity.Property(x => x.JobKey).IsRequired().HasMaxLength(100);
                entity.Property(x => x.DisplayName).IsRequired().HasMaxLength(160);
                entity.Property(x => x.Category).HasMaxLength(80);
                entity.Property(x => x.CurrentStatus).HasConversion<string>();
                entity.Property(x => x.LastError).HasMaxLength(2000);
                entity.Property(x => x.LastSummary).HasMaxLength(2000);
            });

            modelBuilder.Entity<BackgroundJobExecution>(entity =>
            {
                entity.ToTable("BackgroundJobExecutions");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => new { x.JobKey, x.StartedAtUtc });
                entity.Property(x => x.JobKey).IsRequired().HasMaxLength(100);
                entity.Property(x => x.Status).HasConversion<string>();
                entity.Property(x => x.Summary).HasMaxLength(2000);
                entity.Property(x => x.Error).HasMaxLength(4000);
                entity.Property(x => x.TriggeredBy).IsRequired().HasMaxLength(30);
            });

            // Checklist module
            modelBuilder.Entity<CustomerArea>(entity =>
            {
                entity.ToTable("CustomerAreas");
                entity.HasKey(a => a.Id);
                entity.Property(a => a.Name).IsRequired().HasMaxLength(120);
                entity.Property(a => a.Active).HasDefaultValue(true);
                entity.HasOne(a => a.Customer)
                      // CustomerArea pertence ao Customer. Customer.Appointments é outra relação (Customer -> Appointment).
                      // Se apontarmos para Appointments aqui, o EF tenta criar FK "CustomerId1" e explode no runtime.
                      .WithMany()
                      .HasForeignKey(a => a.CustomerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(a => a.CustomerAddress)
                      .WithMany(ca => ca.Areas)
                      .HasForeignKey(a => a.CustomerAddressId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(a => new { a.CustomerId, a.CustomerAddressId, a.Name, a.Active }).IsUnique();
            });

            modelBuilder.Entity<ChecklistTemplate>(entity =>
            {
                entity.ToTable("ChecklistTemplates");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name).IsRequired().HasMaxLength(160);
                entity.Property(x => x.TemplateType).IsRequired().HasMaxLength(50);
                entity.Property(x => x.Description).HasMaxLength(1000);
                entity.HasOne(x => x.Company)
                      .WithMany()
                      .HasForeignKey(x => x.CompanyId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ChecklistTemplateItem>(entity =>
            {
                entity.ToTable("ChecklistTemplateItems");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.SpaceName).IsRequired().HasMaxLength(120);
                entity.Property(x => x.Title).IsRequired().HasMaxLength(220);
                entity.Property(x => x.Description).HasMaxLength(1000);
                entity.HasOne(x => x.ChecklistTemplate)
                      .WithMany(t => t.Items)
                      .HasForeignKey(x => x.ChecklistTemplateId)
                      .OnDelete(DeleteBehavior.Cascade);
            });


            modelBuilder.Entity<AutomationFailureLog>(entity =>
            {
                entity.ToTable("AutomationFailureLogs");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => new { x.Source, x.OccurredAtUtc });
                entity.HasIndex(x => x.ExecutionId);
                entity.Property(x => x.Source).IsRequired().HasMaxLength(40);
                entity.Property(x => x.WorkflowKey).IsRequired().HasMaxLength(120);
                entity.Property(x => x.WorkflowName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.NodeName).HasMaxLength(200);
                entity.Property(x => x.ErrorMessage).IsRequired().HasMaxLength(2000);
                entity.Property(x => x.ErrorDetails).HasMaxLength(8000);
                entity.Property(x => x.ExecutionId).HasMaxLength(120);
                entity.Property(x => x.AlertEmailTo).HasMaxLength(320);
                entity.Property(x => x.PayloadJson).HasColumnType("jsonb");
                entity.HasOne(x => x.Company)
                      .WithMany()
                      .HasForeignKey(x => x.CompanyId)
                      .OnDelete(DeleteBehavior.SetNull);
            });


            modelBuilder.Entity<ServiceIssue>(entity =>
            {
                entity.ToTable("ServiceIssues");
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => new { x.CompanyId, x.Status, x.CreatedDate });
                entity.HasIndex(x => x.AppointmentId);
                entity.Property(x => x.Type).IsRequired().HasMaxLength(60);
                entity.Property(x => x.Status).IsRequired().HasMaxLength(30);
                entity.Property(x => x.Summary).IsRequired().HasMaxLength(200);
                entity.Property(x => x.Description).HasMaxLength(2000);
                entity.Property(x => x.InternalNotes).HasMaxLength(1000);
                entity.Property(x => x.PhotoUrlsJson).HasColumnType("jsonb");
                entity.Property(x => x.EstimatedAmount).HasColumnType("numeric(18,2)");
                entity.Property(x => x.ApprovedAmount).HasColumnType("numeric(18,2)");
                entity.HasOne(x => x.Company)
                      .WithMany()
                      .HasForeignKey(x => x.CompanyId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Appointment)
                      .WithMany()
                      .HasForeignKey(x => x.AppointmentId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Customer)
                      .WithMany()
                      .HasForeignKey(x => x.CustomerId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(x => x.CustomerAddress)
                      .WithMany()
                      .HasForeignKey(x => x.CustomerAddressId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(x => x.Professional)
                      .WithMany()
                      .HasForeignKey(x => x.ProfessionalId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(x => x.ReportedByUser)
                      .WithMany()
                      .HasForeignKey(x => x.ReportedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.ReviewedByUser)
                      .WithMany()
                      .HasForeignKey(x => x.ReviewedByUserId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<Checklist>(entity =>
            {
                entity.ToTable("Checklists");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.Status).HasConversion<string>();
                entity.Property(c => c.TemplateNameSnapshot).HasMaxLength(160);
                entity.Property(c => c.PropertyLabel).HasMaxLength(160);
                entity.HasOne(c => c.Customer)
                      .WithMany()
                      .HasForeignKey(c => c.CustomerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(c => c.CustomerAddress)
                      .WithMany(ca => ca.Checklists)
                      .HasForeignKey(c => c.CustomerAddressId)
                      .OnDelete(DeleteBehavior.SetNull);
            
                entity.HasOne(c => c.Appointment)
                      .WithMany()
                      .HasForeignKey(c => c.AppointmentId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(c => c.Professional)
                      .WithMany()
                      .HasForeignKey(c => c.ProfessionalId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(c => c.ChecklistTemplate)
                      .WithMany()
                      .HasForeignKey(c => c.ChecklistTemplateId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(c => c.Company)
                      .WithMany()
                      .HasForeignKey(c => c.CompanyId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<ChecklistItem>(entity =>
            {
                entity.ToTable("ChecklistItems");
                entity.HasKey(i => i.Id);
                entity.Property(i => i.Status).HasConversion<string>().IsRequired(false);
                entity.Property(i => i.SpaceName).IsRequired().HasMaxLength(120);
                entity.Property(i => i.Title).IsRequired().HasMaxLength(220);
                entity.Property(i => i.Description).HasMaxLength(1000);
                entity.HasOne(i => i.Checklist)
                      .WithMany(c => c.Items)
                      .HasForeignKey(i => i.ChecklistId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(i => i.CustomerArea)
                      .WithMany()
                      .HasForeignKey(i => i.CustomerAreaId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(i => i.ChecklistTemplateItem)
                      .WithMany()
                      .HasForeignKey(i => i.ChecklistTemplateItemId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<ChecklistItemPhoto>(entity =>
            {
                entity.ToTable("ChecklistItemPhotos");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Url).IsRequired();
                entity.HasOne(p => p.ChecklistItem)
                      .WithMany(i => i.Photos)
                      .HasForeignKey(p => p.ChecklistItemId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CustomerAddress>(entity =>
            {
                entity.Property(x => x.HouseAccessNotes).HasMaxLength(600);
                entity.Property(x => x.HouseGateCode).HasMaxLength(120);
                entity.Property(x => x.HousePetNotes).HasMaxLength(600);
                entity.Property(x => x.HouseRestrictionsNotes).HasMaxLength(800);
                entity.Property(x => x.HousePriorityNotes).HasMaxLength(800);
                entity.Property(x => x.HousePhotoUrlsJson).HasColumnType("jsonb");
            });

            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.Property(x => x.HouseNotesSnapshotJson).HasColumnType("jsonb");
            });

            base.OnModelCreating(modelBuilder);

            // Users
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(u => u.Id);

                entity.HasIndex(u => u.CustomerId);

                // Optional link for Property Manager users
                entity.HasOne<Customer>()
                      .WithMany()
                      .HasForeignKey(u => u.CustomerId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.Property(u => u.Onboarding)
                      .HasDefaultValue(false);
            });

            // Companies
            modelBuilder.Entity<Company>(entity =>
            {
                entity.ToTable("Companies");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.StripeCustomerId).HasMaxLength(128);
                // Plan agora é opcional: uma Company pode existir sem estar vinculada a um plano.
                entity.HasOne(c => c.Plan)
                      .WithMany()
                      .HasForeignKey(c => c.PlanId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasMany(c => c.Users)
                      .WithOne()
                      .HasForeignKey(u => u.CompanyId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Plans
            modelBuilder.Entity<Plan>(entity =>
            {
                entity.ToTable("Plans");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Name).IsRequired();
                entity.Property(p => p.Price).HasPrecision(18, 2);
                entity.Property(p => p.StripeProductId).HasMaxLength(128);
                entity.Property(p => p.StripePriceId).HasMaxLength(128);
                entity.HasIndex(p => p.StripePriceId);
                entity.Property(p => p.Status).HasConversion<int>();
                entity.Property(p => p.Features).HasColumnType("text[]");
                entity.HasMany(p => p.Subscriptions)
                      .WithOne(s => s.Plan)
                      .HasForeignKey(s => s.PlanId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // PlanSubscriptions
            modelBuilder.Entity<PlanSubscription>(entity =>
            {
                entity.ToTable("PlanSubscriptions");
                entity.HasKey(s => s.Id);
                entity.Property(s => s.Status).HasConversion<int>();
                entity.Property(s => s.StripeSubscriptionId).HasMaxLength(128);
                entity.HasIndex(s => s.StripeSubscriptionId);
                entity.HasOne(s => s.Company)
                      .WithMany()
                      .HasForeignKey(s => s.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // AppointmentMessageLogs
            modelBuilder.Entity<AppointmentMessageLog>(entity =>
            {
                entity.ToTable("AppointmentMessageLogs");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Provider).HasMaxLength(64);
                entity.Property(x => x.ProviderMessageId).HasMaxLength(128);
                entity.Property(x => x.ProviderStatus).HasMaxLength(128);
                entity.Property(x => x.RecipientEmail).HasMaxLength(256);
                entity.Property(x => x.RecipientPhoneE164).HasMaxLength(32);
                entity.Property(x => x.Subject).HasMaxLength(256);
                entity.Property(x => x.TemplateKey).HasMaxLength(128);
                entity.Property(x => x.RequestedByRole).HasMaxLength(32);

                entity.Property(x => x.Kind).HasConversion<int>();
                entity.Property(x => x.Channel).HasConversion<int>();
                entity.Property(x => x.Status).HasConversion<int>();

                entity.HasIndex(x => new { x.AppointmentId, x.Kind, x.Channel, x.CreatedDate });
                entity.HasIndex(x => x.ProviderMessageId);
            });

            // Professionals
            modelBuilder.Entity<Professional>(entity =>
            {
                entity.ToTable("Professionals");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Name).IsRequired();
                entity.Property(p => p.Email).IsRequired();
                entity.Property(p => p.Status).HasConversion<string>();
                entity.Property(p => p.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(p => p.UpdatedDate).HasDefaultValueSql("now()");
                entity.HasOne(p => p.Company)
                      .WithMany()
                      .HasForeignKey(p => p.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Teams
            modelBuilder.Entity<Team>(entity =>
            {
                entity.ToTable("Teams");
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Name).IsRequired();
                entity.Property(t => t.Color)
                      .HasMaxLength(32)
                      .IsRequired(false);
                entity.Property(t => t.Status).HasConversion<string>();
                entity.Property(t => t.Region);
                entity.Property(t => t.Description);
                entity.Property(t => t.Rating);
                entity.Property(t => t.CompletedServices);
                entity.Property(t => t.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(t => t.UpdatedDate).HasDefaultValueSql("now()");
                entity.HasOne(t => t.Company)
                      .WithMany()
                      .HasForeignKey(t => t.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(t => t.Leader)
                      .WithMany()
                      .HasForeignKey(t => t.LeaderId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // Team members
            modelBuilder.Entity<TeamMember>(entity =>
            {
                entity.ToTable("TeamMembers");
                entity.HasKey(m => m.Id);

                entity.Property(m => m.Description)
                      .HasMaxLength(250);

                entity.Property(m => m.CreatedDate)
                      .HasDefaultValueSql("now()");
                entity.Property(m => m.UpdatedDate)
                      .HasDefaultValueSql("now()");

                entity.HasOne(m => m.Team)
                      .WithMany(t => t.Members)
                      .HasForeignKey(m => m.TeamId)
                      .OnDelete(DeleteBehavior.Cascade);


                entity.HasOne(m => m.Professional)
                      .WithMany()
                      .HasForeignKey(m => m.ProfessionalId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.User)
                      .WithMany()
                      .HasForeignKey(m => m.UserId)
                      .OnDelete(DeleteBehavior.SetNull);

            });

            // Leaders
            modelBuilder.Entity<Leader>(entity =>
            {
                entity.ToTable("Leaders");
                entity.HasKey(l => l.Id);
                entity.Property(l => l.Name).IsRequired();
                entity.Property(l => l.Email).IsRequired();
                entity.Property(l => l.Phone);
                entity.Property(l => l.Status).HasConversion<string>();
                entity.Property(l => l.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(l => l.UpdatedDate).HasDefaultValueSql("now()");
                entity.HasOne(l => l.User)
                      .WithMany()
                      .HasForeignKey(l => l.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
            });


            // ServiceTypes (Payroll)
            modelBuilder.Entity<ServiceType>(entity =>
            {
                entity.ToTable("ServiceTypes");
                entity.HasKey(st => st.Id);
                entity.Property(st => st.Name).IsRequired().HasMaxLength(120);
                entity.Property(st => st.IsActive).HasDefaultValue(true);
                entity.Property(st => st.Description).HasMaxLength(500);
                entity.Property(st => st.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(st => st.UpdatedDate).HasDefaultValueSql("now()");

                entity.HasOne(st => st.Company)
                      .WithMany()
                      .HasForeignKey(st => st.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(st => new { st.CompanyId, st.Name }).IsUnique();
            });

            // Payroll rules
            modelBuilder.Entity<PayrollRule>(entity =>
            {
                entity.ToTable("PayrollRules");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.TeamRole).HasConversion<int>();
                entity.Property(r => r.RateType).HasConversion<int>();
                entity.Property(r => r.RateValue).HasPrecision(18, 2);
                entity.Property(r => r.Priority).HasDefaultValue(0);
                entity.Property(r => r.IsActive).HasDefaultValue(true);
                entity.Property(r => r.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(r => r.UpdatedDate).HasDefaultValueSql("now()");

                entity.HasOne(r => r.Company)
                      .WithMany()
                      .HasForeignKey(r => r.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.ServiceType)
                      .WithMany()
                      .HasForeignKey(r => r.ServiceTypeId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(r => new { r.CompanyId, r.ServiceTypeId, r.TeamRole, r.Priority });
            });


            // Payroll runs
            modelBuilder.Entity<PayrollRun>(entity =>
            {
                entity.ToTable("PayrollRuns");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.Status).HasConversion<int>();
                entity.Property(r => r.Notes).HasMaxLength(1000);
                entity.Property(r => r.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(r => r.UpdatedDate).HasDefaultValueSql("now()");

                entity.HasOne(r => r.Company)
                      .WithMany()
                      .HasForeignKey(r => r.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(r => new { r.CompanyId, r.PeriodStart, r.PeriodEnd });
            });

            // Payroll items
            modelBuilder.Entity<PayrollItem>(entity =>
            {
                entity.ToTable("PayrollItems");
                entity.HasKey(i => i.Id);

                // Shadow properties (DB columns) for scoping/filtering without changing the CLR model.
                // Keep them mapped so the EF model stays consistent with the existing schema/snapshot.
                entity.Property<int?>("CustomerId");
                entity.Property<int?>("CustomerAddressId");
                entity.Property(i => i.TeamRole).HasConversion<int>();
                entity.Property(i => i.RateType).HasConversion<int>();
                entity.Property(i => i.RateValue).HasPrecision(18, 2);
                entity.Property(i => i.SourceAmount).HasPrecision(18, 2);
                entity.Property(i => i.CalculatedAmount).HasPrecision(18, 2);
                entity.Property(i => i.CreatedDate).HasDefaultValueSql("now()");

                entity.HasOne(i => i.PayrollRun)
                      .WithMany(r => r.Items)
                      .HasForeignKey(i => i.PayrollRunId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(i => i.Professional)
                      .WithMany()
                      .HasForeignKey(i => i.ProfessionalId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.Appointment)
                      .WithMany()
                      .HasForeignKey(i => i.AppointmentId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(i => i.ServiceType)
                      .WithMany()
                      .HasForeignKey(i => i.ServiceTypeId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(i => i.PayrollRule)
                      .WithMany()
                      .HasForeignKey(i => i.PayrollRuleId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.Property(i => i.OccurrenceStart).HasColumnType("timestamp without time zone");

                entity.Property(i => i.OccurrenceEnd).HasColumnType("timestamp without time zone");

                entity.HasOne(i => i.AppointmentCompletion)
                      .WithMany()
                      .HasForeignKey(i => i.AppointmentCompletionId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex("CustomerId");
                entity.HasIndex("CustomerAddressId");

                entity.HasIndex(i => new { i.PayrollRunId, i.ProfessionalId, i.AppointmentId, i.OccurrenceStart }).IsUnique();
            });


// Appointment completions (snapshot per occurrence)
modelBuilder.Entity<AppointmentCompletion>(entity =>
{
    entity.ToTable("AppointmentCompletions");
    entity.HasKey(x => x.Id);

    entity.Property(x => x.OccurrenceStart).HasColumnType("timestamp without time zone");
    entity.Property(x => x.OccurrenceEnd).HasColumnType("timestamp without time zone");
    entity.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");

    entity.Property(x => x.SourceAmountSnapshot).HasPrecision(18, 2);

    entity.HasOne(x => x.Company)
          .WithMany()
          .HasForeignKey(x => x.CompanyId)
          .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne(x => x.Appointment)
          .WithMany()
          .HasForeignKey(x => x.AppointmentId)
          .OnDelete(DeleteBehavior.Restrict);

    entity.HasIndex(x => new { x.CompanyId, x.AppointmentId, x.OccurrenceStart }).IsUnique();
    entity.HasIndex(x => new { x.CompanyId, x.SeriesId, x.OccurrenceStart });
});

            // Appointments
            modelBuilder.Entity<Appointment>(entity =>
            {
                entity.ToTable("Appointments");
                entity.HasKey(a => a.Id);
                entity.Property(a => a.Title).IsRequired();
                entity.Property(a => a.Address);
                entity.Property(a => a.Start).IsRequired();
                entity.Property(a => a.End).IsRequired();
                entity.Property(a => a.Status).HasConversion<string>().IsRequired();
                entity.Property(a => a.Type).HasConversion<string>().IsRequired();
                entity.Property(a => a.Category);
                entity.Property(a => a.ServiceTypeId);
                entity.Property(a => a.Notes);
                entity.Property(a => a.ProfessionalIdsData);
                entity.Property(a => a.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(a => a.UpdatedDate).HasDefaultValueSql("now()");
                entity.HasOne(a => a.Company)
                      .WithMany()
                      .HasForeignKey(a => a.CompanyId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(a => a.Customer)
                      .WithMany(c => c.Appointments)
                      .HasForeignKey(a => a.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.CustomerAddress)
                      .WithMany()
                      .HasForeignKey(a => a.CustomerAddressId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(a => a.Team)
                      .WithMany()
                      .HasForeignKey(a => a.TeamId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(a => a.ServiceType)
                      .WithMany()
                      .HasForeignKey(a => a.ServiceTypeId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

                        // User permissions
            modelBuilder.Entity<UserPermission>(entity =>
            {
                entity.ToTable("UserPermissions");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.Code)
                      .HasConversion<string>()
                      .IsRequired();
                entity.Property(p => p.Description)
                      .HasMaxLength(200);

                entity.Property(p => p.CreatedDate)
                      .HasDefaultValueSql("now()");
                entity.Property(p => p.UpdatedDate)
                      .HasDefaultValueSql("now()");

                entity.HasOne(p => p.User)
                      .WithMany(u => u.Permissions)
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });


            // Customers
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("Customers");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.ClientType).HasConversion<string>();
                entity.Property(c => c.Name).IsRequired();
                
                entity.Property(c => c.Email).IsRequired(false);
                entity.Property(c => c.Phone);
                entity.Property(c => c.Address).IsRequired();
                entity.Property(c => c.ZipCode);
                entity.Property(c => c.City);
                entity.Property(c => c.State);
                entity.Property(c => c.Observations);
                entity.Property(c => c.Status).HasConversion<string>();
                entity.Property(c => c.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(c => c.UpdatedDate).HasDefaultValueSql("now()");

                entity.Property(c => c.Frequency).HasMaxLength(50);
                entity.Property(c => c.Ssn).HasMaxLength(11);
                entity.Property(c => c.Ticket).HasPrecision(18, 2);
                entity.Property(c => c.PaymentMethod).HasMaxLength(50);

                entity.HasOne(c => c.Company)
                      .WithMany()
                      .HasForeignKey(c => c.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CustomerAddress>(entity =>
            {
                entity.ToTable("CustomerAddresses");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.Label).HasMaxLength(100);
                entity.Property(x => x.AddressLine1).IsRequired();
                entity.Property(x => x.AddressLine2);

                entity.Property(x => x.City).IsRequired();
                entity.Property(x => x.State).HasMaxLength(2).IsRequired();
                entity.Property(x => x.ZipCode);
                entity.Property(x => x.Observations);

                entity.Property(x => x.Ticket).HasPrecision(18, 2);
                entity.Property(x => x.Frequency).HasMaxLength(50);
                entity.Property(x => x.PaymentMethod).HasMaxLength(50);

                entity.Property(x => x.GuestyListingId).HasMaxLength(80);
                entity.Property(x => x.GuestyListingTitle).HasMaxLength(200);
                entity.Property(x => x.GuestySyncedAtUtc);

                entity.Property(x => x.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(x => x.UpdatedDate).HasDefaultValueSql("now()");

                entity.HasOne(x => x.Customer)
                      .WithMany(c => c.Addresses)
                      .HasForeignKey(x => x.CustomerId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => x.CustomerId);
                entity.HasIndex(x => new { x.CustomerId, x.IsPrimary });
                entity.HasIndex(x => new { x.CustomerId, x.GuestyListingId });
            });

            // CheckRecords
            modelBuilder.Entity<CheckRecord>(entity =>
            {
                entity.ToTable("CheckRecords");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.ProfessionalId).IsRequired();
                entity.Property(c => c.CompanyId).IsRequired();
                entity.Property(c => c.CustomerId).IsRequired();
                entity.Property(c => c.AppointmentId).IsRequired();
                entity.Property(c => c.Address).IsRequired();
                entity.Property(c => c.ServiceType).IsRequired();
                entity.Property(c => c.Status).HasConversion<int>();
                entity.Property(c => c.Notes);
                entity.Property(c => c.ProfessionalName);
                entity.Property(c => c.CustomerName);
                entity.Property(c => c.TeamId);
                entity.Property(c => c.TeamName);
                entity.Property(c => c.CheckInTime);
                entity.Property(c => c.CheckOutTime);
                entity.Property(c => c.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(c => c.UpdatedDate).HasDefaultValueSql("now()");
            });

            // Recurrences
            modelBuilder.Entity<Recurrence>(entity =>
            {
                entity.ToTable("Recurrences");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.CompanyId).IsRequired();
                entity.Property(r => r.CustomerId);
                entity.Property(r => r.TeamId);
                entity.Property(r => r.Title).IsRequired().HasMaxLength(200);
                entity.Property(r => r.Description);
                entity.Property(r => r.Address);
                entity.Property(r => r.Frequency).HasConversion<int>().IsRequired();
                entity.Property(r => r.Day);
                entity.Property(r => r.Time).IsRequired();
                entity.Property(r => r.Duration).IsRequired();
                entity.Property(r => r.Status).HasConversion<int>().IsRequired();
                entity.Property(r => r.Type).HasConversion<int>().IsRequired();
                entity.Property(r => r.StartDate).IsRequired();
                entity.Property(r => r.EndDate);
                entity.Property(r => r.Notes);
                entity.Property(r => r.LastExecution);
                entity.Property(r => r.NextExecution);
                entity.Property(r => r.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(r => r.UpdatedDate).HasDefaultValueSql("now()");
                entity.HasOne(r => r.Company)
                      .WithMany()
                      .HasForeignKey(r => r.CompanyId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(r => r.Customer)
                      .WithMany()
                      .HasForeignKey(r => r.CustomerId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(r => r.Team)
                      .WithMany()
                      .HasForeignKey(r => r.TeamId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            // GpsTrackings
            modelBuilder.Entity<GpsTracking>(entity =>
            {
                entity.ToTable("GpsTrackings");
                entity.HasKey(g => g.Id);
                entity.Property(g => g.ProfessionalId).IsRequired();
                entity.Property(g => g.ProfessionalName);
                entity.Property(g => g.CompanyId).IsRequired();
                entity.Property(g => g.CompanyName);
                                entity.OwnsOne(g => g.Location, loc =>
                {
                    loc.Property(l => l.Latitude).HasColumnName("Latitude").IsRequired();
                    loc.Property(l => l.Longitude).HasColumnName("Longitude").IsRequired();
                    loc.Property(l => l.Address).HasColumnName("Address");
                                    });
                entity.Property(g => g.Status).HasConversion<int>().IsRequired();
                entity.Property(g => g.Notes);
                entity.Property(g => g.Timestamp).IsRequired();
                entity.Property(g => g.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(g => g.UpdatedDate).HasDefaultValueSql("now()");
            });

            // Reviews
            modelBuilder.Entity<Review>(entity =>
            {
                entity.ToTable("Reviews");
                entity.HasKey(r => r.Id);
                entity.Property(r => r.CustomerId).IsRequired();
                entity.Property(r => r.CustomerName);
                entity.Property(r => r.CustomerAddressId);
                entity.HasIndex(r => r.CustomerAddressId);
                entity.HasOne(r => r.CustomerAddress)
                      .WithMany()
                      .HasForeignKey(r => r.CustomerAddressId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.Property(r => r.ProfessionalId);
                entity.Property(r => r.ProfessionalName);
                entity.Property(r => r.TeamId);
                entity.Property(r => r.TeamName);
                entity.Property(r => r.CompanyId).IsRequired();
                entity.Property(r => r.CompanyName);
                entity.Property(r => r.AppointmentId).IsRequired();
                entity.Property(r => r.PublicToken);
                entity.HasIndex(r => r.PublicToken).IsUnique();
                entity.HasIndex(r => r.AppointmentId);
                entity.Property(r => r.Rating).IsRequired();
                entity.Property(r => r.Comment);
                entity.Property(r => r.Date).IsRequired();
                entity.Property(r => r.ServiceType).IsRequired();
                entity.Property(r => r.Status).HasConversion<int>().IsRequired();
                entity.Property(r => r.Response);
                entity.Property(r => r.ResponseDate);
                entity.Property(r => r.SubmittedAt);
                entity.Property(r => r.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(r => r.UpdatedDate).HasDefaultValueSql("now()");
            });

            // AppointmentReviewRequestDispatches (review request emails)
            modelBuilder.Entity<AppointmentReviewRequestDispatch>(entity =>
            {
                entity.ToTable("AppointmentReviewRequestDispatches");
                entity.HasKey(d => d.Id);
                entity.Property(d => d.CompanyId).IsRequired();
                entity.Property(d => d.AppointmentCompletionId).IsRequired();
                entity.Property(d => d.ReviewId).IsRequired();
                entity.Property(d => d.CustomerId).IsRequired();
                entity.Property(d => d.RecipientEmail).IsRequired();
                entity.Property(d => d.Status).HasConversion<int>().IsRequired();
                entity.Property(d => d.AttemptCount).IsRequired();
                entity.Property(d => d.LastAttemptAtUtc);
                entity.Property(d => d.SentAtUtc);
                entity.Property(d => d.LastError);

                entity.HasOne(d => d.AppointmentCompletion)
                      .WithMany()
                      .HasForeignKey(d => d.AppointmentCompletionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(d => d.Review)
                      .WithMany()
                      .HasForeignKey(d => d.ReviewId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(d => d.AppointmentCompletionId).IsUnique();
                entity.HasIndex(d => new { d.Status, d.SentAtUtc });
                entity.Property(d => d.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(d => d.UpdatedDate).HasDefaultValueSql("now()");
            });

            // InternalFeedbacks
            modelBuilder.Entity<InternalFeedback>(entity =>
            {
                entity.ToTable("InternalFeedbacks");
                entity.HasKey(f => f.Id);
                entity.Property(f => f.Title).IsRequired();
                entity.Property(f => f.ProfessionalId).IsRequired();
                entity.Property(f => f.TeamId).IsRequired();
                entity.Property(f => f.AppointmentId);
                entity.Property(f => f.CustomerId);
                entity.Property(f => f.CustomerAddressId);
                entity.HasIndex(f => f.AppointmentId);
                entity.HasIndex(f => f.CustomerId);
                entity.HasIndex(f => f.CustomerAddressId);
                entity.HasOne(f => f.Appointment)
                      .WithMany()
                      .HasForeignKey(f => f.AppointmentId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(f => f.Customer)
                      .WithMany()
                      .HasForeignKey(f => f.CustomerId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasOne(f => f.CustomerAddress)
                      .WithMany()
                      .HasForeignKey(f => f.CustomerAddressId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.Property(f => f.Category).IsRequired();
                entity.Property(f => f.Status).HasConversion<string>().IsRequired();
                entity.Property(f => f.Date).IsRequired();
                entity.Property(f => f.Description);
                entity.Property(f => f.Priority).HasConversion<string>().IsRequired();
                entity.Property(f => f.AssignedToId).IsRequired();
                entity.Property(f => f.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(f => f.UpdatedDate).HasDefaultValueSql("now()");
                entity.HasMany(f => f.Comments)
                      .WithOne()
                      .HasForeignKey(c => c.InternalFeedbackId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Cancellations
            modelBuilder.Entity<Cancellation>(entity =>
            {
                entity.ToTable("Cancellations");
                entity.HasKey(c => c.Id);
                entity.Property(c => c.AppointmentId).IsRequired();
                entity.Property(c => c.CustomerId).IsRequired();
                entity.Property(c => c.CustomerName);
                entity.Property(c => c.CompanyId).IsRequired();
                entity.Property(c => c.Reason).IsRequired();
                entity.Property(c => c.CancelledById).IsRequired();
                entity.Property(c => c.CancelledByRole).HasConversion<string>().IsRequired();
                entity.Property(c => c.CancelledAt).IsRequired();
                entity.Property(c => c.RefundStatus);
                entity.Property(c => c.Notes);
                entity.Property(c => c.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(c => c.UpdatedDate).HasDefaultValueSql("now()");
            });

            // Payments
            modelBuilder.Entity<PaymentCategory>(entity =>
            {
                entity.ToTable("PaymentCategories");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.CompanyId).IsRequired();
                entity.Property(x => x.Name).IsRequired().HasMaxLength(120);
                entity.Property(x => x.IsSystem).HasDefaultValue(false);
                entity.Property(x => x.Active).HasDefaultValue(true);
                entity.Property(x => x.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(x => x.UpdatedDate).HasDefaultValueSql("now()");

                entity.HasOne(x => x.Company)
                      .WithMany()
                      .HasForeignKey(x => x.CompanyId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(x => new { x.CompanyId, x.Name }).IsUnique();
            });

            modelBuilder.Entity<Payment>(entity =>
            {
                entity.ToTable("Payments");
                entity.HasKey(p => p.Id);
                entity.Property(p => p.CompanyId).IsRequired();
                entity.Property(p => p.CompanyName);
                entity.Property(p => p.Amount).HasPrecision(18, 2);
                entity.Property(p => p.DueDate).IsRequired();
                entity.Property(p => p.PaymentDate);
                entity.Property(p => p.Status).HasConversion<string>().IsRequired();
                entity.Property(p => p.Method).HasConversion<string>();
                entity.Property(p => p.Reference).IsRequired();
                entity.Property(p => p.FinancialType).HasConversion<string>().IsRequired();
                entity.Property(p => p.PaymentCategoryName).HasMaxLength(120);
                entity.Property(p => p.PlanId).IsRequired(false);
                entity.Property(p => p.PlanName);
                entity.Property(p => p.CustomerId).IsRequired(false);
                entity.Property(p => p.CustomerAddressId).IsRequired(false);
                entity.Property(p => p.PaymentCategoryId).IsRequired(false);

                entity.HasOne(p => p.Customer)
                      .WithMany(c => c.Payments)
                      .HasForeignKey(p => p.CustomerId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(p => p.CustomerAddress)
                      .WithMany(ca => ca.Payments)
                      .HasForeignKey(p => p.CustomerAddressId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(p => p.PaymentCategory)
                      .WithMany(c => c.Payments)
                      .HasForeignKey(p => p.PaymentCategoryId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(p => p.CustomerId);
                entity.HasIndex(p => p.CustomerAddressId);
                entity.HasIndex(p => p.PaymentCategoryId);
                entity.HasIndex(p => new { p.CompanyId, p.FinancialType, p.DueDate });
                entity.Property(p => p.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(p => p.UpdatedDate).HasDefaultValueSql("now()");
            });

            // Notifications
            modelBuilder.Entity<Notification>(entity =>
            {
                entity.ToTable("Notifications");
                entity.HasKey(n => n.Id);
                entity.Property(n => n.Title).IsRequired();
                entity.Property(n => n.Message).IsRequired();
                entity.Property(n => n.Type).HasConversion<string>().IsRequired();
                entity.Property(n => n.RecipientId).IsRequired();
                entity.Property(n => n.RecipientRole).HasConversion<string>().IsRequired();
                entity.Property(n => n.CompanyId);
                entity.Property(n => n.Status).HasConversion<string>().IsRequired();
                entity.Property(n => n.SentAt).IsRequired();
                entity.Property(n => n.ReadAt);
                entity.Property(n => n.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(n => n.UpdatedDate).HasDefaultValueSql("now()");
            });

           
            
        

            // PushSubscriptions (Web Push)
            modelBuilder.Entity<PushSubscription>(entity =>
            {
                entity.ToTable("PushSubscriptions");
                entity.HasKey(p => p.Id);

                entity.Property(p => p.Endpoint).IsRequired();
                entity.Property(p => p.P256dh).IsRequired();
                entity.Property(p => p.Auth).IsRequired();
                entity.Property(p => p.UserRole).HasMaxLength(50);
                entity.Property(p => p.DeviceId).HasMaxLength(150);
                entity.Property(p => p.DeviceName).HasMaxLength(200);
                entity.Property(p => p.Platform).HasMaxLength(100);
                entity.Property(p => p.BrowserName).HasMaxLength(100);
                entity.Property(p => p.PermissionState).HasMaxLength(50);
                entity.Property(p => p.LastError).HasMaxLength(2000);
                entity.Property(p => p.IsPwaInstalled).HasDefaultValue(false);
                entity.Property(p => p.IsActive).HasDefaultValue(true);
                entity.Property(p => p.FailureCount).HasDefaultValue(0);

                entity.Property(p => p.CreatedDate).HasDefaultValueSql("now()");
                entity.Property(p => p.UpdatedDate).HasDefaultValueSql("now()");

                entity.HasIndex(p => new { p.UserId, p.Endpoint }).IsUnique();
                entity.HasIndex(p => new { p.UserId, p.IsActive });
                entity.HasIndex(p => new { p.UserId, p.DeviceId });

                entity.HasOne<User>()
                      .WithMany()
                      .HasForeignKey(p => p.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // AppointmentReminderDispatches (idempotência de lembretes automáticos)
            modelBuilder.Entity<AppointmentReminderDispatch>(entity =>
            {
                entity.ToTable("AppointmentReminderDispatches");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.ReminderType).HasConversion<int>();
                entity.Property(x => x.OccurrenceStartUtc).HasColumnType("timestamp with time zone");

                entity.HasIndex(x => new { x.RecipientUserId, x.AppointmentId, x.SeriesId, x.OccurrenceStartUtc, x.ReminderType })
                      .IsUnique();
            });
}

        public override int SaveChanges()
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is BaseModel &&
                            (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                var model = (BaseModel)entry.Entity;
                model.UpdatedDate = DateTime.UtcNow;
                if (entry.State == EntityState.Added)
                    model.CreatedDate = DateTime.UtcNow;
            }

            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var entries = ChangeTracker.Entries()
                .Where(e => e.Entity is BaseModel &&
                            (e.State == EntityState.Added || e.State == EntityState.Modified));

            foreach (var entry in entries)
            {
                var model = (BaseModel)entry.Entity;
                model.UpdatedDate = DateTime.UtcNow;
                if (entry.State == EntityState.Added)
                    model.CreatedDate = DateTime.UtcNow;
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
