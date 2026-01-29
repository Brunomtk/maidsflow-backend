using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using Core.Exceptions;

namespace Services.Integrations.Guesty
{
    // Minimal OAuth2 client_credentials helper for Guesty.
    public class GuestyAuthClient : IGuestyAuthClient
    {
        private readonly HttpClient _http;

        public GuestyAuthClient(HttpClient http)
        {
            _http = http;
        }

        private static (string tokenUrl, string scope) ResolveDefaults(string apiType, string? authBaseUrlOverride, string? scopeOverride)
        {
            var type = (apiType ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(type)) type = "bookingengine";

            if (type == "openapi" || type == "open-api" || type == "open_api")
            {
                var baseUrl = string.IsNullOrWhiteSpace(authBaseUrlOverride)
                    ? "https://open-api.guesty.com"
                    : authBaseUrlOverride!.Trim().TrimEnd('/');

                var scope = string.IsNullOrWhiteSpace(scopeOverride) ? "open-api" : scopeOverride!.Trim();
                return ($"{baseUrl}/oauth2/token", scope);
            }

            // bookingEngine (Booking Engine API)
            {
                var baseUrl = string.IsNullOrWhiteSpace(authBaseUrlOverride)
                    ? "https://booking.guesty.com"
                    : authBaseUrlOverride!.Trim().TrimEnd('/');

                var scope = string.IsNullOrWhiteSpace(scopeOverride) ? "booking_engine:api" : scopeOverride!.Trim();
                return ($"{baseUrl}/oauth2/token", scope);
            }
        }

        public async Task<GuestyTokenResult> GetTokenAsync(
            string apiType,
            string clientId,
            string clientSecret,
            string? authBaseUrl = null,
            string? scopeOverride = null)
        {
            if (string.IsNullOrWhiteSpace(clientId)) throw new BadRequestException("ClientId é obrigatório.");
            if (string.IsNullOrWhiteSpace(clientSecret)) throw new BadRequestException("ClientSecret é obrigatório.");

            var (tokenUrl, scope) = ResolveDefaults(apiType, authBaseUrl, scopeOverride);

            using var req = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = scope,
                ["client_id"] = clientId.Trim(),
                ["client_secret"] = clientSecret.Trim()
            });

            HttpResponseMessage resp;
            try
            {
                resp = await _http.SendAsync(req);
            }
            catch (Exception ex)
            {
                throw new BadRequestException($"Falha ao conectar no OAuth da Guesty: {ex.Message}");
            }

            var raw = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                // Avoid echoing secrets. Raw might contain helpful error.
                throw new BadRequestException($"Erro ao gerar token na Guesty ({(int)resp.StatusCode}): {raw}");
            }

            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                var accessToken = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
                if (string.IsNullOrWhiteSpace(accessToken))
                    throw new BadRequestException("Guesty não retornou access_token.");

                var tokenType = root.TryGetProperty("token_type", out var tt) ? tt.GetString() : "Bearer";
                var expiresIn = root.TryGetProperty("expires_in", out var ei) && ei.ValueKind == JsonValueKind.Number ? ei.GetInt32() : 0;
                var scopeResp = root.TryGetProperty("scope", out var sc) ? sc.GetString() : null;

                return new GuestyTokenResult
                {
                    AccessToken = accessToken!,
                    TokenType = string.IsNullOrWhiteSpace(tokenType) ? "Bearer" : tokenType!,
                    ExpiresIn = expiresIn,
                    Scope = scopeResp,
                    ApiType = apiType,
                    AuthBaseUrl = authBaseUrl
                };
            }
            catch (BadRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new BadRequestException($"Não foi possível interpretar resposta de token da Guesty: {ex.Message}");
            }
        }
    }
}
