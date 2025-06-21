using Iot.Device.Adc;
using Microsoft.AspNetCore.Mvc;
using System.Device.Gpio;
using System.Device.Spi;
using System.Reflection.Metadata.Ecma335;

namespace EcoSolve.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };
        private readonly int sensorPin = 27; // GPIO27
        private static GpioController gpio = new GpioController();
        private readonly ILogger<WeatherForecastController> _logger;


        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;
            gpio = new GpioController(); // или внедри через DI, если используешь

            try
            {
                if (!gpio.IsPinOpen(sensorPin))
                {
                    gpio.OpenPin(sensorPin, PinMode.Input);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при инициализации GPIO");
            }
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
        [HttpGet("iot-state")]
        public async Task<string> GetIotState()
        {
            try
            {
                // Настройка SPI
                var spiConnection = new SpiConnectionSettings(0, 0) // SPI0, CE0
                {
                    ClockFrequency = 500_000,
                    Mode = SpiMode.Mode0
                };

                using SpiDevice spi = SpiDevice.Create(spiConnection);
                using Mcp3008 mcp = new Mcp3008(spi);

                // Границы влажности (калибруй по своим условиям)
                int dryValue = 950;
                int wetValue = 350;

                //while (true)
                {
                    int rawValue = mcp.Read(0); // CH0 — от A0
                    double percent = (1.0 - (rawValue - wetValue) / (double)(dryValue - wetValue)) * 100;
                    percent = Math.Clamp(percent, 0, 100);

                    
                    return $"RAW: {rawValue} | Влажность: {percent:F1}%";
                }
            }
            catch (Exception ex) 
            {
                return ex.Message + " " + ex.StackTrace;
            }
            
            return "";
        }
        [HttpGet("get-soil-measure")]
        public IActionResult GetSoilMoisture()
        {
            try
            {
                var pin = 27;
                if (!gpio.IsPinOpen(pin))
                {
                    gpio.OpenPin(pin, PinMode.Input);
                }

                var value = gpio.Read(pin);
                bool isDry = value == PinValue.High;

                return Ok(new
                {
                    moisture = isDry ? "Dry" : "Wet",
                    gpio = value.ToString()
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "GPIO read failed",
                    message = ex.Message
                });
            }
        }
    }
}
