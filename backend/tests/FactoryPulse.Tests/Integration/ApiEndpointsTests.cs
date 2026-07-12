using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FactoryPulse.Application.DTOs;

namespace FactoryPulse.Tests.Integration;

public class ApiEndpointsTests : IClassFixture<FactoryPulseApiFactory>
{
    private readonly FactoryPulseApiFactory _factory;

    public ApiEndpointsTests(FactoryPulseApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = FactoryPulseApiFactory.AdminEmail,
            Password = FactoryPulseApiFactory.AdminPassword
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return client;
    }

    [Fact]
    public async Task Login_WithSeededAdmin_ReturnsTokenWithAdminRole()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = FactoryPulseApiFactory.AdminEmail,
            Password = FactoryPulseApiFactory.AdminPassword
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        auth!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.Roles.ShouldContain("Admin");
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = FactoryPulseApiFactory.AdminEmail,
            Password = "definitely-not-the-password"
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMachines_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/machines");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateMachine_ThenGetById_ReturnsTheMachine()
    {
        var client = await CreateAdminClientAsync();

        var create = await client.PostAsJsonAsync("/api/machines", new CreateMachineRequest
        {
            Name = $"CNC-{Guid.NewGuid():N}"[..20],
            Description = "Created by an integration test"
        });

        create.StatusCode.ShouldBe(HttpStatusCode.Created);

        var created = await create.Content.ReadFromJsonAsync<MachineDto>();
        created!.Status.ShouldBe("Idle");

        var get = await client.GetAsync($"/api/machines/{created.Id}");

        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        var fetched = await get.Content.ReadFromJsonAsync<MachineDto>();
        fetched!.Id.ShouldBe(created.Id);
    }

    [Fact]
    public async Task CreateOrder_ThenStart_TransitionsToRunning()
    {
        var client = await CreateAdminClientAsync();

        // A machine to hang the order on.
        var machineResponse = await client.PostAsJsonAsync("/api/machines", new CreateMachineRequest
        {
            Name = $"M-{Guid.NewGuid():N}"[..18]
        });
        machineResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var machine = await machineResponse.Content.ReadFromJsonAsync<MachineDto>();

        var orderResponse = await client.PostAsJsonAsync("/api/orders", new CreateProductionOrderRequest
        {
            OrderNumber = $"ORD-{Guid.NewGuid():N}"[..20],
            ProductName = "Widget",
            Quantity = 10,
            StartDate = DateTime.UtcNow,
            MachineId = machine!.Id
        });

        orderResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await orderResponse.Content.ReadFromJsonAsync<ProductionOrderDto>();
        order!.Status.ShouldBe("Planned");

        var start = await client.PostAsync($"/api/orders/{order.Id}/start", content: null);

        start.StatusCode.ShouldBe(HttpStatusCode.OK);
        var started = await start.Content.ReadFromJsonAsync<ProductionOrderDto>();
        started!.Status.ShouldBe("Running");
    }

    [Fact]
    public async Task CreateOrder_WithDuplicateOrderNumber_ReturnsConflict()
    {
        var client = await CreateAdminClientAsync();

        var machineResponse = await client.PostAsJsonAsync("/api/machines", new CreateMachineRequest
        {
            Name = $"M-{Guid.NewGuid():N}"[..18]
        });
        var machine = await machineResponse.Content.ReadFromJsonAsync<MachineDto>();

        var orderNumber = $"DUP-{Guid.NewGuid():N}"[..20];

        var request = new CreateProductionOrderRequest
        {
            OrderNumber = orderNumber,
            ProductName = "Widget",
            Quantity = 5,
            StartDate = DateTime.UtcNow,
            MachineId = machine!.Id
        };

        var first = await client.PostAsJsonAsync("/api/orders", request);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("/api/orders", request);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
