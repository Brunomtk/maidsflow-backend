using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.Guesty;
using Core.Exceptions;
using Core.Models;
using Infrastructure.Repositories;
using Services.Security;

namespace Services.Integrations.Guesty
{
    public class GuestyCustomerAddressSyncService : IGuestyCustomerAddressSyncService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;
        private readonly IGuestyIntegrationService _guestyIntegration;
        private readonly IGuestyOpenApiClient _guestyClient;

        public GuestyCustomerAddressSyncService(
            IUnitOfWork unitOfWork,
            ICurrentUser currentUser,
            IScopeGuard scope,
            IGuestyIntegrationService guestyIntegration,
            IGuestyOpenApiClient guestyClient)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _scope = scope;
            _guestyIntegration = guestyIntegration;
            _guestyClient = guestyClient;
        }

        public async Task<GuestySyncCustomerAddressesResultDTO> SyncCustomerAddressesAsync(GuestySyncCustomerAddressesRequest request)
        {
            if (!_currentUser.IsAdmin && !_currentUser.IsCompany)
                throw new ForbiddenException("Você não tem permissão para sincronizar endereços do Guesty.");

            if (request == null) throw new BadRequestException("Request inválido.");
            if (request.CustomerId <= 0) throw new BadRequestException("CustomerId é obrigatório.");

            var customer = await _unitOfWork.Customers.GetByIdAsync(request.CustomerId);
            if (customer == null) throw new NotFoundException("Customer não encontrado.");

            await _scope.EnsureCompanyAccessAsync(customer.CompanyId);

            // Obtain token (or throw with a friendly message)
            // IMPORTANT: do NOT pass CompanyId as an "override" here.
            // Passing a value triggers the "only admin can provide CompanyId" protection.
            // The company scope was already validated above via EnsureCompanyAccessAsync.
            var token = await _guestyIntegration.GetAccessTokenOrThrowAsync();

            var listings = await _guestyClient.GetListingsAsync(token, limit: request.Limit);

            // Optional filter (active listings only)
            if (request.OnlyActiveListings)
            {
                listings = listings
                    .Where(l => string.IsNullOrWhiteSpace(l.Status) || l.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var existingAddresses = await _unitOfWork.CustomerAddresses.GetByCustomerAsync(customer.Id);

            var byListingId = existingAddresses
                .Where(a => !string.IsNullOrWhiteSpace(a.GuestyListingId))
                .ToDictionary(a => a.GuestyListingId!, a => a, StringComparer.OrdinalIgnoreCase);

            Dictionary<string, CustomerAddress>? byAddressKey = null;
            if (request.MatchByAddress)
            {
                byAddressKey = existingAddresses
                    .Where(a => !string.IsNullOrWhiteSpace(a.AddressLine1) && !string.IsNullOrWhiteSpace(a.City) && !string.IsNullOrWhiteSpace(a.State))
                    .GroupBy(a => BuildAddressKey(a.AddressLine1, a.City, a.State, a.ZipCode))
                    .ToDictionary(g => g.Key, g => g.First());
            }

            var result = new GuestySyncCustomerAddressesResultDTO
            {
                CustomerId = customer.Id,
                DryRun = request.DryRun,
                ListingsSeen = listings.Count
            };

            var now = DateTime.UtcNow;
            var createdAddresses = new List<CustomerAddress>();

            foreach (var listing in listings)
            {
                var link = new GuestyListingAddressLinkDTO
                {
                    ListingId = listing.Id,
                    ListingTitle = listing.Nickname ?? listing.Title
                };

                // Basic validation: we need at least addressLine1/city/state
                var addr1 = (listing.AddressLine1 ?? string.Empty).Trim();
                var city = (listing.City ?? string.Empty).Trim();
                var state = NormalizeState((listing.State ?? string.Empty).Trim());

                if (string.IsNullOrWhiteSpace(listing.Id))
                {
                    link.Action = "skipped";
                    link.Reason = "Listing sem Id.";
                    result.Skipped++;
                    result.Links.Add(link);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(addr1) || string.IsNullOrWhiteSpace(city) || string.IsNullOrWhiteSpace(state))
                {
                    link.Action = "skipped";
                    link.Reason = "Listing sem endereço completo (addressLine1/city/state).";
                    result.Skipped++;
                    result.Links.Add(link);
                    continue;
                }

                CustomerAddress? address = null;

                if (byListingId.TryGetValue(listing.Id, out var byId))
                {
                    address = byId;
                }
                else if (request.MatchByAddress && byAddressKey != null)
                {
                    var key = BuildAddressKey(addr1, city, state, listing.ZipCode);
                    byAddressKey.TryGetValue(key, out address);
                }

                var title = (listing.Nickname ?? listing.Title ?? $"Guesty {listing.Id}").Trim();

                if (address == null)
                {
                    address = new CustomerAddress
                    {
                        CustomerId = customer.Id,
                        Label = title,
                        AddressLine1 = addr1,
                        AddressLine2 = listing.AddressLine2,
                        City = city,
                        State = state,
                        ZipCode = listing.ZipCode,
                        IsPrimary = false,
                        GuestyListingId = listing.Id,
                        GuestyListingTitle = title,
                        GuestySyncedAtUtc = now
                    };

                    link.Action = "created";
                    link.CustomerAddressId = address.Id; // will be set after save

                    result.Created++;
                    createdAddresses.Add(address);

                    if (!request.DryRun)
                        await _unitOfWork.CustomerAddresses.Add(address);

                    result.Links.Add(link);

                    // update lookup maps
                    byListingId[listing.Id] = address;
                    if (request.MatchByAddress && byAddressKey != null)
                        byAddressKey[BuildAddressKey(addr1, city, state, listing.ZipCode)] = address;

                    continue;
                }

                // Update existing
                if (!request.UpdateExisting && !string.Equals(address.GuestyListingId, listing.Id, StringComparison.OrdinalIgnoreCase))
                {
                    link.Action = "skipped";
                    link.CustomerAddressId = address.Id;
                    link.Reason = "Encontrou por endereço, mas UpdateExisting=false.";
                    result.Skipped++;
                    result.Links.Add(link);
                    continue;
                }

                if (request.UpdateExisting)
                {
                    address.Label = title;
                    address.AddressLine1 = addr1;
                    address.AddressLine2 = listing.AddressLine2;
                    address.City = city;
                    address.State = state;
                    address.ZipCode = listing.ZipCode;

                    address.GuestyListingId = listing.Id;
                    address.GuestyListingTitle = title;
                    address.GuestySyncedAtUtc = now;

                    link.Action = "updated";
                    link.CustomerAddressId = address.Id;

                    result.Updated++;
                    // NOTE:
                    // We intentionally do NOT call Update() here.
                    // The entity is already tracked because it comes from GetByCustomerAsync() (tracked query)
                    // or it was just added in this same DbContext. Calling Update() on an Added entity
                    // causes EF Core to throw (temporary key -> Modified). SaveAsync() will persist changes.
                }

                result.Links.Add(link);
            }

            if (request.SetPrimaryIfNone)
            {
                var hasPrimary = existingAddresses.Any(a => a.IsPrimary) || createdAddresses.Any(a => a.IsPrimary);
                if (!hasPrimary)
                {
                    var first = createdAddresses.FirstOrDefault();
                    if (first != null)
                    {
                        first.IsPrimary = true;
                        // Same rationale: do NOT call Update() here. Just mutate and SaveAsync().
                    }
                }
            }

            if (!request.DryRun)
                await _unitOfWork.SaveAsync();

            // Fix ids for created links (after save). Best-effort.
            foreach (var l in result.Links.Where(x => x.Action == "created" && x.CustomerAddressId == null))
            {
                if (byListingId.TryGetValue(l.ListingId, out var a) && a.Id > 0)
                    l.CustomerAddressId = a.Id;
            }

            return result;
        }

        private static string BuildAddressKey(string? line1, string? city, string? state, string? zip)
        {
            static string N(string? s) => (s ?? string.Empty).Trim().ToLowerInvariant();
            return $"{N(line1)}|{N(city)}|{N(state)}|{N(zip)}";
        }

        private static string NormalizeState(string state)
        {
            if (string.IsNullOrWhiteSpace(state)) return state;
            state = state.Trim();
            if (state.Length == 2) return state.ToUpperInvariant();

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["alabama"] = "AL",
                ["alaska"] = "AK",
                ["arizona"] = "AZ",
                ["arkansas"] = "AR",
                ["california"] = "CA",
                ["colorado"] = "CO",
                ["connecticut"] = "CT",
                ["delaware"] = "DE",
                ["district of columbia"] = "DC",
                ["florida"] = "FL",
                ["georgia"] = "GA",
                ["hawaii"] = "HI",
                ["idaho"] = "ID",
                ["illinois"] = "IL",
                ["indiana"] = "IN",
                ["iowa"] = "IA",
                ["kansas"] = "KS",
                ["kentucky"] = "KY",
                ["louisiana"] = "LA",
                ["maine"] = "ME",
                ["maryland"] = "MD",
                ["massachusetts"] = "MA",
                ["michigan"] = "MI",
                ["minnesota"] = "MN",
                ["mississippi"] = "MS",
                ["missouri"] = "MO",
                ["montana"] = "MT",
                ["nebraska"] = "NE",
                ["nevada"] = "NV",
                ["new hampshire"] = "NH",
                ["new jersey"] = "NJ",
                ["new mexico"] = "NM",
                ["new york"] = "NY",
                ["north carolina"] = "NC",
                ["north dakota"] = "ND",
                ["ohio"] = "OH",
                ["oklahoma"] = "OK",
                ["oregon"] = "OR",
                ["pennsylvania"] = "PA",
                ["rhode island"] = "RI",
                ["south carolina"] = "SC",
                ["south dakota"] = "SD",
                ["tennessee"] = "TN",
                ["texas"] = "TX",
                ["utah"] = "UT",
                ["vermont"] = "VT",
                ["virginia"] = "VA",
                ["washington"] = "WA",
                ["west virginia"] = "WV",
                ["wisconsin"] = "WI",
                ["wyoming"] = "WY",
            };

            if (map.TryGetValue(state, out var code)) return code;

            // fallback: first 2 letters upper (keeps DB constraint happy)
            return state.Length >= 2 ? state.Substring(0, 2).ToUpperInvariant() : state.ToUpperInvariant();
        }
    }
}
