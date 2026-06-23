using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Identity.Entities;
using Prueba.Modules.Identity.Features.Login;

namespace Prueba.UnitTests.Modules.Identity.Features;

public class LoginHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<IJwtTokenGenerator> _jwtTokenGeneratorMock;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly LoginHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public LoginHandlerTests()
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
        _handler = new LoginHandler(_repository, _currentTenantMock.Object, _jwtTokenGeneratorMock.Object);
    }

    private async Task<User> SeedUserAsync(string email = "user@example.com", string password = "SecurePass123!")
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
        var user = User.Create(email, passwordHash, "John", "Doe", _tenantId);
        var guestRole = Role.Create("Guest", _tenantId);
        var userRole = UserRole.Create(user.Id, guestRole.Id, _tenantId);

        _context.Set<User>().Add(user);
        _context.Set<Role>().Add(guestRole);
        _context.Set<UserRole>().Add(userRole);
        await _context.SaveChangesAsync();

        // Detach to avoid tracking conflicts in subsequent queries
        _context.ChangeTracker.Clear();
        return user;
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ShouldReturnToken()
    {
        // Arrange
        await SeedUserAsync();
        _jwtTokenGeneratorMock
            .Setup(x => x.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>()))
            .Returns("valid_jwt_token");

        var command = new LoginCommand(Email: "user@example.com", Password: "SecurePass123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue(result.Error);
        result.Value!.Token.Should().Be("valid_jwt_token");
        result.Value.Email.Should().Be("user@example.com");
        result.Value.Roles.Should().Contain("Guest");
    }

    [Fact]
    public async Task Handle_WithInvalidEmail_ShouldReturnFailure()
    {
        // Arrange
        var command = new LoginCommand(Email: "nonexistent@example.com", Password: "SecurePass123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task Handle_WithInvalidPassword_ShouldReturnFailure()
    {
        // Arrange
        await SeedUserAsync();
        var command = new LoginCommand(Email: "user@example.com", Password: "WrongPassword123!");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task Handle_WithValidCredentials_ShouldGenerateTokenWithCorrectClaims()
    {
        // Arrange
        var user = await SeedUserAsync();
        _jwtTokenGeneratorMock
            .Setup(x => x.GenerateToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<IEnumerable<string>>()))
            .Returns("token");

        var command = new LoginCommand(Email: "user@example.com", Password: "SecurePass123!");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _jwtTokenGeneratorMock.Verify(x => x.GenerateToken(
            user.Id,
            "user@example.com",
            _tenantId,
            It.Is<IEnumerable<string>>(r => r.Contains("Guest"))),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNotRevealWhetherEmailOrPasswordIsWrong()
    {
        // Arrange
        await SeedUserAsync();
        var commandInvalidEmail = new LoginCommand(Email: "nope@example.com", Password: "Pass123!");
        var commandInvalidPassword = new LoginCommand(Email: "user@example.com", Password: "Wrong!");

        // Act
        var resultEmail = await _handler.Handle(commandInvalidEmail, CancellationToken.None);
        var resultPassword = await _handler.Handle(commandInvalidPassword, CancellationToken.None);

        // Assert
        resultEmail.Error.Should().Be(resultPassword.Error);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
