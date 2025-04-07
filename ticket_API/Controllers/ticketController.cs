using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Storage.Queues;

namespace ticket_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TicketsController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly ILogger<TicketsController> _log;
        private readonly QueueClient _ticketQueue;

        public TicketsController(IConfiguration config, ILogger<TicketsController> log)
        {
            _config = config;
            _log = log;

            var azureConnStr = _config["AzureStorageConnectionString"];
            if (string.IsNullOrEmpty(azureConnStr))
            {
                _log.LogError("Azure Storage connection string is not configured");
                throw new InvalidOperationException("Azure Storage connection string is missing");
            }

            try
            {
                _ticketQueue = new QueueClient(azureConnStr, "tickethub");
                _log.LogInformation("Queue client initialized successfully");
            }
            catch (Exception err)
            {
                _log.LogError(err, "Error initializing queue client");
                throw;
            }
        }

        [HttpGet]
        public IActionResult GetStatus()
        {
            _log.LogInformation("GET request received for Tickets Controller");
            return Ok("Tickets API is running");
        }

        [HttpPost]
        public async Task<IActionResult> SubmitTicket([FromBody] ticket ticketData)
        {
            if (!ModelState.IsValid)
            {
                _log.LogWarning("Invalid model state for ticket purchase");
                return BadRequest(ModelState);
            }

            try
            {
                string ticketJson = JsonSerializer.Serialize(ticketData);
                var base64Msg = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(ticketJson));

                await _ticketQueue.CreateIfNotExistsAsync();
                await _ticketQueue.SendMessageAsync(base64Msg);

                _log.LogInformation("Ticket purchase for concert {ConcertId} queued successfully", ticketData.ConcertId);

                return Ok(new
                {
                    Message = "Ticket purchase received and queued for processing",
                    ConcertId = ticketData.ConcertId
                });
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Detailed error processing ticket purchase: " +
                    "ConcertId={ConcertId}, " +
                    "Email={Email}, " +
                    "Error Message={ErrorMessage}",
                    ticketData.ConcertId,
                    ticketData.Email,
                    ex.Message
                );

                return StatusCode(500, new
                {
                    Message = "Internal server error occurred while processing your request",
                    ErrorDetails = ex.Message
                });
            }
        }
    }
}
