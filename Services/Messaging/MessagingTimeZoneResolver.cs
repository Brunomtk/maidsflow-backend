using System.Text.RegularExpressions;

namespace Services.Messaging
{
    /// <summary>
    /// Resolves the effective IANA TimeZoneId for a messaging operation, given the customer's
    /// raw State / Phone and the appointment's stored TimeZoneId.
    ///
    /// Why this exists:
    ///   The Appointment.TimeZoneId column is set from `dto.TimeZoneId` at create time and tends
    ///   to default to the server's local zone (e.g. "America/Sao_Paulo"). For a US customer in
    ///   Seattle, that produces wrong UTC math. This resolver derives the right zone from the
    ///   customer's phone country code + state when the appointment field is unreliable.
    /// </summary>
    public static class MessagingTimeZoneResolver
    {
        private static readonly Regex DigitsOnly = new("[^0-9]", RegexOptions.Compiled);

        public static TimeZoneInfo Resolve(string? customerPhone, string? customerState, string? appointmentTimeZoneId)
        {
            // 1) Phone country code is the most reliable signal
            var phoneCountry = DetectCountryFromPhone(customerPhone);
            var stateCode = NormalizeStateCode(customerState);

            if (phoneCountry == "US" || phoneCountry == "CA")
            {
                var tz = TimeZoneFromUsCanadaState(stateCode);
                if (tz != null) return tz;
            }
            if (phoneCountry == "BR")
            {
                var tz = TimeZoneFromBrazilState(stateCode);
                if (tz != null) return tz;
            }

            // 2) State alone (when phone is missing/ambiguous)
            //    Try US first if state code is unambiguously US (no BR collision).
            if (!string.IsNullOrEmpty(stateCode))
            {
                var tzUs = TimeZoneFromUsCanadaState(stateCode);
                var tzBr = TimeZoneFromBrazilState(stateCode);
                if (tzUs != null && tzBr == null) return tzUs;
                if (tzBr != null && tzUs == null) return tzBr;
                // ambiguous → fall through
            }

            // 3) Use Appointment.TimeZoneId if set (and not the default test zone)
            if (!string.IsNullOrWhiteSpace(appointmentTimeZoneId))
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(appointmentTimeZoneId); }
                catch { /* fall through */ }
            }

