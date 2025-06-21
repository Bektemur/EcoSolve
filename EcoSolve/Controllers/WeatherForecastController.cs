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

        // Статическая инициализация GpioController с LibGpiodDriver
        static WeatherForecastController()
        {
            try
            {
                
               
            }
            catch (Exception ex)
            {
                // Выводим полный стек-трейс для отладки
                Console.Error.WriteLine("Ошибка инициализации GpioController:");
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
                    _logger.LogError(ex, "Не удалось открыть GPIO пин {Pin}", SensorPin);
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
        /// Возвращает сырой A0 через MCP3008 и вычисляет влажность в процентах.
        /// </summary>
        [HttpGet("iot-state")]
        public ActionResult<string> GetIotState()
        {
            try
            {
                // Настройка SPI (SPI0, CE0)
                var spiSettings = new SpiConnectionSettings(0, 0)
                {
                    ClockFrequency = 500_000,
                    Mode = SpiMode.Mode0
                };
                using var spi = SpiDevice.Create(spiSettings);
                using var mcp = new Mcp3008(spi);

                // Границы влажности — откалибруй под свой датчик
                const int dryValue = 950;
                const int wetValue = 350;

                int rawValue = mcp.Read(0); // канал CH0 ? A0 датчика
                double percent = (1.0 - (rawValue - wetValue) / (double)(dryValue - wetValue)) * 100;
                percent = Math.Clamp(percent, 0, 100);

                return Ok($"RAW: {rawValue} | Влажность: {percent:F1}%");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при чтении MCP3008");
                return StatusCode(500, ex.ToString());
            }
        }

        /// <summary>
        /// Возвращает состояние D0 (сухо/влажно) и значение GPIO.
        /// </summary>
        [HttpGet("get-soil-measure")]
        public IActionResult GetSoilMoisture()
        {
            if (!_gpioInitialized)
            {
                return StatusCode(500, new { error = "GPIO не инициализирован" });
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
