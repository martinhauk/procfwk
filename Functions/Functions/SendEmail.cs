using System;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using mrpaulandrew.azure.procfwk.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace mrpaulandrew.azure.procfwk
{
    public class SendEmail
    {
        private readonly ILogger _logger;

        public SendEmail(ILogger<SendEmail> logger)
        {
            _logger = logger;
        }

        [Function("SendEmail")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req)
        {
            _logger.LogInformation("SendEmail Function triggered by HTTP request.");
            #region ParseRequestBody
            _logger.LogInformation("Parsing body from request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string outputString = string.Empty;
            JObject outputJson;

            string toRecipients = data?.emailRecipients;
            string ccRecipients = data?.emailCcRecipients;
            string bccRecipients = data?.emailBccRecipients;
            string subject = data?.emailSubject;
            string message = data?.emailBody;
            string passedImportance = data?.emailImportance ?? ""; //Set normal importance if not provided.            
            #endregion

            #region ValidateRequestBody
            //Check for minimum mailing values in request body
            if (subject == null || message == null)
            {
                _logger.LogInformation("Invalid body - Subject/Body.");

                outputString = "{ \"EmailSent\": false, \"Details\": \"Email subject or body values missing.\"}";
                outputJson = JObject.Parse(outputString);

                return new BadRequestObjectResult(outputJson);
            }

            if (
                (toRecipients == null && ccRecipients == null && bccRecipients == null) ||
                (string.IsNullOrEmpty(toRecipients) && string.IsNullOrEmpty(ccRecipients) && string.IsNullOrEmpty(bccRecipients))
                )
            {
                _logger.LogInformation("Invalid body - To/CC/BCC.");

                outputString = "{ \"EmailSent\": false, \"Details\": \"No email recipients provided as To/CC/BCC.\"}";
                outputJson = JObject.Parse(outputString);

                return new BadRequestObjectResult(outputJson);
            }
            #endregion

            _logger.LogInformation("Creating smtp client.");

            using (var client = SMTPClient.CreateSMTPClient())
            {
                #region CreateMail                
                MailAddress from = new MailAddress(SMTPClient.FromEmail);
                MailMessage mail = new MailMessage
                {
                    From = from,
                    IsBodyHtml = true,
                    Subject = subject,
                    Body = message
                };

                //Set mail importance
                if (passedImportance.ToUpper() == "HIGH")
                {
                    mail.Priority = MailPriority.High;
                }
                else if (passedImportance.ToUpper() == "LOW")
                {
                    mail.Priority = MailPriority.Low;
                }
                else
                {
                    mail.Priority = MailPriority.Normal;
                }
                #endregion

                #region SetRecipients
                //to recipients
                if (!string.IsNullOrEmpty(toRecipients))
                {
                    var allRecipients = toRecipients.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var toAddress in allRecipients)
                    {
                        mail.To.Add(toAddress);
                    }

                    _logger.LogInformation("To Recipients Added: " + allRecipients.Count().ToString());
                }
                else
                {
                    _logger.LogInformation("To Recipients Added: 0");
                }

                //cc recipients
                if (!string.IsNullOrEmpty(ccRecipients))
                {
                    var allCcRecipients = ccRecipients.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var ccAddress in allCcRecipients)
                    {
                        mail.CC.Add(ccAddress);
                    }

                    _logger.LogInformation("CC Recipients Added: " + allCcRecipients.Count().ToString());
                }
                else
                {
                    _logger.LogInformation("CC Recipients Added: 0");
                }

                //bcc recipients
                if (!string.IsNullOrEmpty(bccRecipients))
                {
                    var allBccRecipients = bccRecipients.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var bccAddress in allBccRecipients)
                    {
                        mail.Bcc.Add(bccAddress);
                    }

                    _logger.LogInformation("BCC Recipients Added: " + allBccRecipients.Count().ToString());
                }
                else
                {
                    _logger.LogInformation("BCC Recipients Added: 0");
                }
                #endregion

                #region SendEmail
                try
                {
                    _logger.LogInformation("Sending email.");

                    client.Send(mail);
                    outputString = "{ \"EmailSent\": true }";
                    _logger.LogInformation("Sent email.");
                }
                catch (SmtpException smtpEx)
                {
                    outputString = "{ \"EmailSent\": false, \"Details\": \"SMTP exception caught and logged to error output.\"}";
                    _logger.LogError(smtpEx.Message);
                    _logger.LogInformation("Message has not been sent. Check Azure Function Logs for more information.");
                }
                catch (Exception ex)
                {
                    outputString = "{ \"EmailSent\": false, \"Details\": \"Other exception caught and logged to error output.\"}";
                    _logger.LogError(ex.Message);
                    _logger.LogInformation("Message has not been sent. Check Azure Function Logs for more information.");
                }
                #endregion
            }

            outputJson = JObject.Parse(outputString);
            _logger.LogInformation("SendEmail Function complete.");
            return new OkObjectResult(outputJson);
        }
    }
}
