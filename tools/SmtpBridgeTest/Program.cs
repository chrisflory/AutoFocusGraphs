using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Xml.Linq;

var configPath = args.Length > 0
    ? args[0]
    : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        @"NINA\NINA_Url_ykulh1zxj2m4pcy4p3l2bndsanudr4ly\3.3.0.1048\user.config");

var doc = XDocument.Load(configPath);
var ns = doc.Root!.Name.Namespace;
string Get(string name) =>
    doc.Descendants(ns + "setting")
        .First(s => (string)s.Attribute("name")! == name)
        .Element(ns + "value")?.Value ?? string.Empty;

var host = Get("EmailSmtpHost");
var port = int.Parse(string.IsNullOrWhiteSpace(Get("EmailSmtpPort")) ? "1025" : Get("EmailSmtpPort"));
var user = Get("EmailUsername");
var pass = Get("EmailPassword");
var from = Get("EmailFrom");
var to = Get("EmailTo");

Console.WriteLine($"Host={host}:{port} From={from} To={to} User={user}");

foreach (var mode in new[] { SecureSocketOptions.StartTls, SecureSocketOptions.SslOnConnect, SecureSocketOptions.Auto })
{
    try
    {
        using var client = new SmtpClient();
        client.ServerCertificateValidationCallback = (_, _, _, _) => true;
        await client.ConnectAsync(host, port, mode, CancellationToken.None);
        await client.AuthenticateAsync(user, pass, CancellationToken.None);
        var msg = new MimeMessage();
        msg.From.Add(MailboxAddress.Parse(from));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = "AutoFocusGraphs bridge test";
        msg.Body = new TextPart("plain") { Text = "MailKit test" };
        await client.SendAsync(msg, CancellationToken.None);
        await client.DisconnectAsync(true, CancellationToken.None);
        Console.WriteLine($"SUCCESS with {mode}");
        return;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL {mode}: {ex.GetType().Name}: {ex.Message}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"  inner: {ex.InnerException.Message}");
        }
    }
}

Environment.ExitCode = 1;
