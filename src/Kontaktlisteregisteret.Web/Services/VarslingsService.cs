namespace Kontaktlisteregisteret.Web.Services;

public class VarslingsService(IConfiguration config, ILogger<VarslingsService> logger)
{
    public async Task SendLåstVarslingAsync(string adresselisteTittel, int antallMottakere, string? varslingsTil = null)
    {
        var smtpHost = config["Varsling:SmtpHost"];
        var fra = config["Varsling:Fra"];
        var til = varslingsTil ?? config["Varsling:StandardMottaker"];

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(til))
        {
            logger.LogInformation("Varsling er ikke konfigurert — hopper over e-post for '{Tittel}'", adresselisteTittel);
            return;
        }

        try
        {
            using var melding = new System.Net.Mail.MailMessage
            {
                From = new System.Net.Mail.MailAddress(fra ?? "noreply@kontaktliste.no"),
                Subject = $"Adresseliste låst: {adresselisteTittel}",
                Body = $"Adresselisten «{adresselisteTittel}» er nå låst med {antallMottakere} mottakere.\n\nDenne meldingen er sendt automatisk fra Kontaktlisteregisteret.",
                IsBodyHtml = false
            };
            melding.To.Add(til);

            var port = config.GetValue<int>("Varsling:SmtpPort", 25);
            using var smtp = new System.Net.Mail.SmtpClient(smtpHost, port);
            await smtp.SendMailAsync(melding);

            logger.LogInformation("Varsling sendt til {Til} for '{Tittel}'", til, adresselisteTittel);
        }
        catch (Exception ex)
        {
            // Varsling er beste-innsats — ikke la en e-postfeil stoppe låsingen
            logger.LogWarning(ex, "Klarte ikke sende varsling for '{Tittel}'", adresselisteTittel);
        }
    }
}
