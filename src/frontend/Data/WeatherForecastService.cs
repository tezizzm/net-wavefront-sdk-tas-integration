using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace frontend.Data
{
    public interface IWeatherForecastService
    {
        Task<WeatherForecast[]> GetForecastAsync(DateTime startDate);
    }

    public class WeatherForecastService : IWeatherForecastService
    {
        HttpClient _client;

        public WeatherForecastService(HttpClient client)
        {
            _client = client;
        }

        public async Task<WeatherForecast[]> GetForecastAsync(DateTime startDate)
        {
            var responseMessage = await _client.GetAsync("/weatherforecast");
            return await responseMessage.Content.ReadFromJsonAsync<WeatherForecast[]>();
        }
    }
}
