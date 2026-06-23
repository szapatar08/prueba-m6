using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using Prueba.Application.Interfaces;
using Prueba.Infrastructure.Data;
using Prueba.Infrastructure.Services;
using Prueba.Modules.Identity.Features.Register;

namespace Prueba.UnitTests.Modules.Identity.Features;

public class RegisterHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _context;
    private readonly Repository _repository;
    private readonly Mock<ICurrentTenant> _currentTenantMock;
    private readonly RegisterHandler _handler;
    private readonly Guid _tenantId = Guid.NewGuid();

    public RegisterHandlerTests()
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
        _handler = new RegisterHandler(_repository, _currentTenantMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldCreateUser()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "newuser@example.com",
            Password: "SecurePass123!",
            FirstName: "Jane",
            LastName: "Smith");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Email.Should().Be("newuser@example.com");
        result.Value.FirstName.Should().Be("Jane");
        result.Value.LastName.Should().Be("Smith");
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldHashPassword()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "hash@example.com",
            Password: "SecurePass123!",
            FirstName: "Jane",
            LastName: "Smith");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var savedUser = await _context.Set<Prueba.Modules.Identity.Entities.User>()
            .IgnoreQueryFilters()
            .FirstAsync(u => u.Id == result.Value!.Id);
        savedUser.PasswordHash.Should().NotBe("SecurePass123!");
        BCrypt.Net.BCrypt.Verify("SecurePass123!", savedUser.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithDuplicateEmail_ShouldReturnFailure()
    {
        // Arrange
        var command1 = new RegisterCommand(
            Email: "duplicate@example.com",
            Password: "SecurePass123!",
            FirstName: "Jane",
            LastName: "Smith");
        var command2 = new RegisterCommand(
            Email: "duplicate@example.com",
            Password: "AnotherPass456!",
            FirstName: "John",
            LastName: "Doe");

        // Act
        await _handler.Handle(command1, CancellationToken.None);
        var result = await _handler.Handle(command2, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("already registered");
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldSaveToDatabase()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "persist@example.com",
            Password: "SecurePass123!",
            FirstName: "Jane",
            LastName: "Smith");

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        var savedUser = await _context.Set<Prueba.Modules.Identity.Entities.User>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == "persist@example.com");
        savedUser.Should().NotBeNull();
        savedUser!.FirstName.Should().Be("Jane");
        savedUser.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldAssignGuestRole()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "withrole@example.com",
            Password: "SecurePass123!",
            FirstName: "Jane",
            LastName: "Smith");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        var savedUser = await _context.Set<Prueba.Modules.Identity.Entities.User>()
            .IgnoreQueryFilters()
            .Include(u => u.UserRoles)
            .FirstAsync(u => u.Id == result.Value!.Id);
        savedUser.UserRoles.Should().HaveCount(1);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
