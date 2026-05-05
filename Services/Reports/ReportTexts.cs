namespace Services.Reports;

/// <summary>
/// Localized strings for report content (executive summary, KPI cards, narratives,
/// alerts, recommendations, table headers, etc). Mirrors the user's company language
/// (Company.Language) and falls back to English.
///
/// Supported languages: "en" (default), "pt-br" / "pt", "es" / "es-es", "fr" / "fr-fr".
/// </summary>
public static class ReportTexts
{
    private static string Norm(string? lang) => (lang ?? "en").Trim().ToLowerInvariant();

    // ------------------------------------------------------------------
    // Section titles
    // ------------------------------------------------------------------
    public static string SectionFinancial(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Financeiro",
        "es" or "es-es" => "Financiero",
        "fr" or "fr-fr" => "Financier",
        _ => "Financial"
    };
    public static string SectionOperations(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Operações",
        "es" or "es-es" => "Operaciones",
        "fr" or "fr-fr" => "Opérations",
        _ => "Operations"
    };
    public static string SectionTeam(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Equipe",
        "es" or "es-es" => "Equipo",
        "fr" or "fr-fr" => "Équipe",
        _ => "Team"
    };
    public static string SectionCustomers(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Clientes",
        "es" or "es-es" => "Clientes",
        "fr" or "fr-fr" => "Clients",
        _ => "Customers"
    };

    // ------------------------------------------------------------------
    // Executive Summary
    // ------------------------------------------------------------------
    public static string ExecutiveSummaryHeadline(string l, string companyName) => Norm(l) switch
    {
        "pt-br" or "pt" => $"Resumo Executivo — {companyName}",
        "es" or "es-es" => $"Resumen Ejecutivo — {companyName}",
        "fr" or "fr-fr" => $"Résumé exécutif — {companyName}",
        _ => $"Executive Summary — {companyName}"
    };
    public static string ExecutiveSummaryHeadlinePlatform(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Resumo Executivo — Plataforma",
        "es" or "es-es" => "Resumen Ejecutivo — Plataforma",
        "fr" or "fr-fr" => "Résumé exécutif — Plateforme",
        _ => "Executive Summary — Platform"
    };

    public static string ExecutiveNarrativeCompany(string l, string appts, string revenue, string completion, string cancellation, string recurring) => Norm(l) switch
    {
        "pt-br" or "pt" => $"Durante o período analisado, a empresa processou {appts} agendamentos e {revenue} em receita coletada. As operações fecharam com uma taxa de conclusão de {completion}, taxa de cancelamento de {cancellation} e participação de recorrências de {recurring}.",
        "es" or "es-es" => $"Durante el período analizado, la empresa procesó {appts} citas y {revenue} en ingresos cobrados. Las operaciones cerraron con una tasa de finalización de {completion}, una tasa de cancelación de {cancellation} y una participación recurrente de {recurring}.",
        "fr" or "fr-fr" => $"Pendant la période analysée, l'entreprise a traité {appts} rendez-vous et {revenue} de revenus collectés. Les opérations se sont terminées avec un taux de réalisation de {completion}, un taux d'annulation de {cancellation} et une part récurrente de {recurring}.",
        _ => $"During the analyzed period, the company processed {appts} appointments and {revenue} in collected revenue. Operations closed with a completion rate of {completion}, a cancellation rate of {cancellation}, and a recurring share of {recurring}."
    };

    public static string ExecutiveNarrativePlatform(string l, string appts, string revenue, string collection) => Norm(l) switch
    {
        "pt-br" or "pt" => $"Durante o período analisado, a plataforma processou {appts} agendamentos e {revenue} em receita coletada, com eficiência de cobrança de {collection}.",
        "es" or "es-es" => $"Durante el período analizado, la plataforma procesó {appts} citas y {revenue} en ingresos cobrados, con una eficiencia de cobro de {collection}.",
        "fr" or "fr-fr" => $"Pendant la période analysée, la plateforme a traité {appts} rendez-vous et {revenue} de revenus collectés, avec une efficacité de recouvrement de {collection}.",
        _ => $"During the analyzed period, the platform processed {appts} appointments and {revenue} in collected revenue, with a collection efficiency of {collection}."
    };

    // ------------------------------------------------------------------
    // Overview / KPI labels + descriptions  (the tiles on the company report page)
    // ------------------------------------------------------------------
    public static (string label, string description) AppointmentsInPeriod(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Agendamentos no período", "Volume total de agendamentos no período selecionado."),
        "es" or "es-es" => ("Citas en el período", "Volumen total de citas dentro del período seleccionado."),
        "fr" or "fr-fr" => ("Rendez-vous dans la période", "Volume total de rendez-vous dans la période sélectionnée."),
        _ => ("Appointments in period", "Total appointment volume within the selected period.")
    };
    public static (string label, string description) CompletionRate(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Taxa de conclusão", "Percentual de agendamentos concluídos sobre o total do período."),
        "es" or "es-es" => ("Tasa de finalización", "Porcentaje de citas completadas sobre el total del período."),
        "fr" or "fr-fr" => ("Taux de réalisation", "Pourcentage de rendez-vous terminés sur le total de la période."),
        _ => ("Completion rate", "Percentage of completed appointments out of the total for the period.")
    };
    public static (string label, string description) RevenueCollected(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Receita coletada", "Apenas pagamentos marcados como pagos no período selecionado."),
        "es" or "es-es" => ("Ingresos cobrados", "Solo pagos marcados como pagados dentro del período seleccionado."),
        "fr" or "fr-fr" => ("Revenus collectés", "Uniquement les paiements marqués comme payés dans la période sélectionnée."),
        _ => ("Revenue collected", "Only payments marked as paid within the selected period.")
    };
    public static (string label, string description) ActiveCustomers(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Clientes ativos", "Clientes com pelo menos um agendamento no período."),
        "es" or "es-es" => ("Clientes activos", "Clientes con al menos una cita en el período."),
        "fr" or "fr-fr" => ("Clients actifs", "Clients avec au moins un rendez-vous pendant la période."),
        _ => ("Active customers", "Customers with at least one appointment in the period.")
    };

