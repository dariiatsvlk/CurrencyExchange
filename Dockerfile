# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["CurrencyExchanger.csproj", "."]
RUN dotnet restore "CurrencyExchanger.csproj"
COPY . .
RUN dotnet publish "CurrencyExchanger.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Final stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "CurrencyExchanger.dll"]
