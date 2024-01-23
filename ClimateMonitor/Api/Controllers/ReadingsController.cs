using Microsoft.AspNetCore.Mvc;
using ClimateMonitor.Services;
using ClimateMonitor.Services.Models;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Connections.Features;

namespace ClimateMonitor.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class ReadingsController : ControllerBase
{
    private readonly DeviceSecretValidatorService _secretValidator;
    private readonly AlertService _alertService;

    public ReadingsController(
        DeviceSecretValidatorService secretValidator, 
        AlertService alertService)
    {
        _secretValidator = secretValidator;
        _alertService = alertService;
    }

    /// <summary>
    /// Evaluate a sensor readings from a device and return possible alerts.
    /// </summary>
    /// <remarks>
    /// The endpoint receives sensor readings (temperature, humidity) values
    /// as well as some extra metadata (firmwareVersion), evaluates the values
    /// and generate the possible alerts the values can raise.
    /// 
    /// There are old device out there, and if they get a firmwareVersion 
    /// format error they will request a firmware update to another service.
    /// </remarks>
    /// <param name="deviceSecret">A unique identifier on the device included in the header(x-device-shared-secret).</param>
    /// <param name="deviceReadingRequest">Sensor information and extra metadata from device.</param>
    [HttpPost("evaluate")]
    public ActionResult<IEnumerable<Alert>> EvaluateReading(
        [FromBody] DeviceReadingRequest deviceReadingRequest)
    {
        string deviceSecret = Request.Headers["x-device-shared-secret"];

        if (!_secretValidator.ValidateDeviceSecret(deviceSecret))
        {
            return Problem(
                detail: "Device secret is not within the valid range.",
                statusCode: StatusCodes.Status401Unauthorized);
        }
        string pattern = "^(?<major>0|[1-9]\\d*)\\.(?<minor>0|[1-9]\\d*)\\.(?<patch>0|[1-9]\\d*)(?:-(?<prerelease>(?:0|[1-9]\\d*|\\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\\.(?:0|[1-9]\\d*|\\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\\.[0-9a-zA-Z-]+)*))?$";


        if (Regex.IsMatch(deviceReadingRequest.FirmwareVersion, pattern))
        {
            List<Alert> alerts = new List<Alert>();

            if (deviceReadingRequest.Humidity > 50 || deviceReadingRequest.Humidity < 0)
            {
                alerts.Add(new Alert(AlertType.HumiditySensorOutOfRange, "Invalid range of humidity"));

            }
            if (deviceReadingRequest.Temperature > 50 || deviceReadingRequest.Temperature < 0)
            {
                alerts.Add(new Alert(AlertType.TemperatureSensorOutOfRange, "Invalid range of temperature"));
            }

            return Ok(alerts);
        }
        else
        {
            Dictionary<string, string[]> errors = new Dictionary<string, string[]>();
            string[] errorsfirmware = new string[1];
            errorsfirmware[0] = "The firmware value does not match semantic versioning format.";
            errors["FirmwareVersion"] = errorsfirmware;
            var problemDetails = new { 
                Errors = errors
            };


            return BadRequest(problemDetails);
        }
       

   
    }
}
