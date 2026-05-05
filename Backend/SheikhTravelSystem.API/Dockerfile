FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY SheikhTravelSystem.API/SheikhTravelSystem.API.csproj SheikhTravelSystem.API/
COPY SheikhTravelSystem.Application/SheikhTravelSystem.Application.csproj SheikhTravelSystem.Application/
COPY SheikhTravelSystem.Infrastructure/SheikhTravelSystem.Infrastructure.csproj SheikhTravelSystem.Infrastructure/
COPY SheikhTravelSystem.Domain/SheikhTravelSystem.Domain.csproj SheikhTravelSystem.Domain/
RUN dotnet restore SheikhTravelSystem.API/SheikhTravelSystem.API.csproj

COPY . .
WORKDIR /src/SheikhTravelSystem.API
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:5082
EXPOSE 5082

ENTRYPOINT ["dotnet", "SheikhTravelSystem.API.dll"]
