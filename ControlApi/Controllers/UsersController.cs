using Core.DTO;
using Core.DTO.User;
using Core.Models;
using Infrastructure.Authenticate;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IJWTManager _jwtManager;
        private readonly IUserService _userService;

        public UsersController(IJWTManager jwtManager, IUserService userService)
        {
            _jwtManager = jwtManager;
            _userService = userService;
        }

        [AllowAnonymous]
        [HttpPost("authenticate")]
        public async Task<IActionResult> Authenticate([FromBody] LoginRequest login)
        {
            var user = await _userService.GetUserByEmail(login.Email);
            if (user == null || Encrypt.EncryptPassword(login.Password) != user.Password)
                return Unauthorized("Invalid credentials");

            var token = await _jwtManager.Authenticate(user);
            if (token == null) return Unauthorized("Token generation failed");

            var response = new AuthUserResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status,
                Avatar = user.Avatar,
                Token = token.Token,
                CompanyId = user.CompanyId,
                ProfessionalId = user.ProfessionalId,
                CreatedDate = user.CreatedDate,
                UpdatedDate = user.UpdatedDate
            };

            return Ok(response);
        }

        [AllowAnonymous]
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
        {
            var user = new User
            {
                Name = request.Name,
                Email = request.Email,
                Password = request.Password,
                Role = request.Role,
                Status = request.Status,
                CompanyId = request.CompanyId,
                ProfessionalId = request.ProfessionalId,
                CreatedDate = DateTime.UtcNow,
                UpdatedDate = DateTime.UtcNow
            };

            var created = await _userService.CreateUser(user);
            return created ? Ok(true) : BadRequest("Failed to create user");
        }

        [HttpGet("{userId}")]
        public async Task<IActionResult> GetById(int userId)
        {
            var user = await _userService.GetUserById(userId);
            return user != null ? Ok(user) : NotFound("User not found");
        }

        [HttpGet("paged")]
        public async Task<IActionResult> GetAllPaged([FromQuery] FiltersDTO filters)
        {
            var result = await _userService.GetAllUsuariosPaged(filters);

            return Ok(new
            {
                data = result.Results,
                meta = new
                {
                    currentPage = result.CurrentPage,
                    totalPages = result.PageCount,
                    totalItems = result.TotalItems,
                    itemsPerPage = result.PageSize
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userService.GetAllUsers();
            return Ok(users);
        }

        [HttpPut("{userId}")]
        public async Task<IActionResult> Update(int userId, [FromBody] CreateUserRequest request)
        {
            var updated = await _userService.UpdateUser(request, userId);
            return updated ? Ok(true) : BadRequest("Failed to update user");
        }

        [HttpDelete("{userId}")]
        public async Task<IActionResult> Delete(int userId)
        {
            var deleted = await _userService.DeleteUser(userId);
            return deleted ? Ok(true) : NotFound("User not found");
        }
    }
}
