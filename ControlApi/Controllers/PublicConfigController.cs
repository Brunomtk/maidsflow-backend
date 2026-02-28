using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControlApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PublicConfigController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public PublicConfigController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("maps")]
        [AllowAnonymous]
        public IActionResult GetMapsConfig()
        {
            var apiKey = _configuration["GoogleMaps:ApiKey"];
            var mapId = _configuration["GoogleMaps:MapId"];

            return Ok(new
            {
                googleMapsApiKey = apiKey,
                googleMapsMapId = mapId
            });
        }
    }
}
