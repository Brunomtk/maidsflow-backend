using System;
using System.Collections.Generic;

namespace Services.Localization;

/// <summary>
/// Centralized resource table: all outbound text (SMS, email subjects/bodies, PDF labels,
/// push notification titles/bodies) lives here, organized by dotted key and by language.
///
/// To add a new key:
///   1. Add it under each language (en, pt-BR, es, fr) in the <see cref="Strings"/> dictionary.
///   2. Use <c>{placeholder}</c> tokens for variables; pass values via <see cref="IMessageLocalizer.Get(string, string, object?)"/>.
///
/// Convention for keys:
///   - <c>sms.*</c>      → SMS bodies sent via Twilio
///   - <c>email.*</c>    → email subjects / paragraphs / CTAs sent via SendGrid
///   - <c>pdf.*</c>      → labels rendered inside generated PDFs
///   - <c>push.*</c>     → push notification titles / bodies
///   - <c>shared.*</c>   → tokens reused across multiple channels (greetings, signature)
/// </summary>
public class LocalizationResources
{
    public string? Lookup(string language, string key)
    {
        if (Strings.TryGetValue(language, out var langDict)
            && langDict.TryGetValue(key, out var value))
            return value;
        return null;
    }

    /// <summary>
    /// All translated strings, organized as Strings[language][key] = template.
    /// Keys are flat dotted strings (e.g. "sms.appointmentReminder.body").
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Strings { get; }
        = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            // ============================================================
            // EN — English (default / fallback)
            // ============================================================
            [SupportedLanguages.En] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // ----- shared -----
                ["shared.greeting.hello"] = "Hello {name}",
                ["shared.signature.team"] = "— The {company} team",
                ["shared.signature.maidsflow"] = "— Maids Flow",
                ["shared.button.openApp"] = "Open Maids Flow",
                ["shared.if.notYou"] = "If you didn't request this, please contact support.",

                // ----- SMS -----
                ["sms.appointmentReminder.body"] =
                    "Hi {customer}, reminder: your service \"{title}\" starts in 30 minutes at {time}. Address: {address}",
                ["sms.checkoutReminder.body"] =
                    "Hi {customer}, your service \"{title}\" with {company} just finished. Thanks for choosing us!",
                ["sms.onMyWay.body"] =
                    "Hi {customer}, your professional is on the way and should arrive in about {minutes} minutes.",
                ["sms.confirmation.body"] =
                    "Hi {customer}, please confirm your appointment \"{title}\" on {date} at {time}. Reply YES to confirm.",
                ["sms.reviewRequest.body"] =
                    "Hi {customer}, thanks for choosing {company}! How was the service? Leave a review: {url}",
                ["sms.paymentDue.body"] =
                    "Hi {customer}, your payment of {amount} for \"{title}\" is due on {date}. Thank you!",

                // ----- Email subjects -----
                ["email.credentials.subject"] = "Welcome to {company} on Maids Flow",
                ["email.passwordReset.subject"] = "Reset your Maids Flow password",
                ["email.passwordChanged.subject"] = "Your Maids Flow password was changed",
                ["email.planPaymentSuccess.subject"] = "Payment received — {plan}",
                ["email.planPaymentFailed.subject"] = "Payment failed — please update your billing",
                ["email.reviewRequest.subject"] = "How was your service with {company}?",
                ["email.companyMonthlyReport.subject"] = "{company} — Monthly report ({period})",

                // ----- Email body fragments -----
                ["email.credentials.intro"] =
                    "This is {company}. Here are your Maids Flow access credentials:",
                ["email.credentials.fields.email"] = "Email",
                ["email.credentials.fields.password"] = "Password",
                ["email.credentials.fields.role"] = "Role",
                ["email.credentials.fields.login"] = "Login",
                ["email.credentials.cta"] = "Sign in",

                ["email.passwordReset.intro"] =
                    "We received a request to reset your Maids Flow password.",
                ["email.passwordReset.cta"] = "Reset password",
                ["email.passwordReset.expiry"] = "This link expires in {minutes} minutes.",

                ["email.passwordChanged.intro"] =
                    "Your Maids Flow password has just been changed.",
                ["email.passwordChanged.tip"] =
                    "If you did not change your password, please reset it immediately and contact support.",

                ["email.planPaymentSuccess.intro"] =
                    "We received your payment of {amount} for the {plan} plan. Thank you!",
                ["email.planPaymentSuccess.nextBilling"] = "Next billing date: {date}",
                ["email.planPaymentFailed.intro"] =
                    "We could not process your payment of {amount} for the {plan} plan.",
                ["email.planPaymentFailed.cta"] = "Update payment method",

                ["email.reviewRequest.intro"] =
                    "Hi {customer}, we hope you enjoyed your service with {company}!",
                ["email.reviewRequest.body"] =
                    "Your feedback helps us deliver better service. Could you take a minute to leave a review?",
                ["email.reviewRequest.cta"] = "Leave a review",

                ["email.companyMonthlyReport.intro"] =
                    "Here is the monthly performance report for {company} — {period}.",
                ["email.companyMonthlyReport.cta"] = "View details in the app",
                ["email.companyMonthlyReport.attachmentNote"] =
                    "A detailed PDF report is attached.",

