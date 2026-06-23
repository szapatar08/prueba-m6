using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Api.Controllers;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.Identity.Features.Login;
using Prueba.Modules.Identity.Features.Register;

namespace Prueba.UnitTests.Api.Controllers;

public class AuthControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock;
    private readonly AuthController _controller;
    private readonly Guid _tenantId = Guid.NewGuid();

    public AuthControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _currentTenantMock = new Mock<ICurrentTenant>();
        _currentTenantMock.Setup(x => x.TenantId).Returns(_tenantId);
        _currentTenantMock.Setup(x => x.SchemaName).Returns("public");

        _context = new AppDbContext(options, _currentTenantMock.Object);
        _context.Database.EnsureCreated();
        _repository = new Repository(_context);
        _jwtTokenGeneratorMock = new Mock<IJwtTokenGenerator>();
        _controller = new AuthController(_repository, _currentTenantMock.Object, _jwtTokenGeneratorMock.Object);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturn409()
    {
        // Arrange — seed an existing user
        var existingUser = User.Create("existing@example.com", "hash", "John", "Doe", _tenantId);
        _context.Set<User>().Add(existingUser);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var command = new RegisterCommand(
            Email: "existing@example.com",
            Password: "SecurePass123!",
            FirstName: "Jane",
            LastName: "Smith");

        // Act
        var result = await _controller.Register(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ConflictObjectResult>();
        ((ConflictObjectResult)result).StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task Register_WithValidCommand_ShouldReturn201()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "new@example.com",
            Password: "SecurePass123!",
            FirstName: "Jane",
            LastName: "Smith");

        // Act
        var result = await _controller.Register(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        ((CreatedAtActionResult)result).StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturn401()
    {
        // Arrange
        var command = new LoginCommand(Email: "bad@example.com", Password: "WrongPass123!");

        // Act
        var result = await _controller.Login(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
        ((UnauthorizedObjectResult)result).StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturn200()
    {
        // Arrange
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("SecurePass123!");
        var user = User.Create("user@example.com", passwordHash, "John", "Doe", _tenantId);
        var role = Role.Create("Guest", _tenantId);
        var userRole = UserRole.Create(user.Id, role.Id, _tenantId);

        _context.Set<User>().Add(user);
        _context.Set<Role>().Add(role);
        _context.Set<UserRole>().Add(userRole);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        _jwtTokenGeneratorMock
            .Setup(x => x.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>()))
            .Returns("jwt_token");

        var command = new LoginCommand(Email: "user@example.com", Password: "SecurePass123!");

        // Act
        var result = await _controller.Login(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).StatusCode.Should().Be(200);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