    // ------------------------------------------------------------------
    // Financial section
    // ------------------------------------------------------------------
    public static string FinancialSummary(string l, string revenue, string ticket, string collection) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A empresa gerou {revenue} de receita coletada no período, com ticket médio de {ticket} e eficiência de cobrança de {collection} sobre o valor faturado.",
        "es" or "es-es" => $"La empresa generó {revenue} en ingresos cobrados durante el período, con un ticket promedio de {ticket} y una eficiencia de cobro de {collection} sobre el monto facturado.",
        "fr" or "fr-fr" => $"L'entreprise a généré {revenue} de revenus collectés pendant la période, avec un ticket moyen de {ticket} et une efficacité de recouvrement de {collection} sur le montant facturé.",
        _ => $"The company generated {revenue} in collected revenue during the period, with an average ticket of {ticket} and a collection efficiency of {collection} over the billed amount."
    };
    public static string FinHighlightChange(string l, string signedPct) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A receita coletada variou {signedPct} em relação ao período anterior.",
        "es" or "es-es" => $"Los ingresos cobrados variaron {signedPct} en comparación con el período anterior.",
        "fr" or "fr-fr" => $"Les revenus collectés ont varié de {signedPct} par rapport à la période précédente.",
        _ => $"Revenue collected changed {signedPct} compared with the previous period."
    };
    public static string FinHighlightPerCustomer(string l, string amount) => Norm(l) switch
    {
        "pt-br" or "pt" => $"Cada cliente ativo gerou em média {amount} em receita no período analisado.",
        "es" or "es-es" => $"Cada cliente activo generó en promedio {amount} en ingresos durante el período analizado.",
        "fr" or "fr-fr" => $"Chaque client actif a généré en moyenne {amount} de revenus sur la période analysée.",
        _ => $"Each active customer generated an average of {amount} in revenue during the analyzed period."
    };
    public static string FinHighlightOpen(string l, string open, string overdue) => Norm(l) switch
    {
        "pt-br" or "pt" => $"Há {open} ainda em aberto, dos quais {overdue} já estão vencidos.",
        "es" or "es-es" => $"Hay {open} aún abiertos, de los cuales {overdue} ya están vencidos.",
        "fr" or "fr-fr" => $"Il y a {open} encore ouverts, dont {overdue} déjà en retard.",
        _ => $"There is {open} still open, of which {overdue} is already overdue."
    };

    public static (string label, string description) FinRevenueTotal(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Receita coletada", "Pagamentos efetivamente cobrados dentro do período."),
        "es" or "es-es" => ("Ingresos cobrados", "Pagos efectivamente cobrados dentro del período."),
        "fr" or "fr-fr" => ("Revenus collectés", "Paiements effectivement perçus dans la période."),
        _ => ("Revenue collected", "Payments effectively collected within the selected period.")
    };
    public static (string label, string description) FinReceivable(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Saldo em aberto", "Soma de pagamentos pendentes e vencidos."),
        "es" or "es-es" => ("Saldo abierto", "Suma de pagos pendientes y vencidos."),
        "fr" or "fr-fr" => ("Solde ouvert", "Somme des paiements en attente et en retard."),
        _ => ("Open balance", "Sum of pending and overdue payments.")
    };
    public static (string label, string description) FinAverageTicket(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Ticket médio", "Receita coletada dividida pelo total de agendamentos."),
        "es" or "es-es" => ("Ticket promedio", "Ingresos cobrados divididos por el total de citas."),
        "fr" or "fr-fr" => ("Panier moyen", "Revenus collectés divisés par le total des rendez-vous."),
        _ => ("Average ticket", "Revenue collected divided by the total number of appointments.")
    };
    public static (string label, string description) FinCollectionRate(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Eficiência de cobrança", "Percentual do faturado já marcado como pago no período."),
        "es" or "es-es" => ("Eficiencia de cobro", "Porcentaje del monto facturado ya marcado como pagado en el período."),
        "fr" or "fr-fr" => ("Efficacité de recouvrement", "Pourcentage du montant facturé déjà marqué comme payé."),
        _ => ("Collection efficiency", "Percentage of the billed amount in the period already marked as paid.")
    };

    public static (string label, string description) FinRevenuePerActiveCustomer(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Receita por cliente ativo", "Receita média gerada por cliente ativo no período."),
        "es" or "es-es" => ("Ingresos por cliente activo", "Ingresos promedio generados por cliente activo en el período."),
        "fr" or "fr-fr" => ("Revenu par client actif", "Revenu moyen généré par client actif pendant la période."),
        _ => ("Revenue per active customer", "Average revenue generated per active customer during the period.")
    };
    public static (string label, string description) FinRevenuePerDay(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Receita por dia", "Receita diária média coletada."),
        "es" or "es-es" => ("Ingresos por día", "Ingresos diarios promedio cobrados."),
        "fr" or "fr-fr" => ("Revenu par jour", "Revenu quotidien moyen collecté."),
        _ => ("Revenue per day", "Average daily collected revenue.")
    };
    public static (string label, string description) FinOpenVsBilled(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Saldo em aberto vs. faturado", "Participação do saldo em aberto sobre o valor faturado no período."),
        "es" or "es-es" => ("Saldo abierto vs. facturado", "Participación del saldo abierto sobre el monto facturado en el período."),
        "fr" or "fr-fr" => ("Solde ouvert vs. facturé", "Part du solde ouvert dans le montant facturé sur la période."),
        _ => ("Open balance vs. billed amount", "Share of the open balance within the billed amount for the period.")
    };

    public static string AlertOverdue(string l, string amount) => Norm(l) switch
    {
        "pt-br" or "pt" => $"Saldo vencido identificado: {amount}.",
        "es" or "es-es" => $"Saldo vencido identificado: {amount}.",
        "fr" or "fr-fr" => $"Solde en retard identifié : {amount}.",
        _ => $"Overdue balance identified: {amount}."
    };
    public static string AlertCollection(string l, string pct) => Norm(l) switch
    {
        "pt-br" or "pt" => $"Eficiência de cobrança abaixo do ideal: {pct}.",
        "es" or "es-es" => $"Eficiencia de cobro por debajo del ideal: {pct}.",
        "fr" or "fr-fr" => $"Efficacité de recouvrement inférieure à l'idéal : {pct}.",
        _ => $"Collection efficiency below the ideal level: {pct}."
    };
    public static string AlertOpenExceeds(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "O saldo em aberto já excede a receita coletada do período.",
        "es" or "es-es" => "El saldo abierto ya excede los ingresos cobrados del período.",
        "fr" or "fr-fr" => "Le solde ouvert dépasse déjà les revenus collectés sur la période.",
        _ => "Open balance already exceeds collected revenue for the period."
    };
    public static string AlertNoTicket(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Não há ticket médio calculável para os filtros selecionados.",
        "es" or "es-es" => "No hay ticket promedio calculable para los datos filtrados.",
        "fr" or "fr-fr" => "Aucun panier moyen calculable pour les données filtrées.",
        _ => "There is no calculable average ticket for the filtered data."
    };

    // ------------------------------------------------------------------
    // Operations section
    // ------------------------------------------------------------------
    public static string OpsSummary(string l, string apptTotal, string daily, string completion, string cancellation) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A operação registrou {apptTotal} agendamentos no período, com média de {daily} por dia, taxa de conclusão de {completion} e taxa de cancelamento de {cancellation}.",
        "es" or "es-es" => $"La operación registró {apptTotal} citas durante el período, con un promedio de {daily} por día, una tasa de finalización de {completion} y una tasa de cancelación de {cancellation}.",
        "fr" or "fr-fr" => $"L'opération a enregistré {apptTotal} rendez-vous pendant la période, avec une moyenne de {daily} par jour, un taux de réalisation de {completion} et un taux d'annulation de {cancellation}.",
        _ => $"The operation recorded {apptTotal} appointments during the period, averaging {daily} per day, with a completion rate of {completion} and a cancellation rate of {cancellation}."
    };
    public static string OpsHighlightChange(string l, string signedPct) => Norm(l) switch
    {
        "pt-br" or "pt" => $"O volume variou {signedPct} em relação ao período anterior.",
        "es" or "es-es" => $"El volumen varió {signedPct} en comparación con el período anterior.",
        "fr" or "fr-fr" => $"Le volume a varié de {signedPct} par rapport à la période précédente.",
        _ => $"The volume changed by {signedPct} compared with the previous period."
    };

    public static (string label, string description) OpsAppointments(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Agendamentos", "Volume operacional total."),
        "es" or "es-es" => ("Citas", "Volumen operativo total."),
        "fr" or "fr-fr" => ("Rendez-vous", "Volume opérationnel total."),
        _ => ("Appointments", "Total operational volume.")
    };
    public static (string label, string description) OpsCompleted(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Concluídos", "Agendamentos concluídos com sucesso."),
        "es" or "es-es" => ("Completados", "Citas completadas con éxito."),
        "fr" or "fr-fr" => ("Terminés", "Rendez-vous terminés avec succès."),
        _ => ("Completed", "Appointments successfully completed.")
    };
    public static (string label, string description) OpsScheduled(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Agendados", "Agendamentos ainda agendados."),
        "es" or "es-es" => ("Programados", "Citas aún programadas."),
        "fr" or "fr-fr" => ("Programmés", "Rendez-vous encore programmés."),
        _ => ("Scheduled", "Appointments still scheduled.")
    };
    public static (string label, string description) OpsCancellationRate(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Taxa de cancelamento", "Participação de cancelamentos sobre o volume do período."),
        "es" or "es-es" => ("Tasa de cancelación", "Participación de cancelaciones sobre el volumen del período."),
        "fr" or "fr-fr" => ("Taux d'annulation", "Part des annulations dans le volume de la période."),
        _ => ("Cancellation rate", "Share of cancellations out of the total volume for the period.")
    };

    public static (string label, string description) OpsDailyAverage(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Média diária de agendamentos", "Volume médio por dia corrido no período."),
        "es" or "es-es" => ("Promedio diario de citas", "Volumen promedio por día calendario en el período."),
        "fr" or "fr-fr" => ("Moyenne quotidienne", "Volume moyen par jour calendaire sur la période."),
        _ => ("Daily average appointments", "Average volume per calendar day in the period.")
    };
    public static (string label, string description) OpsRecurringShare(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Participação de recorrências", "Parcela da agenda gerada por serviços recorrentes."),
        "es" or "es-es" => ("Participación recurrente", "Parte de la agenda generada por servicios recurrentes."),
        "fr" or "fr-fr" => ("Part récurrente", "Part du planning généré par des services récurrents."),
        _ => ("Recurring share", "Portion of the schedule generated by recurring services.")
    };
    public static (string label, string description) OpsApptsPerCustomer(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Agendamentos por cliente ativo", "Intensidade média de agendamentos por cliente ativo."),
        "es" or "es-es" => ("Citas por cliente activo", "Intensidad promedio de citas por cliente activo."),
        "fr" or "fr-fr" => ("Rendez-vous par client actif", "Intensité moyenne de rendez-vous par client actif."),
        _ => ("Appointments per active customer", "Average appointment intensity per active customer.")
    };

    public static string OpsAlertHighCancel(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "O cancelamento está alto para o período analisado.",
        "es" or "es-es" => "La cancelación está alta para el período analizado.",
        "fr" or "fr-fr" => "Les annulations sont élevées pour la période analysée.",
        _ => "Cancellation is high for the analyzed period."
    };
    public static string OpsAlertLowCompletion(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Taxa de conclusão abaixo de 70%, há espaço para ajuste operacional.",
        "es" or "es-es" => "Tasa de finalización por debajo del 70%, hay espacio para ajuste operativo.",
        "fr" or "fr-fr" => "Taux de réalisation inférieur à 70 %, marge d'amélioration opérationnelle.",
        _ => "Completion rate is below 70%, indicating room for operational adjustment."
    };
    public static string OpsAlertLowRecurring(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Participação de recorrências baixa; a agenda depende mais de demanda pontual.",
        "es" or "es-es" => "Participación recurrente baja; la agenda depende más de la demanda puntual.",
        "fr" or "fr-fr" => "Part récurrente faible ; le planning dépend davantage de la demande ponctuelle.",
        _ => "Recurring share is low; the schedule depends more on one-time demand."
    };
    public static string OpsAlertLowDensity(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Densidade operacional por dia está baixa no período filtrado.",
        "es" or "es-es" => "La densidad operativa por día es baja en el período filtrado.",
        "fr" or "fr-fr" => "La densité opérationnelle par jour est faible sur la période filtrée.",
        _ => "Operational density per day is low within the filtered period."
    };

    // ------------------------------------------------------------------
    // Team section
    // ------------------------------------------------------------------
    public static string TeamSummary(string l, string total, string active, string rating) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A equipe possui {total} profissionais cadastrados, sendo {active} ativos. A nota média consolidada foi de {rating}.",
        "es" or "es-es" => $"El equipo cuenta con {total} profesionales registrados, de los cuales {active} están activos. La calificación promedio consolidada fue de {rating}.",
        "fr" or "fr-fr" => $"L'équipe comprend {total} professionnels enregistrés, dont {active} actifs. La note moyenne consolidée est de {rating}.",
        _ => $"The team had {total} registered professionals, of which {active} were active. The consolidated average rating was {rating}."
    };
    public static string TeamHighlightCompletions(string l, string avg) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A média de conclusões por profissional ativo foi de {avg}.",
        "es" or "es-es" => $"El promedio de finalizaciones por profesional activo fue de {avg}.",
        "fr" or "fr-fr" => $"La moyenne de réalisations par professionnel actif est de {avg}.",
        _ => $"The average number of completions per engaged professional was {avg}."
    };
    public static string TeamHighlightAllocation(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Receita estimada por alocação ajuda a identificar concentração operacional na equipe.",
        "es" or "es-es" => "Los ingresos estimados por asignación ayudan a identificar la concentración operativa del equipo.",
        "fr" or "fr-fr" => "Le revenu estimé par affectation aide à identifier la concentration opérationnelle dans l'équipe.",
        _ => "Estimated revenue by allocation helps identify operational concentration within the team."
    };

    public static (string label, string description) TeamActiveProfessionals(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Profissionais ativos", "Profissionais marcados como ativos no cadastro."),
        "es" or "es-es" => ("Profesionales activos", "Profesionales marcados como activos en el registro."),
        "fr" or "fr-fr" => ("Professionnels actifs", "Professionnels marqués actifs dans le registre."),
        _ => ("Active professionals", "Professionals marked as active in the registry.")
    };
    public static (string label, string description) TeamWithSchedule(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Profissionais com agenda", "Profissionais que apareceram em ao menos um agendamento no período."),
        "es" or "es-es" => ("Profesionales con agenda", "Profesionales que aparecieron en al menos una cita durante el período."),
        "fr" or "fr-fr" => ("Professionnels en planning", "Professionnels apparus dans au moins un rendez-vous sur la période."),
        _ => ("Professionals with schedule", "Professionals who appeared in at least one appointment during the period.")
    };
    public static (string label, string description) TeamAverageRating(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Nota média", "Média das avaliações recebidas no período."),
        "es" or "es-es" => ("Calificación promedio", "Promedio de las reseñas recibidas durante el período."),
        "fr" or "fr-fr" => ("Note moyenne", "Moyenne des avis reçus pendant la période."),
        _ => ("Average rating", "Average of the reviews received during the period.")
    };
    public static (string label, string description) TeamCompletionsPerPro(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Conclusões por profissional", "Produtividade média dos profissionais engajados."),
        "es" or "es-es" => ("Finalizaciones por profesional", "Productividad promedio de los profesionales activos."),
        "fr" or "fr-fr" => ("Réalisations / professionnel", "Productivité moyenne des professionnels engagés."),
        _ => ("Completions / professional", "Average productivity of engaged professionals.")
    };
    public static (string label, string description) TeamUtilization(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Utilização da equipe", "Percentual de profissionais cadastrados com agendamentos no período."),
        "es" or "es-es" => ("Utilización del equipo", "Porcentaje de profesionales registrados con citas en el período."),
        "fr" or "fr-fr" => ("Utilisation de l'équipe", "Pourcentage de professionnels enregistrés ayant eu des rendez-vous."),
        _ => ("Team utilization", "Percentage of registered professionals who had appointments during the period.")
    };
    public static (string label, string description) TeamRevenuePerPro(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Receita estimada por profissional", "Média estimada com base na ligação entre agendamentos e clientes pagantes."),
        "es" or "es-es" => ("Ingresos estimados por profesional", "Promedio estimado basado en el vínculo entre citas y clientes pagantes."),
        "fr" or "fr-fr" => ("Revenu estimé par professionnel", "Moyenne estimée basée sur le lien entre rendez-vous et clients payants."),
        _ => ("Estimated revenue per professional", "Estimated average based on the link between appointments and paying customers.")
    };
    public static (string label, string description) TeamLeaderConcentration(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Concentração do líder", "Participação do profissional mais produtivo no total de conclusões."),
        "es" or "es-es" => ("Concentración del líder", "Participación del profesional más productivo en el total de finalizaciones."),
        "fr" or "fr-fr" => ("Concentration du leader", "Part du professionnel le plus productif dans le total des réalisations."),
        _ => ("Leader concentration", "Share of the most productive professional in total completions.")
    };

    public static string TeamAlertIdle(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Parte da equipe cadastrada não apareceu na agenda do período, possível ociosidade ou filtro restritivo.",
        "es" or "es-es" => "Parte del equipo registrado no apareció en la agenda del período; posible ociosidad o filtro restrictivo.",
        "fr" or "fr-fr" => "Une partie de l'équipe enregistrée n'apparaît pas dans le planning ; possible inactivité ou filtre trop restrictif.",
        _ => "Part of the registered team did not appear in the schedule for the period, which may signal idleness or an overly restrictive filter."
    };
    public static string TeamAlertConcentration(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "A produtividade está concentrada em poucos profissionais, aumentando a dependência operacional.",
        "es" or "es-es" => "La productividad se concentra en pocos profesionales, lo que aumenta la dependencia operativa.",
        "fr" or "fr-fr" => "La productivité est concentrée sur quelques professionnels, ce qui accroît la dépendance opérationnelle.",
        _ => "Productivity is concentrated among a few professionals, increasing operational dependency."
    };
    public static string TeamAlertLowRating(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Nota média abaixo de 4.0; investigue feedback e experiência do cliente.",
        "es" or "es-es" => "Calificación promedio inferior a 4.0; investiga el feedback y la experiencia del cliente.",
        "fr" or "fr-fr" => "Note moyenne inférieure à 4,0 ; analysez les retours et l'expérience client.",
        _ => "Average rating is below 4.0; investigate feedback and customer experience."
    };

    // ------------------------------------------------------------------
    // Customers section
    // ------------------------------------------------------------------
    public static string CustomersSummary(string l, string newC, string activeC, string recurringC) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A base analisada teve {newC} novos clientes no período, {activeC} clientes ativos e {recurringC} recorrentes, indicando o nível de retenção e dependência da base atual.",
        "es" or "es-es" => $"La base analizada tuvo {newC} nuevos clientes en el período, {activeC} clientes activos y {recurringC} recurrentes, lo que indica el nivel de retención y dependencia de la base actual.",
        "fr" or "fr-fr" => $"La base analysée a eu {newC} nouveaux clients sur la période, {activeC} clients actifs et {recurringC} récurrents, indiquant le niveau de rétention et la dépendance à la base existante.",
        _ => $"The analyzed base had {newC} new customers in the period, {activeC} active customers, and {recurringC} recurring customers, indicating the current level of retention and dependency on the existing base."
    };
    public static string CustomersHighlightRecurring(string l, string pct) => Norm(l) switch
    {
        "pt-br" or "pt" => $"Clientes recorrentes representaram {pct} dos clientes ativos.",
        "es" or "es-es" => $"Los clientes recurrentes representaron {pct} de los clientes activos.",
        "fr" or "fr-fr" => $"Les clients récurrents représentent {pct} des clients actifs.",
        _ => $"Recurring customers represented {pct} of active customers."
    };
    public static string CustomersHighlightTop5(string l, string pct) => Norm(l) switch
    {
        "pt-br" or "pt" => $"Os 5 maiores clientes concentram {pct} da receita coletada.",
        "es" or "es-es" => $"Los 5 mayores clientes concentran {pct} de los ingresos cobrados.",
        "fr" or "fr-fr" => $"Les 5 plus gros clients représentent {pct} des revenus collectés.",
        _ => $"The top 5 customers account for {pct} of collected revenue."
    };
    public static string CustomersHighlightServed(string l, string total) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A empresa atendeu {total} clientes diferentes no intervalo filtrado.",
        "es" or "es-es" => $"La empresa atendió a {total} clientes diferentes en el intervalo filtrado.",
        "fr" or "fr-fr" => $"L'entreprise a servi {total} clients différents dans la période filtrée.",
        _ => $"The company served {total} different customers in the filtered interval."
    };

    public static (string label, string description) CustNew(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Novos clientes", "Clientes criados dentro do período selecionado."),
        "es" or "es-es" => ("Nuevos clientes", "Clientes creados dentro del período seleccionado."),
        "fr" or "fr-fr" => ("Nouveaux clients", "Clients créés dans la période sélectionnée."),
        _ => ("New customers", "Customers created within the selected period.")
    };
    public static (string label, string description) CustActive(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Clientes ativos", "Clientes com pelo menos um agendamento no período."),
        "es" or "es-es" => ("Clientes activos", "Clientes con al menos una cita en el período."),
        "fr" or "fr-fr" => ("Clients actifs", "Clients avec au moins un rendez-vous pendant la période."),
        _ => ("Active customers", "Customers with at least one appointment in the period.")
    };
    public static (string label, string description) CustRecurring(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Clientes recorrentes", "Clientes com mais de um agendamento no período."),
        "es" or "es-es" => ("Clientes recurrentes", "Clientes con más de una cita en el período."),
        "fr" or "fr-fr" => ("Clients récurrents", "Clients avec plus d'un rendez-vous sur la période."),
        _ => ("Recurring customers", "Customers with more than one appointment in the period.")
    };
    public static (string label, string description) CustRevenuePer(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Receita por cliente ativo", "Receita coletada dividida pelo número de clientes ativos."),
        "es" or "es-es" => ("Ingresos por cliente activo", "Ingresos cobrados divididos por el número de clientes activos."),
        "fr" or "fr-fr" => ("Revenu par client actif", "Revenus collectés divisés par le nombre de clients actifs."),
        _ => ("Revenue per active customer", "Revenue collected divided by active customers.")
    };
    public static (string label, string description) CustNewOverActive(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Novos sobre ativos", "Participação de aquisição recente na base ativa."),
        "es" or "es-es" => ("Nuevos sobre activos", "Participación de la adquisición reciente en la base activa."),
        "fr" or "fr-fr" => ("Nouveaux / actifs", "Part de l'acquisition récente dans la base active."),
        _ => ("New over active", "Share of recent acquisition within the active base.")
    };
    public static (string label, string description) CustRecurrenceShare(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Recorrência da base", "Participação de clientes com serviço repetido."),
        "es" or "es-es" => ("Recurrencia de la base", "Participación de clientes con servicio repetido."),
        "fr" or "fr-fr" => ("Récurrence de la base", "Part de clients avec service répété."),
        _ => ("Base recurrence", "Share of customers with repeat service.")
    };
    public static (string label, string description) CustTop5Avg(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Receita média do top 5", "Ticket médio dos maiores clientes do período."),
        "es" or "es-es" => ("Ingresos promedio del top 5", "Ticket promedio de los principales clientes del período."),
        "fr" or "fr-fr" => ("Revenu moyen du top 5", "Panier moyen des principaux clients de la période."),
        _ => ("Average revenue of top 5", "Average ticket value of the top customers in the period.")
    };

    public static string CustAlertLowRecurrence(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Recorrência baixa em relação à base ativa de clientes.",
        "es" or "es-es" => "Recurrencia baja en relación con la base activa de clientes.",
        "fr" or "fr-fr" => "Récurrence faible par rapport à la base active de clients.",
        _ => "Recurrence is low relative to the active customer base."
    };
    public static string CustAlertConcentration(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Receita concentrada em poucos clientes; atenção ao risco de dependência.",
        "es" or "es-es" => "Ingresos concentrados en pocos clientes; atención al riesgo de dependencia.",
        "fr" or "fr-fr" => "Revenus concentrés sur quelques clients ; attention au risque de dépendance.",
        _ => "Revenue is concentrated among a few customers; watch for dependency risk."
    };
    public static string CustAlertNoNew(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Nenhum cliente novo entrou na base no período filtrado.",
        "es" or "es-es" => "Ningún cliente nuevo ingresó a la base durante el período filtrado.",
        "fr" or "fr-fr" => "Aucun nouveau client n'a rejoint la base durant la période filtrée.",
        _ => "No new customer entered the base during the filtered period."
    };
    // ------------------------------------------------------------------
    // Strengths / Risks / Recommendations (Company Executive Summary)
    // ------------------------------------------------------------------
    public static string StrengthRevenueUp(string l, string signedPct) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A receita coletada manteve trajetória positiva, variando {signedPct} em relação ao período anterior.",
        "es" or "es-es" => $"Los ingresos cobrados mantuvieron una trayectoria positiva, variando {signedPct} en comparación con el período anterior.",
        "fr" or "fr-fr" => $"Les revenus collectés ont conservé une trajectoire positive, variant de {signedPct} par rapport à la période précédente.",
        _ => $"Collected revenue remained on a positive trajectory, changing by {signedPct} compared with the previous period."
    };
    public static string StrengthOpsUp(string l, string signedPct) => Norm(l) switch
    {
        "pt-br" or "pt" => $"O volume operacional cresceu {signedPct} em relação ao período anterior, indicando ganho de tração.",
        "es" or "es-es" => $"El volumen operativo creció {signedPct} en comparación con el período anterior, indicando ganancia de tracción.",
        "fr" or "fr-fr" => $"Le volume opérationnel a augmenté de {signedPct} par rapport à la période précédente, indiquant une dynamique positive.",
        _ => $"Operational volume grew {signedPct} compared with the previous period, indicating gained traction."
    };
    public static string StrengthCompletion(string l, string pct) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A taxa de conclusão atingiu {pct}, demonstrando consistência na execução dos serviços.",
        "es" or "es-es" => $"La tasa de finalización alcanzó {pct}, mostrando consistencia en la ejecución de los servicios.",
        "fr" or "fr-fr" => $"Le taux de réalisation a atteint {pct}, montrant la régularité de l'exécution des services.",
        _ => $"Completion rate reached {pct}, demonstrating consistency in service execution."
    };
    public static string StrengthRetention(string l, string n) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A base de clientes mostra retenção ativa, com {n} clientes recorrentes no período.",
        "es" or "es-es" => $"La base de clientes muestra retención activa, con {n} clientes recurrentes durante el período.",
        "fr" or "fr-fr" => $"La base de clients montre une rétention active, avec {n} clients récurrents sur la période.",
        _ => $"The customer base shows active retention, with {n} recurring customers during the period."
    };

    public static string RiskOverdue(string l, string amount) => Norm(l) switch
    {
        "pt-br" or "pt" => $"Há {amount} em valores vencidos, pressionando o fluxo de caixa e reduzindo a previsibilidade.",
        "es" or "es-es" => $"Hay {amount} en montos vencidos, lo que presiona el flujo de caja y reduce la previsibilidad.",
        "fr" or "fr-fr" => $"Il y a {amount} d'impayés, ce qui pèse sur la trésorerie et réduit la visibilité.",
        _ => $"There is {amount} in overdue amounts, which puts pressure on cash flow and reduces predictability."
    };
    public static string RiskOpenExceedsRevenue(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "O saldo em aberto já excede a receita coletada do período, sinalizando risco de cobrança.",
        "es" or "es-es" => "El saldo abierto ya excede los ingresos cobrados del período, lo que indica riesgo de cobro.",
        "fr" or "fr-fr" => "Le solde ouvert dépasse déjà les revenus collectés sur la période, signalant un risque de recouvrement.",
        _ => "Open balance already exceeds collected revenue for the period, signaling collection risk."
    };
    public static string RiskNoActiveCustomers(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Não houve clientes ativos no período filtrado, o que pode indicar filtro restritivo ou baixa atividade operacional.",
        "es" or "es-es" => "No hubo clientes activos en el período filtrado, lo que puede indicar un filtro restrictivo o baja actividad operativa.",
        "fr" or "fr-fr" => "Aucun client actif sur la période filtrée, ce qui peut indiquer un filtre trop restrictif ou une faible activité.",
        _ => "There were no active customers in the filtered period, which may indicate an overly restrictive filter or low operational activity."
    };

    public static string RecActionTrends(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Use o PDF para comparar receita, cancelamentos e retenção entre períodos e medir tendências, não apenas um retrato isolado.",
        "es" or "es-es" => "Use el PDF para comparar ingresos, cancelaciones y retención entre períodos y medir tendencias, no solo un retrato aislado.",
        "fr" or "fr-fr" => "Utilisez le PDF pour comparer revenus, annulations et rétention entre périodes et mesurer les tendances, pas un instantané isolé.",
        _ => "Use the PDF to compare revenue, cancellations, and retention across upcoming periods and measure trends, not just an isolated snapshot."
    };
    public static string RecActionCollection(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Priorize uma rotina de cobrança para reduzir saldos vencidos e converter o faturado em caixa real.",
        "es" or "es-es" => "Prioriza un flujo de cobranza para reducir saldos vencidos y mejorar la conversión de lo facturado en efectivo.",
        "fr" or "fr-fr" => "Mettez en place un processus de recouvrement pour réduire les impayés et transformer le facturé en trésorerie réelle.",
        _ => "Prioritize a collection workflow to reduce overdue balances and improve the conversion of billed amounts into real cash."
    };
    public static string RecActionCancellations(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Analise os motivos de cancelamento por serviço, cliente e profissional para resolver o gargalo real.",
        "es" or "es-es" => "Analiza los motivos de cancelación por servicio, cliente y profesional para abordar el verdadero cuello de botella.",
        "fr" or "fr-fr" => "Analysez les motifs d'annulation par service, client et professionnel pour traiter le véritable goulot.",
        _ => "Analyze cancellation reasons by service, customer, and professional to address the true bottleneck."
    };
    public static string RecActionRetention(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Crie uma ação de retenção para converter aquisição recente em recorrência real.",
        "es" or "es-es" => "Crea una acción de retención para convertir la adquisición reciente en recurrencia real.",
        "fr" or "fr-fr" => "Mettez en place une action de rétention pour convertir l'acquisition récente en récurrence réelle.",
        _ => "Create a retention action to convert recent acquisition into real recurrence."
    };

    // ------------------------------------------------------------------
    // Tables — Recent transactions / appointments / Customer activity
    // ------------------------------------------------------------------
    public static (string title, string description) TableRecentTransactions(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Transações recentes", "Conjunto detalhado para o PDF, com os recebimentos e cobranças mais recentes do período filtrado."),
        "es" or "es-es" => ("Transacciones recientes", "Conjunto detallado para el PDF, con los cobros y cargos más recientes del período filtrado."),
        "fr" or "fr-fr" => ("Transactions récentes", "Jeu de données détaillé pour le PDF avec les encaissements et facturations récents."),
        _ => ("Recent transactions", "Detailed dataset for the PDF with the most recent receipts and charges in the filtered period.")
    };
    public static (string title, string description) TableRecentAppointments(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Agendamentos recentes", "Conjunto operacional pronto para exportação em PDF, útil para auditoria e revisão detalhada."),
        "es" or "es-es" => ("Citas recientes", "Conjunto operativo listo para exportación en PDF, útil para auditoría y revisión detallada."),
        "fr" or "fr-fr" => ("Rendez-vous récents", "Jeu de données opérationnel prêt pour l'export PDF, utile pour audit et revue détaillée."),
        _ => ("Recent appointments", "Operational dataset ready for PDF export, useful for auditing and detailed service-level review.")
    };
    public static (string title, string description) TableCustomerActivity(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Atividade de clientes", "Tabela detalhada para o PDF com a frequência de agendamentos e a participação de receita por cliente."),
        "es" or "es-es" => ("Actividad de clientes", "Tabla detallada para el PDF con la frecuencia de citas y la participación de ingresos por cliente."),
        "fr" or "fr-fr" => ("Activité clients", "Tableau détaillé pour le PDF avec la fréquence des rendez-vous et la part de revenu par client."),
        _ => ("Customer activity", "Detailed table for the PDF with appointment frequency and revenue share by customer.")
    };

    public static string ColReference(string l) => Norm(l) switch { "pt-br" or "pt" => "Referência", "es" or "es-es" => "Referencia", "fr" or "fr-fr" => "Référence", _ => "Reference" };
    public static string ColCustomer(string l) => Norm(l) switch { "pt-br" or "pt" => "Cliente", "es" or "es-es" => "Cliente", "fr" or "fr-fr" => "Client", _ => "Customer" };
    public static string ColStatus(string l) => Norm(l) switch { "pt-br" or "pt" => "Status", "es" or "es-es" => "Estado", "fr" or "fr-fr" => "Statut", _ => "Status" };
    public static string ColMethod(string l) => Norm(l) switch { "pt-br" or "pt" => "Método", "es" or "es-es" => "Método", "fr" or "fr-fr" => "Mode", _ => "Method" };
    public static string ColAmount(string l) => Norm(l) switch { "pt-br" or "pt" => "Valor", "es" or "es-es" => "Valor", "fr" or "fr-fr" => "Montant", _ => "Amount" };
    public static string ColAppointment(string l) => Norm(l) switch { "pt-br" or "pt" => "Agendamento", "es" or "es-es" => "Cita", "fr" or "fr-fr" => "Rendez-vous", _ => "Appointment" };
    public static string ColService(string l) => Norm(l) switch { "pt-br" or "pt" => "Serviço", "es" or "es-es" => "Servicio", "fr" or "fr-fr" => "Service", _ => "Service" };
    public static string ColTeam(string l) => Norm(l) switch { "pt-br" or "pt" => "Profissionais", "es" or "es-es" => "Profesionales", "fr" or "fr-fr" => "Professionnels", _ => "Professionals" };
    public static string ColAppointments(string l) => Norm(l) switch { "pt-br" or "pt" => "Agendamentos", "es" or "es-es" => "Citas", "fr" or "fr-fr" => "Rendez-vous", _ => "Appointments" };
    public static string ColCompleted(string l) => Norm(l) switch { "pt-br" or "pt" => "Concluídos", "es" or "es-es" => "Completados", "fr" or "fr-fr" => "Terminés", _ => "Completed" };
    public static string ColRevenue(string l) => Norm(l) switch { "pt-br" or "pt" => "Receita", "es" or "es-es" => "Ingresos", "fr" or "fr-fr" => "Revenu", _ => "Revenue" };
    public static string ColProfile(string l) => Norm(l) switch { "pt-br" or "pt" => "Perfil", "es" or "es-es" => "Perfil", "fr" or "fr-fr" => "Profil", _ => "Profile" };
    public static string ColCompany(string l) => Norm(l) switch { "pt-br" or "pt" => "Empresa", "es" or "es-es" => "Empresa", "fr" or "fr-fr" => "Entreprise", _ => "Company" };
    public static string ColCustomers(string l) => Norm(l) switch { "pt-br" or "pt" => "Clientes", "es" or "es-es" => "Clientes", "fr" or "fr-fr" => "Clients", _ => "Customers" };
    public static string ColProfessionals(string l) => Norm(l) switch { "pt-br" or "pt" => "Profissionais", "es" or "es-es" => "Profesionales", "fr" or "fr-fr" => "Professionnels", _ => "Professionals" };

    public static string ValNoCustomer(string l) => Norm(l) switch { "pt-br" or "pt" => "Sem cliente", "es" or "es-es" => "Sin cliente", "fr" or "fr-fr" => "Aucun client", _ => "No customer" };
    public static string ValNoService(string l) => Norm(l) switch { "pt-br" or "pt" => "Sem serviço", "es" or "es-es" => "Sin servicio", "fr" or "fr-fr" => "Aucun service", _ => "No service" };
    public static string ValNotInformed(string l) => Norm(l) switch { "pt-br" or "pt" => "Não informado", "es" or "es-es" => "No informado", "fr" or "fr-fr" => "Non renseigné", _ => "Not informed" };
    public static string ValNotAssigned(string l) => Norm(l) switch { "pt-br" or "pt" => "Sem atribuição", "es" or "es-es" => "Sin asignar", "fr" or "fr-fr" => "Non attribué", _ => "Not assigned" };
    public static string BadgeOneTime(string l) => Norm(l) switch { "pt-br" or "pt" => "Pontual", "es" or "es-es" => "Puntual", "fr" or "fr-fr" => "Ponctuel", _ => "One-time" };
    public static string BadgeRecurring(string l) => Norm(l) switch { "pt-br" or "pt" => "Recorrente", "es" or "es-es" => "Recurrente", "fr" or "fr-fr" => "Récurrent", _ => "Recurring" };
    public static string BadgeAttention(string l) => Norm(l) switch { "pt-br" or "pt" => "Atenção", "es" or "es-es" => "Atención", "fr" or "fr-fr" => "Attention", _ => "Attention" };
    public static string BadgeActive(string l) => Norm(l) switch { "pt-br" or "pt" => "Ativo", "es" or "es-es" => "Activo", "fr" or "fr-fr" => "Actif", _ => "Active" };
    public static string LabelAppointmentsRaw(string l) => Norm(l) switch { "pt-br" or "pt" => "agendamentos", "es" or "es-es" => "citas", "fr" or "fr-fr" => "rendez-vous", _ => "appointments" };
    public static string LabelCompletedRaw(string l) => Norm(l) switch { "pt-br" or "pt" => "concluídos", "es" or "es-es" => "completados", "fr" or "fr-fr" => "terminés", _ => "completed" };
    public static string BadgeRecurringSuffix(string l, int n) => Norm(l) switch { "pt-br" or "pt" => $"{n} recorrentes", "es" or "es-es" => $"{n} recurrentes", "fr" or "fr-fr" => $"{n} récurrents", _ => $"{n} recurring" };

    public static string CompanyFallback(string l, int id) => Norm(l) switch { "pt-br" or "pt" => $"Empresa {id}", "es" or "es-es" => $"Empresa {id}", "fr" or "fr-fr" => $"Entreprise {id}", _ => $"Company {id}" };

    // Period filter labels
    public static string FilterPeriod(string l, string from, string to) => Norm(l) switch
    {
        "pt-br" or "pt" => $"Período: {from} a {to}",
        "es" or "es-es" => $"Período: {from} a {to}",
        "fr" or "fr-fr" => $"Période : {from} à {to}",
        _ => $"Period: {from} to {to}"
    };

    // ------------------------------------------------------------------
    // Status pies (Scheduled / In Progress / Completed / Cancelled)
    // ------------------------------------------------------------------
    public static string StatusScheduled(string l) => Norm(l) switch { "pt-br" or "pt" => "Agendados", "es" or "es-es" => "Programados", "fr" or "fr-fr" => "Programmés", _ => "Scheduled" };
    public static string StatusInProgress(string l) => Norm(l) switch { "pt-br" or "pt" => "Em andamento", "es" or "es-es" => "En curso", "fr" or "fr-fr" => "En cours", _ => "In Progress" };
    public static string StatusCompleted(string l) => Norm(l) switch { "pt-br" or "pt" => "Concluídos", "es" or "es-es" => "Completados", "fr" or "fr-fr" => "Terminés", _ => "Completed" };
    public static string StatusCancelled(string l) => Norm(l) switch { "pt-br" or "pt" => "Cancelados", "es" or "es-es" => "Cancelados", "fr" or "fr-fr" => "Annulés", _ => "Cancelled" };

    // ------------------------------------------------------------------
    // ADMIN — Platform reports
    // ------------------------------------------------------------------
    public static string AdminBilling(string l) => Norm(l) switch { "pt-br" or "pt" => "Faturamento", "es" or "es-es" => "Facturación", "fr" or "fr-fr" => "Facturation", _ => "Billing" };
    public static string AdminSectionCompanies(string l) => Norm(l) switch { "pt-br" or "pt" => "Empresas", "es" or "es-es" => "Empresas", "fr" or "fr-fr" => "Entreprises", _ => "Companies" };

    public static (string label, string description) AdminCompaniesTotal(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Empresas", "Base total de empresas cadastradas."),
        "es" or "es-es" => ("Empresas", "Base total de empresas registradas."),
        "fr" or "fr-fr" => ("Entreprises", "Base totale d'entreprises enregistrées."),
        _ => ("Companies", "Total base of registered companies.")
    };
    public static (string label, string description) AdminCompaniesActive(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Empresas ativas", "Empresas com status ativo."),
        "es" or "es-es" => ("Empresas activas", "Empresas con estado activo."),
        "fr" or "fr-fr" => ("Entreprises actives", "Entreprises au statut actif."),
        _ => ("Active companies", "Companies with active status.")
    };
    public static (string label, string description) AdminApptsTotal(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Agendamentos", "Volume operacional total no período."),
        "es" or "es-es" => ("Citas", "Volumen operativo total en el período."),
        "fr" or "fr-fr" => ("Rendez-vous", "Volume opérationnel total sur la période."),
        _ => ("Appointments", "Total operational volume in the period.")
    };
    public static (string label, string description) AdminRevenuePaid(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Receita coletada", "Receita efetivamente paga durante o período."),
        "es" or "es-es" => ("Ingresos cobrados", "Ingresos efectivamente pagados durante el período."),
        "fr" or "fr-fr" => ("Revenus collectés", "Revenus effectivement payés pendant la période."),
        _ => ("Revenue collected", "Revenue effectively paid during the period.")
    };
    public static (string label, string description) AdminSubsActive(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Assinaturas ativas", "Assinaturas com status ativo."),
        "es" or "es-es" => ("Suscripciones activas", "Suscripciones con estado activo."),
        "fr" or "fr-fr" => ("Abonnements actifs", "Abonnements au statut actif."),
        _ => ("Active subscriptions", "Subscriptions with active status.")
    };
    public static (string label, string description) AdminCompaniesUsage(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Empresas em uso", "Empresas com pelo menos um agendamento no período."),
        "es" or "es-es" => ("Empresas en uso", "Empresas con al menos una cita en el período."),
        "fr" or "fr-fr" => ("Entreprises actives", "Entreprises avec au moins un rendez-vous sur la période."),
        _ => ("Companies with usage", "Companies with at least one appointment in the period.")
    };
    public static (string label, string description) AdminOverdue(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Valor vencido", "Cobranças vencidas durante o período."),
        "es" or "es-es" => ("Monto vencido", "Cargos vencidos durante el período."),
        "fr" or "fr-fr" => ("Montant en retard", "Facturations en retard pendant la période."),
        _ => ("Overdue amount", "Overdue charges during the period.")
    };
    public static (string label, string description) AdminCompletionRate(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Taxa de conclusão", "Agendamentos concluídos sobre o total."),
        "es" or "es-es" => ("Tasa de finalización", "Citas completadas sobre el total."),
        "fr" or "fr-fr" => ("Taux de réalisation", "Rendez-vous terminés sur le total."),
        _ => ("Completion rate", "Completed appointments over total appointments.")
    };
    public static (string label, string description) AdminCustomersTotal(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Clientes", "Clientes totais na base."),
        "es" or "es-es" => ("Clientes", "Clientes totales en la base."),
        "fr" or "fr-fr" => ("Clients", "Clients totaux dans la base."),
        _ => ("Customers", "Total customers in the base.")
    };
    public static (string label, string description) AdminProfessionalsTotal(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Profissionais", "Profissionais totais na base."),
        "es" or "es-es" => ("Profesionales", "Profesionales totales en la base."),
        "fr" or "fr-fr" => ("Professionnels", "Professionnels totaux dans la base."),
        _ => ("Professionals", "Total professionals in the base.")
    };

    public static string AdminBillingSummary(string l, string revenue, string collection, string overdue) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A plataforma registrou {revenue} em receita coletada, com eficiência de cobrança de {collection} e {overdue} em valores vencidos no período filtrado.",
        "es" or "es-es" => $"La plataforma registró {revenue} en ingresos cobrados, con eficiencia de cobro de {collection} y {overdue} en montos vencidos en el período filtrado.",
        "fr" or "fr-fr" => $"La plateforme a enregistré {revenue} de revenus collectés, avec une efficacité de recouvrement de {collection} et {overdue} d'impayés sur la période filtrée.",
        _ => $"The platform recorded {revenue} in collected revenue, with a collection efficiency of {collection} and {overdue} in overdue amounts during the filtered period."
    };
    public static string AdminBillingHighlightSubs(string l, string n) => Norm(l) switch
    {
        "pt-br" or "pt" => $"Há {n} assinaturas ativas na base.",
        "es" or "es-es" => $"Hay {n} suscripciones activas en la base.",
        "fr" or "fr-fr" => $"Il y a {n} abonnements actifs dans la base.",
        _ => $"There are {n} active subscriptions in the base."
    };
    public static string AdminBillingHighlightChange(string l, string signed) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A receita variou {signed} em relação ao período anterior.",
        "es" or "es-es" => $"Los ingresos variaron {signed} en comparación con el período anterior.",
        "fr" or "fr-fr" => $"Le revenu a varié de {signed} par rapport à la période précédente.",
        _ => $"Revenue changed by {signed} compared with the previous period."
    };

    public static (string label, string description) AdminRevenuePerActive(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Receita por empresa ativa", "Monetização média por empresa ativa."),
        "es" or "es-es" => ("Ingresos por empresa activa", "Monetización promedio por empresa activa."),
        "fr" or "fr-fr" => ("Revenu par entreprise active", "Monétisation moyenne par entreprise active."),
        _ => ("Revenue per active company", "Average monetization per active company.")
    };
    public static (string label, string description) AdminUsageBase(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Uso operacional da base", "Percentual da base com atividade operacional no período."),
        "es" or "es-es" => ("Uso operativo de la base", "Porcentaje de la base con actividad operativa en el período."),
        "fr" or "fr-fr" => ("Utilisation opérationnelle", "Pourcentage de la base avec activité sur la période."),
        _ => ("Operational usage of the base", "Percentage of the base with operational activity during the period.")
    };
    public static (string label, string description) AdminOverdueOverBilled(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Vencido sobre faturado", "Participação de saldo vencido no faturado do período."),
        "es" or "es-es" => ("Vencido sobre facturado", "Participación del saldo vencido en lo facturado en el período."),
        "fr" or "fr-fr" => ("Impayés / facturé", "Part des impayés dans le montant facturé."),
        _ => ("Overdue over billed", "Share of overdue balance within the billed amount for the period.")
    };

    public static string AdminOpsSummary(string l, string apptTotal, string completion, string cancellation) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A operação consolidada da plataforma registrou {apptTotal} agendamentos, com taxa de conclusão de {completion} e cancelamento de {cancellation}.",
        "es" or "es-es" => $"La operación consolidada de la plataforma registró {apptTotal} citas, con una tasa de finalización de {completion} y de cancelación de {cancellation}.",
        "fr" or "fr-fr" => $"L'opération consolidée a enregistré {apptTotal} rendez-vous, avec un taux de réalisation de {completion} et d'annulation de {cancellation}.",
        _ => $"The platform's consolidated operation recorded {apptTotal} appointments, with a completion rate of {completion} and a cancellation rate of {cancellation}."
    };
    public static string AdminOpsHighlightBase(string l, string customers, string professionals) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A base total tem {customers} clientes e {professionals} profissionais cadastrados.",
        "es" or "es-es" => $"La base total tiene {customers} clientes y {professionals} profesionales registrados.",
        "fr" or "fr-fr" => $"La base totale compte {customers} clients et {professionals} professionnels enregistrés.",
        _ => $"The total base has {customers} customers and {professionals} registered professionals."
    };
    public static string AdminOpsHighlightChange(string l, string signed) => Norm(l) switch
    {
        "pt-br" or "pt" => $"O volume operacional variou {signed} em relação ao período anterior.",
        "es" or "es-es" => $"El volumen operativo varió {signed} en comparación con el período anterior.",
        "fr" or "fr-fr" => $"Le volume opérationnel a varié de {signed} par rapport à la période précédente.",
        _ => $"Operational volume changed by {signed} compared with the previous period."
    };

    public static string AdminCompaniesSummary(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "O ranking consolidado mostra quais empresas geram receita e volume operacional, ajudando a frente a gerar um PDF executivo com comparação de tenants, concentração de resultado e exposição de risco financeiro.",
        "es" or "es-es" => "El ranking consolidado muestra qué empresas generan ingresos y volumen operativo, ayudando al front a generar un PDF ejecutivo con comparación de tenants, concentración de resultado y exposición de riesgo financiero.",
        "fr" or "fr-fr" => "Le classement consolidé indique quelles entreprises génèrent revenus et volume opérationnel, permettant un PDF exécutif avec comparaison des tenants, concentration des résultats et exposition au risque financier.",
        _ => "The consolidated ranking shows which companies drive revenue and operational volume, helping the front end generate an executive PDF with tenant comparison, result concentration, and financial risk exposure."
    };
    public static string AdminCompaniesHighlightTop5(string l, string pct) => Norm(l) switch
    {
        "pt-br" or "pt" => $"As 5 maiores empresas concentram {pct} da receita coletada.",
        "es" or "es-es" => $"Las 5 empresas más grandes concentran {pct} de los ingresos cobrados.",
        "fr" or "fr-fr" => $"Les 5 plus grandes entreprises représentent {pct} des revenus collectés.",
        _ => $"The top 5 companies account for {pct} of collected revenue."
    };
    public static string AdminCompaniesHighlightAvgVol(string l, string n) => Norm(l) switch
    {
        "pt-br" or "pt" => $"O volume médio operacional foi de {n} agendamentos por empresa ativa.",
        "es" or "es-es" => $"El volumen operativo promedio fue de {n} citas por empresa activa.",
        "fr" or "fr-fr" => $"Le volume opérationnel moyen est de {n} rendez-vous par entreprise active.",
        _ => $"Average operational volume was {n} appointments per active company."
    };
    public static string AdminCompaniesHighlightCombo(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "O ranking combina receita e volume operacional para evitar uma leitura unidimensional.",
        "es" or "es-es" => "El ranking combina ingresos y volumen operativo para evitar una lectura unidimensional.",
        "fr" or "fr-fr" => "Le classement combine revenus et volume opérationnel pour éviter une lecture unidimensionnelle.",
        _ => "The ranking combines revenue and operational volume to avoid a one-dimensional reading."
    };

    public static (string label, string description) AdminAvgRevenueTop5(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Receita média top 5", "Receita média entre os líderes da base."),
        "es" or "es-es" => ("Ingresos promedio top 5", "Ingresos promedio entre los líderes de la base."),
        "fr" or "fr-fr" => ("Revenu moyen top 5", "Revenu moyen parmi les leaders de la base."),
        _ => ("Average revenue top 5", "Average revenue among the leaders of the base.")
    };
    public static (string label, string description) AdminOverallAvgRevenue(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Receita média geral", "Distribuição média de receita por empresa cadastrada."),
        "es" or "es-es" => ("Ingreso medio general", "Distribución promedio de ingresos por empresa registrada."),
        "fr" or "fr-fr" => ("Revenu moyen global", "Distribution moyenne du revenu par entreprise enregistrée."),
        _ => ("Overall average revenue", "Average revenue distribution per registered company.")
    };
    public static (string label, string description) AdminShareActiveCompanies(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Participação de empresas ativas", "Participação das empresas ativas sobre a base total."),
        "es" or "es-es" => ("Participación de empresas activas", "Participación de empresas activas sobre la base total."),
        "fr" or "fr-fr" => ("Part d'entreprises actives", "Part des entreprises actives dans la base totale."),
        _ => ("Share of active companies", "Share of active companies over the total base.")
    };

    public static (string title, string description) AdminCompanyRanking(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => ("Ranking de empresas", "Tabela consolidada para o PDF administrativo e comparação de empresas da plataforma."),
        "es" or "es-es" => ("Ranking de empresas", "Tabla consolidada para el PDF administrativo y comparación de empresas de la plataforma."),
        "fr" or "fr-fr" => ("Classement des entreprises", "Tableau consolidé pour le PDF administratif et la comparaison des entreprises."),
        _ => ("Company ranking", "Consolidated table for the administrative PDF and platform company comparison.")
    };

    // ------------------------------------------------------------------
    // CSV
    // ------------------------------------------------------------------
    public static string CsvHeader(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Seção,Indicador,Valor",
        "es" or "es-es" => "Sección,Indicador,Valor",
        "fr" or "fr-fr" => "Section,Indicateur,Valeur",
        _ => "Section,Indicator,Value"
    };
    public static string CsvSecOverview(string l) => Norm(l) switch { "pt-br" or "pt" => "Visão Geral", "es" or "es-es" => "Resumen", "fr" or "fr-fr" => "Vue d'ensemble", _ => "Overview" };
    public static string CsvSecFinancial(string l) => SectionFinancial(l);
    public static string CsvSecOperations(string l) => SectionOperations(l);
    public static string CsvSecTeam(string l) => SectionTeam(l);
    public static string CsvSecCustomers(string l) => SectionCustomers(l);
    public static string CsvSecBilling(string l) => AdminBilling(l);
    // ------------------------------------------------------------------
    // ADMIN — Strengths / Risks / Recommendations
    // ------------------------------------------------------------------
    public static string AdmStrengthBase(string l, string active, string total) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A plataforma possui {active} empresas ativas dentro de uma base de {total} empresas.",
        "es" or "es-es" => $"La plataforma tiene {active} empresas activas dentro de una base de {total} empresas.",
        "fr" or "fr-fr" => $"La plateforme compte {active} entreprises actives sur une base de {total}.",
        _ => $"The platform has {active} active companies within a base of {total} companies."
    };
    public static string AdmStrengthSubs(string l, string n) => Norm(l) switch
    {
        "pt-br" or "pt" => $"Foram registradas {n} assinaturas ativas, sustentando a visão de monetização da base.",
        "es" or "es-es" => $"Se registraron {n} suscripciones activas, sosteniendo la visión de monetización de la base.",
        "fr" or "fr-fr" => $"On compte {n} abonnements actifs, soutenant la vision de monétisation de la base.",
        _ => $"There were {n} active subscriptions, supporting the monetization view of the base."
    };
    public static string AdmStrengthRevChange(string l, string signed) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A receita coletada variou {signed} em relação ao período anterior.",
        "es" or "es-es" => $"Los ingresos cobrados variaron {signed} en comparación con el período anterior.",
        "fr" or "fr-fr" => $"Les revenus collectés ont varié de {signed} par rapport à la période précédente.",
        _ => $"Collected revenue changed by {signed} compared with the previous period."
    };
    public static string AdmRiskOverdue(string l, string amount) => Norm(l) switch
    {
        "pt-br" or "pt" => $"A base possui {amount} em valores vencidos, exigindo acompanhamento de cobrança.",
        "es" or "es-es" => $"La base mantiene {amount} en montos vencidos, lo que requiere seguimiento de cobranza.",
        "fr" or "fr-fr" => $"La base détient {amount} d'impayés, nécessitant un suivi de recouvrement.",
        _ => $"The base holds {amount} in overdue amounts, which requires collection follow-up."
    };
    public static string AdmRiskCollection(string l, string pct) => Norm(l) switch
    {
        "pt-br" or "pt" => $"Eficiência de cobrança em {pct}, abaixo do ideal para previsibilidade de caixa.",
        "es" or "es-es" => $"Eficiencia de cobro en {pct}, por debajo del ideal para previsibilidad de caja.",
        "fr" or "fr-fr" => $"Efficacité de recouvrement à {pct}, sous le niveau idéal pour la prévisibilité de trésorerie.",
        _ => $"Collection efficiency is at {pct}, below the ideal level for cash-flow predictability."
    };
    public static string AdmRiskVolumeDown(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "O volume operacional caiu em relação ao período anterior e pode indicar uso reduzido em parte da base.",
        "es" or "es-es" => "El volumen operativo disminuyó respecto al período anterior y puede indicar un uso reducido en parte de la base.",
        "fr" or "fr-fr" => "Le volume opérationnel a diminué par rapport à la période précédente, ce qui peut indiquer un usage réduit d'une partie de la base.",
        _ => "Operational volume declined compared with the previous period and may indicate reduced usage across part of the base."
    };
    public static string AdmRecLeading(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Use o PDF administrativo para destacar empresas líderes, risco de inadimplência e densidade de uso da plataforma.",
        "es" or "es-es" => "Use el PDF administrativo para destacar empresas líderes, riesgo de morosidad y densidad de uso de la plataforma.",
        "fr" or "fr-fr" => "Utilisez le PDF administratif pour mettre en valeur les entreprises leaders, le risque d'impayés et la densité d'utilisation.",
        _ => "Use the administrative PDF to highlight leading companies, delinquency risk, and platform usage density."
    };
    public static string AdmRecCrossRef(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Cruze empresas com maior saldo vencido com aquelas de menor uso para identificar churn e risco de cobrança.",
        "es" or "es-es" => "Cruza empresas con mayor saldo vencido con aquellas de menor uso para identificar churn y riesgo de cobranza.",
        "fr" or "fr-fr" => "Croisez les entreprises au plus grand impayé avec celles à faible usage pour identifier churn et risque de recouvrement.",
        _ => "Cross-reference companies with higher overdue balances against those with lower usage to identify churn and collection risk."
    };
    public static string AdmRecMonitor(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "Monitore a evolução da receita por empresa ativa para diferenciar crescimento saudável de concentração excessiva.",
        "es" or "es-es" => "Monitorea la evolución de los ingresos por empresa activa para distinguir crecimiento saludable de concentración excesiva.",
        "fr" or "fr-fr" => "Suivez l'évolution du revenu par entreprise active pour distinguer croissance saine et concentration excessive.",
        _ => "Monitor revenue evolution per active company to distinguish healthy growth from excessive concentration."
    };
    // Inline labels for leaderboard rows (lowercase, used as PrimaryLabel/SecondaryLabel)
    public static string LabelEstimatedRevenue(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "receita estimada",
        "es" or "es-es" => "ingresos estimados",
        "fr" or "fr-fr" => "revenu estimé",
        _ => "estimated revenue"
    };
    public static string LabelRevenue(string l) => Norm(l) switch
    {
        "pt-br" or "pt" => "receita",
        "es" or "es-es" => "ingresos",
        "fr" or "fr-fr" => "revenu",
        _ => "revenue"
    };
    public static string ColDate(string l) => Norm(l) switch { "pt-br" or "pt" => "Data", "es" or "es-es" => "Fecha", "fr" or "fr-fr" => "Date", _ => "Date" };
}