                // ----- PDF (monthly report) -----
                ["pdf.monthlyReport.title"] = "Monthly performance report",
                ["pdf.monthlyReport.period"] = "Period",
                ["pdf.monthlyReport.section.summary"] = "Summary",
                ["pdf.monthlyReport.section.appointments"] = "Appointments",
                ["pdf.monthlyReport.section.financial"] = "Financial",
                ["pdf.monthlyReport.section.team"] = "Team performance",
                ["pdf.monthlyReport.kpi.totalAppointments"] = "Total appointments",
                ["pdf.monthlyReport.kpi.completed"] = "Completed",
                ["pdf.monthlyReport.kpi.cancelled"] = "Cancelled",
                ["pdf.monthlyReport.kpi.revenue"] = "Revenue",
                ["pdf.monthlyReport.kpi.expenses"] = "Expenses",
                ["pdf.monthlyReport.kpi.netProfit"] = "Net profit",
                ["pdf.monthlyReport.kpi.avgTicket"] = "Average ticket",
                ["pdf.monthlyReport.table.headers.professional"] = "Professional",
                ["pdf.monthlyReport.table.headers.completed"] = "Completed",
                ["pdf.monthlyReport.table.headers.rating"] = "Avg. rating",
                ["pdf.monthlyReport.table.headers.revenue"] = "Revenue",
                ["pdf.monthlyReport.empty"] = "No data for this period.",
                ["pdf.monthlyReport.footer"] =
                    "Generated by Maids Flow on {date}",

                // ----- Push notifications -----
                ["push.appointment.assigned.title"] = "New appointment assigned",
                ["push.appointment.assigned.body"] =
                    "{title} on {date} at {time} — {address}",
                ["push.appointment.updated.title"] = "Appointment updated",
                ["push.appointment.updated.body"] =
                    "{title} was updated. New time: {date} at {time}.",
                ["push.appointment.cancelled.title"] = "Appointment cancelled",
                ["push.appointment.cancelled.body"] = "{title} on {date} was cancelled.",
                ["push.appointment.checkInReminder.title"] = "Time to check in",
                ["push.appointment.checkInReminder.body"] =
                    "Your appointment \"{title}\" starts in {minutes} minutes.",
                ["push.feedback.new.title"] = "New feedback received",
                ["push.feedback.new.body"] = "{name} sent feedback: {preview}",
                ["push.review.new.title"] = "New review",
                ["push.review.new.body"] = "{customer} left a {stars}-star review.",
                ["push.payment.received.title"] = "Payment received",
                ["push.payment.received.body"] = "Payment of {amount} from {customer}.",
            
                ["notifications.appointmentReminder.title"] = "Appointment in 30 minutes",
                ["notifications.checkoutReminder.title"] = "Checkout pending",
                ["notifications.checkoutReminder.body"] = "{title}: you checked in and still haven't checked out. Address: {address}. Please complete checkout now.",
                ["notifications.appointmentDefaultTitle"] = "Your appointment",
                ["notifications.payment.overdue.title"] = "{kind} overdue",
                ["notifications.payment.overdue.body"] = "The {kind} {reference} became overdue on {date}. Category: {category}. Amount: {amount}. {token}",
                ["notifications.payment.dueToday.title"] = "{kind} due today",
                ["notifications.payment.dueToday.body"] = "The {kind} {reference} is due today ({date}). Category: {category}. Amount: {amount}. {token}",
            
                ["notifications.payment.kind.payable"] = "Accounts payable",
                ["notifications.payment.kind.receivable"] = "Accounts receivable",
                ["notifications.payment.kindLower.payable"] = "accounts payable",
                ["notifications.payment.kindLower.receivable"] = "accounts receivable",
                ["notifications.payment.uncategorized"] = "Uncategorized",
                ["notifications.payment.entryRef"] = "entry #{id}",
                ["notifications.payment.titleUpdated"] = "{kind} updated",
                ["notifications.payment.titleNew"] = "New {kind} entry",
                ["notifications.payment.bodyCreated"] = "A new {kind} was created: {reference}. Category: {category}. Amount: {amount}. Due date: {dueDate}.",
                ["notifications.payment.bodyStatusUpdated"] = "A {kind} {reference} was updated to status {status}. Category: {category}. Amount: {amount}.",
                ["notifications.payment.bodyUpdated"] = "A {kind} {reference} was updated. Category: {category}. Amount: {amount}. Due date: {dueDate}.",
            
