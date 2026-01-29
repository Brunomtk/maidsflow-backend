using System;
using System.Threading.Tasks;
using Core.DTO.Guesty;
using Core.Exceptions;
using Infrastructure.Repositories;
using Services.Security;

namespace Services.Integrations.Guesty
{
    public class GuestyIntegrationService : IGuestyIntegrationService
    {
        private readonly IUnitOfWork _uow;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;
        private readonly IGuestyAuthClient _auth;

        public GuestyIntegrationService(
            IUnitOfWork uow,
            ICurrentUser currentUser,
            IScopeGuard scope,
            IGuestyAuthClient auth)
        {
            _uow = uow;
            _currentUser = currentUser;
            _scope = scope;
            _auth = auth;
        }

        private async Task<int> ResolveCompanyIdAsync(int? companyIdOverride)
        {
            if (companyIdOverride.HasValue)
            {
                if (!_currentUser.IsAdmin)
                    throw new ForbiddenException("Somente admin pode informar CompanyId.");
                return companyIdOverride.Value;
            }

            var scopedCompanyId = await _scope.GetScopedCompanyIdAsync();
            if (!scopedCompanyId.HasValue)
                throw new ForbiddenException("Escopo de company inválido.");

            return scopedCompanyId.Value;
        }

        public async Task<GuestyIntegrationStatusDTO> GetStatusAsync(int? companyIdOverride = null)
        {
            var companyId = await ResolveCompanyIdAsync(companyIdOverride);
            var company = await _uow.Companies.GetByIdAsync(companyId);
            if (company == null)
                throw new NotFoundException("Company não encontrada.");

            return new GuestyIntegrationStatusDTO
            {
                HasToken = !string.IsNullOrWhiteSpace(company.GuestyAccessToken),
                ExpiresAtUtc = company.GuestyTokenExpiresAtUtc,
                UpdatedAtUtc = company.GuestyTokenUpdatedAtUtc,
                HasCredentials = !string.IsNullOrWhiteSpace(company.GuestyClientId) && !string.IsNullOrWhiteSpace(company.GuestyClientSecret),
                ApiType = company.GuestyApiType
            };
        }

        public async Task<GuestyIntegrationStatusDTO> UpdateTokenAsync(UpdateGuestyTokenRequest request)
        {
            if (request == null) throw new BadRequestException("Request inválido.");

            var companyId = await ResolveCompanyIdAsync(request.CompanyId);
            var company = await _uow.Companies.GetByIdAsync(companyId);
            if (company == null)
                throw new NotFoundException("Company não encontrada.");

            var hasCredentials = !string.IsNullOrWhiteSpace(request.ClientId) && !string.IsNullOrWhiteSpace(request.ClientSecret);
            var hasManualToken = !string.IsNullOrWhiteSpace(request.AccessToken);

            if (!hasCredentials && !hasManualToken)
                throw new BadRequestException("Informe AccessToken (manual) ou ClientId/ClientSecret para gerar o token.");

            // 1) Store credentials (optional)
            if (hasCredentials)
            {
                company.GuestyClientId = request.ClientId!.Trim();
                company.GuestyClientSecret = request.ClientSecret!.Trim();
                company.GuestyApiType = string.IsNullOrWhiteSpace(request.ApiType) ? (company.GuestyApiType ?? "bookingEngine") : request.ApiType!.Trim();
                company.GuestyAuthBaseUrl = string.IsNullOrWhiteSpace(request.AuthBaseUrl) ? company.GuestyAuthBaseUrl : request.AuthBaseUrl!.Trim();
                company.GuestyAuthScope = string.IsNullOrWhiteSpace(request.AuthScope) ? company.GuestyAuthScope : request.AuthScope!.Trim();
            }

            // 2) If manual token was provided, store it. Otherwise, generate via OAuth.
            if (hasManualToken)
            {
                company.GuestyAccessToken = request.AccessToken!.Trim();
                company.GuestyTokenType = string.IsNullOrWhiteSpace(request.TokenType) ? (company.GuestyTokenType ?? "Bearer") : request.TokenType!.Trim();
                company.GuestyTokenExpiresAtUtc = request.ExpiresAtUtc;
                company.GuestyTokenUpdatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                // Generate token immediately so the user can test endpoints right away.
                var token = await _auth.GetTokenAsync(
                    apiType: company.GuestyApiType ?? "bookingEngine",
                    clientId: company.GuestyClientId!,
                    clientSecret: company.GuestyClientSecret!,
                    authBaseUrl: company.GuestyAuthBaseUrl,
                    scopeOverride: company.GuestyAuthScope);

                company.GuestyAccessToken = token.AccessToken;
                company.GuestyTokenType = string.IsNullOrWhiteSpace(token.TokenType) ? "Bearer" : token.TokenType;

                // expires_in is seconds. Apply a small safety buffer (60s).
                if (token.ExpiresIn > 0)
                {
                    var buffered = Math.Max(0, token.ExpiresIn - 60);
                    company.GuestyTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(buffered);
                }
                else
                {
                    company.GuestyTokenExpiresAtUtc = null;
                }

                company.GuestyTokenUpdatedAtUtc = DateTime.UtcNow;
            }

            _uow.Companies.Update(company);
            await _uow.SaveAsync();

            return await GetStatusAsync(companyId);
        }

