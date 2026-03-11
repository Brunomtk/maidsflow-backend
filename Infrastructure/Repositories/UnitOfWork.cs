// Infrastructure/Repositories/UnitOfWork.cs
using System;
using System.Threading.Tasks;
using Infrastructure;

namespace Infrastructure.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IAppointmentRepository Appointments { get; }
        IAppointmentCompletionRepository AppointmentCompletions { get; }
        IAppointmentMessageLogRepository AppointmentMessageLogs { get; }
        IServiceTypeRepository ServiceTypes { get; }
        IPayrollRuleRepository PayrollRules { get; }
        IPayrollRunRepository PayrollRuns { get; }
        IPayrollItemRepository PayrollItems { get; }
        ICancellationRepository Cancellations { get; }
        ICheckRecordRepository CheckRecords { get; }
        ICompanyRepository Companies { get; }
        ICustomerRepository Customers { get; }
        ICustomerAddressRepository CustomerAddresses { get; }
        IGpsTrackingRepository GpsTrackings { get; }
        IInternalFeedbackRepository InternalFeedbacks { get; }
        ILeaderRepository Leaders { get; }
        INotificationRepository Notifications { get; }
        IPushSubscriptionRepository PushSubscriptions { get; }
        IPaymentRepository Payments { get; }
        IPlanRepository Plans { get; }
        IPlanSubscriptionRepository PlanSubscriptions { get; }
        IProfessionalRepository Professionals { get; }
        IRecurrenceRepository Recurrences { get; }
        IReviewRepository Reviews { get; }
        ITeamRepository Teams { get; }
        IUserRepository Users { get; }
        ICustomerAreaRepository CustomerAreas { get; }
        IChecklistRepository Checklists { get; }
        IChecklistItemRepository ChecklistItems { get; }
        IChecklistItemPhotoRepository ChecklistItemPhotos { get; }
        IChecklistTemplateRepository ChecklistTemplates { get; }
        IChecklistTemplateItemRepository ChecklistTemplateItems { get; }
        int Save();
        Task<int> SaveAsync();
    }

    public class UnitOfWork : IUnitOfWork
    {
        private readonly DbContextClass _dbContext;

        public UnitOfWork(
            DbContextClass dbContext,
            IAppointmentRepository appointmentRepository,
            IAppointmentCompletionRepository appointmentCompletionRepository,
            IAppointmentMessageLogRepository appointmentMessageLogRepository,
            IServiceTypeRepository serviceTypeRepository,
            IPayrollRuleRepository payrollRuleRepository,
            IPayrollRunRepository payrollRunRepository,
            IPayrollItemRepository payrollItemRepository,
            ICancellationRepository cancellationRepository,
            ICheckRecordRepository checkRecordRepository,
            ICompanyRepository companyRepository,
            ICustomerRepository customerRepository,
            ICustomerAddressRepository customerAddressRepository,
            IGpsTrackingRepository gpsTrackingRepository,
            IInternalFeedbackRepository internalFeedbackRepository,
            ILeaderRepository leaderRepository,
            INotificationRepository notificationRepository,
            IPushSubscriptionRepository pushSubscriptionRepository,
            IPaymentRepository paymentRepository,
            IPlanRepository planRepository,
            IPlanSubscriptionRepository planSubscriptionRepository,
            IProfessionalRepository professionalRepository,
            IRecurrenceRepository recurrenceRepository,
            IReviewRepository reviewRepository,
            ITeamRepository teamRepository,
            IUserRepository userRepository,
            ICustomerAreaRepository customerAreaRepository,
            IChecklistRepository checklistRepository,
            IChecklistItemRepository checklistItemRepository,
            IChecklistItemPhotoRepository checklistItemPhotoRepository,
            IChecklistTemplateRepository checklistTemplateRepository,
            IChecklistTemplateItemRepository checklistTemplateItemRepository
        )
        {
            _dbContext = dbContext;
            Appointments = appointmentRepository;
            AppointmentCompletions = appointmentCompletionRepository;
            AppointmentMessageLogs = appointmentMessageLogRepository;
            ServiceTypes = serviceTypeRepository;
            PayrollRules = payrollRuleRepository;
            PayrollRuns = payrollRunRepository;
            PayrollItems = payrollItemRepository;
            Cancellations = cancellationRepository;
            CheckRecords = checkRecordRepository;
            Companies = companyRepository;
            Customers = customerRepository;
            CustomerAddresses = customerAddressRepository;
            GpsTrackings = gpsTrackingRepository;
            InternalFeedbacks = internalFeedbackRepository;
            Leaders = leaderRepository;
            Notifications = notificationRepository;
            PushSubscriptions = pushSubscriptionRepository;
            Payments = paymentRepository;
            Plans = planRepository;
            PlanSubscriptions = planSubscriptionRepository;
            Professionals = professionalRepository;
            Recurrences = recurrenceRepository;
            Reviews = reviewRepository;
            Teams = teamRepository;
            Users = userRepository;
            CustomerAreas = customerAreaRepository;
            Checklists = checklistRepository;
            ChecklistItems = checklistItemRepository;
            ChecklistItemPhotos = checklistItemPhotoRepository;
            ChecklistTemplates = checklistTemplateRepository;
            ChecklistTemplateItems = checklistTemplateItemRepository;
        }

        public IAppointmentRepository Appointments { get; }
        public IAppointmentCompletionRepository AppointmentCompletions { get; }
        public IAppointmentMessageLogRepository AppointmentMessageLogs { get; }
        public IServiceTypeRepository ServiceTypes { get; }
        public IPayrollRuleRepository PayrollRules { get; }
        public IPayrollRunRepository PayrollRuns { get; }
        public IPayrollItemRepository PayrollItems { get; }
        public ICancellationRepository Cancellations { get; }
        public ICheckRecordRepository CheckRecords { get; }
        public ICompanyRepository Companies { get; }
        public ICustomerRepository Customers { get; }
        public ICustomerAddressRepository CustomerAddresses { get; }
        public IGpsTrackingRepository GpsTrackings { get; }
        public IInternalFeedbackRepository InternalFeedbacks { get; }
        public ILeaderRepository Leaders { get; }
        public INotificationRepository Notifications { get; }
        public IPushSubscriptionRepository PushSubscriptions { get; }
        public IPaymentRepository Payments { get; }
        public IPlanRepository Plans { get; }
        public IPlanSubscriptionRepository PlanSubscriptions { get; }
        public IProfessionalRepository Professionals { get; }
        public IRecurrenceRepository Recurrences { get; }
        public IReviewRepository Reviews { get; }
        public ITeamRepository Teams { get; }
        public IUserRepository Users { get; }
        public ICustomerAreaRepository CustomerAreas { get; }
        public IChecklistRepository Checklists { get; }
        public IChecklistItemRepository ChecklistItems { get; }
        public IChecklistItemPhotoRepository ChecklistItemPhotos { get; }
        public IChecklistTemplateRepository ChecklistTemplates { get; }
        public IChecklistTemplateItemRepository ChecklistTemplateItems { get; }

        public int Save() => _dbContext.SaveChanges();
        public Task<int> SaveAsync() => _dbContext.SaveChangesAsync();

        public void Dispose()
        {
            _dbContext.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
