FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Prueba.Api/Prueba.Api.csproj", "Prueba.Api/"]
COPY ["src/Prueba.Application/Prueba.Application.csproj", "Prueba.Application/"]
COPY ["src/Prueba.Domain/Prueba.Domain.csproj", "Prueba.Domain/"]
COPY ["src/Prueba.Infrastructure/Prueba.Infrastructure.csproj", "Prueba.Infrastructure/"]
COPY ["src/Prueba.Modules/Prueba.Modules.Identity/Prueba.Modules.Identity.csproj", "Prueba.Modules/Prueba.Modules.Identity/"]
COPY ["src/Prueba.Modules/Prueba.Modules.Properties/Prueba.Modules.Properties.csproj", "Prueba.Modules/Prueba.Modules.Properties/"]
COPY ["src/Prueba.Modules/Prueba.Modules.Booking/Prueba.Modules.Booking.csproj", "Prueba.Modules/Prueba.Modules.Booking/"]
COPY ["src/Prueba.Modules/Prueba.Modules.Wishlist/Prueba.Modules.Wishlist.csproj", "Prueba.Modules/Prueba.Modules.Wishlist/"]
COPY ["src/Prueba.Modules/Prueba.Modules.KYC/Prueba.Modules.KYC.csproj", "Prueba.Modules/Prueba.Modules.KYC/"]
COPY ["src/Prueba.Modules/Prueba.Modules.Notifications/Prueba.Modules.Notifications.csproj", "Prueba.Modules/Prueba.Modules.Notifications/"]
COPY ["src/Prueba.Modules/Prueba.Modules.Dashboard/Prueba.Modules.Dashboard.csproj", "Prueba.Modules/Prueba.Modules.Dashboard/"]
COPY ["src/Prueba.Modules/Prueba.Modules.Reports/Prueba.Modules.Reports.csproj", "Prueba.Modules/Prueba.Modules.Reports/"]
RUN dotnet restore "Prueba.Api/Prueba.Api.csproj"
COPY src/ .
WORKDIR "/src/Prueba.Api"
RUN dotnet build "Prueba.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Prueba.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Prueba.Api.dll"]
