using Iot.Device.Adc;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Device.Gpio;
using System.Device.Gpio.Drivers;
using System.Device.Spi;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EcoSolve.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild",
            "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private const int SensorPin = 27; // GPIO27 for D0
        private static readonly GpioController _gpio = new GpioController( );
        private static readonly bool _gpioInitialized = true;
        private readonly ILogger<WeatherForecastController> _logger;

        // ����������� ������������� GpioController � LibGpiodDriver
        static WeatherForecastController()
        {
            try
            {
                
               
            }
            catch (Exception ex)
            {
                // ������� ������ ����-����� ��� �������
                Console.Error.WriteLine("������ ������������� GpioController:");
                Console.Error.WriteLine(ex.ToString());
                throw;
            }
        }

        public WeatherForecastController(ILogger<WeatherForecastController> logger)
        {
            _logger = logger;

            if (_gpioInitialized)
            {
                try
                {
                    if (!_gpio.IsPinOpen(SensorPin))
                    {
                        _gpio.OpenPin(SensorPin, PinMode.Input);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "�� ������� ������� GPIO ��� {Pin}", SensorPin);
                }
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

        /// <summary>
        /// ���������� ����� A0 ����� MCP3008 � ��������� ��������� � ���������.
        /// </summary>
        [HttpGet("iot-state")]
        public ActionResult<string> GetIotState()
        {
            try
            {
                // ��������� SPI (SPI0, CE0)
                var spiSettings = new SpiConnectionSettings(0, 0)
                {
                    ClockFrequency = 500_000,
                    Mode = SpiMode.Mode0
                };
                using var spi = SpiDevice.Create(spiSettings);
                using var mcp = new Mcp3008(spi);

                // ������� ��������� � ���������� ��� ���� ������
                const int dryValue = 950;
                const int wetValue = 350;

                int rawValue = mcp.Read(0); // ����� CH0 ? A0 �������
                double percent = (1.0 - (rawValue - wetValue) / (double)(dryValue - wetValue)) * 100;
                percent = Math.Clamp(percent, 0, 100);

                return Ok($"RAW: {rawValue} | ���������: {percent:F1}%");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "������ ��� ������ MCP3008");
                return StatusCode(500, ex.ToString());
            }
        }

        /// <summary>
        /// ���������� ��������� D0 (����/������) � �������� GPIO.
        /// </summary>
        [HttpGet("get-soil-measure")]
        public IActionResult GetSoilMoisture()
        {
            if (!_gpioInitialized)
            {
                return StatusCode(500, new { error = "GPIO �� ���������������" });
            }

            try
            {
                var pinValue = _gpio.Read(SensorPin);
                bool isDry = pinValue == PinValue.High;

                return Ok(new
                {
                    moisture = isDry ? "Dry" : "Wet",
                    gpio = pinValue.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GPIO read failed");
                return StatusCode(500, new
                {
                    error = "GPIO read failed",
                    message = ex.ToString()
                });
            }
        }
    }
}