        public async Task ClearTokenAsync(int? companyIdOverride = null)
        {
            var companyId = await ResolveCompanyIdAsync(companyIdOverride);

            var company = await _uow.Companies.GetByIdAsync(companyId);
            if (company == null)
                throw new NotFoundException("Company não encontrada.");

            company.GuestyAccessToken = null;
            company.GuestyTokenType = null;
            company.GuestyTokenExpiresAtUtc = null;
            company.GuestyTokenUpdatedAtUtc = DateTime.UtcNow;

            _uow.Companies.Update(company);
            await _uow.SaveAsync();
        }

        private bool TokenIsExpired(DateTime? expiresAtUtc)
        {
            return expiresAtUtc.HasValue && expiresAtUtc.Value <= DateTime.UtcNow;
        }

        private async Task RefreshTokenIfPossibleAsync(Core.Models.Company company)
        {
            var hasCreds = !string.IsNullOrWhiteSpace(company.GuestyClientId) && !string.IsNullOrWhiteSpace(company.GuestyClientSecret);
            if (!hasCreds)
                throw new ForbiddenException("Guesty token expirado e não há ClientId/ClientSecret salvos para renovar automaticamente.");

            var token = await _auth.GetTokenAsync(
                apiType: company.GuestyApiType ?? "bookingEngine",
                clientId: company.GuestyClientId!,
                clientSecret: company.GuestyClientSecret!,
                authBaseUrl: company.GuestyAuthBaseUrl,
                scopeOverride: company.GuestyAuthScope);

            company.GuestyAccessToken = token.AccessToken;
            company.GuestyTokenType = string.IsNullOrWhiteSpace(token.TokenType) ? "Bearer" : token.TokenType;

            if (token.ExpiresIn > 0)
            {
                var buffered = Math.Max(0, token.ExpiresIn - 60);
                company.GuestyTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(buffered);
            }
            else
            {
                company.GuestyTokenExpiresAtUtc = null;
            }

            company.GuestyTokenUpdatedAtUtc = DateTime.UtcNow;
            _uow.Companies.Update(company);
            await _uow.SaveAsync();
        }

        public async Task<string> GetAccessTokenOrThrowAsync(int? companyIdOverride = null)
        {
            var companyId = await ResolveCompanyIdAsync(companyIdOverride);
            var company = await _uow.Companies.GetByIdAsync(companyId);
            if (company == null)
                throw new NotFoundException("Company não encontrada.");

            if (string.IsNullOrWhiteSpace(company.GuestyAccessToken))
            {
                // If credentials exist, try generating now.
                var hasCreds = !string.IsNullOrWhiteSpace(company.GuestyClientId) && !string.IsNullOrWhiteSpace(company.GuestyClientSecret);
                if (!hasCreds)
                    throw new ForbiddenException("Guesty não configurado. Informe ClientId/ClientSecret em Profile > Integrations.");

                await RefreshTokenIfPossibleAsync(company);
            }
            else if (TokenIsExpired(company.GuestyTokenExpiresAtUtc))
            {
                await RefreshTokenIfPossibleAsync(company);
            }

            return company.GuestyAccessToken!;
        }
    }
}