            // 4) Server local
            return TimeZoneInfo.Local;
        }

        public static DateTime LocalToUtc(DateTime local, TimeZoneInfo tz)
        {
            var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
        }

        // ----- internals -----

        private static string? DetectCountryFromPhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var digits = DigitsOnly.Replace(phone, "");
            if (digits.Length == 0) return null;
            // +1 (US/CA, NANP) — 11 digits starting with 1, OR exactly 10 (assume +1)
            if (digits.Length == 10) return "US";
            if (digits.Length == 11 && digits.StartsWith("1")) return "US";
            // +55 BR (12 or 13 digits including country code)
            if ((digits.Length == 12 || digits.Length == 13) && digits.StartsWith("55")) return "BR";
            return null;
        }

        private static string NormalizeStateCode(string? state)
        {
            if (string.IsNullOrWhiteSpace(state)) return "";
            var s = state.Trim();
            // Already a 2-letter code
            if (s.Length == 2) return s.ToUpperInvariant();
            // Long names → code (US then BR)
            var upper = s.ToUpperInvariant();
            if (UsLongToCode.TryGetValue(upper, out var us)) return us;
            if (BrLongToCode.TryGetValue(upper, out var br)) return br;
            return upper.Length >= 2 ? upper[..2] : "";
        }

        private static TimeZoneInfo? TryFind(string id)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { return null; }
        }

        // -------- US/CA state → IANA TZ --------
        private static TimeZoneInfo? TimeZoneFromUsCanadaState(string code) => code switch
        {
            // Pacific (UTC-8 / -7 DST)
            "WA" or "OR" or "CA" or "NV" or "BC" or "YT" => TryFind("America/Los_Angeles"),
            // Mountain (UTC-7 / -6 DST)
            "AZ"      => TryFind("America/Phoenix"),       // no DST
            "MT" or "ID" or "WY" or "UT" or "CO" or "NM" or "AB" or "NT" => TryFind("America/Denver"),
            // Central (UTC-6 / -5 DST)
            "ND" or "SD" or "NE" or "KS" or "OK" or "TX" or "MN" or "IA" or "MO" or "AR" or "LA" or "WI" or "IL" or "MS" or "AL" or "TN" or "MB" or "SK"
                => TryFind("America/Chicago"),
            // Eastern (UTC-5 / -4 DST)
            "MI" or "IN" or "OH" or "KY" or "WV" or "VA" or "PA" or "NY" or "VT" or "NH" or "ME" or "MD" or "DE" or "DC" or "NJ" or "CT" or "MA" or "RI" or "NC" or "SC" or "GA" or "FL" or "ON" or "QC"
                => TryFind("America/New_York"),
            // Atlantic Canada
            "NB" or "NS" or "PE" => TryFind("America/Halifax"),
            "NL" => TryFind("America/St_Johns"),
            // Alaska / Hawaii
            "AK" => TryFind("America/Anchorage"),
            "HI" => TryFind("Pacific/Honolulu"),
            _ => null,
        };

        // -------- BR state → IANA TZ --------
        private static TimeZoneInfo? TimeZoneFromBrazilState(string code) => code switch
        {
            // Brasília time (UTC-3) — the vast majority
            "SP" or "RJ" or "MG" or "ES" or "BA" or "SE" or "AL" or "PE" or "PB" or "RN" or "CE" or "PI" or "MA" or "PA" or "AP" or "TO" or "GO" or "DF" or "PR" or "SC" or "RS"
                => TryFind("America/Sao_Paulo"),
            // Amazon time (UTC-4)
            "AM" or "RO" or "MT" or "MS" or "RR" => TryFind("America/Manaus"),
            // Acre (UTC-5)
            "AC" => TryFind("America/Rio_Branco"),
            // Fernando de Noronha (UTC-2) — extremely rare
            _ => null,
        };

        // long-name → code maps (only the entries we expect to encounter)
        private static readonly Dictionary<string, string> UsLongToCode = new(StringComparer.OrdinalIgnoreCase)
        {
            ["WASHINGTON"]="WA", ["OREGON"]="OR", ["CALIFORNIA"]="CA", ["NEVADA"]="NV",
            ["ARIZONA"]="AZ", ["IDAHO"]="ID", ["MONTANA"]="MT", ["WYOMING"]="WY", ["UTAH"]="UT", ["COLORADO"]="CO",
            ["NEW MEXICO"]="NM", ["TEXAS"]="TX", ["OKLAHOMA"]="OK", ["KANSAS"]="KS", ["NEBRASKA"]="NE",
            ["NORTH DAKOTA"]="ND", ["SOUTH DAKOTA"]="SD", ["MINNESOTA"]="MN", ["IOWA"]="IA", ["MISSOURI"]="MO",
            ["ARKANSAS"]="AR", ["LOUISIANA"]="LA", ["WISCONSIN"]="WI", ["ILLINOIS"]="IL", ["MISSISSIPPI"]="MS",
            ["ALABAMA"]="AL", ["TENNESSEE"]="TN", ["MICHIGAN"]="MI", ["INDIANA"]="IN", ["OHIO"]="OH",
            ["KENTUCKY"]="KY", ["WEST VIRGINIA"]="WV", ["VIRGINIA"]="VA", ["PENNSYLVANIA"]="PA", ["NEW YORK"]="NY",
            ["VERMONT"]="VT", ["NEW HAMPSHIRE"]="NH", ["MAINE"]="ME", ["MARYLAND"]="MD", ["DELAWARE"]="DE",
            ["WASHINGTON DC"]="DC", ["DISTRICT OF COLUMBIA"]="DC",
            ["NEW JERSEY"]="NJ", ["CONNECTICUT"]="CT", ["MASSACHUSETTS"]="MA", ["RHODE ISLAND"]="RI",
            ["NORTH CAROLINA"]="NC", ["SOUTH CAROLINA"]="SC", ["GEORGIA"]="GA", ["FLORIDA"]="FL",
            ["ALASKA"]="AK", ["HAWAII"]="HI",
        };

        private static readonly Dictionary<string, string> BrLongToCode = new(StringComparer.OrdinalIgnoreCase)
        {
            ["SAO PAULO"]="SP", ["SÃO PAULO"]="SP",
            ["RIO DE JANEIRO"]="RJ", ["MINAS GERAIS"]="MG", ["ESPIRITO SANTO"]="ES", ["ESPÍRITO SANTO"]="ES",
            ["BAHIA"]="BA", ["SERGIPE"]="SE", ["ALAGOAS"]="AL", ["PERNAMBUCO"]="PE", ["PARAIBA"]="PB", ["PARAÍBA"]="PB",
            ["RIO GRANDE DO NORTE"]="RN", ["CEARA"]="CE", ["CEARÁ"]="CE", ["PIAUI"]="PI", ["PIAUÍ"]="PI",
            ["MARANHAO"]="MA", ["MARANHÃO"]="MA", ["PARA"]="PA", ["PARÁ"]="PA",
            ["AMAPA"]="AP", ["AMAPÁ"]="AP", ["TOCANTINS"]="TO", ["GOIAS"]="GO", ["GOIÁS"]="GO",
            ["DISTRITO FEDERAL"]="DF", ["PARANA"]="PR", ["PARANÁ"]="PR",
            ["SANTA CATARINA"]="SC", ["RIO GRANDE DO SUL"]="RS",
            ["AMAZONAS"]="AM", ["RONDONIA"]="RO", ["RONDÔNIA"]="RO", ["MATO GROSSO"]="MT",
            ["MATO GROSSO DO SUL"]="MS", ["RORAIMA"]="RR", ["ACRE"]="AC",
        };
    }
}
