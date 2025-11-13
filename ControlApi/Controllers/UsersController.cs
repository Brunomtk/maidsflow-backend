using System;
﻿using Core.DTO;
using Core.DTO.User;
using Core.Models;
using Infrastructure.Authenticate;
using Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IJWTManager _jwtManager;
        private readonly IUserService _userService;
        private readonly IConfiguration _configuration;

        public UsersController(IJWTManager jwtManager, IUserService userService, IConfiguration configuration)
        {
            _configuration = configuration;
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

            var token = await _jwtManager.Authenticate(user, login.RememberMe);
            if (token == null) return Unauthorized("Token generation failed");
            if (login.RememberMe)
            {
                var refreshDays = _configuration.GetValue<int?>("Jwt:RememberMeRefreshDays") ?? 30;
                await _userService.UpdateRefreshToken(user.Id, token.RefreshToken, DateTime.UtcNow.AddDays(refreshDays));
            }
            else
            {
                // login sem "lembrar de mim": não persiste refresh token
                await _userService.UpdateRefreshToken(user.Id, null, null);
            }

            var response = new AuthUserResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status,
                Avatar = user.Avatar,
                Token = token.Token,
                RefreshToken = token.RefreshToken,
                Language = user.Language,
                Theme = user.Theme,
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
                Language = request.Language,
                Theme = request.Theme,
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
            if (updated)
            {
                await _userService.UpdateUserPreferences(userId, request.Language, request.Theme);
                return Ok(true);
            }
            return BadRequest("Failed to update user");
        }

        [HttpDelete("{userId}")]
        public async Task<IActionResult> Delete(int userId)
        {
            var deleted = await _userService.DeleteUser(userId);
            return deleted ? Ok(true) : NotFound("User not found");
        }
    
        [AllowAnonymous]
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] TokenJWT body)
        {
            if (string.IsNullOrEmpty(body.RefreshToken)) return BadRequest("Missing refresh token");
            var user = await _userService.GetByRefreshToken(body.RefreshToken);
            if (user == null) return Unauthorized("Invalid refresh token");
            if (user.RefreshTokenExpiresAt == null || user.RefreshTokenExpiresAt < DateTime.UtcNow) return Unauthorized("Refresh token expired");

            var token = await _jwtManager.Authenticate(user, true); // keep remember-style lifespan on refresh
            if (token == null) return Unauthorized("Token generation failed");

            await _userService.UpdateRefreshToken(user.Id, token.RefreshToken, DateTime.UtcNow.AddDays(_configuration.GetValue<int?>("Jwt:RememberMeRefreshDays") ?? 30));

            var response = new AuthUserResponse
            {
                Id = user.Id,
                Name = user.Name,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status,
                Avatar = user.Avatar,
                Token = token.Token,
                RefreshToken = token.RefreshToken,
                CompanyId = user.CompanyId,
                ProfessionalId = user.ProfessionalId,
                Language = user.Language,
                Theme = user.Theme,
                CreatedDate = user.CreatedDate,
                UpdatedDate = user.UpdatedDate
            };
            return Ok(response);
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            // best-effort: clear refresh token of current user if authenticated
            var userIdClaim = User?.Claims?.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var userId))
            {
                await _userService.UpdateRefreshToken(userId, null, null);
                return Ok(true);
            }
            return BadRequest("Unable to resolve current user");
        }
    
    }
}