                ["report.email.healthStatus"] = "Health status",
                ["report.email.healthNeutral"] = "Neutral",
                ["report.email.healthHint"] = "Use this summary as a quick reference before opening the full dashboard.",
                ["report.email.executiveSummary"] = "Executive summary",
                ["report.email.atAGlance"] = "At a glance",
                ["report.email.financial.title"] = "Financial performance",
                ["report.email.financial.subtitle"] = "Revenue, payments, and billing momentum for the selected period.",
                ["report.email.operations.title"] = "Operations snapshot",
                ["report.email.operations.subtitle"] = "Appointments, delivery consistency, and operational flow.",
                ["report.email.team.title"] = "Team highlights",
                ["report.email.team.subtitle"] = "Capacity, output, and professional performance indicators.",
                ["report.email.customers.title"] = "Customer view",
                ["report.email.customers.subtitle"] = "Retention, quality signals, and customer activity signals.",
                ["report.email.whatGoingWell"] = "What is going well",
                ["report.email.pointsAttention"] = "Points of attention",
                ["report.email.recommendedActions"] = "Recommended next actions",
                ["report.email.openReportCta"] = "Open full report in Maids Flow",
                ["report.email.footerMessage"] = "This message was sent automatically by Maids Flow to {email}. You can trigger this report manually at any time or keep the scheduled delivery on the first day of each month.",
                ["report.email.plainCompany"] = "Company",
                ["report.email.plainPeriod"] = "Period",
                ["report.email.plainGenerated"] = "Generated",
                ["report.email.plainOverview"] = "Overview",
                ["report.email.plainFinancial"] = "Financial",
                ["report.email.plainOperations"] = "Operations",
                ["report.email.plainTeam"] = "Team",
                ["report.email.plainCustomers"] = "Customers",
            },

            // ============================================================
            // PT-BR — Portuguese (Brazil)
            // ============================================================
            [SupportedLanguages.PtBr] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["shared.greeting.hello"] = "Olá {name}",
                ["shared.signature.team"] = "— Equipe {company}",
                ["shared.signature.maidsflow"] = "— Maids Flow",
                ["shared.button.openApp"] = "Abrir Maids Flow",
                ["shared.if.notYou"] = "Se você não solicitou isso, entre em contato com o suporte.",

                ["sms.appointmentReminder.body"] =
                    "Olá {customer}, lembrete: seu serviço \"{title}\" começa em 30 minutos, às {time}. Endereço: {address}",
                ["sms.checkoutReminder.body"] =
                    "Olá {customer}, seu serviço \"{title}\" com a {company} acabou de ser finalizado. Obrigado por escolher a gente!",
                ["sms.onMyWay.body"] =
                    "Olá {customer}, o profissional está a caminho e deve chegar em cerca de {minutes} minutos.",
                ["sms.confirmation.body"] =
                    "Olá {customer}, confirme seu agendamento \"{title}\" em {date} às {time}. Responda SIM para confirmar.",
                ["sms.reviewRequest.body"] =
                    "Olá {customer}, obrigado por escolher a {company}! Como foi o serviço? Deixe sua avaliação: {url}",
                ["sms.paymentDue.body"] =
                    "Olá {customer}, seu pagamento de {amount} referente a \"{title}\" vence em {date}. Obrigado!",

                ["email.credentials.subject"] = "Bem-vindo(a) à {company} no Maids Flow",
                ["email.passwordReset.subject"] = "Redefinir sua senha do Maids Flow",
                ["email.passwordChanged.subject"] = "Sua senha do Maids Flow foi alterada",
                ["email.planPaymentSuccess.subject"] = "Pagamento recebido — {plan}",
                ["email.planPaymentFailed.subject"] = "Falha no pagamento — atualize sua forma de cobrança",
                ["email.reviewRequest.subject"] = "Como foi seu serviço com a {company}?",
                ["email.companyMonthlyReport.subject"] = "{company} — Relatório mensal ({period})",

                ["email.credentials.intro"] =
                    "Aqui é a {company}. Estes são seus dados de acesso ao Maids Flow:",
                ["email.credentials.fields.email"] = "E-mail",
                ["email.credentials.fields.password"] = "Senha",
                ["email.credentials.fields.role"] = "Perfil",
                ["email.credentials.fields.login"] = "Acesso",
                ["email.credentials.cta"] = "Entrar",

                ["email.passwordReset.intro"] =
                    "Recebemos um pedido para redefinir sua senha do Maids Flow.",
                ["email.passwordReset.cta"] = "Redefinir senha",
                ["email.passwordReset.expiry"] = "Este link expira em {minutes} minutos.",

                ["email.passwordChanged.intro"] =
                    "Sua senha do Maids Flow acabou de ser alterada.",
                ["email.passwordChanged.tip"] =
                    "Se você não alterou sua senha, redefina imediatamente e entre em contato com o suporte.",

                ["email.planPaymentSuccess.intro"] =
                    "Recebemos seu pagamento de {amount} referente ao plano {plan}. Obrigado!",
                ["email.planPaymentSuccess.nextBilling"] = "Próxima cobrança: {date}",
                ["email.planPaymentFailed.intro"] =
                    "Não conseguimos processar seu pagamento de {amount} do plano {plan}.",
                ["email.planPaymentFailed.cta"] = "Atualizar forma de pagamento",

                ["email.reviewRequest.intro"] =
                    "Olá {customer}, esperamos que tenha gostado do serviço com a {company}!",
                ["email.reviewRequest.body"] =
                    "Sua opinião nos ajuda a melhorar. Pode dedicar um minuto para deixar uma avaliação?",
                ["email.reviewRequest.cta"] = "Avaliar serviço",

                ["email.companyMonthlyReport.intro"] =
                    "Segue o relatório mensal de desempenho da {company} — {period}.",
                ["email.companyMonthlyReport.cta"] = "Ver detalhes no app",
                ["email.companyMonthlyReport.attachmentNote"] =
                    "Um relatório detalhado em PDF está em anexo.",

                ["pdf.monthlyReport.title"] = "Relatório mensal de desempenho",
                ["pdf.monthlyReport.period"] = "Período",
                ["pdf.monthlyReport.section.summary"] = "Resumo",
                ["pdf.monthlyReport.section.appointments"] = "Agendamentos",
                ["pdf.monthlyReport.section.financial"] = "Financeiro",
                ["pdf.monthlyReport.section.team"] = "Desempenho da equipe",
                ["pdf.monthlyReport.kpi.totalAppointments"] = "Total de agendamentos",
                ["pdf.monthlyReport.kpi.completed"] = "Concluídos",
                ["pdf.monthlyReport.kpi.cancelled"] = "Cancelados",
                ["pdf.monthlyReport.kpi.revenue"] = "Receita",
                ["pdf.monthlyReport.kpi.expenses"] = "Despesas",
                ["pdf.monthlyReport.kpi.netProfit"] = "Lucro líquido",
                ["pdf.monthlyReport.kpi.avgTicket"] = "Ticket médio",
                ["pdf.monthlyReport.table.headers.professional"] = "Profissional",
                ["pdf.monthlyReport.table.headers.completed"] = "Concluídos",
                ["pdf.monthlyReport.table.headers.rating"] = "Avaliação média",
                ["pdf.monthlyReport.table.headers.revenue"] = "Receita",
                ["pdf.monthlyReport.empty"] = "Sem dados neste período.",
                ["pdf.monthlyReport.footer"] =
                    "Gerado pelo Maids Flow em {date}",

                ["push.appointment.assigned.title"] = "Novo agendamento atribuído",
                ["push.appointment.assigned.body"] =
                    "{title} em {date} às {time} — {address}",
                ["push.appointment.updated.title"] = "Agendamento atualizado",
                ["push.appointment.updated.body"] =
                    "{title} foi atualizado. Novo horário: {date} às {time}.",
                ["push.appointment.cancelled.title"] = "Agendamento cancelado",
                ["push.appointment.cancelled.body"] = "{title} em {date} foi cancelado.",
                ["push.appointment.checkInReminder.title"] = "Hora de fazer o check-in",
                ["push.appointment.checkInReminder.body"] =
                    "Seu agendamento \"{title}\" começa em {minutes} minutos.",
                ["push.feedback.new.title"] = "Novo feedback recebido",
                ["push.feedback.new.body"] = "{name} enviou um feedback: {preview}",
                ["push.review.new.title"] = "Nova avaliação",
                ["push.review.new.body"] = "{customer} deixou uma avaliação de {stars} estrelas.",
                ["push.payment.received.title"] = "Pagamento recebido",
                ["push.payment.received.body"] = "Pagamento de {amount} de {customer}.",
            
                ["notifications.appointmentReminder.title"] = "Agendamento em 30 minutos",
                ["notifications.checkoutReminder.title"] = "Checkout pendente",
                ["notifications.checkoutReminder.body"] = "{title}: você fez check-in e ainda não fez check-out. Endereço: {address}. Finalize o check-out agora.",
                ["notifications.appointmentDefaultTitle"] = "Seu agendamento",
                ["notifications.payment.overdue.title"] = "{kind} vencido(a)",
                ["notifications.payment.overdue.body"] = "O(A) {kind} {reference} venceu em {date}. Categoria: {category}. Valor: {amount}. {token}",
                ["notifications.payment.dueToday.title"] = "{kind} vence hoje",
                ["notifications.payment.dueToday.body"] = "O(A) {kind} {reference} vence hoje ({date}). Categoria: {category}. Valor: {amount}. {token}",
            
                ["notifications.payment.kind.payable"] = "Conta a pagar",
                ["notifications.payment.kind.receivable"] = "Conta a receber",
                ["notifications.payment.kindLower.payable"] = "conta a pagar",
                ["notifications.payment.kindLower.receivable"] = "conta a receber",
                ["notifications.payment.uncategorized"] = "Sem categoria",
                ["notifications.payment.entryRef"] = "lançamento #{id}",
                ["notifications.payment.titleUpdated"] = "{kind} atualizada",
                ["notifications.payment.titleNew"] = "Nova {kind}",
                ["notifications.payment.bodyCreated"] = "Foi criada uma nova {kind}: {reference}. Categoria: {category}. Valor: {amount}. Vencimento: {dueDate}.",
                ["notifications.payment.bodyStatusUpdated"] = "A {kind} {reference} mudou para o status {status}. Categoria: {category}. Valor: {amount}.",
                ["notifications.payment.bodyUpdated"] = "A {kind} {reference} foi atualizada. Categoria: {category}. Valor: {amount}. Vencimento: {dueDate}.",
            
                ["report.email.healthStatus"] = "Status de saúde",
                ["report.email.healthNeutral"] = "Neutro",
                ["report.email.healthHint"] = "Use este resumo como referência rápida antes de abrir o painel completo.",
                ["report.email.executiveSummary"] = "Resumo executivo",
                ["report.email.atAGlance"] = "Visão geral",
                ["report.email.financial.title"] = "Desempenho financeiro",
                ["report.email.financial.subtitle"] = "Receita, pagamentos e ritmo de cobrança no período selecionado.",
                ["report.email.operations.title"] = "Operação",
                ["report.email.operations.subtitle"] = "Agendamentos, consistência de entrega e fluxo operacional.",
                ["report.email.team.title"] = "Destaques da equipe",
                ["report.email.team.subtitle"] = "Capacidade, produção e indicadores de desempenho dos profissionais.",
                ["report.email.customers.title"] = "Visão do cliente",
                ["report.email.customers.subtitle"] = "Retenção, indicadores de qualidade e sinais de atividade do cliente.",
                ["report.email.whatGoingWell"] = "O que está indo bem",
                ["report.email.pointsAttention"] = "Pontos de atenção",
                ["report.email.recommendedActions"] = "Próximas ações recomendadas",
                ["report.email.openReportCta"] = "Abrir relatório completo no Maids Flow",
                ["report.email.footerMessage"] = "Esta mensagem foi enviada automaticamente pelo Maids Flow para {email}. Você pode disparar o relatório manualmente a qualquer momento ou manter o envio programado no primeiro dia de cada mês.",
                ["report.email.plainCompany"] = "Empresa",
                ["report.email.plainPeriod"] = "Período",
                ["report.email.plainGenerated"] = "Gerado em",
                ["report.email.plainOverview"] = "Visão geral",
                ["report.email.plainFinancial"] = "Financeiro",
                ["report.email.plainOperations"] = "Operação",
                ["report.email.plainTeam"] = "Equipe",
                ["report.email.plainCustomers"] = "Clientes",
            },

            // ============================================================
            // ES — Spanish (neutral LATAM)
            // ============================================================
            [SupportedLanguages.Es] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["shared.greeting.hello"] = "Hola {name}",
                ["shared.signature.team"] = "— Equipo {company}",
                ["shared.signature.maidsflow"] = "— Maids Flow",
                ["shared.button.openApp"] = "Abrir Maids Flow",
                ["shared.if.notYou"] = "Si no fuiste tú quien lo solicitó, contacta al soporte.",

                ["sms.appointmentReminder.body"] =
                    "Hola {customer}, recordatorio: tu servicio \"{title}\" comienza en 30 minutos, a las {time}. Dirección: {address}",
                ["sms.checkoutReminder.body"] =
                    "Hola {customer}, tu servicio \"{title}\" con {company} acaba de finalizar. ¡Gracias por elegirnos!",
                ["sms.onMyWay.body"] =
                    "Hola {customer}, el profesional está en camino y llegará en aproximadamente {minutes} minutos.",
                ["sms.confirmation.body"] =
                    "Hola {customer}, por favor confirma tu cita \"{title}\" el {date} a las {time}. Responde SÍ para confirmar.",
                ["sms.reviewRequest.body"] =
                    "Hola {customer}, ¡gracias por elegir a {company}! ¿Cómo estuvo el servicio? Deja tu reseña: {url}",
                ["sms.paymentDue.body"] =
                    "Hola {customer}, tu pago de {amount} por \"{title}\" vence el {date}. ¡Gracias!",

                ["email.credentials.subject"] = "Bienvenido(a) a {company} en Maids Flow",
                ["email.passwordReset.subject"] = "Restablece tu contraseña de Maids Flow",
                ["email.passwordChanged.subject"] = "Tu contraseña de Maids Flow fue cambiada",
                ["email.planPaymentSuccess.subject"] = "Pago recibido — {plan}",
                ["email.planPaymentFailed.subject"] = "Pago fallido — actualiza tu método de pago",
                ["email.reviewRequest.subject"] = "¿Cómo fue tu servicio con {company}?",
                ["email.companyMonthlyReport.subject"] = "{company} — Informe mensual ({period})",

                ["email.credentials.intro"] =
                    "Te saluda {company}. Estas son tus credenciales de acceso a Maids Flow:",
                ["email.credentials.fields.email"] = "Correo",
                ["email.credentials.fields.password"] = "Contraseña",
                ["email.credentials.fields.role"] = "Rol",
                ["email.credentials.fields.login"] = "Acceso",
                ["email.credentials.cta"] = "Iniciar sesión",

                ["email.passwordReset.intro"] =
                    "Recibimos una solicitud para restablecer tu contraseña de Maids Flow.",
                ["email.passwordReset.cta"] = "Restablecer contraseña",
                ["email.passwordReset.expiry"] = "Este enlace expira en {minutes} minutos.",

                ["email.passwordChanged.intro"] =
                    "Acabas de cambiar tu contraseña de Maids Flow.",
                ["email.passwordChanged.tip"] =
                    "Si no fuiste tú, restablece la contraseña de inmediato y contacta al soporte.",

                ["email.planPaymentSuccess.intro"] =
                    "Recibimos tu pago de {amount} por el plan {plan}. ¡Gracias!",
                ["email.planPaymentSuccess.nextBilling"] = "Próximo cobro: {date}",
                ["email.planPaymentFailed.intro"] =
                    "No pudimos procesar tu pago de {amount} del plan {plan}.",
                ["email.planPaymentFailed.cta"] = "Actualizar método de pago",

                ["email.reviewRequest.intro"] =
                    "Hola {customer}, esperamos que hayas disfrutado el servicio con {company}.",
                ["email.reviewRequest.body"] =
                    "Tu opinión nos ayuda a mejorar. ¿Podrías dejarnos una reseña?",
                ["email.reviewRequest.cta"] = "Dejar reseña",

                ["email.companyMonthlyReport.intro"] =
                    "Aquí está el informe mensual de desempeño de {company} — {period}.",
                ["email.companyMonthlyReport.cta"] = "Ver detalles en la app",
                ["email.companyMonthlyReport.attachmentNote"] =
                    "Adjuntamos un informe detallado en PDF.",

                ["pdf.monthlyReport.title"] = "Informe mensual de desempeño",
                ["pdf.monthlyReport.period"] = "Período",
                ["pdf.monthlyReport.section.summary"] = "Resumen",
                ["pdf.monthlyReport.section.appointments"] = "Citas",
                ["pdf.monthlyReport.section.financial"] = "Financiero",
                ["pdf.monthlyReport.section.team"] = "Desempeño del equipo",
                ["pdf.monthlyReport.kpi.totalAppointments"] = "Total de citas",
                ["pdf.monthlyReport.kpi.completed"] = "Completadas",
                ["pdf.monthlyReport.kpi.cancelled"] = "Canceladas",
                ["pdf.monthlyReport.kpi.revenue"] = "Ingresos",
                ["pdf.monthlyReport.kpi.expenses"] = "Gastos",
                ["pdf.monthlyReport.kpi.netProfit"] = "Beneficio neto",
                ["pdf.monthlyReport.kpi.avgTicket"] = "Ticket promedio",
                ["pdf.monthlyReport.table.headers.professional"] = "Profesional",
                ["pdf.monthlyReport.table.headers.completed"] = "Completadas",
                ["pdf.monthlyReport.table.headers.rating"] = "Calificación promedio",
                ["pdf.monthlyReport.table.headers.revenue"] = "Ingresos",
                ["pdf.monthlyReport.empty"] = "Sin datos para este período.",
                ["pdf.monthlyReport.footer"] =
                    "Generado por Maids Flow el {date}",

                ["push.appointment.assigned.title"] = "Nueva cita asignada",
                ["push.appointment.assigned.body"] =
                    "{title} el {date} a las {time} — {address}",
                ["push.appointment.updated.title"] = "Cita actualizada",
                ["push.appointment.updated.body"] =
                    "{title} fue actualizada. Nuevo horario: {date} a las {time}.",
                ["push.appointment.cancelled.title"] = "Cita cancelada",
                ["push.appointment.cancelled.body"] = "{title} el {date} fue cancelada.",
                ["push.appointment.checkInReminder.title"] = "Hora de hacer check-in",
                ["push.appointment.checkInReminder.body"] =
                    "Tu cita \"{title}\" comienza en {minutes} minutos.",
                ["push.feedback.new.title"] = "Nuevo comentario recibido",
                ["push.feedback.new.body"] = "{name} envió un comentario: {preview}",
                ["push.review.new.title"] = "Nueva reseña",
                ["push.review.new.body"] = "{customer} dejó una reseña de {stars} estrellas.",
                ["push.payment.received.title"] = "Pago recibido",
                ["push.payment.received.body"] = "Pago de {amount} de {customer}.",
            
                ["notifications.appointmentReminder.title"] = "Cita en 30 minutos",
                ["notifications.checkoutReminder.title"] = "Checkout pendiente",
                ["notifications.checkoutReminder.body"] = "{title}: registraste la entrada y aún no la salida. Dirección: {address}. Completa la salida ahora.",
                ["notifications.appointmentDefaultTitle"] = "Tu cita",
                ["notifications.payment.overdue.title"] = "{kind} vencido",
                ["notifications.payment.overdue.body"] = "El {kind} {reference} venció el {date}. Categoría: {category}. Monto: {amount}. {token}",
                ["notifications.payment.dueToday.title"] = "{kind} vence hoy",
                ["notifications.payment.dueToday.body"] = "El {kind} {reference} vence hoy ({date}). Categoría: {category}. Monto: {amount}. {token}",
            
                ["notifications.payment.kind.payable"] = "Cuentas por pagar",
                ["notifications.payment.kind.receivable"] = "Cuentas por cobrar",
                ["notifications.payment.kindLower.payable"] = "cuentas por pagar",
                ["notifications.payment.kindLower.receivable"] = "cuentas por cobrar",
                ["notifications.payment.uncategorized"] = "Sin categoría",
                ["notifications.payment.entryRef"] = "registro #{id}",
                ["notifications.payment.titleUpdated"] = "{kind} actualizadas",
                ["notifications.payment.titleNew"] = "Nuevo registro de {kind}",
                ["notifications.payment.bodyCreated"] = "Se creó un nuevo registro de {kind}: {reference}. Categoría: {category}. Monto: {amount}. Vencimiento: {dueDate}.",
                ["notifications.payment.bodyStatusUpdated"] = "El registro de {kind} {reference} cambió al estado {status}. Categoría: {category}. Monto: {amount}.",
                ["notifications.payment.bodyUpdated"] = "El registro de {kind} {reference} fue actualizado. Categoría: {category}. Monto: {amount}. Vencimiento: {dueDate}.",
            
                ["report.email.healthStatus"] = "Estado de salud",
                ["report.email.healthNeutral"] = "Neutral",
                ["report.email.healthHint"] = "Usa este resumen como referencia rápida antes de abrir el panel completo.",
                ["report.email.executiveSummary"] = "Resumen ejecutivo",
                ["report.email.atAGlance"] = "Vista general",
                ["report.email.financial.title"] = "Desempeño financiero",
                ["report.email.financial.subtitle"] = "Ingresos, pagos y ritmo de facturación del período seleccionado.",
                ["report.email.operations.title"] = "Operaciones",
                ["report.email.operations.subtitle"] = "Citas, consistencia de entrega y flujo operativo.",
                ["report.email.team.title"] = "Aspectos del equipo",
                ["report.email.team.subtitle"] = "Capacidad, producción e indicadores de desempeño de los profesionales.",
                ["report.email.customers.title"] = "Vista del cliente",
                ["report.email.customers.subtitle"] = "Retención, indicadores de calidad y señales de actividad del cliente.",
                ["report.email.whatGoingWell"] = "Lo que va bien",
                ["report.email.pointsAttention"] = "Puntos de atención",
                ["report.email.recommendedActions"] = "Próximas acciones recomendadas",
                ["report.email.openReportCta"] = "Abrir informe completo en Maids Flow",
                ["report.email.footerMessage"] = "Este mensaje fue enviado automáticamente por Maids Flow a {email}. Puedes generar el informe manualmente cuando quieras o mantener el envío programado el primer día de cada mes.",
                ["report.email.plainCompany"] = "Empresa",
                ["report.email.plainPeriod"] = "Período",
                ["report.email.plainGenerated"] = "Generado el",
                ["report.email.plainOverview"] = "Vista general",
                ["report.email.plainFinancial"] = "Financiero",
                ["report.email.plainOperations"] = "Operaciones",
                ["report.email.plainTeam"] = "Equipo",
                ["report.email.plainCustomers"] = "Clientes",
            },

            // ============================================================
            // FR — French (France)
            // ============================================================
            [SupportedLanguages.Fr] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["shared.greeting.hello"] = "Bonjour {name}",
                ["shared.signature.team"] = "— L'équipe {company}",
                ["shared.signature.maidsflow"] = "— Maids Flow",
                ["shared.button.openApp"] = "Ouvrir Maids Flow",
                ["shared.if.notYou"] = "Si vous n'êtes pas à l'origine de cette demande, contactez le support.",

                ["sms.appointmentReminder.body"] =
                    "Bonjour {customer}, rappel : votre prestation \"{title}\" commence dans 30 minutes, à {time}. Adresse : {address}",
                ["sms.checkoutReminder.body"] =
                    "Bonjour {customer}, votre prestation \"{title}\" avec {company} vient de se terminer. Merci de nous avoir choisis !",
                ["sms.onMyWay.body"] =
                    "Bonjour {customer}, le professionnel est en route et arrivera dans environ {minutes} minutes.",
                ["sms.confirmation.body"] =
                    "Bonjour {customer}, merci de confirmer votre rendez-vous \"{title}\" le {date} à {time}. Répondez OUI pour confirmer.",
                ["sms.reviewRequest.body"] =
                    "Bonjour {customer}, merci d'avoir choisi {company} ! Comment s'est passée la prestation ? Laissez un avis : {url}",
                ["sms.paymentDue.body"] =
                    "Bonjour {customer}, votre paiement de {amount} pour \"{title}\" est dû le {date}. Merci !",

                ["email.credentials.subject"] = "Bienvenue chez {company} sur Maids Flow",
                ["email.passwordReset.subject"] = "Réinitialiser votre mot de passe Maids Flow",
                ["email.passwordChanged.subject"] = "Votre mot de passe Maids Flow a été modifié",
                ["email.planPaymentSuccess.subject"] = "Paiement reçu — {plan}",
                ["email.planPaymentFailed.subject"] = "Échec du paiement — mettez à jour votre moyen de paiement",
                ["email.reviewRequest.subject"] = "Comment s'est passée votre prestation avec {company} ?",
                ["email.companyMonthlyReport.subject"] = "{company} — Rapport mensuel ({period})",

                ["email.credentials.intro"] =
                    "Ici {company}. Voici vos identifiants Maids Flow :",
                ["email.credentials.fields.email"] = "E-mail",
                ["email.credentials.fields.password"] = "Mot de passe",
                ["email.credentials.fields.role"] = "Rôle",
                ["email.credentials.fields.login"] = "Connexion",
                ["email.credentials.cta"] = "Se connecter",

                ["email.passwordReset.intro"] =
                    "Nous avons reçu une demande de réinitialisation de votre mot de passe Maids Flow.",
                ["email.passwordReset.cta"] = "Réinitialiser le mot de passe",
                ["email.passwordReset.expiry"] = "Ce lien expire dans {minutes} minutes.",

                ["email.passwordChanged.intro"] =
                    "Votre mot de passe Maids Flow vient d'être modifié.",
                ["email.passwordChanged.tip"] =
                    "Si vous n'êtes pas à l'origine de ce changement, réinitialisez le mot de passe immédiatement et contactez le support.",

                ["email.planPaymentSuccess.intro"] =
                    "Nous avons reçu votre paiement de {amount} pour le plan {plan}. Merci !",
                ["email.planPaymentSuccess.nextBilling"] = "Prochaine facturation : {date}",
                ["email.planPaymentFailed.intro"] =
                    "Nous n'avons pas pu traiter votre paiement de {amount} pour le plan {plan}.",
                ["email.planPaymentFailed.cta"] = "Mettre à jour le moyen de paiement",

                ["email.reviewRequest.intro"] =
                    "Bonjour {customer}, nous espérons que la prestation avec {company} vous a plu !",
                ["email.reviewRequest.body"] =
                    "Votre avis nous aide à nous améliorer. Pourriez-vous prendre une minute pour laisser un avis ?",
                ["email.reviewRequest.cta"] = "Laisser un avis",

                ["email.companyMonthlyReport.intro"] =
                    "Voici le rapport mensuel de performance de {company} — {period}.",
                ["email.companyMonthlyReport.cta"] = "Voir les détails dans l'app",
                ["email.companyMonthlyReport.attachmentNote"] =
                    "Un rapport détaillé en PDF est joint.",

                ["pdf.monthlyReport.title"] = "Rapport mensuel de performance",
                ["pdf.monthlyReport.period"] = "Période",
                ["pdf.monthlyReport.section.summary"] = "Résumé",
                ["pdf.monthlyReport.section.appointments"] = "Rendez-vous",
                ["pdf.monthlyReport.section.financial"] = "Finances",
                ["pdf.monthlyReport.section.team"] = "Performance de l'équipe",
                ["pdf.monthlyReport.kpi.totalAppointments"] = "Total de rendez-vous",
                ["pdf.monthlyReport.kpi.completed"] = "Terminés",
                ["pdf.monthlyReport.kpi.cancelled"] = "Annulés",
                ["pdf.monthlyReport.kpi.revenue"] = "Revenu",
                ["pdf.monthlyReport.kpi.expenses"] = "Dépenses",
                ["pdf.monthlyReport.kpi.netProfit"] = "Bénéfice net",
                ["pdf.monthlyReport.kpi.avgTicket"] = "Ticket moyen",
                ["pdf.monthlyReport.table.headers.professional"] = "Professionnel",
                ["pdf.monthlyReport.table.headers.completed"] = "Terminés",
                ["pdf.monthlyReport.table.headers.rating"] = "Note moyenne",
                ["pdf.monthlyReport.table.headers.revenue"] = "Revenu",
                ["pdf.monthlyReport.empty"] = "Aucune donnée pour cette période.",
                ["pdf.monthlyReport.footer"] =
                    "Généré par Maids Flow le {date}",

                ["push.appointment.assigned.title"] = "Nouveau rendez-vous assigné",
                ["push.appointment.assigned.body"] =
                    "{title} le {date} à {time} — {address}",
                ["push.appointment.updated.title"] = "Rendez-vous mis à jour",
                ["push.appointment.updated.body"] =
                    "{title} a été mis à jour. Nouvel horaire : {date} à {time}.",
                ["push.appointment.cancelled.title"] = "Rendez-vous annulé",
                ["push.appointment.cancelled.body"] = "{title} le {date} a été annulé.",
                ["push.appointment.checkInReminder.title"] = "Heure de pointer l'arrivée",
                ["push.appointment.checkInReminder.body"] =
                    "Votre rendez-vous \"{title}\" commence dans {minutes} minutes.",
                ["push.feedback.new.title"] = "Nouveau commentaire reçu",
                ["push.feedback.new.body"] = "{name} a envoyé un commentaire : {preview}",
                ["push.review.new.title"] = "Nouvel avis",
                ["push.review.new.body"] = "{customer} a laissé un avis {stars} étoiles.",
                ["push.payment.received.title"] = "Paiement reçu",
                ["push.payment.received.body"] = "Paiement de {amount} de {customer}.",
            
                ["notifications.appointmentReminder.title"] = "Rendez-vous dans 30 minutes",
                ["notifications.checkoutReminder.title"] = "Pointage de sortie en attente",
                ["notifications.checkoutReminder.body"] = "{title} : vous avez pointé l'arrivée et n'avez pas encore pointé la sortie. Adresse : {address}. Veuillez pointer la sortie maintenant.",
                ["notifications.appointmentDefaultTitle"] = "Votre rendez-vous",
                ["notifications.payment.overdue.title"] = "{kind} en retard",
                ["notifications.payment.overdue.body"] = "Le {kind} {reference} est en retard depuis le {date}. Catégorie : {category}. Montant : {amount}. {token}",
                ["notifications.payment.dueToday.title"] = "{kind} dû aujourd'hui",
                ["notifications.payment.dueToday.body"] = "Le {kind} {reference} est dû aujourd'hui ({date}). Catégorie : {category}. Montant : {amount}. {token}",
            
                ["notifications.payment.kind.payable"] = "Comptes fournisseurs",
                ["notifications.payment.kind.receivable"] = "Comptes clients",
                ["notifications.payment.kindLower.payable"] = "comptes fournisseurs",
                ["notifications.payment.kindLower.receivable"] = "comptes clients",
                ["notifications.payment.uncategorized"] = "Sans catégorie",
                ["notifications.payment.entryRef"] = "écriture n°{id}",
                ["notifications.payment.titleUpdated"] = "{kind} mis à jour",
                ["notifications.payment.titleNew"] = "Nouvelle écriture {kind}",
                ["notifications.payment.bodyCreated"] = "Une nouvelle écriture {kind} a été créée : {reference}. Catégorie : {category}. Montant : {amount}. Échéance : {dueDate}.",
                ["notifications.payment.bodyStatusUpdated"] = "L'écriture {kind} {reference} est passée au statut {status}. Catégorie : {category}. Montant : {amount}.",
                ["notifications.payment.bodyUpdated"] = "L'écriture {kind} {reference} a été mise à jour. Catégorie : {category}. Montant : {amount}. Échéance : {dueDate}.",
            
                ["report.email.healthStatus"] = "État de santé",
                ["report.email.healthNeutral"] = "Neutre",
                ["report.email.healthHint"] = "Utilisez ce résumé comme référence rapide avant d'ouvrir le tableau de bord complet.",
                ["report.email.executiveSummary"] = "Résumé exécutif",
                ["report.email.atAGlance"] = "En un coup d'œil",
                ["report.email.financial.title"] = "Performance financière",
                ["report.email.financial.subtitle"] = "Revenus, paiements et rythme de facturation pour la période sélectionnée.",
                ["report.email.operations.title"] = "Opérations",
                ["report.email.operations.subtitle"] = "Rendez-vous, régularité de livraison et flux opérationnel.",
                ["report.email.team.title"] = "Faits saillants de l'équipe",
                ["report.email.team.subtitle"] = "Capacité, production et indicateurs de performance des professionnels.",
                ["report.email.customers.title"] = "Vue client",
                ["report.email.customers.subtitle"] = "Fidélisation, indicateurs de qualité et signaux d'activité client.",
                ["report.email.whatGoingWell"] = "Ce qui va bien",
                ["report.email.pointsAttention"] = "Points d'attention",
                ["report.email.recommendedActions"] = "Prochaines actions recommandées",
                ["report.email.openReportCta"] = "Ouvrir le rapport complet dans Maids Flow",
                ["report.email.footerMessage"] = "Ce message a été envoyé automatiquement par Maids Flow à {email}. Vous pouvez déclencher ce rapport manuellement à tout moment ou conserver l'envoi programmé le premier jour de chaque mois.",
                ["report.email.plainCompany"] = "Entreprise",
                ["report.email.plainPeriod"] = "Période",
                ["report.email.plainGenerated"] = "Généré le",
                ["report.email.plainOverview"] = "Aperçu",
                ["report.email.plainFinancial"] = "Finances",
                ["report.email.plainOperations"] = "Opérations",
                ["report.email.plainTeam"] = "Équipe",
                ["report.email.plainCustomers"] = "Clients",
            },
        };
}
