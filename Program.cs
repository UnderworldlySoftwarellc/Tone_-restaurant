using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors("AllowLocalhost");
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.Combine(Directory.GetCurrentDirectory())),
    RequestPath = string.Empty
});

app.MapPost("/api/send-form", async (HttpRequest request) =>
{
    var form = await request.ReadFormAsync();
    var formName = form["form-name"].ToString();
    var subject = form["subject"].ToString();
    var senderEmail = form["email"].ToString();
    var replyToEmail = !string.IsNullOrWhiteSpace(senderEmail) ? senderEmail : null;
    var targetEmail = GetConfigurationValue(request.HttpContext, "Gmail:TargetEmail") ?? "keira.murray.0316@gmail.com";
    var smtpUsername = GetConfigurationValue(request.HttpContext, "Gmail:Username") ?? "keira.murray.0316@gmail.com";
    var smtpPassword = GetConfigurationValue(request.HttpContext, "Gmail:Password");

    if (string.IsNullOrWhiteSpace(targetEmail) || string.IsNullOrWhiteSpace(smtpUsername) || string.IsNullOrWhiteSpace(smtpPassword))
    {
        return Results.Problem("Email configuration is missing. Set Gmail:Username, Gmail:Password, and Gmail:TargetEmail in appsettings or environment variables.");
    }

    if (string.IsNullOrWhiteSpace(subject))
    {
        subject = string.IsNullOrWhiteSpace(formName)
            ? "New website form submission"
            : $"New {formName} submission";
    }

    var body = BuildMessageBody(form, formName);

    try
    {
        SendEmail(smtpUsername, smtpPassword, targetEmail, subject, body, replyToEmail);
        return Results.Text("Thank you! Your submission has been received and emailed successfully.", "text/plain");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Unable to send email: {ex.Message}");
    }
});

app.Run();

static string GetConfigurationValue(HttpContext context, string key)
{
    return context.RequestServices.GetRequiredService<IConfiguration>()[key]
        ?? Environment.GetEnvironmentVariable(key.Replace(":", "_"));
}

static string BuildMessageBody(IFormCollection form, string formName)
{
    var lines = new List<string>();

    if (!string.IsNullOrWhiteSpace(formName))
    {
        lines.Add($"Form: {formName}");
        lines.Add(string.Empty);
    }

    foreach (var field in form)
    {
        if (string.Equals(field.Key, "subject", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(field.Key, "form-name", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var value = string.Join(", ", field.Value.Where(v => !string.IsNullOrWhiteSpace(v)));
        lines.Add($"{field.Key}: {value}");
    }

    lines.Add(string.Empty);
    lines.Add("---");
    lines.Add("This email was sent from the King Meech website form.");

    return string.Join(Environment.NewLine, lines);
}

static void SendEmail(string username, string password, string recipient, string subject, string body, string? replyToEmail)
{
    using var message = new MailMessage();
    message.From = new MailAddress(username, "King Meech Website");
    message.To.Add(recipient);
    message.Subject = subject;
    message.Body = body;
    message.IsBodyHtml = false;

    if (!string.IsNullOrWhiteSpace(replyToEmail))
    {
        message.ReplyToList.Add(new MailAddress(replyToEmail));
    }

    using var smtpClient = new SmtpClient("smtp.gmail.com", 587)
    {
        Credentials = new NetworkCredential(username, password),
        EnableSsl = true
    };

    smtpClient.Send(message);
}
